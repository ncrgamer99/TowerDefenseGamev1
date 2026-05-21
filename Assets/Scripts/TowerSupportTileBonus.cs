using UnityEngine;

public class TowerSupportTileBonus : MonoBehaviour
{
    [Header("Support Tile Bonus")]
    public PathBuildOptionType tileType = PathBuildOptionType.RangeTile;
    public TowerSupportTileEffect sourceTile;

    [Header("Buff Values")]
    public float rangeBonus = 1.0f;
    public float damageMultiplierBonus = 0.20f;
    public float fireRateMultiplierBonus = 0.20f;
    public float xpMultiplierBonus = 0.25f;
    public int pointUpgradePowerBonus = 1;

    [Header("Caps")]
    public float maxRangeBonus = 3.0f;
    public float maxDamageMultiplierBonus = 0.75f;
    public float maxFireRateMultiplierBonus = 0.75f;
    public float maxXPMultiplierBonus = 1.0f;
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
        pointUpgradePowerBonus = tile.pointUpgradePowerBonus;

        maxRangeBonus = tile.maxRangeBonus;
        maxDamageMultiplierBonus = tile.maxDamageMultiplierBonus;
        maxFireRateMultiplierBonus = tile.maxFireRateMultiplierBonus;
        maxXPMultiplierBonus = tile.maxXPMultiplierBonus;
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
