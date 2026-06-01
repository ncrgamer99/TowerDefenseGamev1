using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum AlchemistTowerMasteryPath
{
    Trunk,
    Catalysis,
    Distillation,
    Transmutation
}

public enum AlchemistTowerKeystone
{
    None,
    ReactionCore,
    StableBrew,
    GoldenReaction
}

[Serializable]
public class AlchemistTowerMasteryNodeState
{
    public string nodeId;
    public int rank;
}

public class AlchemistTowerMasteryNodeDefinition
{
    public string nodeId;
    public string displayName;
    public AlchemistTowerMasteryPath path;
    public TowerMasteryMilestone gate;
    public AlchemistTowerKeystone keystone;
    public int maxRank;
    public int[] rankCosts;
    public string effectText;

    public bool IsKeystone => keystone != AlchemistTowerKeystone.None;

    public int GetCostForNextRank(int currentRank)
    {
        return TowerMasteryManager.GetMasteryNodeCostForNextRank(currentRank, maxRank, rankCosts);
    }
}

public class AlchemistTowerMasteryShotContext
{
    public Enemy primaryTarget;
    public int statusCountAtShot;
}

public class AlchemistTowerMasteryManager : MonoBehaviour
{
    private const string PlayerPrefsPrefix = "TD_AlchemistMastery_";

    public const string CleanMixture = "clean_mixture";
    public const string BitterPoison = "bitter_poison";
    public const string HeavyVapors = "heavy_vapors";
    public const string LabRange = "lab_range";
    public const string AnalysisProtocol = "analysis_protocol";

    public const string ReactionTheory = "reaction_theory";
    public const string UnstableMixture = "unstable_mixture";
    public const string CatalysisPoint = "catalysis_point";
    public const string UnstableHit = "unstable_hit";
    public const string StatusWindow = "status_window";
    public const string TripleReaction = "triple_reaction";
    public const string RiftCatalysis = "rift_catalysis";
    public const string ComboPreparation = "combo_preparation";
    public const string ReactionCore = "reaction_core";

    public const string PureDistillate = "pure_distillate";
    public const string HeavyMist = "heavy_mist";
    public const string DoubleSolution = "double_solution";
    public const string FineDosage = "fine_dosage";
    public const string SlowPoison = "slow_poison";
    public const string StableMist = "stable_mist";
    public const string ResistanceSample = "resistance_sample";
    public const string LearnerAnalysis = "learner_analysis";
    public const string StableBrew = "stable_brew";

    public const string CopperFormula = "copper_formula";
    public const string StudyNotes = "study_notes";
    public const string CleanExtraction = "clean_extraction";
    public const string MiniBossSample = "miniboss_sample";
    public const string BossSample = "boss_sample";
    public const string GoldenTrail = "golden_trail";
    public const string RiftSample = "rift_sample";
    public const string JustAnalysis = "just_analysis";
    public const string GoldenReaction = "golden_reaction";

    public static AlchemistTowerMasteryManager Instance { get; private set; }

    [Header("References")]
    public GameManager gameManager;

    [Header("Nodes")]
    public List<AlchemistTowerMasteryNodeState> nodeStates = new List<AlchemistTowerMasteryNodeState>();

    [Header("XP Rewards")]
    public float directDamageToMasteryXPRatio = 0.03f;
    public int maxDirectDamageMasteryXPPerWave = 16;
    public int debuffApplicationXP = 1;
    public int maxDebuffApplicationXPPerWave = 20;
    public int statusAssistXP = 5;
    public int catalystBonusXP = 3;
    public int tankKnightDebuffKillXP = 8;
    public int miniBossDebuffBonusXP = 14;
    public int bossDebuffBonusXP = 34;
    public int chaosVariantKillXP = 8;
    public int resistanceToughnessWaveXP = 12;
    public int transmutationSampleXP = 20;

    [Header("Status Caps")]
    public int statusAssistsForMasteryGate = 80;
    public int maxAssistTowerXPBonusPerWave = 12;
    public int maxTransmutationGoldPerWave = 4;
    public int reactionCooldownMilliseconds = 2500;

    [Header("Effect Tuning")]
    public float alchemistDebuffMarkDuration = 4f;
    public float unstableDuration = 2.5f;
    public float reactionCoreDebuffExtension = 0.35f;
    public int tripleReactionBonusDamage = 2;
    public int reactionCoreBonusDamage = 4;

    private readonly List<AlchemistTowerMasteryNodeDefinition> definitions = new List<AlchemistTowerMasteryNodeDefinition>();
    private readonly Dictionary<string, AlchemistTowerMasteryNodeDefinition> definitionById = new Dictionary<string, AlchemistTowerMasteryNodeDefinition>();
    private readonly HashSet<int> catalysisXPEnemiesThisWave = new HashSet<int>();
    private readonly HashSet<int> tripleReactionEnemiesThisWave = new HashSet<int>();
    private readonly HashSet<int> miniBossSamplesThisRun = new HashSet<int>();
    private readonly HashSet<int> bossSamplesThisRun = new HashSet<int>();
    private readonly Dictionary<int, float> reactionCooldownByEnemyId = new Dictionary<int, float>();

    private int currentWaveNumber = 0;
    private bool currentWaveHadAlchemistContribution = false;
    private bool currentWaveHasResistance = false;
    private bool currentWaveHasToughness = false;
    private float currentWaveDirectMasteryXP = 0f;
    private float directMasteryXPFraction = 0f;
    private int currentWaveDebuffApplicationXP = 0;
    private int currentWaveAssistTowerXPBonus = 0;
    private int currentWaveTransmutationGold = 0;
    private bool copperFormulaAwardedThisWave = false;
    private bool goldTrailAwardedThisWave = false;
    private bool goldenReactionAwardedThisWave = false;
    private int totalStatusAssists = 0;

