using UnityEngine;
using TMPro;

public class NightCycle : MonoBehaviour
{
    [Header("Night timing")]
    [Tooltip("Duration of the night in seconds. Default = 6 minutes (360s).")]
    public float nightDurationSeconds = 360f;

    [Header("Finished UI")]
    [Tooltip("UI GameObject (panel) that will be activated when the night finishes. Assign in inspector.")]
    public GameObject finishedScreen;

    // runtime
    private float remainingSeconds;
    private bool running = false;
    private bool finished = false;

    // start countdown immediately after loading
    private void Awake()
    {
        remainingSeconds = Mathf.Max(0f, nightDurationSeconds);

        if (finishedScreen != null)
            finishedScreen.SetActive(false);

        StartNight();
    }

    private void Update()
    {
        if (!running || finished) return;

        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0f)
            FinishNight();
    }

    private void StartNight()
    {
        running = true;
        finished = false;
    }

    private void FinishNight()
    {
        finished = true;
        running = false;

        if (finishedScreen != null)
            finishedScreen.SetActive(true);

        Debug.Log("[NightCycle] Night finished.");
        // future: trigger different end variants via events/hooks here
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins:D2}:{secs:D2}";
    }

    // Public API
    public float RemainingTime => Mathf.Max(0f, remainingSeconds);
    public bool IsFinished => finished;
    public bool IsRunning => running;
}
