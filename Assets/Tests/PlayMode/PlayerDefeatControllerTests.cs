using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class PlayerDefeatControllerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player, enemy, canvasObject, eventObject;
    private Health health, enemyHealth;
    private Rigidbody2D body;
    private PlayerController movement;
    private PlayerMeleeAttack attack;
    private PotionAimInput aim;
    private EnemyCombatState state;
    private EnemyMeleeTelegraph telegraph;
    private EnemyMeleeStrike strike;
    private PlayerDefeatController defeat;
    private Button button;
    private EventSystem eventSystem;
    private Object moveReference;
    private Object moveAsset;
    private int defeats;

    [SetUp]
    public void SetUp()
    {
        defeats = 0;
        player = new GameObject("DefeatTestPlayer");
        player.SetActive(false);
        body = player.AddComponent<Rigidbody2D>();
        movement = player.AddComponent<PlayerController>();
        ConfigureMovementInput();
        health = player.AddComponent<Health>();
        Set(health, "maxHealth", 60);
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        attack = player.AddComponent<PlayerMeleeAttack>();

        canvasObject = new GameObject("DefeatTestCanvas", typeof(Canvas));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var aimObject = new GameObject("Aim", typeof(RectTransform));
        aimObject.transform.SetParent(canvasObject.transform, false);
        aim = aimObject.AddComponent<PotionAimInput>();
        var buttonObject = new GameObject("X", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        button = buttonObject.GetComponent<Button>();
        Set(attack, "attackButton", button);
        eventObject = new GameObject("DefeatTestEvents", typeof(EventSystem));
        eventSystem = eventObject.GetComponent<EventSystem>();
        player.SetActive(true);

        enemy = new GameObject("DefeatTestEnemy");
        enemy.SetActive(false);
        enemyHealth = enemy.AddComponent<Health>();
        enemy.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        state = enemy.AddComponent<EnemyCombatState>();
        Set(state, "playerAttack", attack);
        telegraph = enemy.AddComponent<EnemyMeleeTelegraph>();
        Set(telegraph, "player", player.transform);
        strike = enemy.AddComponent<EnemyMeleeStrike>();
        Set(strike, "playerHealth", health);
        enemy.transform.position = Vector2.down;
        enemy.SetActive(true);

        // Configurar antes de OnEnable, igual que referencias guardadas en Inspector.
        player.SetActive(false);
        defeat = player.AddComponent<PlayerDefeatController>();
        Set(defeat, "playerHealth", health);
        Set(defeat, "playerMovement", movement);
        Set(defeat, "playerBody", body);
        Set(defeat, "playerAttack", attack);
        Set(defeat, "potionAimInput", aim);
        Set(defeat, "enemyCombatState", state);
        Set(defeat, "enemyTelegraph", telegraph);
        Set(defeat, "enemyStrike", strike);
        defeat.Defeated += () => defeats++;
        player.SetActive(true);
        // Ejecutar Start explicitamente evita esperar un frame en estos tests sincronicos.
        Call(defeat, "Start");
        Physics2D.SyncTransforms();
    }

    private void ConfigureMovementInput()
    {
        // El asmdef existente no referencia InputSystem directamente. Reflexion solo
        // en pruebas permite crear una accion real sin modificar ese archivo.
        Type referenceType = typeof(PlayerController).GetField("moveActionReference", Private).FieldType;
        Type assetType = referenceType.Assembly.GetType("UnityEngine.InputSystem.InputActionAsset", true);
        const string json = "{\"name\":\"DefeatTestInput\",\"maps\":[{\"name\":\"Player\",\"actions\":" +
            "[{\"name\":\"Move\",\"type\":\"Value\",\"expectedControlType\":\"Vector2\"}],\"bindings\":[]}]}";
        moveAsset = (Object)assetType.GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { json });
        object action = assetType.GetMethod("FindAction", new[] { typeof(string), typeof(bool) })
            .Invoke(moveAsset, new object[] { "Player/Move", true });
        moveReference = (Object)referenceType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new[] { action });
        Set(movement, "moveActionReference", moveReference);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(eventObject);
        Object.DestroyImmediate(moveReference);
        Object.DestroyImmediate(moveAsset);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private void Kill() => health.TakeDamage(60);

    private void BeginWarning()
    {
        Call(attack, "AttackAt", 10f);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Call(telegraph, "TickAt", 10f);
        Assert.That(telegraph.IsWarning, Is.True);
    }

    private PointerEventData Pointer() => new PointerEventData(eventSystem)
    {
        pointerId = 1,
        button = PointerEventData.InputButton.Left,
        position = new Vector2(100f, 100f)
    };

    [Test]
    public void PositiveHealth_RemainsActive()
    {
        health.TakeDamage(59);
        Assert.That(defeat.IsDefeated, Is.False);
        Assert.That(defeats, Is.Zero);
        Assert.That(movement.enabled && attack.enabled && aim.enabled, Is.True);
        Assert.That(state.enabled && telegraph.enabled && strike.enabled, Is.True);
        Assert.That(body.constraints, Is.Not.EqualTo(RigidbodyConstraints2D.FreezeAll));
    }

    [Test]
    public void ZeroHealth_DefeatsOnce_EvenAfterReenableAndRepeatedNotification()
    {
        int deaths = 0;
        health.Died += () => deaths++;
        Kill();
        health.TakeDamage(100);
        defeat.enabled = false;
        defeat.enabled = true;
        Call(defeat, "OnDied");
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(defeats, Is.EqualTo(1));
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(health.CurrentHealth, Is.Zero);
    }

    [Test]
    public void Defeat_DisablesMovement_ClearsVelocities_AndFreezesBody()
    {
        Assert.That(movement.isActiveAndEnabled, Is.True);
        Set(movement, "movementInput", Vector2.right);
        Call(movement, "FixedUpdate");
        Assert.That(body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));
        body.angularVelocity = 30f;
        Kill();
        Assert.That(movement.enabled, Is.False);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(body.angularVelocity, Is.Zero);
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
    }

    [Test]
    public void Defeat_BlocksButtonAndDirectAttack()
    {
        button.onClick.Invoke();
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(95));
        Kill();
        button.onClick.Invoke();
        attack.Attack();
        Call(attack, "AttackAt", 1000f);
        Assert.That(attack.enabled, Is.False);
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(95));
    }

    [Test]
    public void Defeat_CancelsActiveAim_AndBlocksNewGestures()
    {
        int canceled = 0, finished = 0;
        aim.AimCanceled += () => canceled++;
        aim.AimFinished += result => finished++;
        PointerEventData pointer = Pointer();
        aim.OnPointerDown(pointer);
        Assert.That(aim.IsAiming, Is.True);
        Kill();
        aim.OnDrag(pointer);
        aim.OnPointerUp(pointer);
        aim.OnPointerDown(pointer);
        Assert.That(aim.enabled, Is.False);
        Assert.That(aim.IsAiming, Is.False);
        Assert.That(canceled, Is.EqualTo(1));
        Assert.That(finished, Is.Zero);
    }

    [Test]
    public void Defeat_CancelsPendingWarningAndImpact_AndPreventsNewWarnings()
    {
        BeginWarning();
        int completed = 0;
        telegraph.WarningCompleted += (center, size, angle) => completed++;
        Kill();
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(Read<GameObject>(telegraph, "zoneObject").activeSelf, Is.False);
        Assert.That(state.enabled || telegraph.enabled || strike.enabled, Is.False);
        Call(telegraph, "TickAt", Read<float>(telegraph, "warningEndsAt"));
        Call(telegraph, "TickAt", 1000f);
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(completed, Is.Zero);
        // Solo queda el observador del test: Strike retiro su suscripcion pendiente.
        var handlers = Read<Action<Vector2, Vector2, float>>(telegraph, "WarningCompleted");
        Assert.That(handlers.GetInvocationList().Length, Is.EqualTo(1));
    }

    [Test]
    public void LethalEnemyImpact_StopsCombatInsideDamageCallback()
    {
        health.TakeDamage(50);
        BeginWarning();
        Call(telegraph, "TickAt", Read<float>(telegraph, "warningEndsAt"));
        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(defeats, Is.EqualTo(1));
        Assert.That(strike.enabled || telegraph.enabled || state.enabled, Is.False);
    }

    [Test]
    public void DefeatedEvent_ObservesStoppedCombat_AndActiveHealthAndObjects()
    {
        float timeScale = Time.timeScale;
        defeat.Defeated += () =>
        {
            Assert.That(defeat.IsDefeated, Is.True);
            Assert.That(movement.enabled || attack.enabled || aim.enabled || strike.enabled, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        };
        Kill();
        Assert.That(player.activeInHierarchy && enemy.activeInHierarchy, Is.True);
        Assert.That(health.isActiveAndEnabled && enemyHealth.isActiveAndEnabled, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
    }

    [Test]
    public void DisabledController_Unsubscribes_AndDetectsMissedDeathOnReenable()
    {
        defeat.enabled = false;
        Assert.That(Read<Action>(health, "Died"), Is.Null);
        Kill();
        Assert.That(defeat.IsDefeated, Is.False);
        defeat.enabled = true;
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(defeats, Is.EqualTo(1));
    }

    [Test]
    public void Start_DetectsHealthAlreadyDeadBeforeSubscription()
    {
        defeat.enabled = false;
        Set(defeat, "hasStarted", false);
        Kill();
        defeat.enabled = true;
        Assert.That(defeat.IsDefeated, Is.False);
        Call(defeat, "Start");
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(defeats, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyController_RemovesSubscriptionWithoutExceptions(bool afterDeath)
    {
        if (afterDeath) Kill();
        Assert.DoesNotThrow(() => Object.DestroyImmediate(defeat));
        Assert.That(Read<Action>(health, "Died"), Is.Null);
        Assert.DoesNotThrow(Kill);
        Assert.That(defeats, Is.EqualTo(afterDeath ? 1 : 0));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RepeatedEnable_SubscriptionRemainsUnique_AndUsesOriginalSource()
    {
        defeat.enabled = false;
        defeat.enabled = true;
        defeat.enabled = false;
        defeat.enabled = true;
        Assert.That(Read<Action>(health, "Died").GetInvocationList().Length, Is.EqualTo(1));
        Set(defeat, "playerHealth", enemyHealth);
        defeat.enabled = false;
        Assert.That(Read<Action>(health, "Died"), Is.Null);
    }

    [Test]
    public void MissingHealth_ReportsErrorAndDisablesController()
    {
        defeat.enabled = false;
        Set(defeat, "playerHealth", null);
        LogAssert.Expect(LogType.Error,
            "PlayerDefeatController: asigna Player Health; no se puede detectar la derrota.");
        defeat.enabled = true;
        Assert.That(defeat.enabled, Is.False);
        Assert.That(defeat.IsDefeated, Is.False);
    }

    [TestCase("playerMovement")]
    [TestCase("playerBody")]
    [TestCase("playerAttack")]
    [TestCase("potionAimInput")]
    [TestCase("enemyCombatState")]
    [TestCase("enemyTelegraph")]
    [TestCase("enemyStrike")]
    public void MissingOptionalReference_WarnsAndStillRecordsDefeat(string field)
    {
        Set(defeat, field, null);
        LogAssert.Expect(LogType.Warning,
            $"PlayerDefeatController: falta {field}; no se podra detener ese componente.");
        Call(defeat, "Start");
        Assert.DoesNotThrow(Kill);
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(defeats, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }
}
