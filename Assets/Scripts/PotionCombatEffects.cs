using UnityEngine;

[RequireComponent(typeof(Health)), DisallowMultipleComponent]
public sealed class PotionCombatEffects : MonoBehaviour
{
    [Tooltip("Secuencia del encuentro al que pertenece este personaje.")]
    [SerializeField] private DuelSequenceController sequence;
    private readonly PotionEffectTimers timers = new PotionEffectTimers();
    private Health health;
    private double showHealingUntil;
    private int lastHealing;

    public bool CanReceive => isActiveAndEnabled && health != null && health.isActiveAndEnabled &&
        !health.IsDead && Time.timeScale > 0f && (sequence == null ||
        (sequence.isActiveAndEnabled && sequence.Phase == DuelPhase.Combat));
    public bool HasFire => timers.HasFire(Time.timeAsDouble);
    public bool IsProtected => timers.IsProtected(Time.timeAsDouble);
    public float SpeedFactor => timers.SpeedFactor(Time.timeAsDouble);
    public int VisibleHealing => Time.timeAsDouble < showHealingUntil ? lastHealing : -1;
    public int MeleeDamage(int baseDamage) => timers.MeleeDamage(baseDamage, Time.timeAsDouble);

    internal bool AllowsMeleeTarget(Health target) => sequence == null ||
        (sequence.isActiveAndEnabled && sequence.Phase == DuelPhase.Combat && sequence.OpponentHealth == target);

    private void Awake() => health = GetComponent<Health>();

    private void OnEnable()
    {
        health.Died += Clear;
        if (sequence != null) sequence.Changed += OnPhaseChanged;
    }

    private void OnPhaseChanged()
    {
        if (sequence.Phase != DuelPhase.Combat && sequence.Phase != DuelPhase.Paused)
            Clear();
    }

    internal void Apply(PotionFormula formula, bool isPlayer)
    {
        if (formula == null || !CanReceive || (!isPlayer && formula.Base == PotionBase.Vial)) return;
        if (formula.Effect == PotionEffect.Savia)
        {
            int previous = health.CurrentHealth;
            health.Heal(isPlayer ? 10 : 15);
            lastHealing = health.CurrentHealth - previous;
            showHealingUntil = Time.timeAsDouble + 1.5;
        }
        else timers.Apply(formula, Time.timeAsDouble);
    }

    private void Clear()
    {
        timers.Clear();
        showHealingUntil = 0;
    }

    private void OnDisable()
    {
        if (sequence != null) sequence.Changed -= OnPhaseChanged;
        if (health != null) health.Died -= Clear;
        Clear();
    }
}