    public static AlchemistTowerMasteryManager GetOrCreate(GameManager preferredGameManager = null)
    {
        if (Instance != null)
        {
            if (preferredGameManager != null)
                Instance.gameManager = preferredGameManager;

            return Instance;
        }

        AlchemistTowerMasteryManager existing = FindObjectOfType<AlchemistTowerMasteryManager>();
        if (existing != null)
        {
            if (preferredGameManager != null)
                existing.gameManager = preferredGameManager;

            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("AlchemistTowerMasteryManager");
        AlchemistTowerMasteryManager manager = go.AddComponent<AlchemistTowerMasteryManager>();
        manager.gameManager = preferredGameManager;
        return manager;
    }

    public static bool TryGetActive(out AlchemistTowerMasteryManager manager)
    {
        manager = Instance;
        return manager != null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildDefinitions();
        LoadProfile();
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
    }

    public void StartNewRun()
    {
        currentWaveNumber = 0;
        currentWaveHadAlchemistContribution = false;
        currentWaveHasResistance = false;
        currentWaveHasToughness = false;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWaveDebuffApplicationXP = 0;
        currentWaveAssistTowerXPBonus = 0;
        currentWaveTransmutationGold = 0;
        copperFormulaAwardedThisWave = false;
        goldTrailAwardedThisWave = false;
        goldenReactionAwardedThisWave = false;
        catalysisXPEnemiesThisWave.Clear();
        tripleReactionEnemiesThisWave.Clear();
        miniBossSamplesThisRun.Clear();
        bossSamplesThisRun.Clear();
        reactionCooldownByEnemyId.Clear();
    }

    public void HandleTowerBuilt(Tower tower)
    {
        if (!IsAlchemistTower(tower))
            return;
    }

    public void RecordAlchemistKill(Tower tower, EnemyRole killedRole)
    {
        if (!IsAlchemistTower(tower))
            return;

        currentWaveHadAlchemistContribution = true;
    }

    public void RecordAlchemistAssist(Tower tower, EnemyRole assistedRole)
    {
        if (!IsAlchemistTower(tower))
            return;

        currentWaveHadAlchemistContribution = true;
        AwardStudyNotesXP(tower);
    }

    public void RecordAlchemistDirectDamage(Tower tower, Enemy enemy, float appliedDamage)
    {
        if (!IsAlchemistTower(tower) || enemy == null || appliedDamage <= 0f)
            return;

        currentWaveHadAlchemistContribution = true;

        if (currentWaveDirectMasteryXP < maxDirectDamageMasteryXPPerWave)
        {
            directMasteryXPFraction += appliedDamage * Mathf.Max(0f, directDamageToMasteryXPRatio);
            int wholeXP = Mathf.FloorToInt(directMasteryXPFraction);
            int remainingCap = Mathf.Max(0, maxDirectDamageMasteryXPPerWave - Mathf.FloorToInt(currentWaveDirectMasteryXP));
            int awarded = Mathf.Min(wholeXP, remainingCap);

            if (awarded > 0)
            {
                directMasteryXPFraction -= awarded;
                currentWaveDirectMasteryXP += awarded;
                AddAlchemistMasteryXP(awarded);
            }
        }

        TryAwardBossDebuffDamageXP(enemy);
    }

    public void RecordAlchemistPoisonDamage(Tower tower, Enemy enemy, float appliedDamage)
    {
        if (!IsAlchemistTower(tower) || enemy == null || appliedDamage <= 0f)
            return;

        currentWaveHadAlchemistContribution = true;
        TryAwardBossDebuffDamageXP(enemy);
    }

    public void RecordAlchemistPoisonApplied(Tower tower, Enemy enemy, float duration)
    {
        if (!IsAlchemistTower(tower) || enemy == null)
            return;

        currentWaveHadAlchemistContribution = true;
        enemy.ApplyAlchemistDebuffMark(GetAlchemistDebuffMarkDuration(duration), tower);
        AwardDebuffApplicationXP();
        TryAwardCatalysisXP(enemy);

        if (GetNodeRank(CatalysisPoint) > 0 && enemy.HasPoison() && enemy.HasSlow())
            enemy.ApplyAlchemistUnstable(GetUnstableDuration(), tower);
    }

    public void RecordAlchemistSlowApplied(Tower tower, Enemy enemy, float slowMultiplier, float duration)
    {
        if (!IsAlchemistTower(tower) || enemy == null)
            return;

        currentWaveHadAlchemistContribution = true;
        enemy.ApplyAlchemistDebuffMark(GetAlchemistDebuffMarkDuration(duration), tower);
        AwardDebuffApplicationXP();
        TryAwardCatalysisXP(enemy);

        if (GetNodeRank(CatalysisPoint) > 0 && enemy.HasPoison() && enemy.HasSlow())
            enemy.ApplyAlchemistUnstable(GetUnstableDuration(), tower);
    }

    public void RecordEnemyDefeated(Enemy enemy, Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (enemy == null)
            return;

        Tower alchemistContributor = FindAlchemistContributor(killingTower, contributors);
        bool hadAlchemistDebuff = enemy.WasRecentlyDebuffedByAlchemist();
        Tower debuffSource = enemy.GetAlchemistDebuffSourceTower();

        if (alchemistContributor == null && !hadAlchemistDebuff)
            return;

        currentWaveHadAlchemistContribution = true;
        Tower rewardTower = alchemistContributor != null ? alchemistContributor : debuffSource;
        bool statusKill = enemy.GetActiveStatusEffectCount() >= 2 || hadAlchemistDebuff;

        totalStatusAssists += 1;
        MarkMasteryThreeObjective(false);
        AddAlchemistMasteryXP(GetAssistMasteryXP());
        TryAwardCopperFormula();

        if (enemy.enemyRole == EnemyRole.Tank || enemy.enemyRole == EnemyRole.Knight)
            AddAlchemistMasteryXP(tankKnightDebuffKillXP);

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
        {
            AddAlchemistMasteryXP(miniBossDebuffBonusXP);
            MarkMasteryThreeObjective(true);
            TryAwardRunSampleXP(enemy, miniBossSamplesThisRun, GetNodeRank(MiniBossSample) > 0);
        }

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
        {
            AddAlchemistMasteryXP(bossDebuffBonusXP);
            MarkMasteryThreeObjective(true);
            TryAwardRunSampleXP(enemy, bossSamplesThisRun, GetNodeRank(BossSample) > 0);
        }

        if (enemy.IsChaosVariant())
        {
            AddAlchemistMasteryXP(chaosVariantKillXP);
            if (GetNodeRank(RiftSample) > 0)
                AddAlchemistMasteryXP(4);
        }

        if (statusKill)
        {
            TryAwardCleanExtractionXP(rewardTower);
            TryAwardGoldTrail();
            TryAwardGoldenReaction(rewardTower);
        }
    }

    public AlchemistTowerMasteryShotContext PrepareAlchemistShot(Tower tower, Enemy target)
    {
        return new AlchemistTowerMasteryShotContext
        {
            primaryTarget = target,
            statusCountAtShot = target != null ? target.GetActiveStatusEffectCount() : 0
        };
    }

    public int CalculateAlchemistShotDamage(Tower tower, Enemy target, int baseDamage, AlchemistTowerMasteryShotContext context)
    {
        float damage = Mathf.Max(0, baseDamage);

        if (target == null)
            return Mathf.Max(0, Mathf.RoundToInt(damage));

        int statusCount = context != null ? Mathf.Max(context.statusCountAtShot, target.GetActiveStatusEffectCount()) : target.GetActiveStatusEffectCount();

        if (statusCount >= 2)
            damage *= 1f + GetNodeRank(ReactionTheory) * 0.01f;

        if (target.HasAlchemistUnstable() && GetNodeRank(UnstableHit) > 0)
            damage *= 1.08f;

        if (target.enemyRole == EnemyRole.Learner)
            damage *= 1f + GetNodeRank(LearnerAnalysis) * 0.05f;

        if (target.HasPoison() && target.HasSlow() && GetNodeRank(SlowPoison) > 0)
            damage *= 1.08f;

        if (target.IsChaosVariant() && statusCount >= 2 && GetNodeRank(RiftCatalysis) > 0 && !target.immuneToEffects)
            damage *= 1.05f;

        return Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    public void ApplyAlchemistPostHitEffects(Tower tower, Enemy enemy)
    {
        if (!IsAlchemistTower(tower) || enemy == null || enemy.currentHealth <= 0f)
            return;

        int statusCount = enemy.GetActiveStatusEffectCount();

        if (GetNodeRank(CatalysisPoint) > 0 && enemy.HasPoison() && enemy.HasSlow())
            enemy.ApplyAlchemistUnstable(GetUnstableDuration(), tower);

        if (GetNodeRank(TripleReaction) > 0 && statusCount >= 3 && !tripleReactionEnemiesThisWave.Contains(enemy.GetInstanceID()))
        {
            tripleReactionEnemiesThisWave.Add(enemy.GetInstanceID());
            enemy.TakeDamage(Mathf.Max(1, tripleReactionBonusDamage), tower, false, 0);
            TryAwardCatalysisXP(enemy);
        }

        if (GetActiveKeystone() == AlchemistTowerKeystone.ReactionCore && statusCount >= 2 && CanUseReaction(enemy))
        {
            enemy.TakeDamage(Mathf.Max(1, reactionCoreBonusDamage), tower, false, 0);
            enemy.ApplyAlchemistDebuffMark(reactionCoreDebuffExtension + GetAlchemistDebuffMarkDuration(0.5f), tower);
            TryAwardCatalysisXP(enemy);
        }
    }

    public float GetModifiedAlchemistPoisonDamage(Tower tower, Enemy enemy, float baseDamage)
    {
        if (!IsAlchemistTower(tower))
            return baseDamage;

        float damage = Mathf.Max(0f, baseDamage);
        damage += GetNodeRank(BitterPoison) * 0.2f;
        damage += GetNodeRank(PureDistillate) * 0.25f;

        if (enemy != null && enemy.HasPoison() && enemy.HasSlow() && GetNodeRank(SlowPoison) > 0)
            damage *= 1.08f;

        return damage;
    }

    public float GetModifiedAlchemistPoisonDuration(Tower tower, Enemy enemy, float baseDuration)
    {
        if (!IsAlchemistTower(tower))
            return baseDuration;

        float duration = Mathf.Max(0f, baseDuration);

        if (enemy != null && enemy.HasSlow() && GetNodeRank(DoubleSolution) > 0)
            duration += 0.25f;

        if (enemy != null && EnemyAlreadyHasStatus(enemy) && GetNodeRank(UnstableMixture) > 0)
            duration += GetNodeRank(UnstableMixture) * 0.15f;

        if (GetActiveKeystone() == AlchemistTowerKeystone.StableBrew)
            duration += 0.45f;

        if (currentWaveHasResistance && (GetNodeRank(ResistanceSample) > 0 || GetActiveKeystone() == AlchemistTowerKeystone.StableBrew))
            duration += 0.25f;

        return duration;
    }

    public float GetModifiedAlchemistSlowAmount(Tower tower, Enemy enemy, float baseSlowAmount)
    {
        if (!IsAlchemistTower(tower))
            return baseSlowAmount;

        float slowAmount = Mathf.Clamp(baseSlowAmount, 0.1f, 1f);

        if (GetNodeRank(StableMist) > 0)
            slowAmount = Mathf.Max(0.1f, slowAmount - 0.02f);

        if (GetActiveKeystone() == AlchemistTowerKeystone.StableBrew)
            slowAmount = Mathf.Max(0.1f, slowAmount - 0.03f);

        return slowAmount;
    }

    public float GetModifiedAlchemistSlowDuration(Tower tower, Enemy enemy, float baseDuration)
    {
        if (!IsAlchemistTower(tower))
            return baseDuration;

        float duration = Mathf.Max(0f, baseDuration);
        duration += GetNodeRank(HeavyVapors) * 0.1f;
        duration += GetNodeRank(HeavyMist) * 0.15f;

        if (enemy != null && enemy.HasPoison() && GetNodeRank(DoubleSolution) > 0)
            duration += 0.25f;

        if (enemy != null && EnemyAlreadyHasStatus(enemy) && GetNodeRank(UnstableMixture) > 0)
            duration += GetNodeRank(UnstableMixture) * 0.15f;

        if (GetActiveKeystone() == AlchemistTowerKeystone.StableBrew)
            duration += 0.45f;

        if (currentWaveHasResistance && (GetNodeRank(ResistanceSample) > 0 || GetActiveKeystone() == AlchemistTowerKeystone.StableBrew))
            duration += 0.25f;

        return duration;
    }

    public float GetAlchemistDamageBaseBonus()
    {
        return GetNodeRank(CleanMixture) * 0.2f;
    }

    public float GetAlchemistRangeBonus()
    {
        return GetNodeRank(LabRange) * 0.08f;
    }

    public float GetAlchemistFireRateAdditive()
    {
        return GetNodeRank(FineDosage) * 0.03f;
    }

    public float GetAlchemistXPMultiplier()
    {
        return 1f + GetNodeRank(AnalysisProtocol) * 0.03f;
    }

    public int GetNodeRank(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return 0;

        foreach (AlchemistTowerMasteryNodeState state in nodeStates)
        {
            if (state != null && state.nodeId == nodeId)
                return Mathf.Max(0, state.rank);
        }

        return 0;
    }

    public AlchemistTowerMasteryNodeDefinition GetDefinition(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        definitionById.TryGetValue(nodeId, out AlchemistTowerMasteryNodeDefinition definition);
        return definition;
    }

    public IEnumerable<AlchemistTowerMasteryNodeDefinition> GetDefinitions()
    {
        return definitions;
    }

    public bool IsMilestoneUnlocked(TowerMasteryMilestone milestone)
    {
        if (milestone == TowerMasteryMilestone.None)
            return true;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.IsMilestoneUnlocked(TowerRole.Alchemist, milestone);
    }

    public bool CanEditMetaProgression()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery == null || towerMastery.CanEditMetaProgression();
    }

    public bool CanPurchaseNode(string nodeId)
    {
        AlchemistTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanEditMetaProgression())
            return false;

        int rank = GetNodeRank(nodeId);
        if (rank >= definition.maxRank)
            return false;

        if (!IsMilestoneUnlocked(definition.gate))
            return false;

        TowerMasteryRoleProfile profile = GetAlchemistProfile();
        return profile != null && profile.unspentPoints >= definition.GetCostForNextRank(rank);
    }

