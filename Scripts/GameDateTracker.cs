using System;
using UnityEngine;

public class GameDateTracker : MonoBehaviour
{
    public static GameDateTracker Instance { get; private set; }

    [Header("Calendar Settings")]
    [SerializeField] private int startYear = 2024;
    [SerializeField] private int startMonth = 1;
    [SerializeField] private int startDay = 1;
    [SerializeField] private float secondsPerDay = 3f;

    private DateTime startDate;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildStartDate();
    }

    private void OnValidate()
    {
        BuildStartDate();
    }

    public string FormatDateLabel(float elapsedSeconds)
    {
        DateTime currentDate = GetCurrentDate(elapsedSeconds);
        int dayNumber = GetCurrentDayNumber(elapsedSeconds);
        return $"Day {dayNumber:000} — {currentDate:dd.MM.yyyy}";
    }

    public DateTime GetCurrentDate(float elapsedSeconds)
    {
        int daysPassed = GetElapsedDays(elapsedSeconds);
        return startDate.AddDays(daysPassed);
    }

    public int GetCurrentDayNumber(float elapsedSeconds)
    {
        return GetElapsedDays(elapsedSeconds) + 1;
    }

    private int GetElapsedDays(float elapsedSeconds)
    {
        float safeSecondsPerDay = Mathf.Max(0.001f, secondsPerDay);
        float clampedElapsed = Mathf.Max(0f, elapsedSeconds);
        return Mathf.FloorToInt(clampedElapsed / safeSecondsPerDay);
    }

    private void BuildStartDate()
    {
        startYear = Mathf.Max(1, startYear);
        startMonth = Mathf.Clamp(startMonth, 1, 12);

        int maxDayInMonth = DateTime.DaysInMonth(startYear, startMonth);
        startDay = Mathf.Clamp(startDay, 1, maxDayInMonth);

        if (secondsPerDay <= 0f)
            secondsPerDay = 3f;

        startDate = new DateTime(startYear, startMonth, startDay);
    }
}
