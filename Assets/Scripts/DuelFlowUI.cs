using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DuelFlowUI : MonoBehaviour
{
    [SerializeField] private DuelSequenceController sequence;
    [SerializeField] private PlayerController movement;
    [SerializeField] private PlayerMeleeAttack attack;
    [SerializeField] private GameObject movementControls, combatControls, fieldBar, workshop, modal;
    [SerializeField] private RectTransform inventoryRow;
    [SerializeField] private Text fieldText, workshopText, modalText;
    [SerializeField] private Button fieldAction, modalAction, fightButton, returnButton, modifierButton;
    private bool inWorkshop;
    private Health observed;

    private void OnEnable()
    {
        if (sequence == null || movement == null || attack == null || movementControls == null ||
            combatControls == null || fieldBar == null || workshop == null || inventoryRow == null || modal == null ||
            fieldText == null || workshopText == null || modalText == null || fieldAction == null ||
            modalAction == null || fightButton == null || returnButton == null || modifierButton == null ||
            fieldAction.GetComponentInChildren<Text>(true) == null ||
            modalAction.GetComponentInChildren<Text>(true) == null)
        {
            Debug.LogError("DuelFlowUI: faltan referencias. Revisa la integración de tres duelos.", this);
            enabled = false;
            return;
        }
        sequence.Changed += Refresh;
        fieldAction.onClick.AddListener(OnFieldAction);
        modalAction.onClick.AddListener(OnModalAction);
        fightButton.onClick.AddListener(sequence.BeginDuel);
        returnButton.onClick.AddListener(ReturnToField);
        Refresh();
    }

    private void OnFieldAction()
    {
        if (sequence.Phase == DuelPhase.Combat) sequence.Pause();
        else if (sequence.Phase == DuelPhase.Preparation)
        {
            inWorkshop = true;
            Refresh();
        }
    }

    private void ReturnToField()
    {
        inWorkshop = false;
        Refresh();
    }

    private void OnModalAction()
    {
        switch (sequence.Phase)
        {
            case DuelPhase.Menu: sequence.BeginGame(); break;
            case DuelPhase.Reward: sequence.ContinueToPreparation(); break;
            case DuelPhase.Paused: sequence.Resume(); break;
            case DuelPhase.Completed: sequence.RestartCompletedGame(); break;
        }
    }

    private void Refresh()
    {
        var phase = sequence.Phase;
        bool preparing = phase == DuelPhase.Preparation;
        bool battling = phase == DuelPhase.Combat;
        if (!preparing) inWorkshop = false;
        bool onField = battling || (preparing && !inWorkshop);
        movement.enabled = onField;
        attack.enabled = battling;
        movementControls.SetActive(onField);
        combatControls.SetActive(battling);
        fieldBar.SetActive(onField);
        workshop.SetActive(inWorkshop);
        inventoryRow.gameObject.SetActive(preparing);
        modal.SetActive(!preparing && !battling && phase != DuelPhase.Defeated);

        if (observed != null) observed.HealthChanged -= UpdateOpponentLabel;
        observed = battling ? sequence.OpponentHealth : null;
        if (observed != null) observed.HealthChanged += UpdateOpponentLabel;

        switch (phase)
        {
            case DuelPhase.Menu:
                SetModal("Proyecto Miscellaneus\nTres duelos con el mago hongo", "INICIAR PARTIDA");
                break;
            case DuelPhase.Preparation:
                // Al recolectar, los contadores dejan visible el punto de Savia al norte.
                inventoryRow.anchorMin = inWorkshop ? new Vector2(.04f, .71f) : new Vector2(.36f, .03f);
                inventoryRow.anchorMax = inWorkshop ? new Vector2(.96f, .80f) : new Vector2(.98f, .12f);
                string objective = sequence.CurrentDuel == 1 ? "Elabora una poción." :
                    sequence.CurrentDuel == 2 ? "Elabora una poción de Savia." :
                    "Elabora una fórmula válida con Doble.";
                fieldText.text = $"Duelo {sequence.CurrentDuel}/3 · " +
                    (sequence.CanBeginDuel ? "Todo listo. Visita a la bruja." : objective + " Recoge ingredientes.");
                workshopText.text = $"Bruja · Preparación {sequence.CurrentDuel}/3\n" +
                    (sequence.CanBeginDuel ? "Tu poción está lista. Puedes comenzar." : objective);
                SetCaption(fieldAction, "VISITAR BRUJA");
                fightButton.interactable = sequence.CanBeginDuel;
                modifierButton.interactable = sequence.DoubleUnlocked;
                break;
            case DuelPhase.Combat:
                SetCaption(fieldAction, "PAUSA");
                if (observed != null) UpdateOpponentLabel(observed.CurrentHealth, observed.MaxHealth);
                break;
            case DuelPhase.Reward:
                SetModal($"Victoria en el duelo {sequence.CurrentDuel}\n" +
                    (sequence.CurrentDuel == 1 ? "Savia desbloqueada" : "Doble desbloqueado"),
                    "CONTINUAR");
                break;
            case DuelPhase.Paused:
                SetModal($"Duelo {sequence.CurrentDuel}/3 en pausa", "REANUDAR");
                break;
            case DuelPhase.Completed:
                SetModal("JUEGO COMPLETADO\nHas ganado los tres duelos.", "NUEVA PARTIDA");
                break;
        }
    }

    private void UpdateOpponentLabel(int current, int maximum) =>
        fieldText.text = $"Duelo {sequence.CurrentDuel}/3 · Rival {current}/{maximum} PV";

    private void SetModal(string message, string action)
    {
        modalText.text = message;
        SetCaption(modalAction, action);
    }

    private static void SetCaption(Button button, string value) =>
        button.GetComponentInChildren<Text>(true).text = value;

    private void OnDisable()
    {
        if (sequence != null)
        {
            sequence.Changed -= Refresh;
            if (fightButton != null) fightButton.onClick.RemoveListener(sequence.BeginDuel);
        }
        if (observed != null) observed.HealthChanged -= UpdateOpponentLabel;
        if (fieldAction != null) fieldAction.onClick.RemoveListener(OnFieldAction);
        if (modalAction != null) modalAction.onClick.RemoveListener(OnModalAction);
        if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToField);
        observed = null;
    }
}
