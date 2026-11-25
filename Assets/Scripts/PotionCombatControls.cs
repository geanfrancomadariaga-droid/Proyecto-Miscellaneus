using UnityEngine;
using UnityEngine.UI;

/// <summary>Entrada de viales y lectura de efectos dentro del grupo de controles de combate.</summary>
[DisallowMultipleComponent]
public sealed class PotionCombatControls : MonoBehaviour
{
    [SerializeField] private DuelSequenceController sequence;
    [SerializeField] private EquippedPotionCharges charges;
    [SerializeField] private Button useVial;
    [SerializeField] private Text status;

    private PotionCombatEffects player, opponent;
    private Health playerHealth;
    private int lastUseFrame = -1;
    private (int hp, bool fire, bool shield, bool slow, bool rivalSlow, int heal, int rivalHeal)? displayed;

    private void OnEnable()
    {
        if (sequence == null || charges == null || useVial == null || status == null ||
            !charges.TryGetComponent(out player) || !charges.TryGetComponent(out playerHealth))
        {
            Debug.LogError("PotionCombatControls: asigna secuencia, cargas de Player con Health y PotionCombatEffects, botón USAR VIAL y texto de estado.", this);
            enabled = false;
            return;
        }
        var rival = sequence.OpponentHealth;
        opponent = rival != null ? rival.GetComponent<PotionCombatEffects>() : null;
        useVial.onClick.AddListener(UseVial);
        displayed = null;
        Refresh();
    }

    private bool CanDrink() => isActiveAndEnabled && sequence.isActiveAndEnabled &&
        sequence.Phase == DuelPhase.Combat && Time.timeScale > 0f && player.CanReceive &&
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
