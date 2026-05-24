using UnityEngine;

public class GameSpeedManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("Speed Values")]
    public float normalSpeed = 1f;
    public float fastSpeed = 2f;
    public float fasterSpeed = 6f;

    [Header("Safety")]
    public bool applyFixedDeltaTimeScaling = true;
    public float maxAllowedTimeScale = 8f;

    [Header("Meta Unlocks")]
    public bool requireGeneralMetaUnlocks = true;

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
        ResolveReferences();

        if (gameManager != null && gameManager.IsGameplayInputLockedByModalUI())
            return;

        if (Input.GetKeyDown(KeyCode.Alpha7))
            SetNormalSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha8))
            SetFastSpeed();

        if (Input.GetKeyDown(KeyCode.Alpha9))
            SetFasterSpeed();
    }

    private void ResolveReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
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
        if (!CanUseFastSpeed())
        {
            Debug.Log("Game Speed Fast ist noch gesperrt. Im Meta-Hub unter Komfort / QoL freischalten.");
            SetNormalSpeed();
            return;
        }

        currentSpeedMode = 2;
        ApplySpeed(fastSpeed, "Fast");
    }

    public void SetFasterSpeed()
    {
        if (!CanUseFasterSpeed())
        {
            Debug.Log("Game Speed Faster ist noch gesperrt. Im Meta-Hub unter Komfort / QoL freischalten.");
            SetNormalSpeed();
            return;
        }

        currentSpeedMode = 3;
        ApplySpeed(fasterSpeed, "Faster");
    }

    private bool CanUseFastSpeed()
    {
        if (!requireGeneralMetaUnlocks)
            return true;

        GeneralMetaProgressionManager generalMeta = GeneralMetaProgressionManager.GetOrCreate(gameManager);
        return generalMeta != null && generalMeta.CanUseFastSpeed();
    }

    private bool CanUseFasterSpeed()
    {
        if (!requireGeneralMetaUnlocks)
            return true;

        GeneralMetaProgressionManager generalMeta = GeneralMetaProgressionManager.GetOrCreate(gameManager);
        return generalMeta != null && generalMeta.CanUseFasterSpeed();
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
