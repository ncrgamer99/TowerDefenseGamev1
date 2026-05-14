using UnityEngine;

public class GameSpeedManager : MonoBehaviour
{
    [Header("Speed Values")]
    public float normalSpeed = 1f;
    public float fastSpeed = 2f;
    public float fasterSpeed = 6f;

    [Header("Safety")]
    public bool applyFixedDeltaTimeScaling = true;
    public float maxAllowedTimeScale = 8f;

    private int currentSpeedMode = 1;
    private float defaultFixedDeltaTime;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        SetNormalSpeed();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7))
            SetNormalSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha8))
            SetFastSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha9))
            SetFasterSpeed();
    }

    private void OnDisable()
    {
        ResetTimeScale();
    }

    private void OnDestroy()
    {
        ResetTimeScale();
    }

    public void SetNormalSpeed()
    {
        currentSpeedMode = 1;
        ApplySpeed(normalSpeed, "Normal");
    }

    public void SetFastSpeed()
    {
        currentSpeedMode = 2;
        ApplySpeed(fastSpeed, "Fast");
    }

    public void SetFasterSpeed()
    {
        currentSpeedMode = 3;
        ApplySpeed(fasterSpeed, "Faster");
    }

    private void ApplySpeed(float targetSpeed, string label)
    {
        float safeMax = Mathf.Max(0.1f, maxAllowedTimeScale);
        float safeSpeed = Mathf.Clamp(targetSpeed, 0.1f, safeMax);

        Time.timeScale = safeSpeed;

        if (applyFixedDeltaTimeScaling)
        {
            Time.fixedDeltaTime = defaultFixedDeltaTime * safeSpeed;
        }

        Debug.Log("Game Speed: " + label + " | TimeScale: " + Time.timeScale);
    }

    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }

    public int GetCurrentSpeedMode()
    {
        return currentSpeedMode;
    }
}
