using System;

public enum PotionCraftingResult
{
    Success,
    MissingDependency,
    InvalidFormula,
    InsufficientIngredients,
    IncompatibleFormula,
    ChargeAdditionRejected
}

/// <summary>Elabora una carga de forma sincrona en el hilo principal, sin estado propio ni UI.</summary>
public sealed class PotionCraftingService
{
    /// <summary>
    /// Consume una unidad del efecto solo si se obtiene una carga. PotionFormula valida
    /// sus combinaciones al construirse; aqui null representa una formula invalida.
    /// Usar con el equipamiento despues de su Awake, que aplica las cargas iniciales.
    /// </summary>
    public bool TryCraft(PotionFormula formula, PlayerIngredientInventory inventory,
        EquippedPotionCharges equipment, out PotionCraftingResult result)
    {
        result = PotionCraftingResult.MissingDependency;
        if (inventory == null || equipment == null)
            return false;

        result = PotionCraftingResult.InvalidFormula;
        if (formula == null)
            return false;

        result = PotionCraftingResult.InsufficientIngredients;
        if (inventory.GetQuantity(formula.Effect) < 1)
            return false;

        // La API no expone CanAdd. Consultar la reserva y reutilizar la igualdad
        // de PotionFormula; TryAdd sigue siendo la autoridad final de aceptacion.
        result = PotionCraftingResult.IncompatibleFormula;
        if (equipment.GetCharges(formula.Effect) > 0 &&
            equipment.GetActiveFormula(formula.Effect) != formula)
            return false;

        result = PotionCraftingResult.InsufficientIngredients;
        if (!inventory.TryConsume(formula.Effect, 1))
            return false;

        if (!equipment.TryAdd(formula, 1))
        {
            // TryAdd no cambia nada al rechazar. El consumo anterior dejo espacio
            // para devolver exactamente una unidad, incluso partiendo de int.MaxValue.
            // Estas APIs no tienen callbacks y no hay intercalado en el hilo principal.
            if (!inventory.TryAdd(formula.Effect, 1))
                throw new InvalidOperationException("No se pudo restaurar el ingrediente consumido.");

            result = PotionCraftingResult.ChargeAdditionRejected;
            return false;
        }

        result = PotionCraftingResult.Success;
        return true;
    }
}
