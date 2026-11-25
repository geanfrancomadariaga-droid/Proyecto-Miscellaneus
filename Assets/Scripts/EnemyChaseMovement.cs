using UnityEngine;

/// <summary>Persigue en duelo cercano; no decide estados ni aplica dano.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(EnemyCombatState))]
[RequireComponent(typeof(EnemyMeleeTelegraph))]
[DisallowMultipleComponent]
public sealed class EnemyChaseMovement : MonoBehaviour
{
    [Tooltip("Transform del Player que contiene Health, o de uno de sus hijos.")]
    [SerializeField] private Transform player;
    [SerializeField, Min(0f)] private float chaseSpeed = 2f;
    [Tooltip("Distancia entre centros en XY, en unidades del mundo.")]
    [SerializeField, Min(0f)] private float stoppingDistance = 1.05f;

    private Rigidbody2D body;
    private Health health;
    private EnemyCombatState combatState;
    private EnemyMeleeTelegraph telegraph;
    private Transform cachedPlayer;
    private Health playerHealth;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        combatState = GetComponent<EnemyCombatState>();
        telegraph = GetComponent<EnemyMeleeTelegraph>();
    }

    private void FixedUpdate()
    {
        // Resolver una sola vez por referencia; permite reasignarla en el Inspector.
        if (cachedPlayer != player)
        {
            cachedPlayer = player;
            playerHealth = player != null ? player.GetComponentInParent<Health>() : null;
        }

        if (!isActiveAndEnabled || body == null || health == null || health.IsDead ||
            combatState == null || !combatState.isActiveAndEnabled ||
            combatState.CurrentState != EnemyCombatState.Mode.DueloCercano ||
            telegraph == null || telegraph.IsWarning || player == null ||
            !player.gameObject.activeInHierarchy || playerHealth == null || playerHealth.IsDead)
        {
            StopMovement();
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        float distance = Mathf.Max(0f, stoppingDistance);
        body.linearVelocity = offset.sqrMagnitude <= distance * distance
            ? Vector2.zero
            : offset.normalized * Mathf.Max(0f, chaseSpeed);
    }

    private void StopMovement()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void OnDisable()
    {
        StopMovement();
    }
}
