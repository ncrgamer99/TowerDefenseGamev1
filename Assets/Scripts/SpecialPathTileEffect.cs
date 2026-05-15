using System.Collections.Generic;
using UnityEngine;

public class SpecialPathTileEffect : MonoBehaviour
{
    private static readonly Dictionary<Vector2Int, SpecialPathTileEffect> effectsByGridPosition = new Dictionary<Vector2Int, SpecialPathTileEffect>();

    [Header("Effect")]
    public PathBuildOptionType tileType = PathBuildOptionType.TrapTile;
    public Vector2Int gridPosition;
    public float tileSize = 1f;

    [Header("Trap Tile")]
    public float bleedDamagePerSecond = 3f;
    public float bleedDuration = 5f;

    [Header("Slow Tile")]
    public float slowMultiplier = 0.45f;
    public float slowDuration = 2f;

    [Header("Knock Tile")]
    public int knockBackTiles = 3;
    public float knockBackDuration = 0.35f;
    public float knockCooldown = 5f;

    [Header("Combo Tile")]
    public float comboBleedDamagePerSecond = 2f;
    public float comboBleedDuration = 4f;
    public float comboSlowMultiplier = 0.65f;
    public float comboSlowDuration = 1.5f;
    public int comboKnockBackTiles = 1;
    public float comboKnockBackDuration = 0.20f;
    public float comboCooldown = 4f;

    private float nextKnockTime = 0f;

    public void Configure(PathBuildOptionType newTileType, Vector2Int newGridPosition, float newTileSize)
    {
        if (effectsByGridPosition.ContainsKey(gridPosition) && effectsByGridPosition[gridPosition] == this)
            effectsByGridPosition.Remove(gridPosition);

        tileType = newTileType;
        gridPosition = newGridPosition;
        tileSize = Mathf.Max(0.1f, newTileSize);
        effectsByGridPosition[gridPosition] = this;
    }

    private void OnEnable()
    {
        effectsByGridPosition[gridPosition] = this;
    }

    private void OnDisable()
    {
        if (effectsByGridPosition.ContainsKey(gridPosition) && effectsByGridPosition[gridPosition] == this)
            effectsByGridPosition.Remove(gridPosition);
    }

    public static void TryApplyAtWorldPosition(Vector3 worldPosition, Enemy enemy)
    {
        if (enemy == null)
            return;

        foreach (SpecialPathTileEffect effect in effectsByGridPosition.Values)
        {
            if (effect == null)
                continue;

            Vector3 effectWorldPosition = new Vector3(
                effect.gridPosition.x * effect.tileSize,
                worldPosition.y,
                effect.gridPosition.y * effect.tileSize
            );

            if (Vector3.Distance(worldPosition, effectWorldPosition) <= Mathf.Max(0.05f, effect.tileSize * 0.25f))
            {
                effect.Apply(enemy);
                return;
            }
        }
    }

    private void Apply(Enemy enemy)
    {
        if (enemy == null)
            return;

        switch (tileType)
        {
            case PathBuildOptionType.TrapTile:
                enemy.ApplyBleed(bleedDamagePerSecond, bleedDuration);
                break;

            case PathBuildOptionType.SlowTile:
                enemy.ApplySlow(slowMultiplier, slowDuration);
                break;

            case PathBuildOptionType.KnockTile:
                TryApplyKnock(enemy, knockBackTiles, knockBackDuration, knockCooldown);
                break;

            case PathBuildOptionType.ComboTile:
                enemy.ApplyBleed(comboBleedDamagePerSecond, comboBleedDuration);
                enemy.ApplySlow(comboSlowMultiplier, comboSlowDuration);
                TryApplyKnock(enemy, comboKnockBackTiles, comboKnockBackDuration, comboCooldown);
                break;
        }
    }

    private void TryApplyKnock(Enemy enemy, int tiles, float duration, float cooldown)
    {
        if (enemy == null)
            return;

        if (enemy.IsBossOrMiniBossTarget())
            return;

        if (Time.time < nextKnockTime)
            return;

        if (enemy.KnockBackPathTiles(tiles, duration))
            nextKnockTime = Time.time + Mathf.Max(0f, cooldown);
    }
                if (Time.time < nextKnockTime)
                    return;

                if (enemy.KnockBackPathTiles(knockBackTiles, knockBackDuration))
                    nextKnockTime = Time.time + Mathf.Max(0f, knockCooldown);
                break;
        }
    }
}
