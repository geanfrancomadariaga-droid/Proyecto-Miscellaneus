#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// El Test Runner ya abrió su propia escena. El menú se ejecuta en edición y la
// copia exclusiva de esta ejecución permite probar el cableado antes de guardar.
public class IntegratedPotionTestScene : IPrebuildSetup, IPostBuildCleanup
{
    private const string Key = "Miscellaneus.Tests.IntegratedPotionScene";
    public static string Path => SessionState.GetString(Key, "");

    public void Setup()
    {
        var previous = SceneManager.GetActiveScene();
        var sample = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(sample);
            for (int i = 0; i < 2; i++)
                Assert.That(EditorApplication.ExecuteMenuItem("Tools/Miscellaneus/Integrar efectos mágicos"), Is.True);
            Assert.That(sample.isDirty, Is.True);
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Tests/PlayMode/IntegratedPotionTest.unity");
            Assert.That(EditorSceneManager.SaveScene(sample, path, true), Is.True);
            SessionState.SetString(Key, path);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(sample, true);
        }
    }

    public void Cleanup()
    {
        if (Path.Length == 0) return;
        Assert.That(AssetDatabase.DeleteAsset(Path), Is.True, Path);
        SessionState.EraseString(Key);
    }
}

[PrebuildSetup(typeof(IntegratedPotionTestScene)), PostBuildCleanup(typeof(IntegratedPotionTestScene))]
public class PotionDuelIntegrationTests : SampleDuelTestFixture
{
    protected override string ScenePath => IntegratedPotionTestScene.Path;

