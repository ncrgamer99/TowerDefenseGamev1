using System.Collections.Generic;
using UnityEngine;

public class TowerSupportTileEffect : MonoBehaviour
{
    private static readonly List<TowerSupportTileEffect> activeSupportTiles = new List<TowerSupportTileEffect>();

    [Header("Support Tile")]
    public PathBuildOptionType tileType = PathBuildOptionType.RangeTile;
    public Vector2Int gridPosition;
    public float tileSize = 1f;
    public float auraRadius = 2.25f;

    [Header("Buff Values")]
    public float rangeBonus = 1.0f;
    public float damageMultiplierBonus = 0.20f;
    public float fireRateMultiplierBonus = 0.20f;
    public float xpMultiplierBonus = 0.25f;
    public int pointUpgradePowerBonus = 1;

    [Header("Stack Caps")]
    public float maxRangeBonus = 3.0f;
    public float maxDamageMultiplierBonus = 0.75f;
    public float maxFireRateMultiplierBonus = 0.75f;
    public float maxXPMultiplierBonus = 1.0f;
    public int maxPointUpgradePowerBonus = 2;

    public void Configure(PathBuildOptionType newTileType, Vector2Int newGridPosition, float newTileSize)
    {
        tileType = newTileType;
        gridPosition = newGridPosition;
        tileSize = Mathf.Max(0.1f, newTileSize);
        auraRadius = Mathf.Max(tileSize, auraRadius);
        RegisterIfNeeded();
    }

    private void OnEnable()
    {
        RegisterIfNeeded();
    }

    private void OnDisable()
    {
        activeSupportTiles.Remove(this);
    }

    private void RegisterIfNeeded()
    {
        if (!activeSupportTiles.Contains(this))
            activeSupportTiles.Add(this);
    }

    public static float GetRangeBonus(Tower tower)
    {
        float bonus = 0f;
        float cap = 0f;

        foreach (TowerSupportTileEffect tile in activeSupportTiles)
        {
            if (!IsValidTileForTower(tile, tower, PathBuildOptionType.RangeTile))
                continue;

            bonus += Mathf.Max(0f, tile.rangeBonus);
            cap = Mathf.Max(cap, tile.maxRangeBonus);
        }

        return cap > 0f ? Mathf.Min(bonus, cap) : bonus;
    }

    public static float GetDamageMultiplier(Tower tower)
    {
        return 1f + GetCappedMultiplierBonus(tower, PathBuildOptionType.DamageTile, tile => tile.damageMultiplierBonus, tile => tile.maxDamageMultiplierBonus);
    }

    public static float GetFireRateMultiplier(Tower tower)
    {
        return 1f + GetCappedMultiplierBonus(tower, PathBuildOptionType.RateTile, tile => tile.fireRateMultiplierBonus, tile => tile.maxFireRateMultiplierBonus);
    }

    public static float GetXPMultiplier(Tower tower)
    {
        return 1f + GetCappedMultiplierBonus(tower, PathBuildOptionType.XPTile, tile => tile.xpMultiplierBonus, tile => tile.maxXPMultiplierBonus);
    }

    public static int GetPointUpgradePowerBonus(Tower tower)
    {
        int bonus = 0;
        int cap = 0;

        foreach (TowerSupportTileEffect tile in activeSupportTiles)
        {
            if (!IsValidTileForTower(tile, tower, PathBuildOptionType.UpgradeTile))
                continue;

            bonus += Mathf.Max(0, tile.pointUpgradePowerBonus);
            cap = Mathf.Max(cap, tile.maxPointUpgradePowerBonus);
        }

        return cap > 0 ? Mathf.Min(bonus, cap) : bonus;
    }

    public static int ApplyXPMultiplier(Tower tower, int amount)
    {
        if (amount <= 0)
            return 0;

        float multiplier = GetXPMultiplier(tower);
        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    private static float GetCappedMultiplierBonus(Tower tower, PathBuildOptionType type, System.Func<TowerSupportTileEffect, float> getBonus, System.Func<TowerSupportTileEffect, float> getCap)
    {
        float bonus = 0f;
        float cap = 0f;

        foreach (TowerSupportTileEffect tile in activeSupportTiles)
        {
            if (!IsValidTileForTower(tile, tower, type))
                continue;

            bonus += Mathf.Max(0f, getBonus(tile));
            cap = Mathf.Max(cap, getCap(tile));
        }

        return cap > 0f ? Mathf.Min(bonus, cap) : bonus;
    }

    private static bool IsValidTileForTower(TowerSupportTileEffect tile, Tower tower, PathBuildOptionType type)
    {
        if (tile == null || tower == null)
            return false;

        if (tile.tileType != type)
            return false;

        float radius = Mathf.Max(tile.tileSize, tile.auraRadius);
        return Vector3.Distance(tile.transform.position, tower.transform.position) <= radius;
    }
}
