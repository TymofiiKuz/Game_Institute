using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLogUI : MonoBehaviour
{
    [SerializeField] Text logText;
    [SerializeField, Range(1, 200)] int maxLines = 40;

    void Awake()
    {
        if (logText == null)
            logText = GetComponent<Text>();
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        if (PlayerLog.Instance != null)
            PlayerLog.Instance.LogChanged -= OnLogChanged;
    }

    void Subscribe()
    {
        PlayerLog log = PlayerLog.GetOrCreate();
        if (log != null)
            log.LogChanged += OnLogChanged;
    }

    void OnLogChanged(IReadOnlyList<PlayerLog.LogEntry> _)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (logText == null)
            return;

        PlayerLog log = PlayerLog.Instance;
        logText.text = log != null ? log.BuildLogText(maxLines) : string.Empty;
    }
}
