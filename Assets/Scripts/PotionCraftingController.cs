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

    private readonly PotionCraftingService service = new PotionCraftingService();
    private static readonly PotionFormula FireBomb = new PotionFormula(
        PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno);

    /// <summary>Enlazar una sola vez a Button.onClick. Cada llamada solicita una carga.</summary>
    public void CraftFireBomb()
    {
        service.TryCraft(FireBomb, inventory, equipment, out PotionCraftingResult result);
        // La comparacion de Unity tambien reconoce un Text destruido.
        if (resultText != null)
            resultText.text = GetMessage(result);
    }

    private static string GetMessage(PotionCraftingResult result)
    {
        switch (result)
        {
            case PotionCraftingResult.Success:
                return "Bomba de Fuego preparada (+1)";
            case PotionCraftingResult.MissingDependency:
                return "Falta asignar el inventario o el equipamiento.";
            case PotionCraftingResult.InvalidFormula:
                return "La fórmula no es válida.";
            case PotionCraftingResult.InsufficientIngredients:
                return "No tienes ingredientes de Fuego.";
            case PotionCraftingResult.IncompatibleFormula:
                return "Ya hay otra fórmula de Fuego con cargas.";
            case PotionCraftingResult.ChargeAdditionRejected:
                return "No se pudo añadir la carga de Fuego.";
            default:
                return "No se pudo elaborar la poción.";
        }
    }
}
