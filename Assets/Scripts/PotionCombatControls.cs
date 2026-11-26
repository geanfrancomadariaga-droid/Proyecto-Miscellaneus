using UnityEngine;
using UnityEngine.UI;

/// <summary>Entrada de viales y lectura de efectos dentro del grupo de controles de combate.</summary>
[DisallowMultipleComponent]
public sealed class PotionCombatControls : MonoBehaviour
{
    [SerializeField] private DuelSequenceController sequence;
    [SerializeField] private EquippedPotionCharges charges;
    [SerializeField] private PotionAimInput aimInput;
    [SerializeField] private Button useVial;
    [SerializeField] private Text status;

    private PotionCombatEffects player, opponent;
    private Health playerHealth;
    private int lastUseFrame = -1;
    private (int hp, bool fire, bool shield, bool slow, bool rivalSlow, int heal, int rivalHeal)? displayed;

    private void OnEnable()
    {
        if (sequence == null || charges == null || aimInput == null || useVial == null || status == null ||
            !charges.TryGetComponent(out player) || !charges.TryGetComponent(out playerHealth))
        {
            Debug.LogError("PotionCombatControls: revisa secuencia, cargas del jugador, efectos, apuntado, botón USAR VIAL y texto de estado.", this);
            enabled = false;
            return;
        }
        var rival = sequence.OpponentHealth;
        opponent = rival != null ? rival.GetComponent<PotionCombatEffects>() : null;
        useVial.onClick.AddListener(UseVial);
        displayed = null;
        Refresh();
    }

private bool CanDrink() =>
    isActiveAndEnabled &&
    sequence != null &&
    sequence.isActiveAndEnabled &&
    sequence.Phase == DuelPhase.Combat &&
    Time.timeScale > 0f &&
    player != null && player.CanReceive &&
    opponent != null && opponent.CanReceive &&
    aimInput != null && !aimInput.IsAiming &&
    charges != null &&
    charges.SelectedFormula?.Base == PotionBase.Vial;


    private void UseVial()
    {
        if (lastUseFrame == Time.frameCount || !CanDrink()) return;
        var formula = charges.SelectedFormula;
        if (!charges.TryConsume(formula)) return;
        // Un callback duplicado o reentrante del mismo clic no alcanza otra reserva.
        lastUseFrame = Time.frameCount;
        player.Apply(formula, true);
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        // Ambos controles ocupan el mismo lugar. Un gesto iniciado conserva su destino
        // aunque otro dedo cambie la selección antes de soltarlo.
        bool aiming = aimInput.IsAiming || charges.SelectedFormula?.Base == PotionBase.Bomba;
        if (aimInput.gameObject.activeSelf != aiming) aimInput.gameObject.SetActive(aiming);
        bool drinking = !aiming && charges.SelectedFormula?.Base == PotionBase.Vial;
        if (useVial.gameObject.activeSelf != drinking) useVial.gameObject.SetActive(drinking);
        useVial.interactable = CanDrink();
        var state = (hp: playerHealth.CurrentHealth, fire: player.HasFire, shield: player.IsProtected,
            slow: player.SpeedFactor < 1f, rivalSlow: opponent != null && opponent.SpeedFactor < 1f,
            heal: player.VisibleHealing, rivalHeal: opponent != null ? opponent.VisibleHealing : -1);
        if (displayed.HasValue && displayed.Value.Equals(state)) return;
        displayed = state;
        string message = $"Jugador {state.hp}/{playerHealth.MaxHealth} PV";
        if (state.fire) message += " · Ataque ×1,5";
        if (state.shield) message += " · Protección";
        if (state.slow) message += " · Lentitud";
        if (state.heal >= 0) message += $" · Savia +{state.heal} PV";
        if (state.rivalSlow) message += "\nRival · Lentitud";
        if (state.rivalHeal >= 0) message += $"\nRival · Savia +{state.rivalHeal} PV";
        status.text = message;
    }

    private void OnDisable()
    {
        if (useVial != null) useVial.onClick.RemoveListener(UseVial);
    }
}
