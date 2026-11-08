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
}
