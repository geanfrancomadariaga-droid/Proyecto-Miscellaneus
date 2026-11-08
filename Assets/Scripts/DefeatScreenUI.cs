using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Muestra la derrota y reinicia el duelo recargando la escena activa.</summary>
[DisallowMultipleComponent]
public class DefeatScreenUI : MonoBehaviour
{
    [SerializeField] private PlayerDefeatController defeatController;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button retryButton;

    private PlayerDefeatController subscribedController;
    private Button subscribedButton;
    private bool panelShown;
    private bool reloadRequested;

    protected virtual void OnEnable()
    {
        // Nunca ocultar este objeto ni un ancestro: perderiamos los eventos.
        if (defeatPanel != null && transform.IsChildOf(defeatPanel.transform))
        {
            Debug.LogError("DefeatScreenUI: Defeat Panel no puede ser este objeto ni un ancestro. " +
                "Coloca el componente en el Canvas y el panel como hijo.", this);
            enabled = false;
            return;
        }

        if (defeatPanel != null && (defeatController == null || !defeatController.IsDefeated))
            defeatPanel.SetActive(false);
        panelShown = false;

        if (defeatController == null || defeatPanel == null || retryButton == null)
        {
            Debug.LogError("DefeatScreenUI: asigna Defeat Controller, Defeat Panel y Retry Button " +
                "en el Inspector. El componente se desactivara.", this);
            enabled = false;
            return;
        }

        subscribedController = defeatController;
        subscribedButton = retryButton;
        subscribedController.Defeated += ShowDefeat;
        subscribedButton.onClick.AddListener(Retry);
        if (subscribedController.IsDefeated)
            ShowDefeat();
    }

    private void ShowDefeat()
    {
        if (!isActiveAndEnabled || panelShown || defeatPanel == null)
            return;

        panelShown = true;
        defeatPanel.SetActive(true);
    }

    private void Retry()
    {
        if (!isActiveAndEnabled || reloadRequested || subscribedController == null ||
            !subscribedController.IsDefeated)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!CanLoadScene(scene.buildIndex))
        {
            Debug.LogError($"DefeatScreenUI: no se puede recargar la escena '{scene.name}' " +
                $"(indice {scene.buildIndex}). Incluyela y habilitala en la Scene List " +
                "del Build Profile activo o en su lista compartida.", this);
            return;
        }

        // LoadScene termina en el siguiente frame: ignorar clics adicionales.
        reloadRequested = true;
        ReloadScene(scene.buildIndex);
    }

    // Solo estas operaciones se sustituyen en pruebas; no recargan el Test Runner.
    protected virtual bool CanLoadScene(int buildIndex) =>
        buildIndex >= 0 && Application.CanStreamedLevelBeLoaded(buildIndex);

    protected virtual void ReloadScene(int buildIndex) =>
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);

    protected virtual void OnDisable()
    {
        // Retirar de las fuentes originales aunque cambien los campos del Inspector.
        if (subscribedController != null)
            subscribedController.Defeated -= ShowDefeat;
        if (subscribedButton != null)
            subscribedButton.onClick.RemoveListener(Retry);
        subscribedController = null;
        subscribedButton = null;
    }
}
