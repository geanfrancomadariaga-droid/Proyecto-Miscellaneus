using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DuelPhase { Menu, Preparation, Combat, Reward, Paused, Defeated, Completed }

[DisallowMultipleComponent]
public sealed class DuelSequenceController : MonoBehaviour
{
    [SerializeField] private EnemyVictoryController[] rivals;
    [SerializeField] private PlayerDefeatController defeat;
    [SerializeField] private PotionCraftingController crafting;
    [SerializeField] private IngredientPickup sapPickup;
    [Tooltip("El lanzador original vive en Player. Los de los duelos 2 y 3 viven en sus rivales.")]
    [SerializeField] private PotionThrowController firstLauncher;

    private readonly DuelProgressionGate gate = new DuelProgressionGate();
    private EnemyVictoryController fighting;
    private PlayerDefeatController subscribedDefeat;
    private PotionCraftingController subscribedCrafting;
    private bool ready, notifying, reloadRequested;
    private float previousTimeScale;
    public DuelPhase Phase { get; private set; } = DuelPhase.Menu;
    public int CurrentDuel { get; private set; }
    public bool SapUnlocked => gate.SapUnlocked;
    public bool DoubleUnlocked => gate.DoubleUnlocked;
    public bool CanBeginDuel => Available(DuelPhase.Preparation) && gate.Prepared;
    public Health OpponentHealth => fighting != null ? fighting.GetComponent<Health>() : null;
    public event Action Changed;

    private void OnEnable()
    {
        if (!ValidConfiguration())
        {
            enabled = false;
            return;
        }
        ready = true;
        subscribedDefeat = defeat;
        subscribedCrafting = crafting;
        subscribedDefeat.Defeated += OnDefeat;
        subscribedCrafting.Crafted += OnCrafted;
        firstLauncher.enabled = false;
        sapPickup.gameObject.SetActive(SapUnlocked);
        foreach (var rival in rivals)
            if (rival != fighting) rival.gameObject.SetActive(false);
        if (defeat.IsDefeated) OnDefeat();
        else if (fighting != null)
        {
            SetOpponentActive(true);
            fighting.VictoryAchieved += OnVictory;
            OnVictory(); // Recupera una muerte ocurrida mientras estaba deshabilitado.
        }
        Notify();
    }

    private bool ValidConfiguration()
    {
        if (defeat == null || crafting == null || sapPickup == null || firstLauncher == null ||
            !crafting.IsLinkedTo(this) || sapPickup.Ingredient != PotionEffect.Savia)
        {
            Debug.LogError("DuelSequenceController: comprueba derrota, elaboración vinculada, lanzador inicial y pickup de Savia.", this);
            return false;
        }
        if (rivals == null || rivals.Length != 3)
        {
            Debug.LogError("DuelSequenceController: asigna exactamente tres rivales.", this);
            return false;
        }
        for (int i = 0; i < rivals.Length; i++)
        {
            var rival = rivals[i];
            if (rival == null || !rival.enabled || !rival.TryGetComponent<Health>(out _) ||
                Array.IndexOf(rivals, rival) != i || transform.IsChildOf(rival.transform))
            {
                Debug.LogError($"DuelSequenceController: revisa el rival {i + 1}, su Health y las referencias repetidas.", this);
                return false;
            }
        }
        return true;
    }

    private bool Available(DuelPhase phase) => ready && isActiveAndEnabled && Phase == phase;
    public bool AllowsCraft(PotionFormula formula) => Available(DuelPhase.Preparation) && gate.Allows(formula);

    public void BeginGame()
    {
        if (!Available(DuelPhase.Menu) || notifying) return;
        CurrentDuel = 1;
        SetPhase(DuelPhase.Preparation);
    }

    public void ContinueToPreparation()
    {
        if (!Available(DuelPhase.Reward) || notifying) return;
        CurrentDuel++;
        SetPhase(DuelPhase.Preparation);
    }

    private void OnCrafted(PotionFormula formula)
    {
        if (Available(DuelPhase.Preparation) && gate.RecordCraft(formula)) Notify();
    }

    public void BeginDuel()
    {
        if (!CanBeginDuel || notifying || subscribedDefeat.IsDefeated) return;
        Phase = DuelPhase.Combat;
        fighting = rivals[CurrentDuel - 1];
        fighting.VictoryAchieved += OnVictory;
        SetOpponentActive(true);
        Notify();
    }

    private void OnVictory()
    {
        if ((!Available(DuelPhase.Combat) && !Available(DuelPhase.Paused)) ||
            fighting == null || !fighting.IsVictorious || !OpponentHealth.IsDead) return;
        RestoreClock();
        RetireOpponent();
        gate.RecordVictory(CurrentDuel);
        sapPickup.gameObject.SetActive(SapUnlocked);
        SetPhase(CurrentDuel == 3 ? DuelPhase.Completed : DuelPhase.Reward);
    }

    private void OnDefeat()
    {
        if (!subscribedDefeat.IsDefeated || Phase == DuelPhase.Defeated || Phase == DuelPhase.Completed) return;
        RestoreClock();
        RetireOpponent(); // También cancela aviso, persecución y lanzador del rival 2 o 3.
        SetPhase(DuelPhase.Defeated);
    }

    private void RetireOpponent()
    {
        if (fighting == null) return;
        fighting.VictoryAchieved -= OnVictory;
        SetOpponentActive(false);
        fighting = null;
    }

    private void SetOpponentActive(bool active)
    {
        if (fighting == rivals[0] && firstLauncher != null) firstLauncher.enabled = active;
        fighting.gameObject.SetActive(active);
    }

    public void Pause()
    {
        if (!Available(DuelPhase.Combat) || notifying) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        SetPhase(DuelPhase.Paused);
    }

    public void Resume()
    {
        if (!Available(DuelPhase.Paused) || notifying) return;
        RestoreClock();
        SetPhase(DuelPhase.Combat);
    }

    private void RestoreClock()
    {
        if (Phase == DuelPhase.Paused) Time.timeScale = previousTimeScale;
    }

    public void RestartCompletedGame()
    {
        if (!Available(DuelPhase.Completed) || notifying || reloadRequested) return;
        var scene = gameObject.scene;
        if (scene.buildIndex < 0 || !Application.CanStreamedLevelBeLoaded(scene.buildIndex))
        {
            Debug.LogError($"DuelSequenceController: habilita {scene.name} en la Scene List para reiniciar.", this);
            return;
        }
        reloadRequested = true;
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void SetPhase(DuelPhase phase)
    {
        Phase = phase;
        Notify();
    }

    private void Notify()
    {
        bool wasNotifying = notifying;
        notifying = true;
        try { Changed?.Invoke(); }
        finally { notifying = wasNotifying; }
    }

    private void OnDisable()
    {
        if (!ready) return;
        ready = false;
        if (subscribedDefeat != null) subscribedDefeat.Defeated -= OnDefeat;
        if (subscribedCrafting != null) subscribedCrafting.Crafted -= OnCrafted;
        subscribedDefeat = null;
        subscribedCrafting = null;
        if (fighting != null)
        {
            fighting.VictoryAchieved -= OnVictory;
            SetOpponentActive(false);
        }
        if (Phase == DuelPhase.Paused)
        {
            RestoreClock();
            Phase = DuelPhase.Combat;
        }
    }
}
