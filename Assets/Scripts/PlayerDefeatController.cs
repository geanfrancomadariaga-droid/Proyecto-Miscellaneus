using System;
using UnityEngine;

/// <summary>Detiene el combate una sola vez al morir el protagonista.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDefeatController : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerController playerMovement;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private PlayerMeleeAttack playerAttack;
    [SerializeField] private PotionAimInput potionAimInput;
    [SerializeField] private EnemyCombatState enemyCombatState;
    [SerializeField] private EnemyMeleeTelegraph enemyTelegraph;
    [SerializeField] private EnemyMeleeStrike enemyStrike;

    public bool IsDefeated { get; private set; }
    public event Action Defeated;

    private Health subscribedHealth;
    private bool hasStarted;

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            Debug.LogError("PlayerDefeatController: asigna Player Health; no se puede detectar la derrota.", this);
            enabled = false;
            return;
        }

        subscribedHealth = playerHealth;
        subscribedHealth.Died += OnDied;
        // En el primer OnEnable, Health.Awake podria no haberse ejecutado aun.
        if (hasStarted && subscribedHealth.IsDead)
            OnDied();
    }

    private void Start()
    {
        hasStarted = true;
        WarnIfMissing(playerMovement, nameof(playerMovement));
        WarnIfMissing(playerBody, nameof(playerBody));
        WarnIfMissing(playerAttack, nameof(playerAttack));
        WarnIfMissing(potionAimInput, nameof(potionAimInput));
        WarnIfMissing(enemyCombatState, nameof(enemyCombatState));
        WarnIfMissing(enemyTelegraph, nameof(enemyTelegraph));
        WarnIfMissing(enemyStrike, nameof(enemyStrike));
        // Cubre una muerte anterior a la suscripcion o mientras estaba deshabilitado.
        if (subscribedHealth != null && subscribedHealth.IsDead)
            OnDied();
    }

    private void WarnIfMissing(UnityEngine.Object reference, string field)
    {
        if (reference == null)
            Debug.LogWarning($"PlayerDefeatController: falta {field}; no se podra detener ese componente.", this);
    }

    private void OnDied()
    {
        if (!isActiveAndEnabled || IsDefeated)
            return;

        // Marcar antes de cancelar acciones evita reentradas desde otros eventos.
        IsDefeated = true;
        if (enemyStrike != null) enemyStrike.enabled = false;
        if (enemyTelegraph != null) enemyTelegraph.enabled = false;
        if (enemyCombatState != null) enemyCombatState.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            // Conserva los colliders; impide que fuerzas o contactos lo desplacen.
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (potionAimInput != null) potionAimInput.enabled = false;

        // La futura UI recibe el evento cuando la detencion ya esta completa.
        Defeated?.Invoke();
    }

    private void OnDisable()
    {
        if (subscribedHealth != null)
            subscribedHealth.Died -= OnDied;
        subscribedHealth = null;
        // Deshabilitar este controlador no reinicia ni reanuda el combate.
    }
}
