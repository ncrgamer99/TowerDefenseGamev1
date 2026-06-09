using UnityEngine;

public class TowerSupportTileEffect : MonoBehaviour
{
    [Header("Support Tile")]
    public PathBuildOptionType tileType = PathBuildOptionType.RangeTile;
    public Vector2Int gridPosition;
    public float tileSize = 1f;

    [Header("Build State")]
    public bool isOccupied = false;
    public Tower occupyingTower;

    [Header("Buff Values")]
    public float rangeBonus = 1.0f;
    public float damageMultiplierBonus = 0.25f;
    public float fireRateMultiplierBonus = 0.20f;
    public float xpMultiplierBonus = 0.25f;
    public float goldKillMultiplierBonus = 0.10f;
    public int pointUpgradePowerBonus = 1;
    public float healTileDamageMultiplier = 0.80f;
    public float healOnKillChance = 0.02f;
    public int healAmountOnKill = 1;

    [Header("Bonus Caps")]
    public float maxRangeBonus = 3.0f;
    public float maxDamageMultiplierBonus = 0.75f;
    public float maxFireRateMultiplierBonus = 0.75f;
    public float maxXPMultiplierBonus = 1.0f;
    public float maxGoldKillMultiplierBonus = 0.50f;
    public int maxPointUpgradePowerBonus = 2;

    public void Configure(PathBuildOptionType newTileType, Vector2Int newGridPosition, float newTileSize)
    {
        tileType = newTileType;
        gridPosition = newGridPosition;
        tileSize = Mathf.Max(0.1f, newTileSize);
    }

    public bool IsTowerSupportTile()
    {
        return IsTowerSupportTileType(tileType);
    }

    public bool IsAvailableForBuild()
    {
        if (!IsTowerSupportTile())
            return false;

        if (occupyingTower == null)
            isOccupied = false;

        return !isOccupied;
    }

    public bool TryBuildTowerOnTile(BuildOption option, GameManager gameManager, TileManager tileManager)
    {
        if (!IsTowerSupportTile())
            return false;

        if (option == null || option.prefab == null)
        {
            Debug.Log("Kein Tower ausgewaehlt!");
            return false;
        }

        if (option.placementType != PlacementType.BuildTile)
        {
            Debug.Log("Diese Auswahl kann nicht auf SupportTiles platziert werden.");
            return false;
        }

        if (tileManager == null || !tileManager.IsBuildAllowed())
        {
            Debug.Log("Bauen ist aktuell nicht erlaubt.");
            return false;
        }

        if (!IsAvailableForBuild())
        {
            Debug.Log("Dieses SupportTile ist bereits belegt.");
            return false;
        }

        if (gameManager == null)
        {
            Debug.LogError("TowerSupportTileEffect: GameManager fehlt!");
            return false;
        }

        int masteryAdjustedCost = BasicTowerMasteryManager.GetModifiedBuildCost(option.cost, option.prefab, option.displayName);
        GeneralMetaProgressionManager generalMeta = gameManager.GetGeneralMetaProgressionManager();
        int discountedCost = generalMeta != null ? generalMeta.GetBuildCostAfterStartOptions(masteryAdjustedCost) : masteryAdjustedCost;
        int finalCost = gameManager.GetTowerBuildCostWithTypeScaling(discountedCost, option.prefab, option.displayName);

        if (!gameManager.SpendGold(finalCost, RunGoldSpendSource.TowerBuild))
            return false;

        if (generalMeta != null)
            generalMeta.ConsumeFirstTowerDiscountIfAvailable(masteryAdjustedCost);

        if (gridPosition == Vector2Int.zero)
            gridPosition = tileManager.WorldToGridPublic(transform.position);

        Vector3 towerPosition = transform.position + Vector3.up * 0.5f;
        GameObject towerObject = Instantiate(option.prefab, towerPosition, Quaternion.identity);
        Tower builtTower = towerObject != null ? towerObject.GetComponent<Tower>() : null;

        if (builtTower != null)
        {
            builtTower.InitializeBuildData(finalCost, gridPosition);
            ApplyToTower(builtTower);
        }

        occupyingTower = builtTower;
        isOccupied = builtTower != null;

        gameManager.RegisterTowerBuilt(builtTower, finalCost, gridPosition, towerPosition, option.prefab, option.displayName);
        tileManager.RegisterTowerPosition(transform.position);

        Debug.Log("Tower auf " + GetDisplayName(tileType) + " gebaut.");
        return true;
    }

    public void ApplyToTower(Tower tower)
    {
        if (tower == null)
            return;

        TowerSupportTileBonus bonus = tower.GetComponent<TowerSupportTileBonus>();
        if (bonus == null)
            bonus = tower.gameObject.AddComponent<TowerSupportTileBonus>();

        bonus.ConfigureFromTile(this);
    }

    public void ClearOccupyingTower(Tower tower)
    {
        if (occupyingTower != null && tower != null && occupyingTower != tower)
            return;

        occupyingTower = null;
        isOccupied = false;
    }

