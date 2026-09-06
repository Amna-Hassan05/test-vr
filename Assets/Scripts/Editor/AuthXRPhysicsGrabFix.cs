using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Fixes AuthXR floor collision, instrument grab colliders, teleport layer conflict, and hover highlight.
/// Menu: AuthXR → Fix Floor + Grab + Highlight
/// Batch: -executeMethod AuthXRPhysicsGrabFix.FixFromBatch
/// </summary>
public static class AuthXRPhysicsGrabFix
{
    const string ScenePath = "Assets/MyFirstVRScene.unity";
    const float MinWorldColliderSize = 0.08f;
    const int TeleportInteractionLayer = 31;

    [MenuItem("AuthXR/Fix Floor + Grab + Highlight")]
    public static void FixFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        FixInternal(openScene: true);
        EditorUtility.DisplayDialog(
            "AuthXR Fix",
            "Applied floor colliders, instrument grab colliders/Rigidbodies, teleport layer, and GrabHighlight.",
            "OK");
    }

    public static void FixFromBatch()
    {
        try
        {
            FixInternal(openScene: true);
            Debug.Log("[AuthXRPhysicsGrabFix] Fix completed successfully.");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AuthXRPhysicsGrabFix] Fix failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    static void FixInternal(bool openScene)
    {
        if (!File.Exists(Path.GetFullPath(ScenePath)))
            throw new FileNotFoundException("Scene not found", ScenePath);

        Scene scene;
        if (openScene || SceneManager.GetActiveScene().path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            scene = SceneManager.GetActiveScene();

        RemoveIncompleteXrOrigin();
        FixFloorAndRoomColliders();
        FixInstrumentTableCollider();
        ConfigureInstrument("pair_of_scalpels", InstrumentType.Scalpel);
        ConfigureInstrument("surgical_needle_holder", InstrumentType.NeedleHolder);
        ConfigureInstrument("scissor", InstrumentType.Scissors);
        StripKidneyTray();
        EnsureAllGrabHighlights();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void RemoveIncompleteXrOrigin()
    {
        var incomplete = GameObject.Find("XR Origin (VR)");
        if (incomplete == null)
            return;

        Object.DestroyImmediate(incomplete);
        Debug.Log("[AuthXRPhysicsGrabFix] Removed inactive incomplete XR Origin (VR).");
    }

    static void FixFloorAndRoomColliders()
    {
        var plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.name = "Floor";

            // Thick box instead of paper-thin MeshCollider (prevents tunneling).
            var meshCol = plane.GetComponent<MeshCollider>();
            if (meshCol != null)
                Object.DestroyImmediate(meshCol);

            var box = plane.GetComponent<BoxCollider>();
            if (box == null)
                box = plane.AddComponent<BoxCollider>();

            box.isTrigger = false;
            // Default Unity Plane is 10x10 in XZ; keep that footprint, add thickness below the surface.
            box.size = new Vector3(10f, 0.25f, 10f);
            box.center = new Vector3(0f, -0.125f, 0f);

            // Align roughly with hospital OR floor and cover the room.
            plane.transform.position = new Vector3(-1.3f, 0.15f, 1.4f);
            plane.transform.localScale = new Vector3(1.6f, 1f, 1.6f);

            var teleport = plane.GetComponent<TeleportationArea>();
            if (teleport != null)
            {
                // Keep teleport off Default so Near-Far grab rays do not fight the floor interactable.
                InteractionLayerMask teleportMask = 1 << TeleportInteractionLayer;
                teleport.interactionLayers = teleportMask;
                Debug.Log("[AuthXRPhysicsGrabFix] Floor TeleportationArea moved to Teleport interaction layer.");
            }

            Debug.Log("[AuthXRPhysicsGrabFix] Floor: BoxCollider (thick), non-trigger, scaled to cover OR.");
        }
        else
        {
            Debug.LogWarning("[AuthXRPhysicsGrabFix] Plane/Floor not found.");
        }

        // Safety catch plate under the OR in case the visual floor sits slightly above Floor.
        const string catchName = "AuthXR_FloorCatch";
        var existingCatch = GameObject.Find(catchName);
        if (existingCatch == null)
        {
            var catchGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            catchGo.name = catchName;
            Object.DestroyImmediate(catchGo.GetComponent<MeshRenderer>());
            catchGo.transform.position = new Vector3(-1.3f, 0.05f, 1.4f);
            catchGo.transform.localScale = new Vector3(16f, 0.2f, 16f);
            var col = catchGo.GetComponent<BoxCollider>();
            col.isTrigger = false;
            Undo.RegisterCreatedObjectUndo(catchGo, "Create AuthXR_FloorCatch");
            Debug.Log("[AuthXRPhysicsGrabFix] Added AuthXR_FloorCatch under OR.");
        }
    }

    static void FixInstrumentTableCollider()
    {
        var table = FindInActiveSceneByName("surgical__instrument_table_collection");
        if (table == null)
        {
            Debug.LogWarning("[AuthXRPhysicsGrabFix] Instrument table not found.");
            return;
        }

        // Root AABB colliders block Near-Far grab rays to instruments sitting on the table.
        foreach (var col in table.GetComponents<Collider>())
            Object.DestroyImmediate(col);

        const int ignoreRaycastLayer = 2;
        Transform existing = table.transform.Find("AuthXR_TabletopCollider");
        GameObject topGo = existing != null
            ? existing.gameObject
            : new GameObject("AuthXR_TabletopCollider");

        if (existing == null)
            topGo.transform.SetParent(table.transform, false);

        topGo.layer = ignoreRaycastLayer;
        var box = topGo.GetComponent<BoxCollider>();
        if (box == null)
            box = topGo.AddComponent<BoxCollider>();

        Vector3 localTop = table.transform.InverseTransformPoint(
            new Vector3(table.transform.position.x, 0.95f, table.transform.position.z));
        box.center = new Vector3(0f, localTop.y, 0f);
        box.size = new Vector3(1.2f, 0.06f, 0.8f);
        box.isTrigger = false;
        Debug.Log("[AuthXRPhysicsGrabFix] Tabletop collider (Ignore Raycast) so grab rays reach instruments.");
    }

    static void StripKidneyTray()
    {
        var tray = FindInActiveSceneByName("kidney_tray");
        if (tray == null)
            return;

        foreach (var c in tray.GetComponents<GrabHighlight>())
            Object.DestroyImmediate(c);
        foreach (var c in tray.GetComponents<GrabEventLogger>())
            Object.DestroyImmediate(c);
        foreach (var c in tray.GetComponents<InstrumentItem>())
            Object.DestroyImmediate(c);
        foreach (var c in tray.GetComponents<XRGrabInteractable>())
            Object.DestroyImmediate(c);

        var rb = tray.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Debug.Log("[AuthXRPhysicsGrabFix] kidney_tray left non-grabbable.");
    }

    static void ConfigureInstrument(string objectName, InstrumentType? type)
    {
        var go = FindInActiveSceneByName(objectName);
        if (go == null)
        {
            Debug.LogWarning($"[AuthXRPhysicsGrabFix] Instrument '{objectName}' not found.");
            return;
        }

        EnsureGrabConfigured(go);

        if (type.HasValue)
        {
            var item = go.GetComponent<InstrumentItem>();
            if (item == null)
                item = go.AddComponent<InstrumentItem>();
            item.type = type.Value;
        }

        if (go.GetComponent<GrabEventLogger>() == null)
            go.AddComponent<GrabEventLogger>();

        if (go.GetComponent<GrabHighlight>() == null)
            go.AddComponent<GrabHighlight>();

        Debug.Log($"[AuthXRPhysicsGrabFix] Configured '{objectName}' for grab + highlight.");
    }

    static void EnsureAllGrabHighlights()
    {
        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);
        foreach (var grab in grabs)
        {
            if (grab.gameObject.name == "kidney_tray")
            {
                Object.DestroyImmediate(grab);
                continue;
            }

            EnsureGrabConfigured(grab.gameObject);
            if (grab.GetComponent<GrabHighlight>() == null)
                grab.gameObject.AddComponent<GrabHighlight>();
            if (grab.GetComponent<GrabEventLogger>() == null)
                grab.gameObject.AddComponent<GrabEventLogger>();
        }
    }

    static void EnsureGrabConfigured(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody>();

        // Stay kinematic while resting so instruments remain visible on the table.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.None;

        var box = go.GetComponent<BoxCollider>();
        if (box == null)
            box = go.AddComponent<BoxCollider>();

        FitBoxColliderToMeshes(go, box, enforceMinWorldSize: true);
        box.isTrigger = false;

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = go.AddComponent<XRGrabInteractable>();

        InteractionLayerMask defaultMask = 1; // Default
        grab.interactionLayers = defaultMask;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.selectMode = InteractableSelectMode.Single;
    }

    /// <summary>
    /// Fits a BoxCollider to mesh vertices in the root object's local space,
    /// then enforces a minimum world-space size so tiny-scale GLBs remain grabbable.
    /// </summary>
    static void FitBoxColliderToMeshes(GameObject go, BoxCollider box, bool enforceMinWorldSize)
    {
        var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
        bool hasBounds = false;
        Bounds localBounds = default;

        Matrix4x4 rootWorldToLocal = go.transform.worldToLocalMatrix;

        foreach (var mf in meshFilters)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
                continue;

            Matrix4x4 localToRoot = rootWorldToLocal * mf.transform.localToWorldMatrix;
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = localToRoot.MultiplyPoint3x4(verts[i]);
                if (!hasBounds)
                {
                    localBounds = new Bounds(p, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(p);
                }
            }
        }

        if (!hasBounds)
        {
            // Fallback: renderer world bounds → approximate local box.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds world = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    world.Encapsulate(renderers[i].bounds);

                Vector3 min = go.transform.InverseTransformPoint(world.min);
                Vector3 max = go.transform.InverseTransformPoint(world.max);
                localBounds.center = (min + max) * 0.5f;
                localBounds.size = new Vector3(
                    Mathf.Abs(max.x - min.x),
                    Mathf.Abs(max.y - min.y),
                    Mathf.Abs(max.z - min.z));
                hasBounds = true;
            }
        }

        if (hasBounds)
        {
            box.center = localBounds.center;
            box.size = localBounds.size;
        }
        else
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
        }

        if (!enforceMinWorldSize)
            return;

        Vector3 lossy = go.transform.lossyScale;
        float ax = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
        float ay = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        float az = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));

        Vector3 size = box.size;
        size.x = Mathf.Max(size.x, MinWorldColliderSize / ax);
        size.y = Mathf.Max(size.y, MinWorldColliderSize / ay);
        size.z = Mathf.Max(size.z, MinWorldColliderSize / az);
        // Slight pad so near/far casters register more reliably in headset.
        size *= 1.15f;
        box.size = size;
    }

    static GameObject FindInActiveSceneByName(string objectName)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindChildRecursive(root.transform, objectName);
            if (t != null)
                return t.gameObject;
        }

        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
        {
            if (t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindChildRecursive(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
