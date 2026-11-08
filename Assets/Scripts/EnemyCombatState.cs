using System;
using UnityEngine;

/// <summary>Primera transicion del enemigo; no ejecuta ataques ni movimiento.</summary>
[RequireComponent(typeof(Health))]
[DisallowMultipleComponent]
public sealed class EnemyCombatState : MonoBehaviour
{
    public enum Mode
    {
        AtaqueConPociones,
        DueloCercano,
        PersecucionFrenetica
    }

    [Tooltip("Asignar el PlayerMeleeAttack del Player antes de activar el componente.")]
    [SerializeField] private PlayerMeleeAttack playerAttack;

    // Visible en el Inspector en modo Debug, sin permitir configurar el estado inicial.
    private Mode currentState = Mode.AtaqueConPociones;
    private Health health;
    private PlayerMeleeAttack subscribedAttack;

    public Mode CurrentState => currentState;
    public event Action<Mode> StateChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        subscribedAttack = playerAttack;
        if (subscribedAttack != null)
            subscribedAttack.HitConfirmed += OnHitConfirmed;
    }

    private void OnDisable()
    {
        // Conservar la fuente original permite retirar exactamente la suscripcion creada.
        if (subscribedAttack != null)
            subscribedAttack.HitConfirmed -= OnHitConfirmed;
        subscribedAttack = null;
    }

    private void OnHitConfirmed(Health target)
    {
        if (!isActiveAndEnabled || health == null || target != health || health.IsDead ||
            currentState != Mode.AtaqueConPociones)
            return;

        currentState = Mode.DueloCercano;
        StateChanged?.Invoke(currentState);
    }
}
