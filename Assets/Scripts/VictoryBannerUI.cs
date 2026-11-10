using UnityEngine;

/// <summary>Muestra una vez un aviso temporal, sin intervenir en el combate.</summary>
[DisallowMultipleComponent]
public sealed class VictoryBannerUI : MonoBehaviour
{
    [SerializeField] private EnemyVictoryController victoryController;
    [SerializeField] private GameObject victoryBanner;
    [SerializeField, Min(0f)] private float displaySeconds = 3f;

    private EnemyVictoryController subscribedController;
    private bool bannerShown;
    private bool hidePending;
    private float hideAt;

    private void OnEnable()
    {
        // Ocultar el banner nunca debe desactivar al observador de eventos.
        if (victoryBanner != null && transform.IsChildOf(victoryBanner.transform))
        {
            Debug.LogError("VictoryBannerUI: Victory Banner no puede ser este objeto ni un ancestro. " +
                "Coloca el componente en un objeto activo del Canvas, fuera del banner.", this);
            enabled = false;
            return;
        }

        HideBanner();
        if (victoryController == null || victoryBanner == null)
        {
            Debug.LogError("VictoryBannerUI: asigna Victory Controller y Victory Banner " +
                "en el Inspector. El componente se desactivara.", this);
            enabled = false;
            return;
        }

        subscribedController = victoryController;
        subscribedController.VictoryAchieved += ShowVictory;
        if (subscribedController.IsVictorious)
            ShowVictory();
    }

    private void ShowVictory()
    {
        if (!isActiveAndEnabled || bannerShown || victoryBanner == null ||
            subscribedController == null || !subscribedController.IsVictorious)
            return;

        // Marcar antes de activar impide duplicados, incluso por reentrada.
        bannerShown = true;
        hideAt = Time.unscaledTime + Mathf.Max(0f, displaySeconds);
        hidePending = true;
        victoryBanner.SetActive(true);
    }

    private void Update() => TickAt(Time.unscaledTime);

    // Un solo plazo, sin corutinas; permite probar limites sin esperas reales.
    private void TickAt(float now)
    {
        if (isActiveAndEnabled && hidePending && now >= hideAt)
            HideBanner();
    }

    private void HideBanner()
    {
        hidePending = false;
        if (victoryBanner != null && !transform.IsChildOf(victoryBanner.transform))
            victoryBanner.SetActive(false);
    }

    private void OnDisable()
    {
        // Retirar de la fuente original aunque cambie el campo del Inspector.
        if (subscribedController != null)
            subscribedController.VictoryAchieved -= ShowVictory;
        subscribedController = null;
        HideBanner();
    }

    private void OnDestroy() => OnDisable();
}
