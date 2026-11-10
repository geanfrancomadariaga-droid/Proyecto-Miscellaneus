using System.Collections.Generic;
using UnityEngine;

/// <summary>Aplica una sola explosion de fuego al enemigo indicado.</summary>
public sealed class FireExplosion : MonoBehaviour
{
    [Tooltip("Health del enemigo que puede recibir esta explosion.")]
    [SerializeField] private Health enemyHealth;
    [Tooltip("Radio en unidades del mundo, independiente de la escala del objeto.")]
    [SerializeField, Min(0.01f)] private float radius = 0.75f;

    private bool resolved;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    /// <summary>Configura el objetivo sin reiniciar una explosion ya resuelta.</summary>
    public void SetTarget(Health target)
    {
        enemyHealth = target;
    }

    /// <summary>Resuelve una sola vez, incluso si no alcanza al enemigo.</summary>
    public void Resolve()
    {
        if (resolved)
            return;

        // Cerrar antes del dano tambien impide duplicados desde HealthChanged.
        resolved = true;
        if (enemyHealth == null || enemyHealth.IsDead ||
            !enemyHealth.gameObject.activeInHierarchy)
            return;

        Physics2D.SyncTransforms();
        // Comprueba el objetivo directamente, sin depender de queriesHitTriggers.
        Vector2 center = transform.position;
        float effectiveRadius = Mathf.Max(0.01f, radius);
        enemyHealth.GetComponentsInChildren<Collider2D>(false, hits);
        foreach (Collider2D hit in hits)
        {
            if (!hit.enabled || !hit.gameObject.activeInHierarchy ||
                hit.GetComponentInParent<Health>() != enemyHealth)
                continue;

            if (hit.attachedRigidbody != null && !hit.attachedRigidbody.simulated)
                continue;

            // La igualdad incluye el contacto tangente con el borde del radio.
            Vector2 closestPoint = Physics2D.ClosestPoint(center, hit);
            if ((closestPoint - center).sqrMagnitude > effectiveRadius * effectiveRadius)
                continue;

            enemyHealth.TakeDamage(10);
            break; // Varios colliders del objetivo siguen siendo un solo impacto.
        }
        hits.Clear();
    }
}
