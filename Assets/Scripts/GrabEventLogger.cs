using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Logs grab_start / grab_end for an XRGrabInteractable (XRI 3.x / 3.5.1).
/// Attach to any grabbable instrument alongside XRGrabInteractable.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabEventLogger : MonoBehaviour
{
    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrabStart);
        grab.selectExited.AddListener(OnGrabEnd);
    }

    void OnDisable()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabStart);
        grab.selectExited.RemoveListener(OnGrabEnd);
    }

    void OnGrabStart(SelectEnterEventArgs args)
    {
        if (VRInteractionLogger.Instance == null)
            return;

        string interactor = ResolveInteractorName(args.interactorObject);
        VRInteractionLogger.Instance.LogRow(
            eventType: "grab_start",
            objectName: gameObject.name,
            controllerName: interactor,
            position: transform.position,
            rotation: transform.rotation,
            speed: null,
            interactorName: interactor);
    }

    void OnGrabEnd(SelectExitEventArgs args)
    {
        if (VRInteractionLogger.Instance == null)
            return;

        string interactor = ResolveInteractorName(args.interactorObject);
        VRInteractionLogger.Instance.LogRow(
            eventType: "grab_end",
            objectName: gameObject.name,
            controllerName: interactor,
            position: transform.position,
            rotation: transform.rotation,
            speed: null,
            interactorName: interactor);
    }

    static string ResolveInteractorName(IXRSelectInteractor interactor)
    {
        if (interactor == null)
            return "";

        var behaviour = interactor as MonoBehaviour;
        if (behaviour == null)
            return interactor.ToString();

        Transform t = behaviour.transform;
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
                return n;
            t = t.parent;
        }

        return behaviour.gameObject.name;
    }
}
