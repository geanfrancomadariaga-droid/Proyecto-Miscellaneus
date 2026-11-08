using UnityEngine;

/// <summary>Muestra el destino del apuntado; no lanza pociones.</summary>
public sealed class PotionAimIndicator : MonoBehaviour
{
    [SerializeField] private PotionAimInput aimInput;
    [SerializeField] private Transform player;
    [SerializeField] private GameObject potionLandingMarker;

    private PotionAimInput subscribedInput;
    private bool referenceErrorReported;

    private void OnEnable()
    {
        HideMarker();
        if (!ValidateReferences())
            return;

        subscribedInput = aimInput;
        subscribedInput.AimStarted += RefreshMarker;
        subscribedInput.AimUpdated += OnAimUpdated;
        subscribedInput.AimFinished += OnAimFinished;
        subscribedInput.AimCanceled += HideMarker;
        RefreshMarker();
    }

    private void OnDisable()
    {
        if (subscribedInput != null)
        {
            subscribedInput.AimStarted -= RefreshMarker;
            subscribedInput.AimUpdated -= OnAimUpdated;
            subscribedInput.AimFinished -= OnAimFinished;
            subscribedInput.AimCanceled -= HideMarker;
        }

        subscribedInput = null;
        HideMarker();
    }

    private void LateUpdate()
    {
        // Sigue al jugador incluso cuando no llegan nuevos eventos de arrastre.
        RefreshMarker();
    }

    private void OnAimUpdated(PotionAimCalculator.Result result)
    {
        RefreshMarker();
    }

    private void OnAimFinished(PotionAimCalculator.Result result)
    {
        HideMarker();
    }

    private void RefreshMarker()
    {
        if (!ValidateReferences())
            return;

        PotionAimCalculator.Result result = aimInput.CurrentResult;
        if (!aimInput.isActiveAndEnabled || !aimInput.IsAiming || !result.IsValid)
        {
            HideMarker();
            return;
        }

        Vector2 destination = (Vector2)player.position + result.Direction * result.Distance;
        potionLandingMarker.transform.position = new Vector3(destination.x, destination.y, 0f);
        potionLandingMarker.SetActive(true);
    }

    private bool ValidateReferences()
    {
        if (aimInput != null && player != null && potionLandingMarker != null)
            return true;

        if (!referenceErrorReported)
        {
            Debug.LogError(
                "PotionAimIndicator: faltan referencias. Asigna Aim Input, Player y " +
                "Potion Landing Marker en el Inspector. El componente se desactivara.", this);
            referenceErrorReported = true;
        }

        HideMarker();
        enabled = false;
        return false;
    }

    private void HideMarker()
    {
        if (potionLandingMarker != null)
            potionLandingMarker.SetActive(false);
    }
}
