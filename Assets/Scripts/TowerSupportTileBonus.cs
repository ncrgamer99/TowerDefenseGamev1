using UnityEngine;

public class TowerSupportTileBonus : MonoBehaviour
{
    [Header("Support Tile Bonus")]
    public PathBuildOptionType tileType = PathBuildOptionType.RangeTile;
    public TowerSupportTileEffect sourceTile;

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

    [Header("Caps")]
    public float maxRangeBonus = 3.0f;
    public float maxDamageMultiplierBonus = 0.75f;
    public float maxFireRateMultiplierBonus = 0.75f;
    public float maxXPMultiplierBonus = 1.0f;
    public float maxGoldKillMultiplierBonus = 0.50f;
    public int maxPointUpgradePowerBonus = 2;

    public void ConfigureFromTile(TowerSupportTileEffect tile)
    {
        if (tile == null)
            return;

        tileType = tile.tileType;
        sourceTile = tile;

        rangeBonus = tile.rangeBonus;
        damageMultiplierBonus = tile.damageMultiplierBonus;
        fireRateMultiplierBonus = tile.fireRateMultiplierBonus;
        xpMultiplierBonus = tile.xpMultiplierBonus;
        goldKillMultiplierBonus = tile.goldKillMultiplierBonus;
        pointUpgradePowerBonus = tile.pointUpgradePowerBonus;
        healTileDamageMultiplier = tile.healTileDamageMultiplier;
        healOnKillChance = tile.healOnKillChance;
        healAmountOnKill = tile.healAmountOnKill;

        maxRangeBonus = tile.maxRangeBonus;
        maxDamageMultiplierBonus = tile.maxDamageMultiplierBonus;
        maxFireRateMultiplierBonus = tile.maxFireRateMultiplierBonus;
        maxXPMultiplierBonus = tile.maxXPMultiplierBonus;
        maxGoldKillMultiplierBonus = tile.maxGoldKillMultiplierBonus;
        maxPointUpgradePowerBonus = tile.maxPointUpgradePowerBonus;
    }

    private void OnDestroy()
    {
        if (sourceTile == null)
            return;

        Tower tower = GetComponent<Tower>();
        sourceTile.ClearOccupyingTower(tower);
    }
}
