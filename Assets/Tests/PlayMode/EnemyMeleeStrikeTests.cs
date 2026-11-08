using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EnemyMeleeStrikeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player;
    private GameObject enemy;
    private Health playerHealth;
    private Health enemyHealth;
    private PlayerMeleeAttack attack;
    private EnemyCombatState state;
    private EnemyMeleeTelegraph telegraph;
    private EnemyMeleeStrike strike;
    private int healthChanges;

    [SetUp]
    public void SetUp()
    {
        healthChanges = 0;

        player = new GameObject("StrikeTestPlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>().enabled = false;
        attack = player.AddComponent<PlayerMeleeAttack>();
        playerHealth = player.AddComponent<Health>();
        Set(playerHealth, "maxHealth", 60);
        player.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        player.SetActive(true);
        playerHealth.HealthChanged += (current, maximum) => healthChanges++;

        enemy = new GameObject("StrikeTestEnemy");
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
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(player);
    }

    private static void Set(object component, string field, object value)
    {
        component.GetType().GetField(field, PrivateInstance).SetValue(component, value);
    }

    private float ReadTime(string field)
    {
        return (float)typeof(EnemyMeleeTelegraph).GetField(field, PrivateInstance).GetValue(telegraph);
    }

    private void Tick(float now)
    {
        typeof(EnemyMeleeTelegraph).GetMethod("TickAt", PrivateInstance)
            .Invoke(telegraph, new object[] { now });
    }

    private void EnterDuel()
    {
        typeof(PlayerMeleeAttack).GetMethod("AttackAt", PrivateInstance)
            .Invoke(attack, new object[] { 10f });
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
    }

    private void BeginWarning()
    {
        EnterDuel();
        Tick(10f);
        Assert.That(telegraph.IsWarning, Is.True);
    }

    private void Impact()
    {
        Tick(ReadTime("warningEndsAt"));
    }

    [Test]
    public void BeforeEnd_AndRepeatedSameTime_NoDamage()
    {
        BeginWarning();
        for (int i = 0; i < 4; i++) Tick(10f);
        Tick(ReadTime("warningEndsAt") - 0.001f);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
        Assert.That(healthChanges, Is.Zero);
    }

    [Test]
    public void InsideAtExactEnd_ReceivesExactlyOneHit()
    {
        BeginWarning();
        Impact();
        Impact();
        Tick(ReadTime("nextWarningAt") - 0.001f);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
        Assert.That(healthChanges, Is.EqualTo(1));
    }

    [Test]
    public void LeavingZone_DodgesWithoutMovingLockedCenter()
    {
        BeginWarning();
        Vector2 center = telegraph.LockedCenter;
        player.transform.position = Vector2.right * 5f;
        Tick(10.3f);
        Impact();
        Assert.That(telegraph.LockedCenter, Is.EqualTo(center));
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void MovingEnemy_DoesNotMoveRotateOrScaleDamageZone()
    {
        BeginWarning();
        enemy.transform.position = Vector2.left * 5f;
        enemy.transform.rotation = Quaternion.Euler(0, 0, 45);
        enemy.transform.localScale = Vector3.one * 3f;
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
    }

    [Test]
    public void OutsideZoneButNearEnemy_DoesNotTakeDamage()
    {
        BeginWarning();
        player.transform.position = enemy.transform.position + Vector3.right * 0.7f;
        Assert.That(Vector2.Distance(player.transform.position, enemy.transform.position), Is.LessThan(1.2f));
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [TestCase(7)]
    [TestCase(19)]
    public void ConfiguredDamage_IsAppliedExactly(int damage)
    {
        Set(strike, "damage", damage);
        BeginWarning();
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60 - damage));
    }

    [Test]
    public void MultipleCollidersIncludingChild_OnlyDamageReferencedHealthOnce()
    {
        player.AddComponent<CircleCollider2D>().radius = 0.1f;
        GameObject child = new GameObject("Player child collider");
        child.transform.SetParent(player.transform, false);
        child.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        BeginWarning();
        int enemyBefore = enemyHealth.CurrentHealth;
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
        Assert.That(healthChanges, Is.EqualTo(1));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(enemyBefore));
    }

    [Test]
    public void EnemyDeath_CancelsPendingHit()
    {
        BeginWarning();
        enemyHealth.TakeDamage(100);
        Impact();
        Tick(100f);
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [TestCase("strike")]
    [TestCase("telegraph")]
    [TestCase("enemy")]
    public void DisableAndReenable_CancelsPendingHit_AndLaterAttackWorks(string component)
    {
        BeginWarning();
        if (component == "strike") strike.enabled = false;
        else if (component == "telegraph") telegraph.enabled = false;
        else enemy.SetActive(false);
        Assert.That(telegraph.IsWarning, Is.False);
        if (component == "strike") strike.enabled = true;
        else if (component == "telegraph") telegraph.enabled = true;
        else enemy.SetActive(true);
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
        Tick(ReadTime("nextWarningAt"));
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
        Assert.That(healthChanges, Is.EqualTo(1));
    }

    [Test]
    public void PotionMode_ProximityDoesNotAttack()
    {
        Tick(10f);
        Tick(100f);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.AtaqueConPociones));
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void StateChangedAwayFromDuel_CancelsPendingHit()
    {
        BeginWarning();
        Set(state, "currentState", EnemyCombatState.Mode.AtaqueConPociones);
        Impact();
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void MissingTelegraphPlayer_CancelsPendingHit()
    {
        BeginWarning();
        Set(telegraph, "player", null);
        Impact();
        Assert.That(telegraph.IsWarning, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void MissingHealthReference_DoesNotDamageAnything()
    {
        Set(strike, "playerHealth", null);
        BeginWarning();
        int enemyBefore = enemyHealth.CurrentHealth;
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(enemyBefore));
    }

    [Test]
    public void IndependentWarnings_SurviveFiveHits_DieOnSixth()
    {
        BeginWarning();
        for (int i = 1; i <= 6; i++)
        {
            Impact();
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60 - 10 * i));
            Assert.That(playerHealth.IsDead, Is.EqualTo(i == 6));
            if (i < 6) Tick(ReadTime("nextWarningAt"));
        }
        Assert.That(healthChanges, Is.EqualTo(6));
    }

    [TestCase(0f)]
    [TestCase(45f)]
    [TestCase(90f)]
    [TestCase(-135f)]
    public void RotatedRectangle_MatchesVisual_RejectsPointBeyondItsWidth(float angle)
    {
        EnterDuel();
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
        player.transform.position = (Vector2)enemy.transform.position + direction;
        Tick(10f);
        GameObject zone = (GameObject)typeof(EnemyMeleeTelegraph)
            .GetField("zoneObject", PrivateInstance).GetValue(telegraph);
        Assert.That((Vector2)zone.transform.position, Is.EqualTo(telegraph.LockedCenter));
        Assert.That((Vector2)zone.transform.localScale, Is.EqualTo(telegraph.LockedSize));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(zone.transform.eulerAngles.z, telegraph.LockedAngle)), Is.LessThan(0.001f));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        player.transform.position = telegraph.LockedCenter + perpendicular * 0.55f;
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60));
        player.transform.position = (Vector2)enemy.transform.position + direction;
        Tick(ReadTime("nextWarningAt"));
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
    }

    [Test]
    public void ChangingDimensionsDuringWarning_DoesNotChangeLockedDamageZone()
    {
        BeginWarning();
        Set(telegraph, "meleeRange", 0.1f);
        Set(telegraph, "zoneWidth", 0.1f);
        Impact();
        Assert.That(telegraph.LockedSize, Is.EqualTo(new Vector2(1.2f, 0.8f)));
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
    }

    [Test]
    public void PartiallyOverlappingCollider_StillTakesDamage()
    {
        BeginWarning();
        player.GetComponent<BoxCollider2D>().size = Vector2.one * 0.4f;
        player.transform.position = telegraph.LockedCenter + Vector2.right * 0.5f;
        Impact();
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(50));
    }
}
