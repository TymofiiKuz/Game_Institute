using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PlayerLog : MonoBehaviour
{
    [Serializable]
    public struct LogEntry
    {
        public float time;
        public string message;

        public LogEntry(float time, string message)
        {
            this.time = time;
            this.message = message;
        }

        public string ToDisplayString()
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            return $"[{minutes:00}:{seconds:00}] {message}";
        }
    }

    public static PlayerLog Instance { get; private set; }

    [Tooltip("Maximum number of entries kept in memory.")]
    [Range(5, 200)]
    public int maxEntries = 80;

    public event Action<IReadOnlyList<LogEntry>> LogChanged;

    readonly List<LogEntry> entries = new List<LogEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstanceExists()
    {
        CreateInstanceIfNeeded();
    }

    static void CreateInstanceIfNeeded()
    {
        if (Instance != null)
            return;

        var go = new GameObject("PlayerLog");
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<PlayerLog>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static PlayerLog GetOrCreate()
    {
        CreateInstanceIfNeeded();
        return Instance;
    }

    public static void Add(string message)
    {
        PlayerLog log = GetOrCreate();
        if (log != null)
            log.AddInternal(message);
    }

    void AddInternal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        entries.Add(new LogEntry(Time.time, message.Trim()));

        if (entries.Count > maxEntries)
        {
            int overflow = entries.Count - maxEntries;
            entries.RemoveRange(0, overflow);
        }

        LogChanged?.Invoke(entries);
    }

    public IReadOnlyList<LogEntry> Entries => entries;

    public string BuildLogText(int maxLines)
    {
        if (maxLines <= 0 || entries.Count == 0)
            return string.Empty;

        int start = Mathf.Max(0, entries.Count - maxLines);
        StringBuilder sb = new StringBuilder();

        for (int i = start; i < entries.Count; i++)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(entries[i].ToDisplayString());
        }

        return sb.ToString();
    }
}
