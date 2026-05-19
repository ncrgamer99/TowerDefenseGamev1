using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tower))]
public class TowerAimController : MonoBehaviour
{
    [Header("References")]
    public Tower tower;
    public Transform aimPivot;

    [Header("Aim")]
    public float turnSpeed = 720f;
    public bool rotateOnlyYaw = true;
    public Vector3 targetOffset = new Vector3(0f, 0.35f, 0f);
    public bool debugAim = false;

    private void Awake()
    {
        if (tower == null)
            tower = GetComponent<Tower>();
    }

    public void AimAt(Enemy target)
    {
        if (aimPivot == null || target == null)
            return;

        Vector3 targetPosition = target.transform.position + targetOffset;
        Vector3 direction = targetPosition - aimPivot.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);

        float maxDegreesDelta = Mathf.Max(0f, turnSpeed) * Time.deltaTime;
        aimPivot.rotation = Quaternion.RotateTowards(aimPivot.rotation, targetRotation, maxDegreesDelta);

        if (debugAim)
            Debug.DrawLine(aimPivot.position, targetPosition, Color.yellow);
    }
}
