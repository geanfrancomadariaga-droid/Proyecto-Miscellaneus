using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class EnemyVictoryControllerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player, enemy, controllerObject, aimObject;
    private Health playerHealth, enemyHealth;
    private Rigidbody2D body;
    private PlayerController movement;
    private PlayerMeleeAttack attack;
    private PotionAimInput aim;
    private EnemyCombatState state;
    private EnemyMeleeTelegraph telegraph;
    private EnemyMeleeStrike strike;
    private EnemyVictoryController victory;
    private Object moveReference, moveAsset;
    private int victories, completedWarnings;

    [SetUp]
    public void SetUp()
    {
        victories = 0;
        completedWarnings = 0;
        player = new GameObject("VictoryTestPlayer");
        player.SetActive(false);
        body = player.AddComponent<Rigidbody2D>();
        movement = player.AddComponent<PlayerController>();
        ConfigureMovementInput();
        playerHealth = player.AddComponent<Health>();
        Set(playerHealth, "maxHealth", 60);
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        attack = player.AddComponent<PlayerMeleeAttack>();
        player.SetActive(true);
        aimObject = new GameObject("VictoryTestAim", typeof(RectTransform));
        aim = aimObject.AddComponent<PotionAimInput>();

        enemy = new GameObject("VictoryTestEnemy");
        enemy.SetActive(false);
        enemyHealth = enemy.AddComponent<Health>();
        enemy.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        state = enemy.AddComponent<EnemyCombatState>();
        Set(state, "playerAttack", attack);
        telegraph = enemy.AddComponent<EnemyMeleeTelegraph>();
        Set(telegraph, "player", player.transform);
        strike = enemy.AddComponent<EnemyMeleeStrike>();
        Set(strike, "playerHealth", playerHealth);
        enemy.transform.position = Vector2.down;
        enemy.SetActive(true);
        telegraph.WarningCompleted += (center, size, angle) => completedWarnings++;

        controllerObject = new GameObject("VictoryTestController");
        controllerObject.SetActive(false);
        victory = controllerObject.AddComponent<EnemyVictoryController>();
        Set(victory, "enemyHealth", enemyHealth);
        Set(victory, "enemyCombatState", state);
        Set(victory, "enemyTelegraph", telegraph);
        Set(victory, "enemyStrike", strike);
        victory.VictoryAchieved += () => victories++;
        controllerObject.SetActive(true);
        // Igual que las pruebas de derrota: no esperar frames ni tiempo real.
        Call(victory, "Start");
        Physics2D.SyncTransforms();
    }

    private void ConfigureMovementInput()
    {
        // El asmdef no referencia InputSystem. Crear una accion real por reflexion
        // permite probar el movimiento habilitado sin modificar archivos existentes.
        Type referenceType = typeof(PlayerController).GetField("moveActionReference", Private).FieldType;
        Type assetType = referenceType.Assembly.GetType("UnityEngine.InputSystem.InputActionAsset", true);
        const string json = "{\"name\":\"VictoryTestInput\",\"maps\":[{\"name\":\"Player\",\"actions\":" +
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
        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(aimObject);
        Object.DestroyImmediate(moveReference);
        Object.DestroyImmediate(moveAsset);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private void Kill() => enemyHealth.TakeDamage(enemyHealth.CurrentHealth);

    private void BeginWarning()
    {
        Call(attack, "AttackAt", 10f);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Call(telegraph, "TickAt", 10f);
        Assert.That(telegraph.IsWarning, Is.True);
    }

    private int VictorySubscriptions()
    {
        Action handlers = Read<Action>(enemyHealth, "Died");
        int count = 0;
        if (handlers != null)
            foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, victory)) count++;
        return count;
    }

    [Test]
    public void LivingEnemy_DoesNotProduceVictory()
    {
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        Assert.That(victory.IsVictorious, Is.False);
        Assert.That(victories, Is.Zero);
        Assert.That(state.enabled && telegraph.enabled && strike.enabled, Is.True);
    }

    [Test]
    public void PartialDamage_DoesNotProduceVictory()
    {
        enemyHealth.TakeDamage(99);
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(1));
        Assert.That(victory.IsVictorious, Is.False);
        Assert.That(victories, Is.Zero);
    }

    [Test]
    public void ZeroHealth_RecordsVictoryOnce()
    {
        Kill();
        Assert.That(enemyHealth.CurrentHealth, Is.Zero);
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(victories, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedNotificationAndReenable_DoNotDuplicateVictory()
    {
        Kill();
        Read<Action>(enemyHealth, "Died")?.Invoke();
        enemyHealth.TakeDamage(100);
        victory.enabled = false;
        victory.enabled = true;
        Assert.That(victories, Is.EqualTo(1));
    }

    [Test]
    public void NotificationWhileAlive_DoesNotProduceVictory()
    {
        Read<Action>(enemyHealth, "Died")?.Invoke();
        Assert.That(victory.IsVictorious, Is.False);
        Assert.That(victories, Is.Zero);
    }

    [Test]
    public void DistanceAndElapsedTime_DoNotProduceVictory()
    {
        BeginWarning();
        player.transform.position = Vector2.right * 100f;
        Call(telegraph, "TickAt", 1000f);
        Call(telegraph, "TickAt", 2000f);
        Assert.That(victory.IsVictorious, Is.False);
        Assert.That(victories, Is.Zero);
    }

    [Test]
    public void Death_CancelsPendingWarningAndHidesZone()
    {
        BeginWarning();
        Kill();
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(Read<GameObject>(telegraph, "zoneObject").activeSelf, Is.False);
        Assert.That(completedWarnings, Is.Zero);
    }

    [Test]
    public void Death_CancelsPendingImpact_EvenForPreviouslyCapturedCallback()
    {
        BeginWarning();
        var pending = Read<Action<Vector2, Vector2, float>>(telegraph, "WarningCompleted");
        Kill();
        Call(telegraph, "TickAt", Read<float>(telegraph, "warningEndsAt"));
        Assert.That(completedWarnings, Is.Zero);
        // Simular una invocacion ya capturada: Strike tambien comprueba enabled.
        pending.Invoke(telegraph.LockedCenter, telegraph.LockedSize, telegraph.LockedAngle);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
        Assert.That(Read<Action<Vector2, Vector2, float>>(telegraph, "WarningCompleted")
            .GetInvocationList().Length, Is.EqualTo(1));
    }

    [Test]
    public void Victory_DisablesAllEnemyCombat_AndPreventsNewAttacks()
    {
        BeginWarning();
        Kill();
        Call(telegraph, "TickAt", 1000f);
        Call(telegraph, "TickAt", 2000f);
        Call(strike, "Strike", (Vector2)player.transform.position, Vector2.one, 0f);
        Assert.That(state.enabled || telegraph.enabled || strike.enabled, Is.False);
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(completedWarnings, Is.Zero);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void Victory_KeepsMovementEnabled_AndPlayerCanContinueMoving()
    {
        Kill();
        Assert.That(movement.isActiveAndEnabled, Is.True);
        Set(movement, "movementInput", Vector2.right);
        Call(movement, "FixedUpdate");
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
    }

    [Test]
    public void Victory_KeepsPlayerAttackEnabled()
    {
        Kill();
        Assert.That(attack.isActiveAndEnabled, Is.True);
        Call(attack, "AttackAt", 1000f);
        Assert.That(Read<int>(attack, "consecutiveAttacks"), Is.EqualTo(1));
    }

    [Test]
    public void Victory_KeepsPotionAimEnabled()
    {
        Kill();
        Assert.That(aim.isActiveAndEnabled, Is.True);
    }

    [Test]
    public void Victory_PreservesObjectsHealthPhysicsTimeAndScene()
    {
        playerHealth.TakeDamage(7);
        var constraints = body.constraints;
        body.linearVelocity = Vector2.right * 2f;
        body.angularVelocity = 30f;
        float timeScale = Time.timeScale;
        var scene = enemy.scene;
        Kill();
        Assert.That(player.activeInHierarchy && enemy.activeInHierarchy, Is.True);
        Assert.That(playerHealth.isActiveAndEnabled && enemyHealth.isActiveAndEnabled, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(53));
        Assert.That(enemyHealth.CurrentHealth, Is.Zero);
        Assert.That(body.constraints, Is.EqualTo(constraints));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 2f));
        Assert.That(body.angularVelocity, Is.EqualTo(30f).Within(0.0001f));
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
        Assert.That(enemy.scene, Is.EqualTo(scene));
    }

    [Test]
    public void VictoryEvent_ObservesStoppedCombat_AndReentryDoesNotDuplicate()
    {
        victory.VictoryAchieved += () =>
        {
            Assert.That(victory.IsVictorious, Is.True);
            Assert.That(state.enabled || telegraph.enabled || strike.enabled, Is.False);
            Call(victory, "OnDied");
        };
        Kill();
        Assert.That(victories, Is.EqualTo(1));
    }

    [Test]
    public void DisabledController_Unsubscribes_AndDetectsDeathOnReenable()
    {
        victory.enabled = false;
        Assert.That(VictorySubscriptions(), Is.Zero);
        Kill();
        Assert.That(victories, Is.Zero);
        victory.enabled = true;
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(victories, Is.EqualTo(1));
    }

    [Test]
    public void FirstStart_DetectsDeathBeforeSubscription()
    {
        victory.enabled = false;
        Set(victory, "hasStarted", false);
        Kill();
        victory.enabled = true;
        Assert.That(victories, Is.Zero);
        Call(victory, "Start");
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(victories, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyController_RemovesSubscription(bool afterDeath)
    {
        if (afterDeath) Kill();
        Object.DestroyImmediate(victory);
        Assert.That(VictorySubscriptions(), Is.Zero);
        Assert.DoesNotThrow(Kill);
        Assert.That(victories, Is.EqualTo(afterDeath ? 1 : 0));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RepeatedEnable_UsesOneSubscription_AndUnsubscribesOriginalHealth()
    {
        victory.enabled = false;
        victory.enabled = true;
        victory.enabled = false;
        victory.enabled = true;
        Assert.That(VictorySubscriptions(), Is.EqualTo(1));
        Set(victory, "enemyHealth", playerHealth);
        victory.enabled = false;
        Assert.That(VictorySubscriptions(), Is.Zero);
    }

    [Test]
    public void MissingHealth_ReportsErrorAndDisablesController()
    {
        victory.enabled = false;
        Set(victory, "enemyHealth", null);
        LogAssert.Expect(LogType.Error,
            "EnemyVictoryController: asigna Enemy Health; no se puede detectar la victoria.");
        victory.enabled = true;
        Assert.That(victory.enabled, Is.False);
        Assert.That(VictorySubscriptions(), Is.Zero);
        Assert.DoesNotThrow(Kill);
        Assert.That(victories, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase("enemyCombatState")]
    [TestCase("enemyTelegraph")]
    [TestCase("enemyStrike")]
    public void MissingCombatReference_WarnsButStillRecordsVictory(string field)
    {
        Set(victory, field, null);
        LogAssert.Expect(LogType.Warning,
            $"EnemyVictoryController: falta {field}; no se podra detener ese componente.");
        Call(victory, "Start");
        Assert.DoesNotThrow(Kill);
        victory.enabled = false;
        victory.enabled = true;
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(victories, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }
}
