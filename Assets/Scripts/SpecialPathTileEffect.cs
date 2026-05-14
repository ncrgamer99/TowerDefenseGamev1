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
                if (enemy.IsBossOrMiniBossTarget())
                    return;

                if (Time.time < nextKnockTime)
                    return;

                if (enemy.KnockBackPathTiles(knockBackTiles, knockBackDuration))
                    nextKnockTime = Time.time + Mathf.Max(0f, knockCooldown);
                break;
        }
    }
}
