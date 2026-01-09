using UnityEngine;
using TMPro;

public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text tooltipText;

    void Awake()
    {
        Instance = this;
        if (panel != null)
            panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (panel != null && panel.activeSelf)
            panel.transform.position = Input.mousePosition;
    }

    public void Show(string message)
    {
        if (panel == null || tooltipText == null)
            return;

        tooltipText.text = message;
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