    public bool TryPurchaseNode(string nodeId)
    {
        AlchemistTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null || !CanPurchaseNode(nodeId))
            return false;

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (towerMastery == null || !towerMastery.TrySpendRolePoints(TowerRole.Alchemist, cost))
            return false;

        SetNodeRank(nodeId, rank + 1);

        if (definition.keystone != AlchemistTowerKeystone.None && GetActiveKeystone() == AlchemistTowerKeystone.None)
            TryActivateKeystone(definition.keystone);

        SaveProfile();
        return true;
    }

    public bool CanActivateKeystone(AlchemistTowerKeystone keystone)
    {
        if (keystone == AlchemistTowerKeystone.None || !CanEditMetaProgression())
            return false;

        return GetNodeRank(GetKeystoneNodeId(keystone)) > 0;
    }

    public bool TryActivateKeystone(AlchemistTowerKeystone keystone)
    {
        if (!CanActivateKeystone(keystone))
            return false;

        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null && towerMastery.TrySetActiveKeystone(TowerRole.Alchemist, keystone.ToString());
    }

    public AlchemistTowerKeystone GetActiveKeystone()
    {
        TowerMasteryRoleProfile profile = GetAlchemistProfile();

        if (profile == null || string.IsNullOrEmpty(profile.activeKeystoneId))
            return AlchemistTowerKeystone.None;

        try
        {
            return (AlchemistTowerKeystone)Enum.Parse(typeof(AlchemistTowerKeystone), profile.activeKeystoneId);
        }
        catch
        {
            return AlchemistTowerKeystone.None;
        }
    }

    public string GetNodeStateText(AlchemistTowerMasteryNodeDefinition definition)
    {
        if (definition == null)
            return "";

        int rank = GetNodeRank(definition.nodeId);

        if (rank >= definition.maxRank)
        {
            if (definition.keystone != AlchemistTowerKeystone.None && GetActiveKeystone() == definition.keystone)
                return "Aktiv";

            return "Freigeschaltet";
        }

        if (!CanEditMetaProgression())
            return "Read-only im Run";

        if (!IsMilestoneUnlocked(definition.gate))
            return "Gesperrt: " + GetMilestoneDisplayName(definition.gate);

        int cost = definition.GetCostForNextRank(rank);
        TowerMasteryRoleProfile profile = GetAlchemistProfile();
        int unspent = profile != null ? profile.unspentPoints : 0;
        return unspent >= cost ? "Kaufbar: " + cost + " Punkt(e)" : "Kosten: " + cost + " Punkt(e)";
    }

    public string GetOverviewText()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        TowerMasteryRoleProfile profile = GetAlchemistProfile();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Alchemist Mastery XP: " + (profile != null ? profile.masteryXP : 0));
        builder.AppendLine("Punkte: " + (profile != null ? profile.unspentPoints : 0) + " frei | " + (profile != null ? profile.spentPoints : 0) + " ausgegeben");
        builder.AppendLine("Bester Alchemist im Run/Ewig: " + (towerMastery != null ? towerMastery.GetHighestLevelThisRun(TowerRole.Alchemist) : 1) + " / " + (profile != null ? profile.bestLevelEver : 1));
        builder.AppendLine("Aktiver Keystone: " + GetKeystoneDisplayName(GetActiveKeystone()));
        builder.AppendLine("Status-Assists fuer Alchemist III: " + Mathf.Min(totalStatusAssists, Mathf.Max(1, statusAssistsForMasteryGate)) + " / " + Mathf.Max(1, statusAssistsForMasteryGate));
        builder.AppendLine();
        builder.AppendLine("Milestones:");
        builder.AppendLine("- Alchemist I: " + GetMilestoneProgressText(TowerMasteryMilestone.I));
        builder.AppendLine("- Alchemist II: " + GetMilestoneProgressText(TowerMasteryMilestone.II));
        builder.AppendLine("- Alchemist III: " + GetMilestoneProgressText(TowerMasteryMilestone.III));
        builder.AppendLine("- Alchemist IV: " + GetMilestoneProgressText(TowerMasteryMilestone.IV));
        builder.AppendLine("- Alchemist V: " + GetMilestoneProgressText(TowerMasteryMilestone.V));
        builder.AppendLine();
        builder.AppendLine("Punkte werden am Run-Ende ausgezahlt. Es zaehlt nur der hoechste Alchemist Tower des Runs.");
        return builder.ToString();
    }

    public string GetCompactSummaryText()
    {
        TowerMasteryRoleProfile profile = GetAlchemistProfile();

        if (profile == null)
            return "Alchemist Mastery: vorbereitet";

        return "Alchemist Mastery: XP " + profile.masteryXP +
               " | Punkte frei " + profile.unspentPoints +
               " | ausgegeben " + profile.spentPoints +
               " | letzter Run +" + profile.lastRunMasteryPointsGained + " Punkt(e), +" + profile.lastRunMasteryXPGained + " XP" +
               " | Keystone " + GetKeystoneDisplayName(GetActiveKeystone());
    }

    public string GetNodeDetailText(string nodeId)
    {
        AlchemistTowerMasteryNodeDefinition definition = GetDefinition(nodeId);

        if (definition == null)
            return GetOverviewText();

        int rank = GetNodeRank(nodeId);
        int cost = definition.GetCostForNextRank(rank);
        string text =
            "<b>" + definition.displayName + "</b>\n" +
            "Pfad: " + GetPathDisplayName(definition.path) + "\n" +
            "Rang: " + rank + " / " + definition.maxRank + "\n" +
            "Gate: " + GetMilestoneDisplayName(definition.gate) + "\n" +
            "Status: " + GetNodeStateText(definition) + "\n";

        if (rank < definition.maxRank)
            text += "Naechste Kosten: " + cost + " Punkt(e)\n";

        text += "\n" + definition.effectText;

        if (definition.keystone != AlchemistTowerKeystone.None)
            text += "\n\nKeystone-Regel: Nur ein Alchemist Keystone kann aktiv sein. Wechsel wirken erst fuer den naechsten Run.";

        return text;
    }

    public string GetPathDisplayName(AlchemistTowerMasteryPath path)
    {
        switch (path)
        {
            case AlchemistTowerMasteryPath.Catalysis: return "Katalyse";
            case AlchemistTowerMasteryPath.Distillation: return "Destillation";
            case AlchemistTowerMasteryPath.Transmutation: return "Transmutation";
            default: return "Linearer Einstieg";
        }
    }

    public string GetKeystoneDisplayName(AlchemistTowerKeystone keystone)
    {
        switch (keystone)
        {
            case AlchemistTowerKeystone.ReactionCore: return "Reaktionskern";
            case AlchemistTowerKeystone.StableBrew: return "Stabiles Gebraeu";
            case AlchemistTowerKeystone.GoldenReaction: return "Goldene Reaktion";
            default: return "Keiner";
        }
    }

    private void HandleWaveStarted(WaveData waveData)
    {
        currentWaveNumber = waveData != null ? waveData.waveNumber : currentWaveNumber + 1;
        currentWaveHadAlchemistContribution = false;
        currentWaveDirectMasteryXP = 0f;
        directMasteryXPFraction = 0f;
        currentWaveDebuffApplicationXP = 0;
        currentWaveAssistTowerXPBonus = 0;
        currentWaveTransmutationGold = 0;
        copperFormulaAwardedThisWave = false;
        goldTrailAwardedThisWave = false;
        goldenReactionAwardedThisWave = false;
        currentWaveHasResistance = WaveHasBlock(waveData, ChaosWaveBlockType.Resistance);
        currentWaveHasToughness = WaveHasBlock(waveData, ChaosWaveBlockType.Toughness);
        catalysisXPEnemiesThisWave.Clear();
        tripleReactionEnemiesThisWave.Clear();
        reactionCooldownByEnemyId.Clear();
    }

    private void HandleWaveCompleted(WaveCompletionResult result)
    {
        if (result == null || !result.waveCompleted || !currentWaveHadAlchemistContribution)
            return;

        if ((currentWaveHasResistance || currentWaveHasToughness) && result.chaosLevelAtWaveStart > 0)
            AddAlchemistMasteryXP(resistanceToughnessWaveXP);
    }

    private void BuildDefinitions()
    {
        definitions.Clear();
        definitionById.Clear();

        AddDefinition(CleanMixture, "Saubere Mischung", AlchemistTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,20 direkter Alchemist Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(BitterPoison, "Bitteres Gift", AlchemistTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,20 Poison Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(HeavyVapors, "Schwere Daempfe", AlchemistTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 5, "+0,10s Slow Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(LabRange, "Laborreichweite", AlchemistTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "+0,08 Range pro Rang.", 1, 2, 3);
        AddDefinition(AnalysisProtocol, "Analyseprotokoll", AlchemistTowerMasteryPath.Trunk, TowerMasteryMilestone.None, 3, "Alchemist Tower erhalten +3% Tower-XP pro Rang.", 1, 2, 3);

        AddDefinition(ReactionTheory, "Reaktionslehre", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.I, 5, "Gegner mit 2+ Status-Effekten nehmen +1% Alchemist-Schaden pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(UnstableMixture, "Instabile Mischung", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.II, 3, "Alchemist-Debuffs halten +0,15s laenger, wenn Ziel bereits Burn/Poison/Slow hat.", 3, 4, 5);
        AddDefinition(CatalysisPoint, "Katalysepunkt", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.II, 1, "Wenn ein Ziel Poison + Slow traegt, erhaelt es kurz instabil.", 6);
        AddDefinition(UnstableHit, "Instabiler Treffer", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.III, 1, "Instabile Ziele nehmen +8% Alchemist-Schaden.", 6);
        AddDefinition(StatusWindow, "Statusfenster", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.III, 2, "Katalysepunkt haelt +0,25s pro Rang.", 5, 7);
        AddDefinition(TripleReaction, "Dreifachreaktion", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.III, 1, "Ziele mit 3+ Status-Effekten erzeugen kleinen Bonus-Schaden durch Alchemist, stark gedeckelt.", 10);
        AddDefinition(RiftCatalysis, "Risskatalyse", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.IV, 1, "Gegen Chaos-Varianten zaehlt Katalysebonus +5%, falls Ziel nicht immun ist.", 10);
        AddDefinition(ComboPreparation, "Combo-Vorbereitung", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.IV, 1, "Bereitet spaetere Darkness-/Combo-Ausloesungen vor.", 10);
        AddDefinition(ReactionCore, "Keystone: Reaktionskern", AlchemistTowerMasteryPath.Catalysis, TowerMasteryMilestone.V, 1, "Ziele mit 2+ Status-Effekten koennen eine Katalyse-Reaktion ausloesen: kleiner Bonus-Schaden und kurze Debuff-Verlaengerung.", new int[] { 24 }, AlchemistTowerKeystone.ReactionCore);

        AddDefinition(PureDistillate, "Reines Destillat", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.I, 5, "+0,25 Poison Damage pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(HeavyMist, "Schwerer Nebel", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.I, 5, "+0,15s Slow Duration pro Rang.", 1, 2, 3, 4, 5);
        AddDefinition(DoubleSolution, "Doppelte Loesung", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.II, 1, "Poison und Slow stabilisieren einander leicht.", 6);
        AddDefinition(FineDosage, "Feine Dosierung", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.II, 3, "+0,03 Fire Rate pro Rang.", 4, 5, 6);
        AddDefinition(SlowPoison, "Langsames Gift", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.III, 1, "Vergiftete und verlangsamte Ziele nehmen +8% Alchemist-Poison/Schaden.", 8);
        AddDefinition(StableMist, "Stabiler Nebel", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.III, 1, "Slow wird etwas stabiler, ohne Slow Tower zu ersetzen.", 8);
        AddDefinition(ResistanceSample, "Resistance-Probe", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.IV, 1, "In Resistance-Chaos-Waves verlieren Alchemist-Debuffs weniger Wirkung.", 12);
        AddDefinition(LearnerAnalysis, "Learner-Analyse", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.IV, 2, "Gegen Learner zaehlt Alchemist-Direktschaden +5% pro Rang, Debuffs bleiben eingeschraenkt.", 6, 8);
        AddDefinition(StableBrew, "Keystone: Stabiles Gebraeu", AlchemistTowerMasteryPath.Distillation, TowerMasteryMilestone.V, 1, "Alchemist-Debuffs werden stabiler. Keine Immunitaetsumgehung.", new int[] { 22 }, AlchemistTowerKeystone.StableBrew);

        AddDefinition(CopperFormula, "Kupferformel", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.I, 3, "Erster Alchemist-Assist pro Wave gibt +1 Gold pro Rang.", 1, 2, 3);
        AddDefinition(StudyNotes, "Studiennotizen", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.I, 3, "Alchemist-Assists geben +2% Tower-XP pro Rang an Alchemist, stark gedeckelt.", 2, 3, 4);
        AddDefinition(CleanExtraction, "Saubere Extraktion", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.II, 1, "Wenn ein vergiftetes+verlangsamtes Ziel stirbt, gibt es kleine Bonus-XP-Chance.", 6);
        AddDefinition(MiniBossSample, "MiniBoss-Probe", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.II, 1, "Erste MiniBoss-Beteiligung pro Run gibt Bonus-Alchemist-XP.", 6);
        AddDefinition(BossSample, "Boss-Probe", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.III, 1, "Erste Boss-Beteiligung pro Run gibt Bonus-Alchemist-XP.", 8);
        AddDefinition(GoldenTrail, "Goldene Spur", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.III, 1, "Maximal einmal pro Wave: Status-Kill mit Alchemist-Beteiligung gibt kleines Gold.", 10);
        AddDefinition(RiftSample, "Rissprobe", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.IV, 1, "Chaos-Variant-Kill mit Alchemist-Beteiligung gibt kleine Bonus-Mastery-XP.", 10);
        AddDefinition(JustAnalysis, "Gerechte Analyse", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.IV, 1, "Wenn XP-Gerechtigkeit aktiv ist, Alchemist-Mastery-XP aus Assists leicht erhoeht.", 8);
        AddDefinition(GoldenReaction, "Keystone: Goldene Reaktion", AlchemistTowerMasteryPath.Transmutation, TowerMasteryMilestone.V, 1, "Einmal pro Wave kann ein Gegner mit 2+ Status-Effekten bei Tod eine kleine Gold-/XP-Reaktion ausloesen.", new int[] { 20 }, AlchemistTowerKeystone.GoldenReaction);
    }

    private void AddDefinition(string nodeId, string displayName, AlchemistTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, params int[] costs)
    {
        AddDefinition(nodeId, displayName, path, gate, maxRank, effectText, costs, AlchemistTowerKeystone.None);
    }

    private void AddDefinition(string nodeId, string displayName, AlchemistTowerMasteryPath path, TowerMasteryMilestone gate, int maxRank, string effectText, int[] costs, AlchemistTowerKeystone keystone)
    {
        AlchemistTowerMasteryNodeDefinition definition = new AlchemistTowerMasteryNodeDefinition
        {
            nodeId = nodeId,
            displayName = displayName,
            path = path,
            gate = gate,
            maxRank = Mathf.Max(1, maxRank),
            rankCosts = costs ?? new int[] { 1 },
            effectText = effectText ?? "",
            keystone = keystone
        };

        definitions.Add(definition);
        definitionById[nodeId] = definition;
    }

    private void AwardDebuffApplicationXP()
    {
        if (currentWaveDebuffApplicationXP >= maxDebuffApplicationXPPerWave)
            return;

        currentWaveDebuffApplicationXP += debuffApplicationXP;
        AddAlchemistMasteryXP(debuffApplicationXP);
    }

    private void TryAwardCatalysisXP(Enemy enemy)
    {
        if (enemy == null || enemy.GetActiveStatusEffectCount() < 2)
            return;

        int enemyId = enemy.GetInstanceID();
        if (catalysisXPEnemiesThisWave.Contains(enemyId))
            return;

        catalysisXPEnemiesThisWave.Add(enemyId);
        AddAlchemistMasteryXP(catalystBonusXP);
    }

    private void TryAwardBossDebuffDamageXP(Enemy enemy)
    {
        if (enemy == null || !enemy.WasRecentlyDebuffedByAlchemist())
            return;

        if (enemy.enemyRole == EnemyRole.MiniBoss || enemy.isMiniBoss)
            TryAwardRunSampleXP(enemy, miniBossSamplesThisRun, true);

        if (enemy.enemyRole == EnemyRole.Boss || enemy.isBoss)
            TryAwardRunSampleXP(enemy, bossSamplesThisRun, true);
    }

    private void TryAwardRunSampleXP(Enemy enemy, HashSet<int> sampleSet, bool enabled)
    {
        if (!enabled || enemy == null || sampleSet == null)
            return;

        int enemyId = enemy.GetInstanceID();
        if (sampleSet.Contains(enemyId))
            return;

        sampleSet.Add(enemyId);
        AddAlchemistMasteryXP(transmutationSampleXP);
    }

    private void TryAwardCleanExtractionXP(Tower tower)
    {
        if (tower == null || GetNodeRank(CleanExtraction) <= 0)
            return;

        if (currentWaveAssistTowerXPBonus >= maxAssistTowerXPBonusPerWave)
            return;

        int bonus = 2;
        currentWaveAssistTowerXPBonus += bonus;
        tower.AddXP(bonus);
    }

    private void TryAwardCopperFormula()
    {
        int rank = GetNodeRank(CopperFormula);
        if (rank <= 0 || copperFormulaAwardedThisWave || currentWaveTransmutationGold >= maxTransmutationGoldPerWave)
            return;

        copperFormulaAwardedThisWave = true;
        AwardGold(Mathf.Min(rank, maxTransmutationGoldPerWave - currentWaveTransmutationGold));
    }

    private void AwardStudyNotesXP(Tower tower)
    {
        int rank = GetNodeRank(StudyNotes);
        if (tower == null || rank <= 0 || currentWaveAssistTowerXPBonus >= maxAssistTowerXPBonusPerWave)
            return;

        int bonus = Mathf.Max(1, rank);
        int allowed = Mathf.Min(bonus, maxAssistTowerXPBonusPerWave - currentWaveAssistTowerXPBonus);
        currentWaveAssistTowerXPBonus += allowed;
        tower.AddXP(allowed);
    }

    private void TryAwardGoldTrail()
    {
        int rank = GetNodeRank(GoldenTrail);
        if (rank <= 0 || goldTrailAwardedThisWave || currentWaveTransmutationGold >= maxTransmutationGoldPerWave)
            return;

        goldTrailAwardedThisWave = true;
        AwardGold(Mathf.Min(2, maxTransmutationGoldPerWave - currentWaveTransmutationGold));
    }

    private void TryAwardGoldenReaction(Tower tower)
    {
        if (GetActiveKeystone() != AlchemistTowerKeystone.GoldenReaction || goldenReactionAwardedThisWave || currentWaveTransmutationGold >= maxTransmutationGoldPerWave)
            return;

        goldenReactionAwardedThisWave = true;
        AwardGold(Mathf.Min(2, maxTransmutationGoldPerWave - currentWaveTransmutationGold));

        if (tower != null && currentWaveAssistTowerXPBonus < maxAssistTowerXPBonusPerWave)
        {
            currentWaveAssistTowerXPBonus += 2;
            tower.AddXP(2);
        }
    }

    private void AwardGold(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
            return;

        currentWaveTransmutationGold += safeAmount;
        gameManager.AddGold(safeAmount, false, RunGoldSource.Other);
    }

    private int GetAssistMasteryXP()
    {
        int xp = statusAssistXP;
        if (GetNodeRank(JustAnalysis) > 0 && IsXpJusticeActive())
            xp += 2;

        return xp;
    }

    private bool IsXpJusticeActive()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        ChaosJusticeManager chaosJusticeManager = gameManager != null ? gameManager.GetChaosJusticeManager() : null;
        return chaosJusticeManager != null && chaosJusticeManager.runData != null && chaosJusticeManager.runData.xpJusticeLevel > 0;
    }

    private bool CanUseReaction(Enemy enemy)
    {
        int enemyId = enemy.GetInstanceID();
        float now = Time.time;
        if (reactionCooldownByEnemyId.TryGetValue(enemyId, out float readyTime) && now < readyTime)
            return false;

        reactionCooldownByEnemyId[enemyId] = now + Mathf.Max(0.1f, reactionCooldownMilliseconds / 1000f);
        return true;
    }

    private bool EnemyAlreadyHasStatus(Enemy enemy)
    {
        return enemy != null && (enemy.HasBurn() || enemy.HasPoison() || enemy.HasSlow());
    }

    private float GetAlchemistDebuffMarkDuration(float sourceDuration)
    {
        return Mathf.Max(0.35f, alchemistDebuffMarkDuration + Mathf.Max(0f, sourceDuration) * 0.25f);
    }

    private float GetUnstableDuration()
    {
        return Mathf.Max(0.2f, unstableDuration + GetNodeRank(StatusWindow) * 0.25f);
    }

    private Tower FindAlchemistContributor(Tower killingTower, IEnumerable<Tower> contributors)
    {
        if (IsAlchemistTower(killingTower))
            return killingTower;

        if (contributors == null)
            return null;

        foreach (Tower tower in contributors)
        {
            if (IsAlchemistTower(tower))
                return tower;
        }

        return null;
    }

    private bool IsAlchemistTower(Tower tower)
    {
        return tower != null && tower.towerRole == TowerRole.Alchemist;
    }

    private bool WaveHasBlock(WaveData waveData, ChaosWaveBlockType blockType)
    {
        if (waveData == null || waveData.chaosWaveBlocks == null)
            return false;

        foreach (ChaosWaveBlock block in waveData.chaosWaveBlocks)
        {
            if (block == null || !block.IsValid())
                continue;

            if (block.blockType == blockType)
                return true;
        }

        return false;
    }

    private void AddAlchemistMasteryXP(int amount)
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        if (towerMastery != null)
            towerMastery.AddRoleMasteryXP(TowerRole.Alchemist, amount);
    }

    private void MarkMasteryThreeObjective(bool forceByBossParticipation)
    {
        TowerMasteryRoleProfile profile = GetAlchemistProfile();
        TowerMasteryManager towerMastery = GetTowerMasteryManager();

        if (profile == null || towerMastery == null)
            return;

        if (!forceByBossParticipation && totalStatusAssists < Mathf.Max(1, statusAssistsForMasteryGate))
            return;

        profile.bossKillWithTower = true;
        towerMastery.SaveRoleProfile(TowerRole.Alchemist);
    }

    private TowerMasteryRoleProfile GetAlchemistProfile()
    {
        TowerMasteryManager towerMastery = GetTowerMasteryManager();
        return towerMastery != null ? towerMastery.GetProfile(TowerRole.Alchemist) : null;
    }

    private TowerMasteryManager GetTowerMasteryManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        return TowerMasteryManager.GetOrCreate(gameManager);
    }

    private string GetMilestoneProgressText(TowerMasteryMilestone milestone)
    {
        TowerMasteryRoleProfile profile = GetAlchemistProfile();
        bool unlocked = IsMilestoneUnlocked(milestone);
        string state = unlocked ? "frei" : "gesperrt";

        if (profile == null)
            return state;

        switch (milestone)
        {
            case TowerMasteryMilestone.I:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 10) + "/10 | Level 10 " + (profile.reachedLevel10 ? "ja" : "nein");
            case TowerMasteryMilestone.II:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 25) + "/25 | Level 20 " + (profile.reachedLevel20 ? "ja" : "nein");
            case TowerMasteryMilestone.III:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 50) + "/50 | Boss/Status-Ziel " + (profile.bossKillWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.IV:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 85) + "/85 | Chaos 3 " + (profile.chaos3WaveWithTower ? "ja" : "nein");
            case TowerMasteryMilestone.V:
                return state + " | Punkte " + Mathf.Min(profile.spentPoints, 130) + "/130 | Chaos 5/Elite " + (profile.chaos5BossOrEliteWithTower ? "ja" : "nein");
            default:
                return "frei";
        }
    }

    private string GetMilestoneDisplayName(TowerMasteryMilestone milestone)
    {
        switch (milestone)
        {
            case TowerMasteryMilestone.I: return "Alchemist I: Vertrautheit";
            case TowerMasteryMilestone.II: return "Alchemist II: Reaktionslehre";
            case TowerMasteryMilestone.III: return "Alchemist III: Laborpraxis";
            case TowerMasteryMilestone.IV: return "Alchemist IV: Rissalchemie";
            case TowerMasteryMilestone.V: return "Alchemist V: Stein der Wandlung";
            default: return "Linearer Einstieg";
        }
    }

    private string GetKeystoneNodeId(AlchemistTowerKeystone keystone)
    {
        switch (keystone)
        {
            case AlchemistTowerKeystone.ReactionCore: return ReactionCore;
            case AlchemistTowerKeystone.StableBrew: return StableBrew;
            case AlchemistTowerKeystone.GoldenReaction: return GoldenReaction;
            default: return "";
        }
    }

    private void SetNodeRank(string nodeId, int rank)
    {
        AlchemistTowerMasteryNodeState state = null;

        foreach (AlchemistTowerMasteryNodeState candidate in nodeStates)
        {
            if (candidate != null && candidate.nodeId == nodeId)
            {
                state = candidate;
                break;
            }
        }

        if (state == null)
        {
            state = new AlchemistTowerMasteryNodeState { nodeId = nodeId, rank = 0 };
            nodeStates.Add(state);
        }

        AlchemistTowerMasteryNodeDefinition definition = GetDefinition(nodeId);
        int maxRank = definition != null ? definition.maxRank : 1;
        state.rank = Mathf.Clamp(rank, 0, maxRank);
    }

    private void LoadProfile()
    {
        nodeStates.Clear();
        foreach (AlchemistTowerMasteryNodeDefinition definition in definitions)
        {
            int rank = PlayerPrefs.GetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, 0);
            if (rank > 0)
                nodeStates.Add(new AlchemistTowerMasteryNodeState { nodeId = definition.nodeId, rank = Mathf.Clamp(rank, 0, definition.maxRank) });
        }

        totalStatusAssists = PlayerPrefs.GetInt(PlayerPrefsPrefix + "TotalStatusAssists", 0);
    }

    private void SaveProfile()
    {
        foreach (AlchemistTowerMasteryNodeDefinition definition in definitions)
            PlayerPrefs.SetInt(PlayerPrefsPrefix + "Node_" + definition.nodeId, GetNodeRank(definition.nodeId));

        PlayerPrefs.SetInt(PlayerPrefsPrefix + "TotalStatusAssists", Mathf.Max(0, totalStatusAssists));
        PlayerPrefs.Save();
    }
}
