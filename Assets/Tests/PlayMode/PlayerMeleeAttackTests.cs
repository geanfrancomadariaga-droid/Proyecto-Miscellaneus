using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMeleeAttackTests
{
    private GameObject player;
    private GameObject target;
    private GameObject buttonObject;
    private PlayerMeleeAttack attack;
    private Health health;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("MeleeTestPlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>();
        attack = player.AddComponent<PlayerMeleeAttack>();
        buttonObject = new GameObject("X", typeof(RectTransform), typeof(Button));
        typeof(PlayerMeleeAttack).GetField("attackButton", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(attack, buttonObject.GetComponent<Button>());
        // No activamos PlayerController: estas pruebas no simulan entrada de movimiento.
        player.GetComponent<PlayerController>().enabled = false;
        player.SetActive(true);
        target = new GameObject("Target");
        target.transform.position = Vector2.down * 1.25f;
        health = target.AddComponent<Health>();
        target.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(buttonObject);
    }

    [Test]
    public void InitialDirection_IsDown()
    {
        Assert.That(player.GetComponent<PlayerController>().LastMovementDirection, Is.EqualTo(Vector2.down));
    }

    [Test]
    public void ButtonClick_DamagesFiveOnce_EvenWithMultipleColliders()
    {
        target.AddComponent<CircleCollider2D>().radius = 0.3f;
        Physics2D.SyncTransforms();
        buttonObject.GetComponent<Button>().onClick.Invoke();
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        buttonObject.GetComponent<Button>().onClick.Invoke();
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        AttackAt(Read<float>("nextAttackAt"));
        Assert.That(health.CurrentHealth, Is.EqualTo(90));
    }

    [TestCase(0f, -3f)]
    [TestCase(0f, 1.25f)]
    public void OutsideCircle_DoesNotDamage(float x, float y)
    {
        target.transform.position = new Vector2(x, y);
        Physics2D.SyncTransforms();
        attack.Attack();
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
    }

    [Test]
    public void OwnHealth_IsNotDamaged()
    {
        Health ownHealth = player.AddComponent<Health>();
        player.AddComponent<CircleCollider2D>().radius = 1f;
        Physics2D.SyncTransforms();
        attack.Attack();
        Assert.That(ownHealth.CurrentHealth, Is.EqualTo(100));
    }

    // Solo las pruebas proporcionan tiempos artificiales; el boton usa Time.time.
    private void AttackAt(float time)
    {
        typeof(PlayerMeleeAttack).GetMethod("AttackAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attack, new object[] { time });
    }

    private T Read<T>(string field)
    {
        return (T)typeof(PlayerMeleeAttack).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(attack);
    }

    private float ExecuteHits(int count)
    {
        float time = 10f;
        for (int i = 0; i < count; i++)
        {
            AttackAt(time);
            time = Read<float>("nextAttackAt");
        }
        return time;
    }

    [TestCase(1, 0.4f)]
    [TestCase(2, 0.4f)]
    [TestCase(3, 0.8f)]
    [TestCase(4, 0.8f)]
    [TestCase(5, 1.2f)]
    [TestCase(6, 3f)]
    public void Recovery_RejectsEarlyAttempt_AndAcceptsExactBoundary(int hit, float recovery)
    {
        float availableAt = ExecuteHits(hit);
        Assert.That(availableAt - Read<float>("lastAttackAt"), Is.EqualTo(recovery).Within(0.00001f));
        Assert.That(health.CurrentHealth, Is.EqualTo(100 - hit * 5));

        AttackAt(availableAt - 0.001f);
        Assert.That(health.CurrentHealth, Is.EqualTo(100 - hit * 5));
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(hit));
        Assert.That(Read<float>("nextAttackAt"), Is.EqualTo(availableAt));

        AttackAt(availableAt);
        Assert.That(health.CurrentHealth, Is.EqualTo(100 - (hit + 1) * 5));
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(hit == 6 ? 1 : hit + 1));
    }

    [Test]
    public void SixthHit_BlocksPastInactivity_ThenStartsNewSequence()
    {
        float availableAt = ExecuteHits(6);
        float sixthAt = Read<float>("lastAttackAt");
        AttackAt(sixthAt + 2.5f);
        Assert.That(health.CurrentHealth, Is.EqualTo(70));
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(6));
        AttackAt(availableAt);
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(1));
        Assert.That(Read<float>("nextAttackAt") - availableAt, Is.EqualTo(0.4f).Within(0.00001f));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void Inactivity_CountsFromExecutedHit_AndResetsAtExactBoundary(int hits)
    {
        ExecuteHits(hits);
        float lastHit = Read<float>("lastAttackAt");
        AttackAt(lastHit + 0.01f); // Rechazado: no debe aplazar la inactividad.
        Assert.That(Read<float>("lastAttackAt"), Is.EqualTo(lastHit));
        AttackAt(lastHit + 2.5f);
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(1));
        Assert.That(Read<float>("nextAttackAt") - (lastHit + 2.5f),
            Is.EqualTo(0.4f).Within(0.00001f));
    }

    [Test]
    public void BeforeInactivityBoundary_ContinuesSequence()
    {
        ExecuteHits(2);
        AttackAt(Read<float>("lastAttackAt") + 2.499f);
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(3));
    }

    [Test]
    public void Misses_StillReachSixthHitBlock()
    {
        target.transform.position = Vector2.up * 10f;
        Physics2D.SyncTransforms();
        float availableAt = ExecuteHits(6);
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(6));
        Assert.That(availableAt - Read<float>("lastAttackAt"), Is.EqualTo(3f).Within(0.00001f));
    }

    [Test]
    public void RejectedAttempt_DoesNotShowImpactOrDamageOrAdvanceFatigue()
    {
        LineRenderer outline = player.AddComponent<LineRenderer>();
        typeof(PlayerMeleeAttack).GetField("hitOutline", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(attack, outline);
        AttackAt(10f);
        Assert.That(outline.enabled, Is.True);
        float hideAt = Read<float>("hideFeedbackAt");
        outline.enabled = false;
        AttackAt(10.2f);
        Assert.That(outline.enabled, Is.False);
        Assert.That(Read<float>("hideFeedbackAt"), Is.EqualTo(hideAt));
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        Assert.That(Read<int>("consecutiveAttacks"), Is.EqualTo(1));
        Assert.That(Read<float>("lastAttackAt"), Is.EqualTo(10f));
    }
}
