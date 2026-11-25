using UnityEngine;
using UnityEngine.UI;

/// <summary>Conecta la UI provisional con el servicio; no administra ingredientes ni cargas.</summary>
[DisallowMultipleComponent]
public sealed class PotionCraftingController : MonoBehaviour
{
    [SerializeField] private PlayerIngredientInventory inventory;
    [SerializeField] private EquippedPotionCharges equipment;
    [Tooltip("Opcional: muestra el resultado de la ultima solicitud.")]
    [SerializeField] private Text resultText;
    [Tooltip("Opcional: muestra los tres bloques seleccionados.")]
    [SerializeField] private Text selectionText;
    [Tooltip("Opcional en escenas sin secuencia de duelos.")]
    [SerializeField] private DuelSequenceController sequence;
    public event System.Action<PotionFormula> Crafted;

    internal bool IsLinkedTo(DuelSequenceController owner) => sequence == owner;

    private readonly PotionCraftingService service = new PotionCraftingService();
    private readonly PotionFormulaSelection selection = new PotionFormulaSelection();
    private static readonly PotionFormula FireBomb = new PotionFormula(
        PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno);

    /// <summary>Enlazar una sola vez a Button.onClick. Cada llamada solicita una carga.</summary>
    public void CraftFireBomb()
    {
        CraftFormula(FireBomb);
    }

    private void OnEnable() => UpdateSelectionText();

    public void NextBase()
    {
        selection.TrySetBase(selection.SelectedBase == PotionBase.Bomba
            ? PotionBase.Vial : PotionBase.Bomba);
        UpdateSelectionText();
    }

    public void NextEffect()
    {
        switch (selection.SelectedEffect)
        {
            case PotionEffect.Fuego: selection.TrySetEffect(PotionEffect.Escarcha); break;
            case PotionEffect.Escarcha: selection.TrySetEffect(PotionEffect.Savia); break;
            default: selection.TrySetEffect(PotionEffect.Fuego); break;
        }
        UpdateSelectionText();
    }

    public void ToggleModifier()
    {
        if (sequence != null && !sequence.DoubleUnlocked) return;
        selection.TrySetModifier(selection.SelectedModifier == PotionModifier.Ninguno
            ? PotionModifier.Doble : PotionModifier.Ninguno);
        UpdateSelectionText();
    }

    public void CraftSelectedFormula()
    {
        if (!selection.TryGetFormula(out PotionFormula formula))
        {
            ShowResult(PotionCraftingResult.InvalidFormula, null);
            return;
        }
        CraftFormula(formula);
    }

    private void UpdateSelectionText()
    {
        if (selectionText == null) return;
        string blocks = $"{selection.SelectedBase} + {selection.SelectedEffect} + {selection.SelectedModifier}";
        if (sequence != null && selection.SelectedModifier == PotionModifier.Doble &&
            selection.TryGetFormula(out var formula))
            blocks += $" · Duración ×{PotionEffectDuration.For(formula, 1f):0}";
        selectionText.text = blocks;
    }

    private void CraftFormula(PotionFormula formula)
    {
        if (sequence != null && !sequence.AllowsCraft(formula))
        {
            if (resultText != null)
                resultText.text = sequence.Phase == DuelPhase.Preparation
                    ? "La bruja todavía no ha desbloqueado esta fórmula."
                    : "Elabora durante la preparación.";
            return;
        }
        bool crafted = service.TryCraft(formula, inventory, equipment, out PotionCraftingResult result);
        ShowResult(result, formula);
        if (crafted) Crafted?.Invoke(formula);
    }

    // Una reserva solo admite una fórmula. La retirada exige una acción explícita
    // y nunca cuenta como elaboración ni consume ingredientes.
    public void RemoveOneConflictingCharge()
    {
        if (sequence == null || equipment == null || !selection.TryGetFormula(out var desired) ||
            !sequence.AllowsCraft(desired)) return;
        var current = equipment.GetActiveFormula(desired.Effect);
        if (current == null || current == desired)
        {
            if (resultText != null) resultText.text = "La reserva es compatible.";
            return;
        }
        if (equipment.TryConsume(current) && resultText != null)
            resultText.text = $"Retirada 1 carga de {current.Effect}. Quedan {equipment.GetCharges(current.Effect)}.";
    }

    private void ShowResult(PotionCraftingResult result, PotionFormula formula)
    {
        // La comparacion de Unity tambien reconoce un Text destruido.
        if (resultText != null)
            resultText.text = GetMessage(result, formula);
    }

    private static string GetMessage(PotionCraftingResult result, PotionFormula formula)
    {
        switch (result)
        {
            case PotionCraftingResult.Success:
                string modifier = formula.Modifier == PotionModifier.Ninguno
                    ? "" : $" + {formula.Modifier}";
                string prepared = formula.Base == PotionBase.Bomba ? "preparada" : "preparado";
                return $"{formula.Base} de {formula.Effect}{modifier} {prepared} (+1)";
            case PotionCraftingResult.MissingDependency:
                return "Falta asignar el inventario o el equipamiento.";
            case PotionCraftingResult.InvalidFormula:
                return "La fórmula no es válida.";
            case PotionCraftingResult.InsufficientIngredients:
                return $"No tienes ingredientes de {formula.Effect}.";
            case PotionCraftingResult.IncompatibleFormula:
                return $"Ya hay otra fórmula de {formula.Effect} con cargas.";
            case PotionCraftingResult.ChargeAdditionRejected:
                return $"No se pudo añadir la carga de {formula.Effect}.";
            default:
                return "No se pudo elaborar la poción.";
        }
    }
}
