using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class VictoryBannerActivationCounter : MonoBehaviour
{
    public int Openings;
    private void OnEnable() => Openings++;
}

public class VictoryBannerUITests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject enemy, canvasObject, banner, player;
    private Health health;
    private EnemyVictoryController victory;
    private VictoryBannerUI ui;
    private VictoryBannerActivationCounter counter;
    private CanvasGroup group;
    private Object moveAsset, moveReference;

    [SetUp]
    public void SetUp()
    {
        enemy = new GameObject("VictoryBannerTestEnemy");
        enemy.SetActive(false);
        health = enemy.AddComponent<Health>();
        victory = enemy.AddComponent<EnemyVictoryController>();
        Set(victory, "enemyHealth", health);
        enemy.SetActive(true);
        // Tests sincronicos: no avanzan frames ni ejecutan Start del enemigo.
        canvasObject = new GameObject("VictoryBannerTestCanvas", typeof(Canvas));
        canvasObject.SetActive(false);
        banner = new GameObject("VictoryBanner", typeof(RectTransform));
        banner.transform.SetParent(canvasObject.transform, false);
        group = banner.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        counter = banner.AddComponent<VictoryBannerActivationCounter>();
        ui = canvasObject.AddComponent<VictoryBannerUI>();
        Set(ui, "victoryController", victory);
        Set(ui, "victoryBanner", banner);
        canvasObject.SetActive(true);
        counter.Openings = 0;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(moveReference);
        Object.DestroyImmediate(moveAsset);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private Action Handlers() => Read<Action>(victory, "VictoryAchieved");
    private void Kill() => health.TakeDamage(health.MaxHealth);
    private void Tick(float now) => Call(ui, "TickAt", now);
    private void Expire() => Tick(Read<float>(ui, "hideAt"));

    [Test]
    public void NoVictory_StartsHidden_WithThreeSecondDefault()
    {
        Assert.That(banner.activeSelf, Is.False);
        Assert.That(ui.isActiveAndEnabled && canvasObject.activeInHierarchy, Is.True);
        Assert.That(Read<float>(ui, "displaySeconds"), Is.EqualTo(3f));
    }

    [Test]
    public void NoVictory_DamageTimeAndSpuriousNotification_DoNotShowBanner()
    {
        health.TakeDamage(1);
        Tick(float.MaxValue);
        Handlers().Invoke();
        Assert.That(banner.activeSelf, Is.False);
        Assert.That(counter.Openings, Is.Zero);
    }

    [Test]
    public void VictoryEvent_ShowsBanner()
    {
        Kill();
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(banner.activeInHierarchy, Is.True);
        Assert.That(counter.Openings, Is.EqualTo(1));
    }

    [TestCase(3f)]
    [TestCase(0.25f)]
    public void ConfiguredDuration_HidesAtExactDeadline(float seconds)
    {
        Set(ui, "displaySeconds", seconds);
        float start = Time.unscaledTime;
        Kill();
        float deadline = Read<float>(ui, "hideAt");
        Assert.That(deadline, Is.EqualTo(start + seconds));
        Tick(deadline - 0.125f);
        Assert.That(banner.activeSelf, Is.True);
        Tick(deadline);
        Assert.That(banner.activeSelf, Is.False);
        Assert.That(Read<bool>(ui, "hidePending"), Is.False);
        Assert.That(ui.isActiveAndEnabled && canvasObject.activeInHierarchy, Is.True);
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    public void NonPositiveDuration_HidesOnNextTick(float seconds)
    {
        Set(ui, "displaySeconds", seconds);
        Kill();
        Tick(Time.unscaledTime);
        Assert.That(banner.activeSelf, Is.False);
    }

    [Test]
    public void EnabledAfterVictory_RecognizesStoredState()
    {
        ui.enabled = false;
        Kill();
        Assert.That(banner.activeSelf, Is.False);
        ui.enabled = true;
        Assert.That(banner.activeSelf, Is.True);
        Assert.That(counter.Openings, Is.EqualTo(1));
        Expire();
        Assert.That(banner.activeSelf, Is.False);
    }

    [Test]
    public void DuplicateEventsAndReenable_DoNotRestartOrRepeatBanner()
    {
        Kill();
        float deadline = Read<float>(ui, "hideAt");
        Set(ui, "displaySeconds", 99f);
        Handlers().Invoke();
        Assert.That(Read<float>(ui, "hideAt"), Is.EqualTo(deadline));
        Expire();
        Handlers().Invoke();
        ui.enabled = false;
        ui.enabled = true;
        Assert.That(banner.activeSelf, Is.False);
        Assert.That(counter.Openings, Is.EqualTo(1));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void DisableOrDestroy_CleansSubscriptionAndTimer(bool destroy, bool afterVictory)
    {
        if (afterVictory) Kill();
        Action captured = Handlers();
        if (destroy) Object.DestroyImmediate(ui);
        else ui.enabled = false;
        Assert.That(Handlers(), Is.Null);
        Assert.That(banner.activeSelf, Is.False);
        if (!destroy)
        {
            Assert.That(Read<bool>(ui, "hidePending"), Is.False);
            Assert.DoesNotThrow(() => captured.Invoke());
            Tick(float.MaxValue);
        }
        Assert.DoesNotThrow(Kill);
        Assert.That(banner.activeSelf, Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DisableWhileVisible_ReenableDoesNotRepeatConsumedNotice()
    {
        Kill();
        ui.enabled = false;
        ui.enabled = true;
        Assert.That(banner.activeSelf, Is.False);
        Assert.That(counter.Openings, Is.EqualTo(1));
        Assert.That(Read<bool>(ui, "hidePending"), Is.False);
    }

    [Test]
    public void RepeatedEnable_UsesOneSubscription_AndRemovesOriginalSource()
    {
        for (int i = 0; i < 3; i++)
        {
            ui.enabled = false;
            ui.enabled = true;
        }
        Assert.That(Handlers().GetInvocationList().Length, Is.EqualTo(1));
        Set(ui, "victoryController", null);
        ui.enabled = false;
        Assert.That(Handlers(), Is.Null);
    }

    [TestCase("victoryController")]
    [TestCase("victoryBanner")]
    public void MissingReference_ReportsErrorWithoutUnexpectedException(string field)
    {
        ui.enabled = false;
        Set(ui, field, null);
        LogAssert.Expect(LogType.Error, "VictoryBannerUI: asigna Victory Controller y Victory Banner " +
            "en el Inspector. El componente se desactivara.");
        Assert.DoesNotThrow(() => ui.enabled = true);
        Assert.That(ui.enabled, Is.False);
        Assert.That(canvasObject.activeInHierarchy, Is.True);
        Assert.That(Handlers(), Is.Null);
        Assert.DoesNotThrow(Kill);
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BannerIsOwnerOrAncestor_RejectsWithoutDeactivatingOwner(bool ancestor)
    {
        ui.enabled = false;
        if (ancestor) canvasObject.transform.SetParent(enemy.transform, false);
        Set(ui, "victoryBanner", ancestor ? enemy : canvasObject);
        LogAssert.Expect(LogType.Error, "VictoryBannerUI: Victory Banner no puede ser este objeto ni un ancestro. " +
            "Coloca el componente en un objeto activo del Canvas, fuera del banner.");
        ui.enabled = true;
        Assert.That(ui.enabled, Is.False);
        Assert.That(canvasObject.activeInHierarchy && enemy.activeInHierarchy, Is.True);
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void ShowAndHide_PreserveTimeScaleAndCanvasGroup(float scale)
    {
        float original = Time.timeScale;
        try
        {
            Time.timeScale = scale;
            Kill();
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(group.blocksRaycasts || group.interactable, Is.False);
            Expire();
            Assert.That(banner.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(group.blocksRaycasts || group.interactable, Is.False);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(victory.isActiveAndEnabled, Is.True);
        }
        finally { Time.timeScale = original; }
    }

    [Test]
    public void ShowAndHide_PreserveHealthPlayerComponentsPhysicsAndScene()
    {
        player = new GameObject("VictoryBannerTestPlayer");
        player.SetActive(false);
        var body = player.AddComponent<Rigidbody2D>();
        var movement = player.AddComponent<PlayerController>();
        // Misma tecnica que EnemyVictoryControllerTests: no cambiar el asmdef.
        Type referenceType = typeof(PlayerController).GetField("moveActionReference", Private).FieldType;
        Type assetType = referenceType.Assembly.GetType("UnityEngine.InputSystem.InputActionAsset", true);
        const string json = "{\"name\":\"BannerTestInput\",\"maps\":[{\"name\":\"Player\",\"actions\":" +
            "[{\"name\":\"Move\",\"type\":\"Value\",\"expectedControlType\":\"Vector2\"}],\"bindings\":[]}]}";
        moveAsset = (Object)assetType.GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { json });
        object action = assetType.GetMethod("FindAction", new[] { typeof(string), typeof(bool) })
            .Invoke(moveAsset, new object[] { "Player/Move", true });
        moveReference = (Object)referenceType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { action });
        Set(movement, "moveActionReference", moveReference);
        var playerHealth = player.AddComponent<Health>();
        var attack = player.AddComponent<PlayerMeleeAttack>();
        var aim = player.AddComponent<PotionAimInput>();
        player.SetActive(true);
        playerHealth.TakeDamage(7);
        body.linearVelocity = Vector2.right * 2f;
        body.angularVelocity = 30f;
        var constraints = body.constraints;
        var scene = player.scene;
        Kill();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(93));
        Assert.That(movement.isActiveAndEnabled && attack.isActiveAndEnabled && aim.isActiveAndEnabled, Is.True);
        Expire();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(93));
        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(playerHealth.isActiveAndEnabled && health.isActiveAndEnabled, Is.True);
        Assert.That(movement.isActiveAndEnabled && attack.isActiveAndEnabled && aim.isActiveAndEnabled, Is.True);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 2f));
        Assert.That(body.angularVelocity, Is.EqualTo(30f).Within(0.0001f));
        Assert.That(body.constraints, Is.EqualTo(constraints));
        Assert.That(player.scene, Is.EqualTo(scene));
        Assert.That(victory.isActiveAndEnabled, Is.True);
    }
}
