using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EnemyMeleeTelegraphTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player;
    private GameObject enemy;
    private PlayerMeleeAttack attack;
    private EnemyCombatState state;
    private EnemyMeleeTelegraph telegraph;
    private Health enemyHealth;
    private Health playerHealth;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("TelegraphTestPlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>().enabled = false;
        attack = player.AddComponent<PlayerMeleeAttack>();
        playerHealth = player.AddComponent<Health>();
        player.SetActive(true);

        enemy = new GameObject("TelegraphTestEnemy");
        enemyHealth = enemy.AddComponent<Health>();
        enemy.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        enemy.SetActive(false);
        state = enemy.AddComponent<EnemyCombatState>();
        Set(state, "playerAttack", attack);
        telegraph = enemy.AddComponent<EnemyMeleeTelegraph>();
        Set(telegraph, "player", player.transform);
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

    private T Read<T>(string field)
    {
        return (T)typeof(EnemyMeleeTelegraph).GetField(field, PrivateInstance).GetValue(telegraph);
    }

    private void Tick(float time)
    {
        typeof(EnemyMeleeTelegraph).GetMethod("TickAt", PrivateInstance)
            .Invoke(telegraph, new object[] { time });
    }

    private void EnterDuel()
    {
        typeof(PlayerMeleeAttack).GetMethod("AttackAt", PrivateInstance)
            .Invoke(attack, new object[] { 10f });
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
    }

    private void AssertVisible(bool expected)
    {
        Assert.That(telegraph.IsWarning, Is.EqualTo(expected));
        GameObject zone = Read<GameObject>("zoneObject");
        Assert.That(zone != null && zone.activeSelf, Is.EqualTo(expected));
    }

    [Test]
    public void ProximityWithoutHit_NeverStartsWarning()
    {
        Tick(10f);
        Tick(100f);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.AtaqueConPociones));
        AssertVisible(false);
    }

    [Test]
    public void EffectiveHitAndPlayerInRange_StartVisibleDangerZone()
    {
        EnterDuel();
        Tick(10f);
        AssertVisible(true);
        Assert.That(telegraph.LockedDirection, Is.EqualTo(Vector2.up));
        Assert.That(Vector2.Distance(telegraph.LockedCenter, new Vector2(0, -0.4f)), Is.LessThan(0.00001f));
        SpriteRenderer renderer = Read<SpriteRenderer>("zoneRenderer");
        Assert.That(renderer.enabled, Is.True);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sharedMaterial, Is.Not.Null);
        Assert.That(renderer.transform.localScale, Is.EqualTo(new Vector3(1.2f, 0.8f, 1f)));
        Assert.That(renderer.GetComponent<Collider2D>(), Is.Null);
    }

    [TestCase(1.2f, true)]
    [TestCase(1.201f, false)]
    public void Range_UsesInclusiveCenterDistance(float distance, bool expected)
    {
        EnterDuel();
        player.transform.position = enemy.transform.position + Vector3.right * distance;
        Tick(10f);
        AssertVisible(expected);
    }

    [Test]
    public void MovingBothActors_DoesNotMoveRotateOrResizeWarning()
    {
        EnterDuel();
        Tick(10f);
        Transform zone = Read<GameObject>("zoneObject").transform;
        Vector3 position = zone.position;
        Quaternion rotation = zone.rotation;
        Vector3 scale = zone.localScale;
        Vector2 direction = telegraph.LockedDirection;
        Vector2 center = telegraph.LockedCenter;
        player.transform.position = Vector3.right * 10f;
        enemy.transform.position = Vector3.left * 10f;
        enemy.transform.rotation = Quaternion.Euler(0, 0, 90);
        enemy.transform.localScale = Vector3.one * 2f;
        Tick(10.3f);
        AssertVisible(true);
        Assert.That(zone.position, Is.EqualTo(position));
        Assert.That(zone.rotation, Is.EqualTo(rotation));
        Assert.That(zone.localScale, Is.EqualTo(scale));
        Assert.That(telegraph.LockedDirection, Is.EqualTo(direction));
        Assert.That(telegraph.LockedCenter, Is.EqualTo(center));
    }

    [Test]
    public void WarningEndAndRecovery_RespectExactBoundaries()
    {
        EnterDuel();
        Tick(10f);
        float ends = Read<float>("warningEndsAt");
        float next = Read<float>("nextWarningAt");
        Assert.That(ends - 10f, Is.EqualTo(0.6f).Within(0.00001f));
        Assert.That(next - ends, Is.EqualTo(1.8f).Within(0.00001f));
        Tick(ends - 0.001f);
        AssertVisible(true);
        Tick(ends);
        AssertVisible(false);
        Tick(next - 0.001f);
        AssertVisible(false);
        Tick(next);
        AssertVisible(true);
    }

    [Test]
    public void OutsideRangeAfterRecovery_DoesNotStartAgain()
    {
        EnterDuel();
        Tick(10f);
        Tick(Read<float>("warningEndsAt"));
        player.transform.position = Vector3.up * 10f;
        Tick(100f);
        AssertVisible(false);
    }

    [Test]
    public void DeadEnemyInDuel_DoesNotStartWarning()
    {
        EnterDuel();
        enemyHealth.TakeDamage(100);
        Tick(10f);
        AssertVisible(false);
    }

    [Test]
    public void DeathDuringWarning_CancelsImmediately()
    {
        EnterDuel();
        Tick(10f);
        enemyHealth.TakeDamage(100);
        AssertVisible(false);
        Tick(100f);
        AssertVisible(false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Disable_CancelsAndCannotBypassRecovery(bool wholeObject)
    {
        EnterDuel();
        Tick(10f);
        if (wholeObject) enemy.SetActive(false);
        else telegraph.enabled = false;
        AssertVisible(false);
        Tick(10.1f);
        AssertVisible(false);
        if (wholeObject) enemy.SetActive(true);
        else telegraph.enabled = true;
        Tick(10.2f);
        AssertVisible(false);
        Tick(Read<float>("nextWarningAt"));
        AssertVisible(true);
    }

    [Test]
    public void RepeatedWarnings_NeverChangePlayerHealth_AndReuseResources()
    {
        EnterDuel();
        playerHealth.TakeDamage(30);
        int changes = 0;
        playerHealth.HealthChanged += (current, maximum) => changes++;
        Tick(10f);
        GameObject zone = Read<GameObject>("zoneObject");
        Sprite sprite = Read<Sprite>("zoneSprite");
        Texture2D texture = Read<Texture2D>("zoneTexture");
        for (int i = 0; i < 5; i++)
        {
            Tick(Read<float>("warningEndsAt"));
            Tick(Read<float>("nextWarningAt"));
        }
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(70));
        Assert.That(changes, Is.Zero);
        Assert.That(Read<GameObject>("zoneObject"), Is.SameAs(zone));
        Assert.That(Read<Sprite>("zoneSprite"), Is.SameAs(sprite));
        Assert.That(Read<Texture2D>("zoneTexture"), Is.SameAs(texture));
    }

    [Test]
    public void MissingPlayerReference_DoesNotStartWarning()
    {
        EnterDuel();
        Set(telegraph, "player", null);
        Tick(10f);
        AssertVisible(false);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ConfiguredRangeAndTimes_AreUsed()
    {
        EnterDuel();
        Set(telegraph, "meleeRange", 2f);
        Set(telegraph, "warningSeconds", 0.25f);
        Set(telegraph, "recoverySeconds", 3f);
        player.transform.position = enemy.transform.position + Vector3.right * 1.5f;
        Tick(10f);
        AssertVisible(true);
        Assert.That(Read<float>("warningEndsAt"), Is.EqualTo(10.25f));
        Assert.That(Read<float>("nextWarningAt"), Is.EqualTo(13.25f));
    }

    [UnityTest]
    public IEnumerator DestroyComponent_ReleasesVisualResources()
    {
        EnterDuel();
        Tick(10f);
        GameObject zone = Read<GameObject>("zoneObject");
        Sprite sprite = Read<Sprite>("zoneSprite");
        Texture2D texture = Read<Texture2D>("zoneTexture");
        Object.DestroyImmediate(telegraph);
        yield return null; // Solo la destruccion diferida de Unity necesita un frame.
        Assert.That(zone == null, Is.True);
        Assert.That(sprite == null, Is.True);
        Assert.That(texture == null, Is.True);
    }
}
