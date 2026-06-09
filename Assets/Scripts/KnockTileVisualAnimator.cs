using System.Collections.Generic;
using UnityEngine;

public class KnockTileVisualAnimator : MonoBehaviour
{
    public float idleAmplitude = 0.018f;
    public float idleSpeed = 2.5f;
    public float strikeDistance = 0.14f;
    public float strikeSpinDegrees = 0f;
    public float strikeOutTime = 0.06f;
    public float strikeReturnTime = 0.18f;

    private readonly Dictionary<Transform, Vector3> defaultLocalPositions = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Quaternion> defaultLocalRotations = new Dictionary<Transform, Quaternion>();
    private readonly List<Transform> activeMovingParts = new List<Transform>();
    private Transform activeMovingRoot;
    private Vector2Int activeDirection = Vector2Int.zero;
    private float strikeTimer;

    private void Awake()
    {
        InferActiveDirectionFromGroups();
    }

    private void OnEnable()
    {
        InferActiveDirectionFromGroups();
    }

    private void Update()
    {
        if (activeMovingRoot == null && activeMovingParts.Count == 0)
            InferActiveDirectionFromGroups();

        if (activeMovingRoot == null && activeMovingParts.Count == 0)
            return;

        if (strikeTimer <= 0f)
        {
            ResetActiveVisuals();
            return;
        }

        float totalDuration = Mathf.Max(0.01f, strikeOutTime + strikeReturnTime);
        float elapsed = totalDuration - strikeTimer;
        float strikeOffset = GetStrikeOffset(elapsed);

        if (activeMovingRoot != null)
        {
            AnimateTransform(activeMovingRoot, strikeOffset);
        }
        else
        {
            for (int i = 0; i < activeMovingParts.Count; i++)
                AnimateTransform(activeMovingParts[i], strikeOffset);
        }

        strikeTimer = Mathf.Max(0f, strikeTimer - Time.deltaTime);
        if (strikeTimer <= 0f)
            ResetActiveVisuals();
    }

    public void SetDirection(Vector2Int knockDirection)
    {
        activeDirection = NormalizeCardinal(knockDirection);
        SetGroupActive("Knock_North", activeDirection == Vector2Int.up);
        SetGroupActive("Knock_East", activeDirection == Vector2Int.right);
        SetGroupActive("Knock_South", activeDirection == Vector2Int.down);
        SetGroupActive("Knock_West", activeDirection == Vector2Int.left);
        activeMovingRoot = FindActiveMovingRoot();
        RefreshActiveMovingParts();
    }

    public void PlayStrike()
    {
        strikeTimer = Mathf.Max(0.01f, strikeOutTime + strikeReturnTime);
    }

    private float GetStrikeOffset(float elapsed)
    {
        if (elapsed <= strikeOutTime)
            return Mathf.Lerp(0f, strikeDistance, elapsed / Mathf.Max(0.01f, strikeOutTime));

        float returnElapsed = elapsed - strikeOutTime;
        return Mathf.Lerp(strikeDistance, 0f, returnElapsed / Mathf.Max(0.01f, strikeReturnTime));
    }

    private void InferActiveDirectionFromGroups()
    {
        if (IsGroupActive("Knock_North"))
            activeDirection = Vector2Int.up;
        else if (IsGroupActive("Knock_East"))
            activeDirection = Vector2Int.right;
        else if (IsGroupActive("Knock_South"))
            activeDirection = Vector2Int.down;
        else if (IsGroupActive("Knock_West"))
            activeDirection = Vector2Int.left;

        activeMovingRoot = FindActiveMovingRoot();
        RefreshActiveMovingParts();
    }

    private Transform FindActiveMovingRoot()
    {
        Transform group = GetActiveGroup();
        if (group == null)
            return null;

        return FindChildByNameContains(group, "_Moving");
    }

    private Transform GetActiveGroup()
    {
        if (activeDirection == Vector2Int.up)
            return FindChildByExactName(transform, "Knock_North");
        if (activeDirection == Vector2Int.right)
            return FindChildByExactName(transform, "Knock_East");
        if (activeDirection == Vector2Int.down)
            return FindChildByExactName(transform, "Knock_South");
        if (activeDirection == Vector2Int.left)
            return FindChildByExactName(transform, "Knock_West");

        return null;
    }

    private void SetGroupActive(string groupName, bool active)
    {
        Transform group = FindChildByExactName(transform, groupName);
        if (group != null)
            group.gameObject.SetActive(active);

        SetChildrenActiveByNamePrefix(transform, groupName + "_", active);
    }

    private bool IsGroupActive(string groupName)
    {
        Transform group = FindChildByExactName(transform, groupName);
        if (group != null)
            return group.gameObject.activeInHierarchy;

        return AnyChildByNamePrefixActive(transform, groupName + "_");
    }

