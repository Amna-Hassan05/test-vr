using UnityEngine;

/// <summary>
/// Samples controller pose/speed at a fixed rate (default 20 Hz) into VRInteractionLogger.
/// Attach to Left Controller / Right Controller under XR Origin.
/// </summary>
public class ControllerMovementLogger : MonoBehaviour
{
    [Tooltip("Label written to CSV controller_name column.")]
    public string controllerName = "Controller";

    [Tooltip("Samples per second.")]
    [Range(1f, 90f)]
    public float sampleRateHz = 20f;

    float interval;
    float timer;
    Vector3 lastPosition;
    bool hasLast;
    bool warnedMissingLogger;

    void OnEnable()
    {
        interval = 1f / Mathf.Max(1f, sampleRateHz);
        timer = 0f;
        hasLast = false;
        warnedMissingLogger = false;
    }

    void Update()
    {
        if (VRInteractionLogger.Instance == null)
        {
            if (!warnedMissingLogger && Time.frameCount > 5)
            {
                warnedMissingLogger = true;
                Debug.LogWarning(
                    $"[ControllerMovementLogger] VRInteractionLogger.Instance is null on '{name}'. " +
                    "Check Logging_Manager has VRInteractionLogger (not Missing Script).");
            }
            return;
        }

        timer += Time.deltaTime;
        if (timer < interval)
            return;

        timer -= interval;

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        float speed = 0f;

        if (hasLast)
        {
            float dt = Mathf.Max(interval, Time.deltaTime);
            speed = Vector3.Distance(pos, lastPosition) / dt;
        }

        lastPosition = pos;
        hasLast = true;

        VRInteractionLogger.Instance.LogRow(
            eventType: "movement",
            objectName: gameObject.name,
            controllerName: controllerName,
            position: pos,
            rotation: rot,
            speed: speed,
            interactorName: "");
    }
}
