using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveDebugWindow : MonoBehaviour
{
    private class TowerDamageSummary
    {
        public TowerRole towerRole;
        public float directDamage;
        public float effectDamage;
    }

    private class EnemyDebugEntry
    {
        public int instanceId;
        public int orderNumber;
        public int waveNumber;
        public EnemyRole role;
        public EnemyVariantType variantType;
        public string status = "Aktiv";
        public float maxHealth;
        public float speed;
        public int armor;
        public int baseDamage;
        public int goldReward;
        public int killXPReward;
        public int assistXPReward;
        public int globalXPReward;
        public float slowResistance;
        public float effectDamageMultiplier;
        public bool hasChaosWaveEffect;
        public string chaosWaveEffectSummary;
        public readonly List<TowerDamageSummary> damageByTowerRole = new List<TowerDamageSummary>();
    }

    private static EnemyWaveDebugWindow instance;
    private static readonly List<EnemyDebugEntry> currentWaveEntries = new List<EnemyDebugEntry>();
    private static readonly List<EnemyDebugEntry> lastWaveEntries = new List<EnemyDebugEntry>();
    private static readonly Dictionary<int, EnemyDebugEntry> entriesByEnemyInstanceId = new Dictionary<int, EnemyDebugEntry>();
    private static int currentWaveNumber = 0;
    private static int lastWaveNumber = 0;
    private static int nextOrderNumber = 1;

    public GameManager gameManager;
    public KeyCode toggleKey = KeyCode.F3;
    public bool closeWhenLeavingBuildPhase = true;
    public Vector2 windowSize = new Vector2(920f, 620f);

    private bool windowOpen = false;
    private Rect windowRect;
    private Vector2 scrollPosition;

    private void Awake()
    {
        instance = this;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        float width = Mathf.Min(windowSize.x, Screen.width - 40f);
        float height = Mathf.Min(windowSize.y, Screen.height - 40f);
        windowRect = new Rect(20f, 20f, width, height);
    }

    private void OnEnable()
    {
        WaveEventBus.WaveStarted += HandleWaveStarted;
        WaveEventBus.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        WaveEventBus.WaveStarted -= HandleWaveStarted;
        WaveEventBus.WaveCompleted -= HandleWaveCompleted;

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        bool canOpen = IsBuildPhaseAvailable();

        if (closeWhenLeavingBuildPhase && windowOpen && !canOpen)
            windowOpen = false;

        if (Input.GetKeyDown(toggleKey) && canOpen)
            windowOpen = !windowOpen;
    }

    private void OnGUI()
    {
        if (!windowOpen || !IsBuildPhaseAvailable())
            return;

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Enemy Debug letzte Wave (" + toggleKey + ")");
    }

    private bool IsBuildPhaseAvailable()
    {
        return gameManager != null &&
               gameManager.gameStarted &&
               !gameManager.isGameOver &&
               gameManager.currentPhase == GamePhase.Build &&
               !gameManager.IsGameplayInputLockedByModalUI();
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Label("Öffnet nur in der BuildPhase. Jeder Eintrag ist ein einzelner gespawnter Gegner der letzten Wave.");

        if (lastWaveEntries.Count == 0)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Noch keine abgeschlossene Wave aufgezeichnet.");
            GUI.DragWindow();
            return;
        }

        GUILayout.Label("Wave " + lastWaveNumber + " | Gegner: " + lastWaveEntries.Count);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < lastWaveEntries.Count; i++)
        {
            EnemyDebugEntry entry = lastWaveEntries[i];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("#" + entry.orderNumber + " " + entry.role + " (" + entry.variantType + ") - " + entry.status);
            GUILayout.Label("Stats: HP " + entry.maxHealth.ToString("0") + " | Speed " + entry.speed.ToString("0.00") + " | Armor " + entry.armor + " | Base-Dmg " + entry.baseDamage);
            GUILayout.Label("Rewards: Gold " + entry.goldReward + " | KillXP " + entry.killXPReward + " | AssistXP " + entry.assistXPReward + " | GlobalXP " + entry.globalXPReward);
            GUILayout.Label("Resist/Effekte: SlowRes " + (entry.slowResistance * 100f).ToString("0") + "% | Effekt-Dmg x" + entry.effectDamageMultiplier.ToString("0.00") + GetChaosWaveText(entry));
            GUILayout.Label("Tower Damage:");

            if (entry.damageByTowerRole.Count == 0)
            {
                GUILayout.Label("- Kein Tower-Schaden registriert.");
            }
            else
            {
                for (int d = 0; d < entry.damageByTowerRole.Count; d++)
                {
                    TowerDamageSummary damage = entry.damageByTowerRole[d];
                    GUILayout.Label("- " + damage.towerRole + ": Treffer " + damage.directDamage.ToString("0.0") + " | Effekt " + damage.effectDamage.ToString("0.0") + " | Gesamt " + (damage.directDamage + damage.effectDamage).ToString("0.0"));
                }
            }

            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private string GetChaosWaveText(EnemyDebugEntry entry)
    {
        if (entry == null || !entry.hasChaosWaveEffect)
            return "";

        return " | ChaosWave: " + (string.IsNullOrEmpty(entry.chaosWaveEffectSummary) ? "Ja" : entry.chaosWaveEffectSummary);
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveEntries.Clear();
        entriesByEnemyInstanceId.Clear();
        nextOrderNumber = 1;
        currentWaveNumber = waveData != null ? waveData.waveNumber : 0;
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        lastWaveEntries.Clear();
        lastWaveEntries.AddRange(currentWaveEntries);
        lastWaveNumber = result != null ? result.waveNumber : currentWaveNumber;
    }

    public static void EnsureExists(GameManager manager, KeyCode toggleKey)
    {
        if (instance != null)
        {
            if (instance.gameManager == null)
                instance.gameManager = manager;

            instance.toggleKey = toggleKey;
            return;
        }

        GameObject host = manager != null ? manager.gameObject : new GameObject("EnemyWaveDebugWindow");
        EnemyWaveDebugWindow window = host.GetComponent<EnemyWaveDebugWindow>();

        if (window == null)
            window = host.AddComponent<EnemyWaveDebugWindow>();

        window.gameManager = manager;
        window.toggleKey = toggleKey;
        instance = window;
    }

    public static void RegisterEnemySpawned(Enemy enemy, int waveNumber)
    {
        if (enemy == null)
            return;

        int instanceId = enemy.GetInstanceID();

        if (!entriesByEnemyInstanceId.TryGetValue(instanceId, out EnemyDebugEntry entry))
        {
            entry = new EnemyDebugEntry
            {
                instanceId = instanceId,
                orderNumber = nextOrderNumber++,
                waveNumber = waveNumber > 0 ? waveNumber : currentWaveNumber,
            };

            entriesByEnemyInstanceId.Add(instanceId, entry);
            currentWaveEntries.Add(entry);
        }

        RefreshEnemySnapshot(entry, enemy, waveNumber);
    }

    public static void MarkEnemyFinished(Enemy enemy, string status)
    {
        if (enemy == null)
            return;

        if (entriesByEnemyInstanceId.TryGetValue(enemy.GetInstanceID(), out EnemyDebugEntry entry))
        {
            RefreshEnemySnapshot(entry, enemy, currentWaveNumber);
            entry.status = string.IsNullOrEmpty(status) ? entry.status : status;
        }
    }

    public static void RecordDamage(Enemy enemy, Tower sourceTower, float damageAmount, bool isEffectDamage)
    {
        if (enemy == null || sourceTower == null || damageAmount <= 0f)
            return;

        int instanceId = enemy.GetInstanceID();

        if (!entriesByEnemyInstanceId.TryGetValue(instanceId, out EnemyDebugEntry entry))
        {
            RegisterEnemySpawned(enemy, currentWaveNumber);
            entriesByEnemyInstanceId.TryGetValue(instanceId, out entry);
        }

        if (entry == null)
            return;

        TowerDamageSummary summary = GetOrCreateDamageSummary(entry, sourceTower.towerRole);

        if (isEffectDamage)
            summary.effectDamage += damageAmount;
        else
            summary.directDamage += damageAmount;
    }

    private static void RefreshEnemySnapshot(EnemyDebugEntry entry, Enemy enemy, int waveNumber)
    {
        entry.waveNumber = waveNumber > 0 ? waveNumber : entry.waveNumber;
        entry.role = enemy.enemyRole;
        entry.variantType = enemy.enemyVariantType;
        entry.maxHealth = enemy.maxHealth;
        entry.speed = enemy.speed;
        entry.armor = enemy.armor;
        entry.baseDamage = enemy.baseDamage;
        entry.goldReward = enemy.goldReward;
        entry.killXPReward = enemy.killXPReward;
        entry.assistXPReward = enemy.assistXPReward;
        entry.globalXPReward = enemy.globalXPReward;
        entry.slowResistance = enemy.slowResistance;
        entry.effectDamageMultiplier = enemy.effectDamageMultiplier;
        entry.hasChaosWaveEffect = enemy.hasChaosWaveEffect;
        entry.chaosWaveEffectSummary = enemy.chaosWaveEffectSummary;
    }

    private static TowerDamageSummary GetOrCreateDamageSummary(EnemyDebugEntry entry, TowerRole towerRole)
    {
        for (int i = 0; i < entry.damageByTowerRole.Count; i++)
        {
            if (entry.damageByTowerRole[i].towerRole == towerRole)
                return entry.damageByTowerRole[i];
        }

        TowerDamageSummary summary = new TowerDamageSummary { towerRole = towerRole };
        entry.damageByTowerRole.Add(summary);
        return summary;
    }
}