    private void RefreshActiveMovingParts()
    {
        activeMovingParts.Clear();

        if (activeMovingRoot != null)
            return;

        string prefix = GetActiveGroupNamePrefix();
        if (string.IsNullOrEmpty(prefix))
            return;

        CollectMovingPartsByPrefix(transform, prefix, activeMovingParts);
    }

    private string GetActiveGroupNamePrefix()
    {
        if (activeDirection == Vector2Int.up)
            return "Knock_North_";
        if (activeDirection == Vector2Int.right)
            return "Knock_East_";
        if (activeDirection == Vector2Int.down)
            return "Knock_South_";
        if (activeDirection == Vector2Int.left)
            return "Knock_West_";

        return null;
    }

    private void AnimateTransform(Transform target, float offset)
    {
        if (target == null)
            return;

        CacheDefaultTransform(target);
        target.localPosition = defaultLocalPositions[target] + GetMovementDirectionForTarget(target) * offset;
        target.localRotation = defaultLocalRotations[target];
    }

    private void ResetActiveVisuals()
    {
        if (activeMovingRoot != null)
        {
            ResetTransform(activeMovingRoot);
            return;
        }

        for (int i = 0; i < activeMovingParts.Count; i++)
            ResetTransform(activeMovingParts[i]);
    }

    private void ResetTransform(Transform target)
    {
        if (target == null)
            return;

        CacheDefaultTransform(target);
        target.localPosition = defaultLocalPositions[target];
        target.localRotation = defaultLocalRotations[target];
    }

    private Vector3 GetMovementDirectionForTarget(Transform target)
    {
        Vector3 localDirection = GetLocalMovementDirection();

        if (target == null || target.parent == null)
            return localDirection;

        Vector3 worldDirection = transform.TransformDirection(localDirection);
        Vector3 parentLocalDirection = target.parent.InverseTransformDirection(worldDirection);

        if (parentLocalDirection.sqrMagnitude <= 0.0001f)
            return localDirection;

        return parentLocalDirection.normalized;
    }

    private Vector3 GetLocalMovementDirection()
    {
        if (activeDirection == Vector2Int.up)
            return Vector3.forward;
        if (activeDirection == Vector2Int.right)
            return Vector3.right;
        if (activeDirection == Vector2Int.down)
            return Vector3.back;
        if (activeDirection == Vector2Int.left)
            return Vector3.left;

        return Vector3.back;
    }

    private void CacheDefaultTransform(Transform target)
    {
        if (target != null && !defaultLocalPositions.ContainsKey(target))
            defaultLocalPositions[target] = target.localPosition;

        if (target != null && !defaultLocalRotations.ContainsKey(target))
            defaultLocalRotations[target] = target.localRotation;
    }

    private Vector2Int NormalizeCardinal(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0 ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0 ? 1 : -1);
    }

    private Transform FindChildByNameContains(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
            return null;

        if (root.name.Contains(namePart))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByNameContains(root.GetChild(i), namePart);
            if (result != null)
                return result;
        }

        return null;
    }

    private void SetChildrenActiveByNamePrefix(Transform root, string prefix, bool active)
    {
        if (root == null || string.IsNullOrEmpty(prefix))
            return;

        if (root.name.StartsWith(prefix))
            root.gameObject.SetActive(active);

        for (int i = 0; i < root.childCount; i++)
            SetChildrenActiveByNamePrefix(root.GetChild(i), prefix, active);
    }

    private bool AnyChildByNamePrefixActive(Transform root, string prefix)
    {
        if (root == null || string.IsNullOrEmpty(prefix))
            return false;

        if (root.name.StartsWith(prefix) && root.gameObject.activeInHierarchy)
            return true;

        for (int i = 0; i < root.childCount; i++)
        {
            if (AnyChildByNamePrefixActive(root.GetChild(i), prefix))
                return true;
        }

        return false;
    }

    private void CollectMovingPartsByPrefix(Transform root, string prefix, List<Transform> results)
    {
        if (root == null || results == null || string.IsNullOrEmpty(prefix))
            return;

        if (root.name.StartsWith(prefix) && IsMovingPartName(root.name))
            results.Add(root);

        for (int i = 0; i < root.childCount; i++)
            CollectMovingPartsByPrefix(root.GetChild(i), prefix, results);
    }

    private bool IsMovingPartName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.Contains("_PistonRod") ||
               objectName.Contains("_ImpactPlate") ||
               objectName.Contains("_ImpactGlow");
    }

    private Transform FindChildByExactName(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrEmpty(exactName))
            return null;

        if (root.name == exactName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByExactName(root.GetChild(i), exactName);
            if (result != null)
                return result;
        }

        return null;
    }
}
