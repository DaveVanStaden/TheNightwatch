using UnityEngine;
public class PassiveSanityDrain : MonoBehaviour
{
    [Header("Drain settings")]
    [Tooltip("Sanity drained per second. Positive value reduces sanity over time.")]
    public float drainPerSecond = 0.1f;

    [Tooltip("Start draining automatically on Awake.")]
    public bool startOnAwake = true;

    private bool running = false;

    private void Awake()
    {
        running = startOnAwake;
    }

    private void Update()
    {
        if (!running) return;
        if (PlayerStats.Instance == null) return;

        // drainPerSecond is positive for designer convenience; ChangeSanity expects
        // positive to increase sanity, so pass negative to reduce.
        float amountThisFrame = drainPerSecond * Time.deltaTime;
        if (amountThisFrame <= 0f) return;

        PlayerStats.Instance.ChangeSanity(-amountThisFrame);
    }

    // Public API to control drain at runtime
    public void StartDrain() => running = true;
    public void StopDrain() => running = false;
    public bool IsRunning() => running;

    // Allows changing the rate at runtime (positive value reduces sanity)
    public void SetDrainRate(float newDrainPerSecond) => drainPerSecond = Mathf.Max(0f, newDrainPerSecond);
}
