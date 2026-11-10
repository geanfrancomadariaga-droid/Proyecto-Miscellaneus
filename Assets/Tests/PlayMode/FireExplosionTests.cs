using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FireExplosionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject explosionObject;
    private GameObject enemy;
    private GameObject player;
    private FireExplosion explosion;
    private Health enemyHealth;
    private CircleCollider2D enemyCollider;
    private int healthChanges;

    [SetUp]
    public void SetUp()
    {
        explosionObject = new GameObject("FireExplosionTests");
        explosion = explosionObject.AddComponent<FireExplosion>();
        enemy = new GameObject("ExplosionTarget");
        enemyHealth = enemy.AddComponent<Health>();
        enemyCollider = enemy.AddComponent<CircleCollider2D>();
        enemyCollider.radius = 0.25f;
        Set(explosion, "enemyHealth", enemyHealth);
        healthChanges = 0;
        enemyHealth.HealthChanged += (current, maximum) => healthChanges++;
        player = null;
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(explosionObject);
        Object.DestroyImmediate(enemy);
        Object.DestroyImmediate(player);
    }

    private static void Set(object component, string field, object value)
    {
        component.GetType().GetField(field, PrivateInstance).SetValue(component, value);
    }

    private void MoveEnemy(float x)
    {
        enemy.transform.position = Vector2.right * x;
        Physics2D.SyncTransforms();
    }

    private void AssertDamage(int damage, int changes)
    {
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100 - damage));
        Assert.That(healthChanges, Is.EqualTo(changes));
    }

    [TestCase(0.5f, 10)] // Dentro.
    [TestCase(0.75f, 10)] // Centro del enemigo sobre el radio.
    [TestCase(1f, 10)] // Borde del collider tangente: 0.75 + 0.25.
    [TestCase(1.05f, 0)] // Collider completamente fuera.
    public void Resolve_DefaultRadius_UsesColliderOverlap(float x, int damage)
    {
        MoveEnemy(x);
        explosion.Resolve();
        AssertDamage(damage, damage == 0 ? 0 : 1);
    }

    [Test]
    public void ConfiguredRadius_UsesWorldUnitsDespiteObjectScale()
    {
        Set(explosion, "radius", 1.5f);
        explosionObject.transform.localScale = Vector3.one * 0.1f;
        MoveEnemy(1.5f);
        explosion.Resolve();
        AssertDamage(10, 1);
    }

    [Test]
    public void ExplosionCenter_UsesItsWorldPosition()
    {
        explosionObject.transform.position = Vector2.right * 5f;
        MoveEnemy(5.5f);
        explosion.Resolve();
        AssertDamage(10, 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChildCollider_AloneOrWithOtherColliders_DamagesOnce(bool multiple)
    {
        enemyCollider.enabled = multiple;
        GameObject child = new GameObject("TargetCollider");
        child.transform.SetParent(enemy.transform, false);
        child.AddComponent<CircleCollider2D>().radius = 0.25f;
        if (multiple)
            enemy.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
        Physics2D.SyncTransforms();
        explosion.Resolve();
        AssertDamage(10, 1);
    }

    [Test]
    public void OtherHealthInsideArea_IsNotDamaged()
    {
        player = new GameObject("ExplosionPlayer");
        Health playerHealth = player.AddComponent<Health>();
        player.AddComponent<CircleCollider2D>().radius = 0.25f;
        Physics2D.SyncTransforms();
        explosion.Resolve();
        AssertDamage(10, 1);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100));
    }

    [Test]
    public void RepeatedResolve_AndReenable_DoNotRepeatDamageOrDestroyObject()
    {
        explosion.Resolve();
        explosionObject.SetActive(false);
        explosionObject.SetActive(true);
        explosion.Resolve();
        explosion.Resolve();
        AssertDamage(10, 1);
        Assert.That(explosionObject != null, Is.True);
    }

    [Test]
    public void Resolve_FromHealthChanged_DoesNotRepeatDamage()
    {
        enemyHealth.HealthChanged += (current, maximum) => explosion.Resolve();
        explosion.Resolve();
        AssertDamage(10, 1);
    }

    [Test]
    public void Miss_ThenEnemyApproaches_DoesNotDealLateDamage()
    {
        MoveEnemy(3f);
        explosion.Resolve();
        AssertDamage(0, 0);
        MoveEnemy(0f);
        explosion.Resolve();
        AssertDamage(0, 0);
    }

    [Test]
    public void MissingTarget_IsSafe_AndLaterAssignmentDoesNotDealDamage()
    {
        Set(explosion, "enemyHealth", null);
        Assert.DoesNotThrow(() => explosion.Resolve());
        Set(explosion, "enemyHealth", enemyHealth);
        explosion.Resolve();
        AssertDamage(0, 0);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SetTarget_ReplacesPreviousTargetBeforeResolve()
    {
        player = new GameObject("ConfiguredExplosionTarget");
        Health replacement = player.AddComponent<Health>();
        player.AddComponent<CircleCollider2D>();
        explosion.SetTarget(replacement);
        explosion.Resolve();
        AssertDamage(0, 0);
        Assert.That(replacement.CurrentHealth, Is.EqualTo(90));
    }

    [Test]
    public void SetTarget_NullIsSafe_AndAssignmentDoesNotResetResolution()
    {
        explosion.SetTarget(null);
        Assert.DoesNotThrow(() => explosion.Resolve());
        explosion.SetTarget(enemyHealth);
        explosion.Resolve();
        AssertDamage(0, 0);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SetTarget_AfterDamage_DoesNotAllowAnotherExplosion()
    {
        explosion.SetTarget(enemyHealth);
        explosion.Resolve();
        explosion.SetTarget(enemyHealth);
        explosion.Resolve();
        AssertDamage(10, 1);
    }

    [Test]
    public void DisabledCollider_DoesNotReceiveDamage()
    {
        enemyCollider.enabled = false;
        Physics2D.SyncTransforms();
        explosion.Resolve();
        AssertDamage(0, 0);
    }

    [Test]
    public void TriggerTarget_IsDetectedEvenWhenGlobalTriggerQueriesAreDisabled()
    {
        bool previous = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = false;
            enemyCollider.isTrigger = true;
            Physics2D.SyncTransforms();
            explosion.Resolve();
            AssertDamage(10, 1);
        }
        finally
        {
            Physics2D.queriesHitTriggers = previous;
        }
    }

    [Test]
    public void Explosion_DoesNotConfirmMeleeHitOrTransition_ButLaterMeleeStillDoes()
    {
        player = new GameObject("ExplosionStatePlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>().enabled = false;
        PlayerMeleeAttack attack = player.AddComponent<PlayerMeleeAttack>();
        player.SetActive(true);
        int confirmedHits = 0;
        attack.HitConfirmed += hit => confirmedHits++;

        enemy.SetActive(false);
        EnemyCombatState state = enemy.AddComponent<EnemyCombatState>();
        Set(state, "playerAttack", attack);
        int transitions = 0;
        state.StateChanged += mode => transitions++;
        enemy.SetActive(true);
        enemy.transform.position = Vector2.down * 0.9f;
        Physics2D.SyncTransforms();

        explosion.Resolve();
        AssertDamage(10, 1);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.AtaqueConPociones));
        Assert.That(transitions, Is.Zero);
        Assert.That(confirmedHits, Is.Zero);

        attack.Attack();
        AssertDamage(15, 2);
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Assert.That(transitions, Is.EqualTo(1));
        Assert.That(confirmedHits, Is.EqualTo(1));
    }
}
