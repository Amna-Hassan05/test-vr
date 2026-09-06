using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// AuthXR Play Mode setup: keep instruments visible/stable, enable grab + highlight,
/// thicken floor, leave kidney_tray non-grabbable.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AuthXRRuntimeBootstrap : MonoBehaviour
{
    const float MinGrabWorldSize = 0.1f;
    const int TeleportInteractionLayer = 31;

    [SerializeField] bool fixFloor = true;
    [SerializeField] bool fixInstruments = true;

    void Awake()
    {
        if (fixFloor)
            FixFloor();

        // Always strip tray first so it never gets grab/physics.
        StripNonGrabbable("kidney_tray");

        if (fixInstruments)
            FixInstruments();
    }

    void FixFloor()
    {
        var floor = GameObject.Find("Floor") ?? GameObject.Find("Plane");
        if (floor == null)
            return;

        floor.name = "Floor";

        var meshCol = floor.GetComponent<MeshCollider>();
        if (meshCol != null)
            Destroy(meshCol);

        var box = floor.GetComponent<BoxCollider>();
        if (box == null)
            box = floor.AddComponent<BoxCollider>();

        // Thick box under the visual plane surface — do NOT move/scale the Floor transform
        // (moving it at runtime made the room feel wrong / objects disappear relative to view).
        box.isTrigger = false;
        box.size = new Vector3(10f, 0.4f, 10f);
        box.center = new Vector3(0f, -0.2f, 0f);

        var teleport = floor.GetComponent<TeleportationArea>();
        if (teleport != null)
        {
            InteractionLayerMask mask = 1 << TeleportInteractionLayer;
            teleport.interactionLayers = mask;
        }
    }

    void FixInstruments()
    {
        Configure("pair_of_scalpels", InstrumentType.Scalpel);
        Configure("surgical_needle_holder", InstrumentType.NeedleHolder);
        Configure("scissor", InstrumentType.Scissors);

        foreach (var grab in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include))
        {
            if (grab == null || grab.gameObject.name == "kidney_tray")
                continue;

            EnsureGrab(grab.gameObject);
            if (grab.GetComponent<GrabHighlight>() == null)
                grab.gameObject.AddComponent<GrabHighlight>();
            if (grab.GetComponent<GrabEventLogger>() == null)
                grab.gameObject.AddComponent<GrabEventLogger>();
            if (grab.GetComponent<AuthXRRestPoseLock>() == null)
                grab.gameObject.AddComponent<AuthXRRestPoseLock>();
        }
    }

    void StripNonGrabbable(string objectName)
    {
        var go = FindByName(objectName);
        if (go == null)
            return;

        DestroyIfPresent<GrabHighlight>(go);
        DestroyIfPresent<GrabEventLogger>(go);
        DestroyIfPresent<InstrumentItem>(go);
        DestroyIfPresent<AuthXRRestPoseLock>(go);
        DestroyIfPresent<XRGrabInteractable>(go);

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.Sleep();
        }
    }

    void Configure(string objectName, InstrumentType type)
    {
        var go = FindByName(objectName);
        if (go == null)
        {
            Debug.LogWarning($"[AuthXR] Missing instrument '{objectName}'.");
            return;
        }

        // Ensure visible / active
        go.SetActive(true);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        EnsureGrab(go);

        var item = go.GetComponent<InstrumentItem>();
        if (item == null)
            item = go.AddComponent<InstrumentItem>();
        item.type = type;

        if (go.GetComponent<GrabEventLogger>() == null)
            go.AddComponent<GrabEventLogger>();
        if (go.GetComponent<GrabHighlight>() == null)
            go.AddComponent<GrabHighlight>();
        if (go.GetComponent<AuthXRRestPoseLock>() == null)
            go.AddComponent<AuthXRRestPoseLock>();
    }

    static void EnsureGrab(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody>();

        // CRITICAL: stay kinematic while resting so overlapping grab colliders
        // do not PhysX-explode instruments out of the room (invisible in Play Mode).
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.Sleep();

        var box = go.GetComponent<BoxCollider>();
        if (box == null)
            box = go.AddComponent<BoxCollider>();

        FitGrabCollider(go, box);
        box.isTrigger = false;

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = go.AddComponent<XRGrabInteractable>();

        InteractionLayerMask defaultMask = 1;
        grab.interactionLayers = defaultMask;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false; // keep kinematic / visible after release
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.selectMode = InteractableSelectMode.Single;

        grab.colliders.Clear();
        grab.colliders.Add(box);
    }

    /// <summary>
    /// Modest world-space grab volume from renderer bounds — avoids giant local sizes
    /// (e.g. scissors scale 0.004 × size 30) that overlap and launch objects.
    /// </summary>
    static void FitGrabCollider(GameObject go, BoxCollider box)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        Vector3 worldCenter = go.transform.position;
        Vector3 worldSize = new Vector3(MinGrabWorldSize, MinGrabWorldSize, MinGrabWorldSize);

        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    b.Encapsulate(renderers[i].bounds);
            }

            worldCenter = b.center;
            worldSize = b.size;
        }

        worldSize.x = Mathf.Max(worldSize.x, MinGrabWorldSize);
        worldSize.y = Mathf.Max(worldSize.y, MinGrabWorldSize);
        worldSize.z = Mathf.Max(worldSize.z, MinGrabWorldSize);
        // Slight pad for easier aiming — keep modest so neighbors don't overlap heavily.
        worldSize *= 1.15f;

        Vector3 lossy = go.transform.lossyScale;
        float ax = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
        float ay = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        float az = Mathf.Max(0.0001f, Mathf.Abs(lossy.z));

        box.center = go.transform.InverseTransformPoint(worldCenter);
        box.size = new Vector3(worldSize.x / ax, worldSize.y / ay, worldSize.z / az);
    }

    static void DestroyIfPresent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
            Destroy(c);
    }

    static GameObject FindByName(string objectName)
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
        {
            if (t.name == objectName)
                return t.gameObject;
        }
        return null;
    }
}

/// <summary>
/// Keeps instruments kinematic when not held so they stay visible on the table.
/// Lives in the same file as AuthXRRuntimeBootstrap to avoid Editor asm compile order issues.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class AuthXRRestPoseLock : MonoBehaviour
{
    XRGrabInteractable grab;
    Rigidbody body;
    Vector3 restPosition;
    Quaternion restRotation;
    bool hasRestPose;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        restPosition = transform.position;
        restRotation = transform.rotation;
        hasRestPose = true;
    }

    void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        grab.selectExited.AddListener(OnReleased);
        LockAtRest();
    }

    void OnDisable()
    {
        if (grab != null)
            grab.selectExited.RemoveListener(OnReleased);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        LockAtRest();
    }

    void LockAtRest()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (body == null)
            return;

        body.isKinematic = true;
        body.useGravity = false;
        body.Sleep();

        if (hasRestPose && transform.position.y < -1f)
            transform.SetPositionAndRotation(restPosition, restRotation);
    }
}
