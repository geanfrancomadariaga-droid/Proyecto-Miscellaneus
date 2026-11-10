using UnityEngine;

/// <summary>Cuenta las cargas de una formula equipada; no procesa gestos ni lanzamientos.</summary>
public sealed class EquippedPotionCharges : MonoBehaviour
{
    [Tooltip("Cinco es un valor provisional de prueba, no un limite de cargas.")]
    [SerializeField, Min(0)] private int initialCharges = 5;

    public int RemainingCharges { get; private set; }

    private void Awake()
    {
        Initialize(initialCharges);
    }

    /// <summary>Reemplaza las cargas disponibles; las cantidades negativas se convierten en cero.</summary>
    public void Initialize(int amount)
    {
        RemainingCharges = Mathf.Max(0, amount);
    }

    /// <summary>Consume exactamente una carga si existe; en cero no cambia el contador.</summary>
    public bool TryConsumeCharge()
    {
        if (RemainingCharges == 0)
            return false;

        RemainingCharges--;
        return true;
    }
}
