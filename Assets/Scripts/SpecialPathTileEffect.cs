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
    [HideInInspector] public float bleedDamagePerSecond = 3f;
    public float bleedDamagePerTick = 4f;
    public float bleedTickInterval = 3f;
    public float bleedDuration = 20f;

    [Header("Slow Tile")]
    public float slowMultiplier = 0.40f;
    public float slowDuration = 2f;

    [Header("Knock Tile")]
    public int knockBackTiles = 3;
    public float knockBackDuration = 0.35f;
    public float knockCooldown = 8f;

    [Header("Combo Tile")]
    public float darknessDamagePerTick = 20f;
    public float darknessTickInterval = 3f;
    public float darknessDuration = 10f;

    [Header("Weakpoint Tile")]
    public float weakpointArmorReductionPercent = 0.50f;
    public int weakpointArmorReductionFlat = 0;
    public int weakpointArmorMaxCap = 2;
    public float weakpointDuration = 10f;
    public float weakpointCooldown = 0f;

    private float nextKnockTime = 0f;
    private float nextWeakpointTime = 0f;

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

            float triggerRadius = Mathf.Max(0.05f, effect.tileSize * 0.25f);
            bool isWeakpointTile = effect.tileType == PathBuildOptionType.WeakpointTile;
            bool isInsideWeakpointTile =
                isWeakpointTile &&
                Mathf.Abs(worldPosition.x - effectWorldPosition.x) <= effect.tileSize * 0.5f &&
                Mathf.Abs(worldPosition.z - effectWorldPosition.z) <= effect.tileSize * 0.5f;

            if (isInsideWeakpointTile || Vector3.Distance(worldPosition, effectWorldPosition) <= triggerRadius)
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

        if (tileType == PathBuildOptionType.TrapTile)
        {
            enemy.ApplyBleed(bleedDamagePerTick, bleedDuration, bleedTickInterval);
        }
        else if (tileType == PathBuildOptionType.SlowTile)
        {
            enemy.ApplySlow(slowMultiplier, slowDuration);
        }
        else if (tileType == PathBuildOptionType.KnockTile)
        {
            TryApplyKnock(enemy, knockBackTiles, knockBackDuration, knockCooldown);
        }
        else if (tileType == PathBuildOptionType.ComboTile)
        {
            TryApplyDarknessCombo(enemy);
        }
        else if (tileType == PathBuildOptionType.WeakpointTile)
        {
            TryApplyWeakpoint(enemy);
        }
    }

    private void TryApplyWeakpoint(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (Time.time < nextWeakpointTime)
            return;

        enemy.ApplyWeakpointArmorBreak(weakpointArmorReductionPercent, weakpointArmorReductionFlat, weakpointArmorMaxCap, weakpointDuration);
        nextWeakpointTime = Time.time + Mathf.Max(0f, weakpointCooldown);
    }

    private void TryApplyDarknessCombo(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (!enemy.HasBurn() || !enemy.HasPoison() || !enemy.HasBleed())
            return;

        enemy.ApplyDarkness(darknessDamagePerTick, darknessDuration, darknessTickInterval);
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
        {
            nextKnockTime = Time.time + Mathf.Max(0f, cooldown);

            KnockTileVisualAnimator animator = GetComponent<KnockTileVisualAnimator>();
            if (animator != null)
                animator.PlayStrike();
        }
    }
}
