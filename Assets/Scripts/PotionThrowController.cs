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
    private readonly Dictionary<BombProjectile, Action> flights =
        new Dictionary<BombProjectile, Action>();

    private void OnEnable()
    {
        subscribedInput = aimInput;
        if (subscribedInput != null)
            subscribedInput.AimFinished += OnAimFinished;
    }

    private bool CombatIsActive()
    {
        return playerHealth != null && enemyHealth != null &&
            !playerHealth.IsDead && !enemyHealth.IsDead;
    }

    private void OnAimFinished(PotionAimCalculator.Result result)
    {
        if (!isActiveAndEnabled || !result.IsValid || !CombatIsActive() ||
            aimInput == null || charges == null || charges.RemainingCharges == 0 ||
            projectilePrefab == null ||
            !projectilePrefab.TryGetComponent<FireExplosion>(out _) ||
            float.IsNaN(flightDuration) || float.IsInfinity(flightDuration))
            return;

        Vector3 origin = playerHealth.transform.position;
        Vector3 destination = origin + (Vector3)(result.Direction * result.Distance);
        BombProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        FireExplosion explosion = projectile.GetComponent<FireExplosion>();
        explosion.SetTarget(enemyHealth);

        if (!charges.TryConsumeCharge())
        {
            Destroy(projectile.gameObject);
            return;
        }

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
        if (subscribedInput != null)
            subscribedInput.AimFinished -= OnAimFinished;
        subscribedInput = null;

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
