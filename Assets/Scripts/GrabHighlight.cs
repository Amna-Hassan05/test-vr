using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Hover highlight for XRGrabInteractable.
/// Supports URP (_BaseColor) and glTFast (baseColorFactor) materials — PropertyBlock alone
/// was silent on GLB instruments because they don't use _BaseColor.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class GrabHighlight : MonoBehaviour
{
    [SerializeField] Color highlightColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField] [Range(0.2f, 1f)] float blend = 0.95f;

    XRGrabInteractable grab;
    Renderer[] renderers;
    Material[][] originalShared;
    Material[][] highlightMats;
    int hoverCount;
    bool cached;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorFactorId = Shader.PropertyToID("baseColorFactor");

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        renderers = GetComponentsInChildren<Renderer>(true);
        CacheHighlightMaterials();
    }

    void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        grab.hoverEntered.AddListener(OnHoverEntered);
        grab.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        if (grab != null)
        {
            grab.hoverEntered.RemoveListener(OnHoverEntered);
            grab.hoverExited.RemoveListener(OnHoverExited);
        }

        hoverCount = 0;
        Apply(false);
    }

    void OnDestroy()
    {
        if (highlightMats == null)
            return;

        for (int i = 0; i < highlightMats.Length; i++)
        {
            if (highlightMats[i] == null)
                continue;
            for (int j = 0; j < highlightMats[i].Length; j++)
            {
                if (highlightMats[i][j] != null)
                    Destroy(highlightMats[i][j]);
            }
        }
    }

    void CacheHighlightMaterials()
    {
        if (renderers == null || renderers.Length == 0)
        {
            cached = false;
            return;
        }

        originalShared = new Material[renderers.Length][];
        highlightMats = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Material[] shared = renderers[i].sharedMaterials;
            originalShared[i] = shared;
            highlightMats[i] = new Material[shared.Length];

            for (int j = 0; j < shared.Length; j++)
            {
                Material src = shared[j];
                if (src == null)
                    continue;

                Material hi = new Material(src);
                hi.name = src.name + " (Hover)";
                TintMaterial(hi);
                highlightMats[i][j] = hi;
            }
        }

        cached = true;
    }

    void TintMaterial(Material mat)
    {
        // glTFast GLB materials (most instruments in this project)
        if (mat.HasProperty(BaseColorFactorId))
        {
            Color c = mat.GetColor(BaseColorFactorId);
            mat.SetColor(BaseColorFactorId, Color.Lerp(c, highlightColor, blend));
            return;
        }

        // URP Lit
        if (mat.HasProperty(BaseColorId))
        {
            Color c = mat.GetColor(BaseColorId);
            mat.SetColor(BaseColorId, Color.Lerp(c, highlightColor, blend));
            return;
        }

        // Built-in / fallback
        if (mat.HasProperty(ColorId))
        {
            Color c = mat.GetColor(ColorId);
            mat.SetColor(ColorId, Color.Lerp(c, highlightColor, blend));
        }
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        hoverCount++;
        if (hoverCount == 1)
            Apply(true);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        hoverCount = Mathf.Max(0, hoverCount - 1);
        if (hoverCount == 0)
            Apply(false);
    }

    void Apply(bool highlighted)
    {
        if (!cached || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].sharedMaterials = highlighted
                ? highlightMats[i]
                : originalShared[i];
        }
    }
}