    protected override AsyncOperation LoadAdditive(int index) =>
        EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));

    private PotionCombatEffects PlayerEffects => player.GetComponent<PotionCombatEffects>();

    private PointerEventData BeginAim()
    {
        var aim = All<PotionAimInput>().Single();
        var pointer = new PointerEventData(All<EventSystem>().Single())
        {
            position = new Vector2(Screen.width, Screen.height) * .5f,
            button = PointerEventData.InputButton.Left, pointerId = -1
        };
        aim.OnPointerDown(pointer);
        Assert.That(aim.IsAiming, Is.True);
        return pointer;
    }

    private void ReleaseRight(PointerEventData pointer)
    {
        var aim = All<PotionAimInput>().Single();
        pointer.position += Vector2.right * 120f * aim.GetComponentInParent<Canvas>().scaleFactor;
        aim.OnPointerUp(pointer);
    }

    private void AimRight() => ReleaseRight(BeginAim());

    private void Select(PotionEffect effect)
    {
        for (int i = 0; i < 3 && charges.SelectedElement != effect; i++) Click("PotionSelectionButtonUI");
        Assert.That(charges.SelectedElement, Is.EqualTo(effect));
    }

    [Test]
    public void EditorMenuTwice_LeavesOneSetOfControlsAndFourEffectReceivers()
    {
        Assert.That(All<PotionCombatControls>(), Has.Length.EqualTo(1));
        Assert.That(All<PotionCombatEffects>(), Has.Length.EqualTo(4));
        Assert.That(All<PotionThrowController>(), Has.Length.EqualTo(3));
        Assert.That(rivals.All(r => r.GetComponents<PotionCombatEffects>().Length == 1), Is.True);
        Assert.That(PlayerEffects, Is.Not.Null);
        Assert.That(Named<Button>("Usar vial").onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(Named<Text>("Estado de efectos").raycastTarget, Is.False);
        Assert.That(All<PotionCombatControls>().Single().gameObject.activeInHierarchy, Is.False);
    }

    [Test]
    public void CraftedFireVial_UsesOneChargeAndDealsEightWithOriginalCooldown()
    {
        sequence.BeginGame();
        Supply();
        crafting.NextBase();
        for (int i = 0; i < 5; i++) crafting.RemoveOneConflictingCharge();
        crafting.CraftSelectedFormula();
        crafting.CraftSelectedFormula();
        sequence.BeginDuel();
        Assert.That(All<PotionAimInput>().Single().isActiveAndEnabled, Is.False);
        var button = Named<Button>("Usar vial");
        Click(button.name);
        button.onClick.Invoke();
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(1));
        Assert.That(Named<Text>("Estado de efectos").text, Does.Contain("Ataque"));
        var enemy = sequence.OpponentHealth;
        enemy.transform.position = player.transform.position + Vector3.down;
        Physics2D.SyncTransforms();
        Click("MeleeButtonX");
        Click("MeleeButtonX");
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 8));
        Assert.That(All<BombProjectile>(), Is.Empty);
        DefeatOpponent();
        Assert.That(PlayerEffects.HasFire, Is.False);
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FrostBomb_SlowsOnlyCurrentChaseAndSpendsOneCharge(int duel)
    {
        ReachCombat(duel);
        var frost = charges.GetActiveFormula(PotionEffect.Escarcha) ??
            new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha);
        Assert.That(charges.TryAdd(frost, 1), Is.True);
        Select(PotionEffect.Escarcha);
        var enemy = sequence.OpponentHealth;
        enemy.transform.position = player.transform.position + Vector3.down;
        Physics2D.SyncTransforms();
        Click("MeleeButtonX");
        enemy.transform.position = player.transform.position + Vector3.right * 5f;
        Physics2D.SyncTransforms();
        var body = enemy.GetComponent<Rigidbody2D>();
        var chase = enemy.GetComponent<EnemyChaseMovement>();
        chase.SendMessage("FixedUpdate");
        float normal = body.linearVelocity.magnitude;
        Assert.That(normal, Is.GreaterThan(0f));
        int hp = enemy.CurrentHealth;
        int before = charges.GetCharges(PotionEffect.Escarcha);
        AimRight();
        All<BombProjectile>().Single().Advance(1f);
        chase.SendMessage("FixedUpdate");
        Assert.That(body.linearVelocity.magnitude, Is.EqualTo(normal * .75f).Within(.001f));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(before - 1));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(hp));
        foreach (var rival in rivals.Where(r => r.gameObject != enemy.gameObject))
            Assert.That(rival.GetComponent<PotionCombatEffects>().SpeedFactor, Is.EqualTo(1f));
        AssertOpponents(duel);
    }

    [UnityTest]
    public IEnumerator SwitchingToVialDuringAim_PreservesBombAndPauseCancelsNextGesture()
    {
        sequence.BeginGame();
        Supply();
        crafting.NextBase();
        crafting.NextEffect();
        crafting.CraftSelectedFormula();
        sequence.BeginDuel(); // Fuego sigue seleccionado con las cinco bombas iniciales.
        var pointer = BeginAim();
        Select(PotionEffect.Escarcha);
        yield return null;
        Assert.That(Named<Button>("Usar vial").isActiveAndEnabled, Is.False);
        ReleaseRight(pointer);
        Assert.That(All<BombProjectile>(), Has.Length.EqualTo(1));
        Assert.That(charges.RemainingCharges, Is.EqualTo(4));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(1));
        yield return null;
        Click("Usar vial");
        Assert.That(charges.SelectedElement, Is.EqualTo(PotionEffect.Fuego));
        Assert.That(PlayerEffects.IsProtected, Is.True);
        pointer = BeginAim();
        sequence.Pause();
        ReleaseRight(pointer);
        sequence.Resume();
        Assert.That(All<PotionAimInput>().Single().IsAiming, Is.False);
        Assert.That(charges.RemainingCharges, Is.EqualTo(4));
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    public void ThirdDuel_CraftsAndUsesDoubledVialThenCompletesGame(PotionEffect effect)
    {
        ReachCombat(2);
        DefeatOpponent();
        sequence.ContinueToPreparation();
        crafting.NextBase();    // La última receta fue Bomba + Savia.
        crafting.NextEffect();  // Fuego.
        if (effect == PotionEffect.Escarcha) crafting.NextEffect();
        crafting.ToggleModifier();
        int oldCharges = charges.GetCharges(effect);
        for (int i = 0; i < oldCharges; i++) crafting.RemoveOneConflictingCharge();
        crafting.CraftSelectedFormula();
        for (int i = 0; i < 3 && charges.SelectedElement != effect; i++) charges.TrySelectNext();
        Assert.That(charges.SelectedFormula, Is.EqualTo(new PotionFormula(PotionBase.Vial, effect, PotionModifier.Doble)));
        sequence.BeginDuel();
        Click("Usar vial");
        Assert.That(charges.GetCharges(effect), Is.Zero);
        Assert.That(PlayerEffects.HasFire, Is.EqualTo(effect == PotionEffect.Fuego));
        Assert.That(PlayerEffects.IsProtected, Is.EqualTo(effect == PotionEffect.Escarcha));
        Assert.That(PlayerEffects.MeleeDamage(5), Is.EqualTo(effect == PotionEffect.Fuego ? 8 : 5));
        DefeatOpponent();
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Completed));
        Assert.That(PlayerEffects.HasFire || PlayerEffects.IsProtected, Is.False);
        Assert.That(Named<Text>("Mensaje").text, Does.Contain("JUEGO COMPLETADO"));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(1));
    }

    [Test]
    public void FrostVial_BlocksNewBombSlowAndStillTakesFullDamage()
    {
        sequence.BeginGame();
        Supply();
        crafting.NextBase();
        crafting.NextEffect();
        crafting.CraftSelectedFormula();
        charges.TrySelectNext();
        sequence.BeginDuel();
        Click("Usar vial");
        Assert.That(PlayerEffects.IsProtected, Is.True);
        player.TakeDamage(7);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth - 7));
        // Inyectar una sola carga tras agotar el vial permite ensayar un impacto
        // entrante sin crear ataques enemigos ni alterar las reglas de elaboración.
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 1), Is.True);
        Select(PotionEffect.Escarcha);
        var destination = player.transform.position + Vector3.right * 5f;
        AimRight();
        player.transform.position = destination;
        sequence.OpponentHealth.transform.position = destination;
        All<BombProjectile>().Single().Advance(1f);
        Assert.That(PlayerEffects.SpeedFactor, Is.EqualTo(1f));
        Assert.That(sequence.OpponentHealth.GetComponent<PotionCombatEffects>().SpeedFactor, Is.EqualTo(.75f));
    }

    [UnityTest]
    public IEnumerator FrostBlast_FreezesDuringPauseAndBothSpeedsRecoverAfterResume()
    {
        ReachCombat(1);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 1), Is.True);
        Select(PotionEffect.Escarcha);
        var enemy = sequence.OpponentHealth;
        var destination = player.transform.position + Vector3.right * 5f;
        enemy.transform.position = player.transform.position + Vector3.down;
        Physics2D.SyncTransforms();
        Click("MeleeButtonX");
        AimRight();
        player.transform.position = destination;
        enemy.transform.position = destination;
        All<BombProjectile>().Single().Advance(1f);
        var opponent = enemy.GetComponent<PotionCombatEffects>();
        Assert.That(PlayerEffects.SpeedFactor, Is.EqualTo(.75f));
        // Fuera del alcance cuerpo a cuerpo, la IA persigue durante toda la medida.
        enemy.transform.position = destination + Vector3.right * 15f;
        Physics2D.SyncTransforms();
        sequence.Pause();
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.That(PlayerEffects.SpeedFactor, Is.EqualTo(.75f));
        Assert.That(opponent.SpeedFactor, Is.EqualTo(.75f));
        Assert.That(All<PotionCombatControls>().Single().isActiveAndEnabled, Is.False);
        sequence.Resume();
        var chase = enemy.GetComponent<EnemyChaseMovement>();
        var body = enemy.GetComponent<Rigidbody2D>();
        chase.SendMessage("FixedUpdate");
        float slowedSpeed = body.linearVelocity.magnitude;
        Assert.That(slowedSpeed, Is.GreaterThan(0f));
        yield return new WaitForSeconds(1.55f);
        chase.SendMessage("FixedUpdate");
        Assert.That(body.linearVelocity.magnitude, Is.EqualTo(slowedSpeed / .75f).Within(.001f));
        Assert.That(PlayerEffects.SpeedFactor, Is.EqualTo(1f));
        Assert.That(opponent.SpeedFactor, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator SapVial_ConsumesAtFullHealthCapsHealingAndCannotRevive()
    {
        ReachCombat(1);
        DefeatOpponent();
        sequence.ContinueToPreparation();
        crafting.NextBase();
        crafting.NextEffect();
        crafting.NextEffect();
        for (int i = 0; i < 4; i++) crafting.CraftSelectedFormula();
        sequence.BeginDuel();
        Select(PotionEffect.Savia);
        yield return null;
        var button = Named<Button>("Usar vial");
        var controls = All<PotionCombatControls>().Single();
        controls.enabled = false;
        controls.enabled = true;
        Click(button.name);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(3));
        yield return null;
        player.TakeDamage(4);
        Click(button.name);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(2));
        yield return null;
        player.TakeDamage(30);
        Click(button.name);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth - 20));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(1));
        yield return null;
        player.TakeDamage(player.MaxHealth);
        button.onClick.Invoke();
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Defeated));
        Assert.That(player.CurrentHealth, Is.Zero);
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SecondDuel_CraftedSapBombHealsBothParticipantsAndSelectsFire()
    {
        ReachCombat(2);
        Select(PotionEffect.Savia);
        var enemy = sequence.OpponentHealth;
        var destination = player.transform.position + Vector3.right * 5f;
        player.TakeDamage(25);
        enemy.TakeDamage(25);
        AimRight();
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.Zero);
        Assert.That(charges.SelectedElement, Is.EqualTo(PotionEffect.Fuego));
        player.transform.position = destination;
        enemy.transform.position = destination;
        All<BombProjectile>().Single().Advance(1f);
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth - 15));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth - 10));
        Assert.That(sequence.Phase, Is.EqualTo(DuelPhase.Combat));
    }
}
#endif
