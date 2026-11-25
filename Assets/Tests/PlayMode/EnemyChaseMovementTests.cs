using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EnemyChaseMovementTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player, enemy;
    private Health playerHealth, enemyHealth;
    private Rigidbody2D body;
    private PlayerMeleeAttack attack;
    private EnemyCombatState state;
    private EnemyMeleeTelegraph telegraph;
    private EnemyMeleeStrike strike;
    private EnemyChaseMovement chase;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("ChaseTestPlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>().enabled = false;
        playerHealth = player.AddComponent<Health>();
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        attack = player.AddComponent<PlayerMeleeAttack>();
        player.SetActive(true);

        enemy = new GameObject("ChaseTestEnemy");
        enemy.SetActive(false);
        body = enemy.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 8f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        enemyHealth = enemy.AddComponent<Health>();
        enemy.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        state = enemy.AddComponent<EnemyCombatState>();
        Set(state, "playerAttack", attack);
        telegraph = enemy.AddComponent<EnemyMeleeTelegraph>();
        Set(telegraph, "player", player.transform);
        strike = enemy.AddComponent<EnemyMeleeStrike>();
        Set(strike, "playerHealth", playerHealth);
        chase = enemy.AddComponent<EnemyChaseMovement>();
        Set(chase, "player", player.transform);
        enemy.transform.position = Vector2.down;
        enemy.SetActive(true);
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(player);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);

    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private void Step() => Call(chase, "FixedUpdate");

    private void EnterDuel()
    {
        // La transicion usa un impacto real, sin escribir el estado privado.
        Call(attack, "AttackAt", 10f);
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(95));
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
    }

    private void MovePlayer(Vector2 offset)
    {
        player.transform.position = body.position + offset;
        Physics2D.SyncTransforms();
    }

    private void StartChasing()
    {
        EnterDuel();
        MovePlayer(Vector2.right * 5f);
        Step();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 2f));
    }

    private void AssertStopped()
    {
        Step();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Step();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void InitialPotionMode_ClearsExistingVelocityWithoutTransition()
    {
        MovePlayer(Vector2.right * 5f);
        body.linearVelocity = Vector2.one;
        AssertStopped();
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.AtaqueConPociones));
    }

    [UnityTest]
    public IEnumerator Duel_PhysicsStepsBringEnemyCloser()
    {
        EnterDuel();
        MovePlayer(Vector2.right * 5f);
        float before = Vector2.Distance(body.position, player.transform.position);
        // No se invoca FixedUpdate manualmente: Unity mueve el Rigidbody2D.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(Vector2.Distance(body.position, player.transform.position), Is.LessThan(before));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1.05f)]
    public void InsideOrAtStoppingDistance_ClearsVelocity(float distance)
    {
        StartChasing();
        MovePlayer(Vector2.right * distance);
        AssertStopped();
    }

    [Test]
    public void MovingPlayer_ChangesDirectionWithoutDiagonalSpeedIncrease()
    {
        StartChasing();
        MovePlayer(new Vector2(-3f, 4f));
        Step();
        Assert.That(Vector2.Distance(body.linearVelocity, new Vector2(-1.2f, 1.6f)), Is.LessThan(0.00001f));
        Assert.That(body.linearVelocity.magnitude, Is.EqualTo(2f).Within(0.00001f));
    }

    [Test]
    public void Warning_StopsChaseUntilCompletion_ThenResumes()
    {
        StartChasing();
        MovePlayer(Vector2.up * 1.1f);
        Call(telegraph, "TickAt", 10f);
        Assert.That(telegraph.IsWarning, Is.True);
        MovePlayer(Vector2.right * 5f);
        AssertStopped();
        Call(telegraph, "TickAt", 10.3f);
        AssertStopped();
        Call(telegraph, "TickAt", 11f);
        Assert.That(telegraph.IsWarning, Is.False);
        Step();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 2f));
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DeathWithoutOutcomeController_StopsChase(bool enemyDies)
    {
        StartChasing();
        (enemyDies ? enemyHealth : playerHealth).TakeDamage(100);
        AssertStopped();
    }

    [Test]
    public void DisabledCombatState_ClearsVelocity()
    {
        StartChasing();
        state.enabled = false;
        AssertStopped();
    }

    [TestCase(EnemyCombatState.Mode.AtaqueConPociones)]
    [TestCase(EnemyCombatState.Mode.PersecucionFrenetica)]
    public void LeavingDuel_ClearsVelocity(EnemyCombatState.Mode mode)
    {
        StartChasing();
        // No hay transicion publica de salida todavia: simular solo en la prueba.
        Set(state, "currentState", mode);
        AssertStopped();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisableChaseOrObject_StopsImmediately(bool wholeObject)
    {
        StartChasing();
        if (wholeObject) enemy.SetActive(false);
        else chase.enabled = false;
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void MissingPlayer_ClearsVelocityWithoutExceptions()
    {
        StartChasing();
        Set(chase, "player", null);
        Assert.DoesNotThrow(AssertStopped);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void PlayerWithoutHealth_StopsSafely()
    {
        StartChasing();
        Object.DestroyImmediate(playerHealth);
        Assert.DoesNotThrow(AssertStopped);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ConfiguredSpeedAndDistance_AreRespected()
    {
        StartChasing();
        Set(chase, "chaseSpeed", 3f);
        Set(chase, "stoppingDistance", 2f);
        MovePlayer(Vector2.right * 2.1f);
        Step();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.right * 3f));
        MovePlayer(Vector2.right * 2f);
        AssertStopped();
    }

    [Test]
    public void ChaseAndStop_DoNotChangePotionsIngredientsOrPlayerPhysics()
    {
        var charges = player.AddComponent<EquippedPotionCharges>();
        var ingredients = player.AddComponent<PlayerIngredientInventory>();
        var elements = new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };
        foreach (var element in elements)
        {
            if (element != PotionEffect.Fuego)
                Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, element), 5), Is.True);
            Assert.That(ingredients.TryAdd(element, 7), Is.True);
        }
        var playerBody = player.GetComponent<Rigidbody2D>();
        var constraints = playerBody.constraints;
        var velocity = playerBody.linearVelocity;
        var selected = charges.SelectedElement;
        StartChasing();
        for (int i = 0; i < 20; i++) Step();
        MovePlayer(Vector2.zero);
        AssertStopped();
        foreach (var element in elements)
        {
            Assert.That(charges.GetCharges(element), Is.EqualTo(5));
            Assert.That(ingredients.GetQuantity(element), Is.EqualTo(7));
        }
        Assert.That(charges.SelectedElement, Is.EqualTo(selected));
        Assert.That(playerBody.constraints, Is.EqualTo(constraints));
        Assert.That(playerBody.linearVelocity, Is.EqualTo(velocity));
        Assert.That(attack.enabled, Is.True);
    }

    [Test]
    public void ExistingStrike_StillDealsOneHitWhileChaseIsStopped()
    {
        EnterDuel();
        Call(telegraph, "TickAt", 10f);
        Assert.That(telegraph.IsWarning, Is.True);
        AssertStopped();
        Call(telegraph, "TickAt", 11f);
        Call(telegraph, "TickAt", 11f);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(90));
    }

    [Test]
    public void ExistingVictory_StillFiresOnceAndDisablesCombat()
    {
        enemy.SetActive(false);
        var victory = enemy.AddComponent<EnemyVictoryController>();
        Set(victory, "enemyHealth", enemyHealth);
        Set(victory, "enemyCombatState", state);
        Set(victory, "enemyTelegraph", telegraph);
        Set(victory, "enemyStrike", strike);
        int events = 0;
        victory.VictoryAchieved += () => events++;
        enemy.SetActive(true);
        Call(victory, "Start");
        StartChasing();
        enemyHealth.TakeDamage(100);
        enemyHealth.TakeDamage(100);
        AssertStopped();
        Assert.That(victory.IsVictorious, Is.True);
        Assert.That(events, Is.EqualTo(1));
        Assert.That(state.enabled || telegraph.enabled || strike.enabled, Is.False);
        Assert.That(attack.enabled, Is.True);
    }

    [Test]
    public void ExistingDefeat_LethalStrikeStillEndsCombatOnce()
    {
        player.SetActive(false);
        var defeat = player.AddComponent<PlayerDefeatController>();
        var aim = player.AddComponent<PotionAimInput>();
        Set(defeat, "playerHealth", playerHealth);
        Set(defeat, "playerMovement", player.GetComponent<PlayerController>());
        Set(defeat, "playerBody", player.GetComponent<Rigidbody2D>());
        Set(defeat, "playerAttack", attack);
        Set(defeat, "potionAimInput", aim);
        Set(defeat, "enemyCombatState", state);
        Set(defeat, "enemyTelegraph", telegraph);
        Set(defeat, "enemyStrike", strike);
        int events = 0;
        defeat.Defeated += () => events++;
        player.SetActive(true);
        Call(defeat, "Start");
        EnterDuel();
        playerHealth.TakeDamage(90);
        Call(telegraph, "TickAt", 10f);
        Assert.That(telegraph.IsWarning, Is.True);
        body.linearVelocity = Vector2.right;
        Call(telegraph, "TickAt", 11f);
        playerHealth.TakeDamage(100);
        AssertStopped();
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(events, Is.EqualTo(1));
        Assert.That(state.enabled || telegraph.enabled || strike.enabled || attack.enabled, Is.False);
    }
}
