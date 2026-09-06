using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// AuthXR scene wiring for CSV interaction logging + Starter Assets XR Origin.
/// Menu: AuthXR → Setup Logging + Fix XR Origin
/// Batch: -executeMethod AuthXRLoggingSceneSetup.SetupFromBatch
/// </summary>
public static class AuthXRLoggingSceneSetup
{
    const string ScenePath = "Assets/MyFirstVRScene.unity";
    const string XrOriginPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    [MenuItem("AuthXR/Setup Logging + Fix XR Origin")]
    public static void SetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SetupInternal(openScene: true);
        EditorUtility.DisplayDialog(
            "AuthXR Setup",
            "Scene updated: XR Origin (XR Rig), Logging_Manager, controller/instrument loggers.",
            "OK");
    }

    [MenuItem("AuthXR/Repair Logging_Manager")]
    public static void RepairLoggingManagerMenu()
    {
        var go = GameObject.Find("Logging_Manager");
        if (go == null)
        {
            go = new GameObject("Logging_Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Logging_Manager");
        }

        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        EnsureLoggingOnly(go);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "AuthXR",
            $"Logging_Manager repaired.\nRemoved missing scripts: {removed}\nVRInteractionLogger mirrorToConsole = ON",
            "OK");
    }

    /// <summary>Batchmode entry point.</summary>
    public static void SetupFromBatch()
    {
        try
        {
            SetupInternal(openScene: true);
            Debug.Log("[AuthXRLoggingSceneSetup] Setup completed successfully.");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AuthXRLoggingSceneSetup] Setup failed: {e}");
            EditorApplication.Exit(1);
        }
    }

    static void SetupInternal(bool openScene)
    {
        if (!File.Exists(Path.GetFullPath(ScenePath)))
            throw new FileNotFoundException("Scene not found", ScenePath);

        Scene scene;
        if (openScene || SceneManager.GetActiveScene().path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            scene = SceneManager.GetActiveScene();

        ReplaceIncompleteXrOrigin();
        EnsureLoggingManager();
        AttachControllerLoggers();
        ConfigureInstruments();

        // Remove leftover incomplete origin if both exist.
        var incomplete = GameObject.Find("XR Origin (VR)");
        if (incomplete != null && GameObject.Find("XR Origin (XR Rig)") != null)
            Object.DestroyImmediate(incomplete);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void ReplaceIncompleteXrOrigin()
    {
        GameObject existing = GameObject.Find("XR Origin (VR)");
        GameObject alreadyGood = GameObject.Find("XR Origin (XR Rig)");

        if (alreadyGood != null)
        {
            Debug.Log("[AuthXRLoggingSceneSetup] XR Origin (XR Rig) already present.");
            if (existing != null)
                Object.DestroyImmediate(existing);
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
        if (prefab == null)
            throw new FileNotFoundException("XR Origin prefab missing", XrOriginPrefabPath);

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        if (existing != null)
        {
            pos = existing.transform.position;
            rot = existing.transform.rotation;
            Object.DestroyImmediate(existing);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "XR Origin (XR Rig)";
        instance.transform.SetPositionAndRotation(pos, rot);
        Undo.RegisterCreatedObjectUndo(instance, "Add XR Origin (XR Rig)");
        Debug.Log("[AuthXRLoggingSceneSetup] Replaced incomplete XR Origin with Starter Assets XR Origin (XR Rig).");
    }

    static void EnsureLoggingManager()
    {
        var logger = Object.FindAnyObjectByType<VRInteractionLogger>();
        if (logger != null)
        {
            logger.mirrorToConsole = true;
            if (logger.gameObject.name != "Logging_Manager")
                logger.gameObject.name = "Logging_Manager";
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(logger.gameObject);
            return;
        }

        var go = new GameObject("Logging_Manager");
        var comp = go.AddComponent<VRInteractionLogger>();
        comp.mirrorToConsole = true;
        Undo.RegisterCreatedObjectUndo(go, "Create Logging_Manager");
        Debug.Log("[AuthXRLoggingSceneSetup] Created Logging_Manager with VRInteractionLogger (mirrorToConsole=true).");
    }

    static void EnsureLoggingOnly(GameObject go)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        var logger = go.GetComponent<VRInteractionLogger>();
        if (logger == null)
            logger = go.AddComponent<VRInteractionLogger>();
        logger.mirrorToConsole = true;
    }

    static void AttachControllerLoggers()
    {
        AttachMovementLogger("Left Controller", "LeftController");
        AttachMovementLogger("Right Controller", "RightController");
    }

    static void AttachMovementLogger(string objectName, string controllerLabel)
    {
        var go = FindInActiveSceneByName(objectName);
        if (go == null)
        {
            Debug.LogWarning($"[AuthXRLoggingSceneSetup] Could not find '{objectName}'.");
            return;
        }

        var logger = go.GetComponent<ControllerMovementLogger>();
        if (logger == null)
            logger = go.AddComponent<ControllerMovementLogger>();

        logger.controllerName = controllerLabel;
        logger.sampleRateHz = 20f;
        Debug.Log($"[AuthXRLoggingSceneSetup] ControllerMovementLogger on {objectName} ({controllerLabel}).");
    }

    static void ConfigureInstruments()
    {
        // Name patterns -> InstrumentType for GameManager prompts.
        ConfigureOne("pair_of_scalpels", InstrumentType.Scalpel);
        ConfigureOne("surgical_needle_holder", InstrumentType.NeedleHolder);
        ConfigureOne("scissor", InstrumentType.Scissors);

        // Kidney tray must remain non-grabbable (static prop).
        var tray = FindInActiveSceneByName("kidney_tray");
        if (tray != null)
            StripGrabComponents(tray);

        // Any other XRGrabInteractable in scene also gets GrabEventLogger.
        var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include);
        foreach (var grab in grabs)
        {
            if (grab.gameObject.name == "kidney_tray")
            {
                StripGrabComponents(grab.gameObject);
                continue;
            }

            EnsureGrabConfigured(grab.gameObject);
            if (grab.GetComponent<GrabEventLogger>() == null)
                grab.gameObject.AddComponent<GrabEventLogger>();
        }
    }

    static void StripGrabComponents(GameObject go)
    {
        var highlight = go.GetComponent<GrabHighlight>();
        if (highlight != null)
            Object.DestroyImmediate(highlight);

        var logger = go.GetComponent<GrabEventLogger>();
        if (logger != null)
            Object.DestroyImmediate(logger);

        var item = go.GetComponent<InstrumentItem>();
        if (item != null)
            Object.DestroyImmediate(item);

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab != null)
            Object.DestroyImmediate(grab);

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    static void ConfigureOne(string objectName, InstrumentType type)
    {
        var go = FindInActiveSceneByName(objectName);
        if (go == null)
        {
            Debug.LogWarning($"[AuthXRLoggingSceneSetup] Instrument '{objectName}' not found.");
            return;
        }

        EnsureGrabConfigured(go);

        var item = go.GetComponent<InstrumentItem>();
        if (item == null)
            item = go.AddComponent<InstrumentItem>();
        item.type = type;

        if (go.GetComponent<GrabEventLogger>() == null)
            go.AddComponent<GrabEventLogger>();

        Debug.Log($"[AuthXRLoggingSceneSetup] Configured instrument '{objectName}' as {type}.");
    }

    static void EnsureGrabConfigured(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody>();

        // Stay kinematic while resting so instruments do not PhysX-explode off the table.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.None;

        if (go.GetComponent<Collider>() == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            box.isTrigger = false;
        }

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = go.AddComponent<XRGrabInteractable>();

        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;

        if (go.GetComponent<GrabHighlight>() == null)
            go.AddComponent<GrabHighlight>();
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

        // Fallback: include inactive / deep hierarchy via Resources-style search
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
