using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EnemyCombatStateTests
{
    private GameObject player;
    private GameObject target;
    private GameObject otherTarget;
    private PlayerMeleeAttack attack;
    private Health health;
    private EnemyCombatState state;
    private int transitions;
    private int confirmedHits;
    private Health lastHit;

    [SetUp]
    public void SetUp()
    {
        transitions = 0;
        confirmedHits = 0;
        lastHit = null;
        otherTarget = null;

        player = new GameObject("StateTestPlayer");
        player.SetActive(false);
        player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        player.AddComponent<PlayerController>().enabled = false;
        attack = player.AddComponent<PlayerMeleeAttack>();
        player.SetActive(true);
        attack.HitConfirmed += hit => { confirmedHits++; lastHit = hit; };

        target = new GameObject("StateTestTarget");
        health = target.AddComponent<Health>();
        target.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        target.SetActive(false);
        state = target.AddComponent<EnemyCombatState>();
        typeof(EnemyCombatState).GetField("playerAttack", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(state, attack);
        state.StateChanged += mode =>
        {
            transitions++;
            Assert.That(mode, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        };
        target.SetActive(true);
        MoveTarget(Vector2.down * 1.25f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(otherTarget);
        Object.DestroyImmediate(player);
    }

    private void MoveTarget(Vector2 position)
    {
        target.transform.position = position;
        Physics2D.SyncTransforms();
    }

    // Mismo reloj controlado que las pruebas existentes de ataque y fatiga.
    private void AttackAt(float time)
    {
        typeof(PlayerMeleeAttack).GetMethod("AttackAt", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(attack, new object[] { time });
    }

    private void AssertInitial()
    {
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.AtaqueConPociones));
        Assert.That(transitions, Is.Zero);
    }

    [Test]
    public void InitialState_IsAtaqueConPociones()
    {
        AssertInitial();
    }

    [Test]
    public void ProximityWithoutAttack_DoesNotTransition()
    {
        MoveTarget(Vector2.up * 10f);
        MoveTarget(Vector2.down * 0.9f);
        AssertInitial();
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(confirmedHits, Is.Zero);
    }

    [Test]
    public void EffectiveHit_ConfirmsTargetAndTransitionsOnce()
    {
        AttackAt(10f);
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        Assert.That(confirmedHits, Is.EqualTo(1));
        Assert.That(lastHit, Is.SameAs(health));
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void FatigueRejectedAttack_DoesNotConfirmOrTransition()
    {
        MoveTarget(Vector2.up * 10f);
        AttackAt(10f);
        MoveTarget(Vector2.down * 1.25f);
        AttackAt(10.2f);
        AssertInitial();
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(confirmedHits, Is.Zero);
    }

    [Test]
    public void Miss_DoesNotConfirmOrTransition()
    {
        MoveTarget(Vector2.up * 10f);
        AttackAt(10f);
        AssertInitial();
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(confirmedHits, Is.Zero);
    }

    [Test]
    public void DeadTarget_DoesNotConfirmOrTransition()
    {
        health.TakeDamage(100);
        AttackAt(10f);
        AssertInitial();
        Assert.That(confirmedHits, Is.Zero);
    }

    [Test]
    public void LethalHit_ConfirmsDamageButDoesNotTransition()
    {
        health.TakeDamage(95);
        AttackAt(10f);
        Assert.That(health.IsDead, Is.True);
        Assert.That(confirmedHits, Is.EqualTo(1));
        AssertInitial();
    }

    [Test]
    public void SubsequentHitsAndReenable_DoNotRepeatTransition()
    {
        AttackAt(10f);
        state.enabled = false;
        state.enabled = true;
        AttackAt(11f);
        Assert.That(health.CurrentHealth, Is.EqualTo(90));
        Assert.That(confirmedHits, Is.EqualTo(2));
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void MultipleColliders_ConfirmAndTransitionOnlyOnce()
    {
        target.AddComponent<CircleCollider2D>().radius = 0.3f;
        Physics2D.SyncTransforms();
        AttackAt(10f);
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        Assert.That(confirmedHits, Is.EqualTo(1));
        Assert.That(transitions, Is.EqualTo(1));
    }

    [Test]
    public void DamageOutsidePlayerAttack_DoesNotTransition()
    {
        health.TakeDamage(5);
        AssertInitial();
        Assert.That(confirmedHits, Is.Zero);
    }

    [Test]
    public void HitOnAnotherHealth_DoesNotTransitionThisEnemy()
    {
        MoveTarget(Vector2.up * 10f);
        otherTarget = new GameObject("OtherTarget");
        otherTarget.transform.position = Vector2.down * 1.25f;
        Health otherHealth = otherTarget.AddComponent<Health>();
        otherTarget.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
        Physics2D.SyncTransforms();
        AttackAt(10f);
        Assert.That(otherHealth.CurrentHealth, Is.EqualTo(95));
        Assert.That(lastHit, Is.SameAs(otherHealth));
        AssertInitial();
    }

    [Test]
    public void DisabledComponent_IgnoresHit_AndReenableReceivesNextHit()
    {
        state.enabled = false;
        AttackAt(10f);
        AssertInitial();
        state.enabled = true;
        state.enabled = false;
        state.enabled = true;
        AttackAt(11f);
        Assert.That(health.CurrentHealth, Is.EqualTo(90));
        Assert.That(state.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Assert.That(transitions, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DestroyedComponent_UnsubscribesBeforeNextHit()
    {
        Object.DestroyImmediate(state);
        // Tambien comprueba la retirada del delegado: un guard por si solo podria ocultar la fuga.
        var handlers = (System.Action<Health>)typeof(PlayerMeleeAttack)
            .GetField("HitConfirmed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack);
        Assert.That(handlers.GetInvocationList().Length, Is.EqualTo(1));
        Assert.DoesNotThrow(() => AttackAt(10f));
        Assert.That(health.CurrentHealth, Is.EqualTo(95));
        Assert.That(transitions, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void MissingAttackReference_RemainsInitialWithoutErrors()
    {
        state.enabled = false;
        typeof(EnemyCombatState).GetField("playerAttack", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(state, null);
        state.enabled = true;
        AttackAt(10f);
        AssertInitial();
        LogAssert.NoUnexpectedReceived();
    }
}
