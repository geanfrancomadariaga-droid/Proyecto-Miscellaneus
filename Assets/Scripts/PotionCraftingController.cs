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
        if (selectionText != null)
            selectionText.text = $"{selection.SelectedBase} + {selection.SelectedEffect} + {selection.SelectedModifier}";
    }

    private void CraftFormula(PotionFormula formula)
    {
        service.TryCraft(formula, inventory, equipment, out PotionCraftingResult result);
        ShowResult(result, formula);
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
