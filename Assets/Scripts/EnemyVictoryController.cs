using System;
using UnityEngine;

/// <summary>Registra una victoria y detiene el combate del enemigo muerto.</summary>
[DisallowMultipleComponent]
public sealed class EnemyVictoryController : MonoBehaviour
{
    [SerializeField] private Health enemyHealth;
    [SerializeField] private EnemyCombatState enemyCombatState;
    [SerializeField] private EnemyMeleeTelegraph enemyTelegraph;
    [SerializeField] private EnemyMeleeStrike enemyStrike;

    public bool IsVictorious { get; private set; }
    public event Action VictoryAchieved;

    private Health subscribedHealth;
    private bool hasStarted;

    private void OnEnable()
    {
        if (enemyHealth == null)
        {
            Debug.LogError("EnemyVictoryController: asigna Enemy Health; no se puede detectar la victoria.", this);
            enabled = false;
            return;
        }

        subscribedHealth = enemyHealth;
        subscribedHealth.Died += OnDied;
        // En el primer OnEnable, Health.Awake podria no haberse ejecutado aun.
        if (hasStarted && subscribedHealth.IsDead)
            OnDied();
    }

    private void Start()
    {
        hasStarted = true;
        WarnIfMissing(enemyCombatState, nameof(enemyCombatState));
        WarnIfMissing(enemyTelegraph, nameof(enemyTelegraph));
        WarnIfMissing(enemyStrike, nameof(enemyStrike));
        // Detecta tambien una muerte anterior a la primera suscripcion.
        if (subscribedHealth != null && subscribedHealth.IsDead)
            OnDied();
    }

    private void WarnIfMissing(UnityEngine.Object reference, string field)
    {
        if (reference == null)
            Debug.LogWarning($"EnemyVictoryController: falta {field}; no se podra detener ese componente.", this);
    }

    private void OnDied()
    {
        if (!isActiveAndEnabled || IsVictorious || subscribedHealth == null || !subscribedHealth.IsDead)
            return;

        // Marcar primero impide duplicados y reentradas desde otros eventos.
        IsVictorious = true;
        // OnDisable retira el impacto pendiente y cancela la advertencia.
        if (enemyStrike != null) enemyStrike.enabled = false;
        if (enemyTelegraph != null) enemyTelegraph.enabled = false;
        if (enemyCombatState != null) enemyCombatState.enabled = false;

        // Los observadores reciben el resultado con el combate ya detenido.
        VictoryAchieved?.Invoke();
    }

    private void OnDisable()
    {
        // Retirar la fuente original aunque se cambie la referencia del Inspector.
        if (subscribedHealth != null)
            subscribedHealth.Died -= OnDied;
        subscribedHealth = null;
        // Reactivar el controlador no reinicia la victoria ni el combate.
    }
}
