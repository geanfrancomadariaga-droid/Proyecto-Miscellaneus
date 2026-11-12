using UnityEngine;

/// <summary>Administra una reserva por elemento; no procesa gestos ni lanzamientos.</summary>
public sealed class EquippedPotionCharges : MonoBehaviour
{
    [Tooltip("Cinco es un valor provisional de prueba, no un limite de cargas.")]
    [SerializeField, Min(0)] private int initialCharges = 5;

    private static readonly PotionFormula FireBomb =
        new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);

    private ElementPotionSlot fireSlot = new ElementPotionSlot(PotionEffect.Fuego);
    private readonly ElementPotionSlot frostSlot = new ElementPotionSlot(PotionEffect.Escarcha);
    private readonly ElementPotionSlot sapSlot = new ElementPotionSlot(PotionEffect.Savia);

    /// <summary>Cargas compatibles con el lanzador actual de Bomba + Fuego.</summary>
    public int RemainingCharges => fireSlot.ActiveFormula == FireBomb ? fireSlot.Charges : 0;

    private void Awake()
    {
        Initialize(initialCharges);
    }

    /// <summary>
    /// Reemplaza Fuego por Bomba + Fuego; cero y negativos dejan esa reserva vacia.
    /// No altera Escarcha ni Savia. Es un reinicio explicito, no una elaboracion.
    /// </summary>
    public void Initialize(int amount)
    {
        fireSlot = new ElementPotionSlot(PotionEffect.Fuego);
        if (amount > 0)
            fireSlot.TryAdd(FireBomb, amount);
    }

    /// <summary>Consume solo Bomba + Fuego; otra formula o una reserva vacia se rechazan.</summary>
    public bool TryConsumeCharge()
    {
        return TryConsume(FireBomb);
    }

    /// <summary>Cargas del elemento; un elemento desconocido devuelve cero.</summary>
    public int GetCharges(PotionEffect element)
    {
        return GetSlot(element)?.Charges ?? 0;
    }

    /// <summary>Formula inmutable activa; null si la reserva esta vacia o el elemento es invalido.</summary>
    public PotionFormula GetActiveFormula(PotionEffect element)
    {
        return GetSlot(element)?.ActiveFormula;
    }

    /// <summary>Anade una elaboracion compatible; delega cantidades y desbordamientos en la reserva.</summary>
    public bool TryAdd(PotionFormula formula, int amount)
    {
        return formula != null && (GetSlot(formula.Effect)?.TryAdd(formula, amount) ?? false);
    }

    /// <summary>Consume una carga solo si coincide toda la formula solicitada.</summary>
    public bool TryConsume(PotionFormula formula)
    {
        return formula != null && (GetSlot(formula.Effect)?.TryConsume(formula) ?? false);
    }

    private ElementPotionSlot GetSlot(PotionEffect element)
    {
        switch (element)
        {
            case PotionEffect.Fuego: return fireSlot;
            case PotionEffect.Escarcha: return frostSlot;
            case PotionEffect.Savia: return sapSlot;
            default: return null;
        }
    }
}
