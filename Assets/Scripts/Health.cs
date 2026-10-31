using System;
using UnityEngine;

/// <summary>Gestiona la salud sin depender de otros componentes del juego.</summary>
public class Health : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth == 0;

    // Entrega la vida actual y la máxima, en ese orden.
    public event Action<int, int> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
    }

    /// <summary>Aplica daño positivo y notifica la muerte una sola vez.</summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (IsDead)
            Died?.Invoke();
    }

    /// <summary>Recupera vida sin superar el máximo ni revivir al personaje.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead || CurrentHealth == MaxHealth)
            return;

        // Limitar primero la cantidad evita desbordar la suma de enteros.
        CurrentHealth += Mathf.Min(amount, MaxHealth - CurrentHealth);
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}
