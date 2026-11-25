using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class DuelSequenceConfigurationTests
{
    [Test]
    public void MissingSceneReferencesGiveDiagnosticAndRejectCommands()
    {
        var go = new GameObject("Incomplete duel setup");
        try
        {
            go.SetActive(false);
            var controller = go.AddComponent<DuelSequenceController>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DuelSequenceController: comprueba derrota"));
            go.SetActive(true);
            Assert.That(controller.enabled, Is.False);
            Assert.DoesNotThrow(controller.BeginGame);
            Assert.DoesNotThrow(controller.BeginDuel);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Menu));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}

// Se prueba la escena guardada por la herramienta, incluidos sus botones persistentes.
// Cargarla de forma aditiva conserva la escena temporal que aloja al Test Runner.
public class DuelSequenceControllerTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene original, sample;
    private float originalScale;
    private DuelSequenceController sequence;
    private PotionCraftingController crafting;
    private PlayerIngredientInventory stock;
    private EquippedPotionCharges charges;
    private Health player;
    private EnemyVictoryController[] rivals;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        original = SceneManager.GetActiveScene();
        originalScale = Time.timeScale;
        Time.timeScale = 1f;
        yield return LoadCopy(SceneUtility.GetBuildIndexByScenePath(ScenePath));
    }

    private IEnumerator LoadCopy(int index)
    {
        Assert.That(index, Is.GreaterThanOrEqualTo(0), "Habilita SampleScene en la Scene List.");
        int next = SceneManager.sceneCount;
        yield return SceneManager.LoadSceneAsync(index, LoadSceneMode.Additive);
        sample = SceneManager.GetSceneAt(next);
        Assert.That(sample.path, Is.EqualTo(ScenePath));
        SceneManager.SetActiveScene(sample);
        sequence = All<DuelSequenceController>().SingleOrDefault();
        Assert.That(sequence, Is.Not.Null, "Ejecuta Integrar tres duelos y guarda SampleScene antes de estas pruebas.");
        crafting = All<PotionCraftingController>().Single();
        stock = All<PlayerIngredientInventory>().Single();
        charges = stock.GetComponent<EquippedPotionCharges>();
        player = stock.GetComponent<Health>();
        rivals = All<EnemyVictoryController>().OrderBy(r => r.GetComponent<Health>().MaxHealth).ToArray();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        Time.timeScale = originalScale;
    }

    private T[] All<T>() where T : Component => sample.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private T Named<T>(string name) where T : Component => All<T>().Single(c => c.name == name);

    private void Click(string name)
    {
        var button = Named<Button>(name);
        Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
        button.onClick.Invoke();
    }

    private void AssertOpponents(int activeDuel)
    {
        Assert.That(rivals.Length, Is.EqualTo(3));
        for (int i = 0; i < rivals.Length; i++)
            Assert.That(rivals[i].gameObject.activeInHierarchy, Is.EqualTo(i + 1 == activeDuel));
    }

    private void Supply()
    {
        foreach (PotionEffect effect in Enum.GetValues(typeof(PotionEffect)))
            Assert.That(stock.TryAdd(effect, 4), Is.True);
    }

    private void DefeatOpponent()
    {
        var health = sequence.OpponentHealth;
        health.TakeDamage(health.CurrentHealth);
    }

    private void ReachCombat(int duel)
    {
        Supply();
        sequence.BeginGame();
        crafting.CraftFireBomb();
        sequence.BeginDuel();
        if (duel == 1) return;
        DefeatOpponent();
        sequence.ContinueToPreparation();
        crafting.NextEffect();
        crafting.NextEffect();
        crafting.CraftSelectedFormula(); // Bomba + Savia
        sequence.BeginDuel();
        if (duel == 2) return;
        DefeatOpponent();
        sequence.ContinueToPreparation();
        crafting.NextEffect();
        crafting.NextEffect();
        crafting.ToggleModifier();
        crafting.CraftSelectedFormula(); // Bomba + Escarcha + Doble
        sequence.BeginDuel();
        Assert.That(sequence.CurrentDuel, Is.EqualTo(duel));
    }

    [Test]
    public void MenuToCompletion_UsesRealButtonsAndDoesNotSkipOrResetResources()
    {
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Menu));
        Assert.That(sequence.CurrentDuel, Is.Zero);
        AssertOpponents(0);
        var sap = All<IngredientPickup>().Single(p => p.Ingredient == PotionEffect.Savia);
        Assert.That(sap.gameObject.activeSelf, Is.False);
        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
        int crafted = 0;
        crafting.Crafted += formula => crafted++;
        Supply();
        crafting.CraftFireBomb();
        sequence.BeginDuel();
        Assert.That(stock.GetQuantity(PotionEffect.Fuego), Is.EqualTo(4));
        Click("Acción de partida");
        sequence.BeginGame();
        Click("Acción de campo");
        Assert.That(Named<Button>("Comenzar duelo").interactable, Is.False);
        Assert.That(Named<Button>("ButtonCambiarModificador").interactable, Is.False);
        Click("ButtonCambiarEfecto");
        Click("ButtonElaborarFormula"); // Ocupa Escarcha sin Doble.
        Click("ButtonCambiarEfecto");
        Click("ButtonElaborarFormula"); // Savia bloqueada, pese a tener material.
        Assert.That(stock.GetQuantity(PotionEffect.Savia), Is.EqualTo(4));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.Zero);
        Assert.That(crafted, Is.EqualTo(1));
        Click("ButtonElaborarBomba"); // Botón fijo, aunque Savia siga seleccionada.
        Click("Comenzar duelo");
        sequence.BeginDuel();
        AssertOpponents(1);
        Assert.That(sequence.OpponentHealth.MaxHealth, Is.EqualTo(100));
        player.TakeDamage(7);
        int remainingLife = player.CurrentHealth;
        sequence.OpponentHealth.TakeDamage(99);
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Combat));
        Assert.That(sequence.SapUnlocked, Is.False);
        int rewards = 0;
        sequence.Changed += () =>
        {
            if (sequence.Phase != DuelPhase.Reward) return;
            rewards++;
            sequence.ContinueToPreparation(); // Un observador no puede saltar la recompensa.
        };
        DefeatOpponent();
        rivals[0].GetComponent<Health>().TakeDamage(1);
        Assert.That(rewards, Is.EqualTo(1));
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Reward));
        Assert.That(sequence.CurrentDuel, Is.EqualTo(1));
        Assert.That(sequence.SapUnlocked && sap.gameObject.activeInHierarchy, Is.True);
        Assert.That(sequence.DoubleUnlocked, Is.False);
        AssertOpponents(0);
        crafting.CraftSelectedFormula();
        Assert.That(crafted, Is.EqualTo(2));
        Click("Acción de partida");
        sequence.ContinueToPreparation();
        Assert.That(sequence.CurrentDuel, Is.EqualTo(2));
        Click("Acción de campo");
        Click("ButtonElaborarBomba");
        Assert.That(sequence.CanBeginDuel, Is.False);
        Click("ButtonElaborarFormula"); // Savia después del desbloqueo.
        Click("Comenzar duelo");
        AssertOpponents(2);
        Assert.That(sequence.OpponentHealth.MaxHealth, Is.EqualTo(120));
        Assert.That(charges.RemainingCharges, Is.EqualTo(7));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(1));
        Assert.That(stock.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(3));
        DefeatOpponent();
        Assert.That(rewards, Is.EqualTo(2));
        Assert.That(sequence.DoubleUnlocked, Is.True);
        Click("Acción de partida");
        Click("Acción de campo");
        Click("ButtonCambiarModificador"); // Bomba + Savia + Doble, inválida.
        int previousCrafts = crafted;
        Click("ButtonElaborarFormula");
        Assert.That(sequence.CanBeginDuel, Is.False);
        Assert.That(stock.GetQuantity(PotionEffect.Savia), Is.EqualTo(3));
        Assert.That(crafted, Is.EqualTo(previousCrafts));
        Click("ButtonCambiarEfecto");
        Click("ButtonCambiarEfecto"); // Escarcha tiene una carga incompatible.
        Click("ButtonElaborarFormula");
        Assert.That(sequence.CanBeginDuel, Is.False);
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(1));
        Assert.That(stock.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(3));
        Click("Retirar carga");
        Assert.That(sequence.CanBeginDuel, Is.False);
        Click("ButtonElaborarFormula");
        Assert.That(crafted, Is.EqualTo(previousCrafts + 1));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(1));
        Assert.That(stock.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(2));
        Click("Comenzar duelo");
        AssertOpponents(3);
        Assert.That(sequence.OpponentHealth.MaxHealth, Is.EqualTo(150));
        Assert.That(player.CurrentHealth, Is.EqualTo(remainingLife));
        DefeatOpponent();
        sequence.BeginGame();
        sequence.ContinueToPreparation();
        sequence.BeginDuel();
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Completed));
        Assert.That(sequence.CurrentDuel, Is.EqualTo(3));
        Assert.That(Named<Text>("Mensaje").text, Does.Contain("JUEGO COMPLETADO"));
        AssertOpponents(0);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FireBombHitsOnlyCurrentOpponentAndConsumesOneCharge(int duel)
    {
        ReachCombat(duel);
        var enemy = sequence.OpponentHealth;
        enemy.GetComponent<Rigidbody2D>().position = (Vector2)player.transform.position + Vector2.right * 5f;
        var aim = All<PotionAimInput>().Single();
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(All<EventSystem>().Single())
        {
            position = new Vector2(Screen.width, Screen.height) * .5f,
            button = PointerEventData.InputButton.Left, pointerId = -1
        };
        int before = charges.RemainingCharges;
        aim.OnPointerDown(pointer);
        pointer.position += Vector2.right * 120f * aim.GetComponentInParent<Canvas>().scaleFactor;
        aim.OnPointerUp(pointer);
        Assert.That(charges.RemainingCharges, Is.EqualTo(before - 1));
        All<BombProjectile>().Single().Advance(1f);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 10));
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Combat));
    }

    [Test]
    public void LockedDoubleRequestAndMissingIngredientCannotConsumeOrMeetTheRequirement()
    {
        sequence.BeginGame();
        crafting.CraftFireBomb();
        Assert.That(sequence.CanBeginDuel, Is.False);
        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
        Assert.That(stock.TryAdd(PotionEffect.Escarcha, 1), Is.True);
        // Simula una selección que llega obsoleta a la operación final de elaboración.
        var selection = (PotionFormulaSelection)typeof(PotionCraftingController)
            .GetField("selection", Private).GetValue(crafting);
        selection.TrySetEffect(PotionEffect.Escarcha);
        selection.TrySetModifier(PotionModifier.Doble);
        int successes = 0;
        crafting.Crafted += formula => successes++;
        crafting.CraftSelectedFormula();
        Assert.That(stock.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(1));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.Zero);
        Assert.That(successes, Is.Zero);
        Assert.That(sequence.CanBeginDuel, Is.False);
    }

    [UnityTest]
    public IEnumerator FirstMeleeStartsChase_AndPauseFreezesTheSimulation()
    {
        ReachCombat(1);
        var enemy = sequence.OpponentHealth;
        var body = enemy.GetComponent<Rigidbody2D>();
        body.position = (Vector2)player.transform.position + Vector2.down;
        Physics2D.SyncTransforms();
        Click("MeleeButtonX");
        Assert.That(enemy.CurrentHealth, Is.EqualTo(95));
        Assert.That(enemy.GetComponent<EnemyCombatState>().CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        body.position = (Vector2)player.transform.position + Vector2.right * 3f;
        enemy.transform.position = body.position;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        Time.timeScale = .5f;
        Click("Acción de campo");
        sequence.Pause();
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Paused));
        Assert.That(All<RecoverableOnScreenStick>().Single().isActiveAndEnabled, Is.False);
        Assert.That(All<PotionAimInput>().Single().isActiveAndEnabled, Is.False);
        Assert.That(Named<Button>("MeleeButtonX").isActiveAndEnabled, Is.False);
        var position = body.position;
        yield return null;
        float pausedTime = Time.time;
        yield return null;
        Assert.That(body.position, Is.EqualTo(position));
        Assert.That(Time.time, Is.EqualTo(pausedTime));
        Click("Acción de partida");
        sequence.Resume();
        Assert.That(Time.timeScale, Is.EqualTo(.5f));
        Assert.That(sequence.OpponentHealth, Is.SameAs(enemy));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(95));
        AssertOpponents(1);
        sequence.Pause();
        sequence.enabled = false;
        Assert.That(Time.timeScale, Is.EqualTo(.5f));
    }

    [TestCase(2)]
    [TestCase(3)]
    public void DefeatCancelsLaterOpponentAndAllContinuation(int duel)
    {
        ReachCombat(duel);
        var enemy = sequence.OpponentHealth;
        // El aviso lee el Transform y el golpe consulta los colliders en este mismo fotograma.
        enemy.transform.position = player.transform.position + Vector3.down;
        Physics2D.SyncTransforms();
        Click("MeleeButtonX");
        Assert.That(enemy.GetComponent<EnemyCombatState>().CurrentState,
            Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        var warning = enemy.GetComponent<EnemyMeleeTelegraph>();
        warning.SendMessage("TickAt", Time.time);
        Assert.That(warning.IsWarning, Is.True);
        sequence.Pause();
        player.TakeDamage(player.MaxHealth);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(warning.IsWarning || enemy.GetComponent<EnemyMeleeStrike>().isActiveAndEnabled ||
            enemy.GetComponent<EnemyChaseMovement>().isActiveAndEnabled, Is.False);
        Assert.That(Named<Button>("RetryButton").isActiveAndEnabled, Is.True);
        enemy.TakeDamage(enemy.MaxHealth);
        sequence.ContinueToPreparation();
        sequence.BeginDuel();
        sequence.Resume();
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Defeated));
        Assert.That(sequence.CurrentDuel, Is.EqualTo(duel));
        AssertOpponents(0);
    }

    [UnityTest]
    public IEnumerator RetryRequestsSampleOnce_AndReloadRestoresInitialState()
    {
        ReachCombat(3);
        // Sustituye solo la carga Single del botón para conservar el Test Runner.
        var screen = All<DefeatScreenUI>().Single();
        screen.enabled = false;
        var go = new GameObject("Retry test");
        go.SetActive(false);
        var retry = go.AddComponent<DefeatScreenUITestDouble>();
        foreach (string name in new[] { "defeatController", "defeatPanel", "retryButton" })
        {
            var field = typeof(DefeatScreenUI).GetField(name, Private);
            field.SetValue(retry, field.GetValue(screen));
        }
        retry.Available = true;
        go.SetActive(true);
        player.TakeDamage(player.MaxHealth);
        Click("RetryButton");
        Click("RetryButton");
        Assert.That(retry.Requests, Is.EqualTo(1));
        int requested = retry.RequestedIndex;
        Assert.That(requested, Is.EqualTo(sample.buildIndex));
        SceneManager.SetActiveScene(original);
        yield return SceneManager.UnloadSceneAsync(sample);
        yield return LoadCopy(requested);
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Menu));
        Assert.That(sequence.CurrentDuel, Is.Zero);
        Assert.That(sequence.SapUnlocked || sequence.DoubleUnlocked, Is.False);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
        foreach (PotionEffect effect in Enum.GetValues(typeof(PotionEffect)))
        {
            Assert.That(stock.GetQuantity(effect), Is.Zero);
            if (effect != PotionEffect.Fuego) Assert.That(charges.GetCharges(effect), Is.Zero);
        }
        AssertOpponents(0);
    }

    [Test]
    public void DisableReenableAndDestroyDetachExactlyTheSubscribedHandlers()
    {
        ReachCombat(1);
        var rival = rivals[0];
        var defeat = player.GetComponent<PlayerDefeatController>();
        void AssertHandlers(int count)
        {
            foreach (var pair in new (object source, string name)[]
                { (rival, "VictoryAchieved"), (defeat, "Defeated"), (crafting, "Crafted") })
            {
                var handlers = (Delegate)pair.source.GetType().GetField(pair.name, Private).GetValue(pair.source);
                int actual = handlers?.GetInvocationList().Count(d => ReferenceEquals(d.Target, sequence)) ?? 0;
                Assert.That(actual, Is.EqualTo(count), pair.name);
            }
        }
        AssertHandlers(1);
        sequence.enabled = false;
        AssertHandlers(0);
        sequence.enabled = true;
        AssertHandlers(1);
        UnityEngine.Object.DestroyImmediate(sequence);
        AssertHandlers(0);
    }

    [Test]
    public void DuplicatedOpponentDisablesCoordinatorWithDiagnostic()
    {
        sequence.enabled = false;
        typeof(DuelSequenceController).GetField("rivals", Private).SetValue(sequence,
            new[] { rivals[0], rivals[0], rivals[2] });
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DuelSequenceController: revisa el rival 2"));
        sequence.enabled = true;
        Assert.That(sequence.enabled, Is.False);
        Assert.DoesNotThrow(sequence.BeginGame);
        AssertOpponents(0);
    }

    [Test]
    public void DisabledViewStopsObservingAndDetachesItsPauseButton()
    {
        ReachCombat(1);
        var view = All<DuelFlowUI>().Single();
        view.enabled = false;
        Click("Acción de campo");
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Combat));
        var status = Named<Text>("TargetHealth");
        string previous = status.text;
        sequence.OpponentHealth.TakeDamage(5);
        Assert.That(status.text, Is.EqualTo(previous));
        view.enabled = true;
        Assert.That(status.text, Does.Contain("95/100"));
        Click("Acción de campo");
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Paused));
    }
}
