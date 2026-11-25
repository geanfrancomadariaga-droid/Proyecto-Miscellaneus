using UnityEngine;
using UnityEngine.UI;

/// <summary>Presenta la reserva seleccionada y delega el cambio; no consume ni lanza.</summary>
[DisallowMultipleComponent]
public sealed class PotionSelectionButton : MonoBehaviour
{
    [SerializeField] private EquippedPotionCharges charges;
    [SerializeField] private Text selectionLabel;
    [Tooltip("Opcional: desactiva la interaccion cuando no hay pociones.")]
    [SerializeField] private Button selectionButton;

    // Cache solo de presentacion; la seleccion y las cantidades pertenecen a charges.
    private Text displayedLabel;
    private PotionFormula displayedFormula;
    private int displayedCharges;
    private bool hasPresentation;

    private void OnEnable()
    {
        hasPresentation = false;
        RefreshPresentation();
    }

    private void Update() => RefreshPresentation();

    /// <summary>Enlazar una sola vez desde Button.onClick en el Inspector.</summary>
    public void SelectNext()
    {
        if (!isActiveAndEnabled)
            return;

        if (charges != null)
            charges.TrySelectNext();
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        PotionEffect? element = charges != null ? charges.SelectedElement : null;
        int amount = element.HasValue ? charges.GetCharges(element.Value) : 0;
        var formula = charges != null ? charges.SelectedFormula : null;
        bool available = amount > 0 && formula != null;
        if (!available)
        {
            amount = 0;
        }

        if (selectionButton != null && selectionButton.interactable != available)
            selectionButton.interactable = available;

        if (hasPresentation && displayedLabel == selectionLabel &&
            displayedFormula == formula && displayedCharges == amount)
            return;

        if (selectionLabel != null)
            selectionLabel.text = available
                ? $"{formula.Base} · {formula.Effect}{(formula.Modifier == PotionModifier.Doble ? " Doble" : "")}: {amount}"
                : "SIN POCIONES";

        displayedLabel = selectionLabel;
        displayedFormula = formula;
        displayedCharges = amount;
        hasPresentation = true;
    }
}
