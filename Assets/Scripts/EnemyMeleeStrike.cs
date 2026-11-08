using System.Collections.Generic;
using UnityEngine;

/// <summary>Resuelve un impacto en la zona fija al completar un aviso valido.</summary>
[RequireComponent(typeof(EnemyMeleeTelegraph))]
[DisallowMultipleComponent]
public sealed class EnemyMeleeStrike : MonoBehaviour
{
    [Tooltip("Health del mismo Player referenciado por EnemyMeleeTelegraph.")]
    [SerializeField] private Health playerHealth;
    [Tooltip("Dano provisional por impacto enemigo segun el GDD.")]
    [SerializeField, Min(1)] private int damage = 10;

    private EnemyMeleeTelegraph telegraph;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    private void Awake()
    {
        telegraph = GetComponent<EnemyMeleeTelegraph>();
    }

    private void OnEnable()
    {
        telegraph.WarningCompleted += Strike;
    }

    private void Strike(Vector2 center, Vector2 size, float angle)
    {
        if (!isActiveAndEnabled || playerHealth == null || playerHealth.IsDead ||
            !playerHealth.gameObject.activeInHierarchy)
            return;

        // Incluye todas las capas y triggers sin depender de ajustes globales.
        // Sincroniza movimientos por Transform antes de la unica consulta.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        hits.Clear();
        Physics2D.OverlapBox(center, size, angle, filter, hits);
        foreach (Collider2D hit in hits)
        {
            if (hit.GetComponentInParent<Health>() != playerHealth)
                continue;

            playerHealth.TakeDamage(Mathf.Max(1, damage));
            break; // Varios colliders del jugador siguen siendo un solo impacto.
        }
        hits.Clear();
    }

    private void OnDisable()
    {
        if (telegraph != null)
        {
            telegraph.WarningCompleted -= Strike;
            telegraph.CancelWarning();
        }
        hits.Clear();
    }
}
