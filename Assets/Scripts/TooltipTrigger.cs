using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string message;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Tooltip.Instance != null)
            Tooltip.Instance.Show(message);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tooltip.Instance != null)
            Tooltip.Instance.Hide();
    }
}
