using UnityEngine;
using UnityEngine.UI;

/// <summary>Muestra ingredientes mediante consultas; no consume ni administra pociones.</summary>
[DisallowMultipleComponent]
public sealed class IngredientInventoryHUD : MonoBehaviour
{
    [SerializeField] private PlayerIngredientInventory inventory;
    [SerializeField] private Text fireText;
    [SerializeField] private Text frostText;
    [SerializeField] private Text sapText;

    private int displayedFire = -1;
    private int displayedFrost = -1;
    private int displayedSap = -1;
    private Text previousFireText;
    private Text previousFrostText;
    private Text previousSapText;

    private void OnEnable()
    {
        // Las cantidades nunca son negativas: -1 obliga a presentar el valor actual.
        displayedFire = displayedFrost = displayedSap = -1;
        RefreshPresentation();
    }

    private void Update() => RefreshPresentation();

    private void RefreshPresentation()
    {
        RefreshText(fireText, "Fuego", GetQuantity(PotionEffect.Fuego),
            ref previousFireText, ref displayedFire);
        RefreshText(frostText, "Escarcha", GetQuantity(PotionEffect.Escarcha),
            ref previousFrostText, ref displayedFrost);
        RefreshText(sapText, "Savia", GetQuantity(PotionEffect.Savia),
            ref previousSapText, ref displayedSap);
    }

    private int GetQuantity(PotionEffect ingredient) =>
        inventory != null ? inventory.GetQuantity(ingredient) : 0;

    private static void RefreshText(Text label, string name, int quantity,
        ref Text previousLabel, ref int displayedQuantity)
    {
        if (label == null)
        {
            previousLabel = null;
            displayedQuantity = -1;
            return;
        }

        if (label == previousLabel && quantity == displayedQuantity)
            return;

        // Solo construye la cadena y asigna Text.text cuando hace falta.
        label.text = $"{name}: {quantity}";
        previousLabel = label;
        displayedQuantity = quantity;
    }
}
