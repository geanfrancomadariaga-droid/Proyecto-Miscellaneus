using System.Collections.Generic;
using UnityEngine;

/// <summary>Resuelve el área del prefab FireBomb una sola vez, sobre los participantes del duelo.</summary>
public sealed class FireExplosion : MonoBehaviour
{
    [Tooltip("Health del enemigo que puede recibir esta explosion.")]
    [SerializeField] private Health enemyHealth;
    [Tooltip("Radio en unidades del mundo, independiente de la escala del objeto.")]
    [SerializeField, Min(0.01f)] private float radius = 0.75f;

    private bool resolved;
    private PotionFormula formula;
    private Health playerHealth;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    /// <summary>Configura el objetivo sin reiniciar una explosion ya resuelta.</summary>
    public void SetTarget(Health target)
    {
        enemyHealth = target;
    }

    internal void Configure(PotionFormula bomb, Health player, Health opponent)
    {
        formula = bomb;
        playerHealth = player;
        SetTarget(opponent);
    }

    /// <summary>Resuelve una sola vez, incluso si no alcanza al enemigo.</summary>
    public void Resolve()
    {
        if (resolved)
            return;

        // Cerrar antes del dano tambien impide duplicados desde HealthChanged.
        resolved = true;
        Physics2D.SyncTransforms();
        // Los prefabs y llamadas del primer duelo conservan Fuego por defecto.
        if (formula == null || formula.Effect == PotionEffect.Fuego)
        {
            if (Touches(enemyHealth)) enemyHealth.TakeDamage(10);
            return;
        }

        ApplyTo(enemyHealth, false);
        if (playerHealth != enemyHealth) ApplyTo(playerHealth, true);
    }

    private void ApplyTo(Health target, bool isPlayer)
    {
        if (Touches(target) && target.TryGetComponent<PotionCombatEffects>(out var effects))
            effects.Apply(formula, isPlayer);
    }

    private bool Touches(Health target)
    {
        if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
            return false;

        // Comprueba el objetivo directamente, sin depender de queriesHitTriggers.
        Vector2 center = transform.position;
        float effectiveRadius = Mathf.Max(0.01f, radius);
        target.GetComponentsInChildren<Collider2D>(false, hits);
        foreach (Collider2D hit in hits)
        {
            if (!hit.enabled || !hit.gameObject.activeInHierarchy ||
                hit.GetComponentInParent<Health>() != target)
                continue;

            if (hit.attachedRigidbody != null && !hit.attachedRigidbody.simulated)
                continue;

            // La igualdad incluye el contacto tangente con el borde del radio.
            Vector2 closestPoint = Physics2D.ClosestPoint(center, hit);
            if ((closestPoint - center).sqrMagnitude <= effectiveRadius * effectiveRadius)
                return true;
        }
        return false;
    }
}
