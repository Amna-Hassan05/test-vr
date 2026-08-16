using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum InstrumentType { Scalpel, Scissors, Grasper, Clamp, NeedleHolder, Dissector }

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class InstrumentItem : MonoBehaviour
{
    public InstrumentType type;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        GameManager.Instance.OnInstrumentSelected(type);
    }
}