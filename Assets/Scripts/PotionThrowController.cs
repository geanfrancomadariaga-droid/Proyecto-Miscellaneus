using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Conecta el apuntado con las cargas y el ciclo de vida de las bombas.</summary>
public sealed class PotionThrowController : MonoBehaviour
{
    [SerializeField] private PotionAimInput aimInput;
    [SerializeField] private EquippedPotionCharges charges;
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health enemyHealth;
    [Tooltip("BombProjectile y FireExplosion deben estar en el mismo objeto.")]
    [SerializeField] private BombProjectile projectilePrefab;
    [SerializeField, Min(0f)] private float flightDuration = 0.5f;

    private PotionAimInput subscribedInput;
    private Health subscribedPlayerHealth, subscribedEnemyHealth;
    private PotionFormula capturedFormula;
    private readonly Dictionary<BombProjectile, Action> flights =
        new Dictionary<BombProjectile, Action>();

    private void OnEnable()
    {
        subscribedInput = aimInput;
        if (subscribedInput != null)
        {
            subscribedInput.AimStarted += OnAimStarted;
            subscribedInput.AimFinished += OnAimFinished;
            subscribedInput.AimCanceled += ClearCapture;
        }
        subscribedPlayerHealth = playerHealth;
        subscribedEnemyHealth = enemyHealth;
        if (subscribedPlayerHealth != null)
            subscribedPlayerHealth.Died += ClearCapture;
        if (subscribedEnemyHealth != null)
            subscribedEnemyHealth.Died += ClearCapture;
    }

    private void OnAimStarted()
    {
        ClearCapture();
        if (isActiveAndEnabled && CombatIsActive() && aimInput != null && charges != null)
            capturedFormula = charges.SelectedFormula;
    }

    private void ClearCapture()
    {
        capturedFormula = null;
    }

    private bool CombatIsActive()
    {
        return playerHealth != null && enemyHealth != null &&
            !playerHealth.IsDead && !enemyHealth.IsDead;
    }

    private void OnAimFinished(PotionAimCalculator.Result result)
    {
        // Cerrar la captura incluso si la liberacion se rechaza; nunca releer la seleccion.
        PotionFormula formula = capturedFormula;
        ClearCapture();
        if (!isActiveAndEnabled || !result.IsValid || !CombatIsActive() ||
            aimInput == null || charges == null || formula == null ||
            formula.Base != PotionBase.Bomba || formula.Effect != PotionEffect.Fuego ||
            formula.Modifier != PotionModifier.Ninguno ||
            projectilePrefab == null ||
            !projectilePrefab.TryGetComponent<FireExplosion>(out _) ||
            float.IsNaN(flightDuration) || float.IsInfinity(flightDuration))
            return;

        Vector3 origin = playerHealth.transform.position;
        Vector3 destination = origin + (Vector3)(result.Direction * result.Distance);
        BombProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        if (projectile == null)
            return;

        // Validar la instancia antes de consumir; un rechazo nunca inicia el vuelo.
        if (!projectile.TryGetComponent<FireExplosion>(out var explosion) ||
            !charges.TryConsume(formula))
        {
            Destroy(projectile.gameObject);
            return;
        }

        explosion.SetTarget(enemyHealth);

        Action arrived = () => OnArrived(projectile, explosion);
        flights.Add(projectile, arrived);
        projectile.Arrived += arrived;
        // Suscribir antes de Initialize tambien admite duracion cero.
        projectile.Initialize(origin, destination, flightDuration);
    }

    private void OnArrived(BombProjectile projectile, FireExplosion explosion)
    {
        if (!flights.TryGetValue(projectile, out Action arrived))
            return;

        // Retirar antes del dano evita reentradas desde los eventos de Health.
        flights.Remove(projectile);
        projectile.Arrived -= arrived;
        try
        {
            if (isActiveAndEnabled && CombatIsActive() && explosion != null)
                explosion.Resolve();
        }
        finally
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }
    }

    private void OnDisable()
    {
        ClearCapture();
        if (subscribedInput != null)
        {
            subscribedInput.AimStarted -= OnAimStarted;
            subscribedInput.AimFinished -= OnAimFinished;
            subscribedInput.AimCanceled -= ClearCapture;
        }
        subscribedInput = null;
        if (subscribedPlayerHealth != null)
            subscribedPlayerHealth.Died -= ClearCapture;
        if (subscribedEnemyHealth != null)
            subscribedEnemyHealth.Died -= ClearCapture;
        subscribedPlayerHealth = subscribedEnemyHealth = null;

        foreach (var flight in flights)
        {
            if (flight.Key == null)
                continue;

            flight.Key.Arrived -= flight.Value;
            Destroy(flight.Key.gameObject);
        }
        flights.Clear();
    }
}
