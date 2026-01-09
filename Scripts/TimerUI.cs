using UnityEngine;

public class TimerUI : MonoBehaviour
{
    public static TimerUI Instance;
    public float timeElapsed = 0f;
    private bool timerIsRunning = true;

    private void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (timerIsRunning)
        {
            timeElapsed += Time.deltaTime;
            // Просто передаём время менеджеру UI!
            UIManager.Instance.UpdateTimer(timeElapsed);
        }
    }
}
