using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 40f;
    public int damage = 1;

    [Header("Status Effects")]
    public bool appliesBurn = false;
    public int burnDamage = 1;
    public float burnDuration = 3f;

    public bool appliesPoison = false;
    public int poisonDamage = 1;
    public float poisonDuration = 4f;

    public bool appliesSlow = false;
    public float slowAmount = 0.5f;
    public float slowDuration = 2f;

    private Enemy target;
    private Tower ownerTower;

    public void SetTarget(Enemy newTarget, Tower tower)
    {
        target = newTarget;
        ownerTower = tower;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position + Vector3.up * 0.3f;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        target.TakeDamage(damage, ownerTower);

        if (appliesBurn)
        {
            target.ApplyBurn(burnDamage, burnDuration, ownerTower);
        }

        if (appliesPoison)
        {
            target.ApplyPoison(poisonDamage, poisonDuration, ownerTower);
        }

        if (appliesSlow)
        {
            target.ApplySlow(slowAmount, slowDuration, ownerTower);
        }

        Destroy(gameObject);
    }
}