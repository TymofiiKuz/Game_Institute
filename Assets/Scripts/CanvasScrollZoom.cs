using UnityEngine;

/// <summary>
/// Scales a RectTransform in response to mouse scroll wheel input.
/// </summary>
public class CanvasScrollZoom : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float zoomStep = 0.1f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2f;

    private void Awake()
    {
        if (target == null)
            target = transform as RectTransform;
    }

    private void Update()
    {
        if (target == null)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        float currentScale = target.localScale.x;
        currentScale += scroll * zoomStep;
        currentScale = Mathf.Clamp(currentScale, minScale, maxScale);

        target.localScale = Vector3.one * currentScale;
    }
}