    public static float GetRangeBonus(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.RangeTile);
        return bonus != null ? Mathf.Min(Mathf.Max(0f, bonus.rangeBonus), Mathf.Max(0f, bonus.maxRangeBonus)) : 0f;
    }

    public static float GetDamageMultiplier(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.DamageTile);
        float multiplier = 1f + GetClampedMultiplierBonus(bonus != null ? bonus.damageMultiplierBonus : 0f, bonus != null ? bonus.maxDamageMultiplierBonus : 0f);

        TowerSupportTileBonus healBonus = GetDirectBonus(tower, PathBuildOptionType.HealTile);
        if (healBonus != null)
            multiplier *= Mathf.Clamp(healBonus.healTileDamageMultiplier, 0.25f, 1f);

        return multiplier;
    }

    public static float GetFireRateMultiplier(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.RateTile);
        return 1f + GetClampedMultiplierBonus(bonus != null ? bonus.fireRateMultiplierBonus : 0f, bonus != null ? bonus.maxFireRateMultiplierBonus : 0f);
    }

    public static float GetXPMultiplier(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.XPTile);
        return 1f + GetClampedMultiplierBonus(bonus != null ? bonus.xpMultiplierBonus : 0f, bonus != null ? bonus.maxXPMultiplierBonus : 0f);
    }

    public static float GetGoldKillMultiplier(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.GoldTile);
        return 1f + GetClampedMultiplierBonus(bonus != null ? bonus.goldKillMultiplierBonus : 0f, bonus != null ? bonus.maxGoldKillMultiplierBonus : 0f);
    }

    public static int GetPointUpgradePowerBonus(Tower tower)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.UpgradeTile);
        return bonus != null ? Mathf.Min(Mathf.Max(0, bonus.pointUpgradePowerBonus), Mathf.Max(0, bonus.maxPointUpgradePowerBonus)) : 0;
    }

    public static int ApplyXPMultiplier(Tower tower, int amount)
    {
        if (amount <= 0)
            return 0;

        float multiplier = GetXPMultiplier(tower);
        float scaledAmount = amount * multiplier;
        int roundedAmount = multiplier > 1f ? Mathf.CeilToInt(scaledAmount) : Mathf.RoundToInt(scaledAmount);
        return Mathf.Max(1, roundedAmount);
    }

    public static int ApplyGoldKillMultiplier(Tower tower, int amount)
    {
        if (amount <= 0)
            return 0;

        float multiplier = GetGoldKillMultiplier(tower);
        float scaledAmount = amount * multiplier;
        int roundedAmount = multiplier > 1f ? Mathf.CeilToInt(scaledAmount) : Mathf.RoundToInt(scaledAmount);
        return Mathf.Max(1, roundedAmount);
    }

    public static bool TryRestoreLifeOnKill(Tower tower, GameManager gameManager)
    {
        TowerSupportTileBonus bonus = GetDirectBonus(tower, PathBuildOptionType.HealTile);
        if (bonus == null)
            return false;

        float chance = Mathf.Clamp01(bonus.healOnKillChance);
        int amount = Mathf.Max(0, bonus.healAmountOnKill);
        if (chance <= 0f || amount <= 0)
            return false;

        if (Random.value > chance)
            return false;

        if (gameManager == null)
            gameManager = UnityEngine.Object.FindObjectOfType<GameManager>();

        if (gameManager == null)
            return false;

        return gameManager.AddLivesCapped(amount) > 0;
    }

    public static string GetDisplayName(PathBuildOptionType type)
    {
        switch (type)
        {
            case PathBuildOptionType.RangeTile:
                return "Range Tile";
            case PathBuildOptionType.DamageTile:
                return "Damage Tile";
            case PathBuildOptionType.RateTile:
                return "Rate Tile";
            case PathBuildOptionType.XPTile:
                return "XP Tile";
            case PathBuildOptionType.UpgradeTile:
                return "Upgrade Tile";
            case PathBuildOptionType.GoldTile:
                return "Gold Tile";
            case PathBuildOptionType.HealTile:
                return "Heal Tile";
            default:
                return type.ToString();
        }
    }

    public static string GetEffectDescription(PathBuildOptionType type)
    {
        switch (type)
        {
            case PathBuildOptionType.RangeTile:
                return "Der Tower auf diesem Tile erhaelt +1 Reichweite.";
            case PathBuildOptionType.DamageTile:
                return "Der Tower auf diesem Tile verursacht +25% Schaden.";
            case PathBuildOptionType.RateTile:
                return "Der Tower auf diesem Tile feuert +20% schneller.";
            case PathBuildOptionType.XPTile:
                return "Der Tower auf diesem Tile erhaelt +25% XP.";
            case PathBuildOptionType.UpgradeTile:
                return "Point-Upgrades des Towers auf diesem Tile sind +1 staerker.";
            case PathBuildOptionType.GoldTile:
                return "Gibt beim Bau +3% Gold-Rewards. Tower auf diesem Tile erhalten +10% Gold pro Kill.";
            case PathBuildOptionType.HealTile:
                return "Tower macht weniger Schaden, hat aber 2% Chance auf +1 Leben pro Kill.";
            default:
                return "Dieses Tile hat keinen Tower-Bonus.";
        }
    }

    public static bool IsTowerSupportTileType(PathBuildOptionType type)
    {
        return type == PathBuildOptionType.RangeTile ||
               type == PathBuildOptionType.DamageTile ||
               type == PathBuildOptionType.RateTile ||
               type == PathBuildOptionType.XPTile ||
               type == PathBuildOptionType.UpgradeTile ||
               type == PathBuildOptionType.GoldTile ||
               type == PathBuildOptionType.HealTile;
    }

    private static TowerSupportTileBonus GetDirectBonus(Tower tower, PathBuildOptionType type)
    {
        if (tower == null)
            return null;

        TowerSupportTileBonus bonus = tower.GetComponent<TowerSupportTileBonus>();

        if (bonus == null || bonus.tileType != type)
            return null;

        return bonus;
    }

    private static float GetClampedMultiplierBonus(float bonus, float cap)
    {
        float safeBonus = Mathf.Max(0f, bonus);
        float safeCap = Mathf.Max(0f, cap);
        return safeCap > 0f ? Mathf.Min(safeBonus, safeCap) : safeBonus;
    }
}
