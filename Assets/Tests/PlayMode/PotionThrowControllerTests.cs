using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public class PotionThrowControllerTests
{
    private GameObject canvas, eventObject, player, enemy, template, controllerObject;
    private EventSystem events, previousEvents;
    private PotionAimInput aim;
    private EquippedPotionCharges charges;
    private Health playerHealth, enemyHealth;
    private PotionThrowController controller;
    private BombProjectile source;

    [SetUp]
    public void SetUp()
    {
        previousEvents = EventSystem.current;
        eventObject = new GameObject("ThrowTestEvents", typeof(EventSystem));
        events = eventObject.GetComponent<EventSystem>();
        EventSystem.current = events;
        canvas = new GameObject("ThrowTestCanvas", typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var aimObject = new GameObject("ThrowTestAim", typeof(RectTransform));
        aimObject.transform.SetParent(canvas.transform, false);
        aim = aimObject.AddComponent<PotionAimInput>();
        Canvas.ForceUpdateCanvases();

        player = new GameObject("ThrowTestPlayer");
        playerHealth = player.AddComponent<Health>();
        charges = player.AddComponent<EquippedPotionCharges>();
        charges.Initialize(3);
        enemy = new GameObject("ThrowTestEnemy");
        enemyHealth = enemy.AddComponent<Health>();
        enemy.AddComponent<CircleCollider2D>().radius = 0.25f;
        enemy.transform.position = Vector3.right * 5f;

        // Objeto fuente creado en memoria: no crea ni necesita un asset prefab.
        template = new GameObject("ThrowTestBomb");
        source = template.AddComponent<BombProjectile>();
        source.enabled = false; // Tambien los clones: solo Advance controla el tiempo.
        template.AddComponent<FireExplosion>();
        controllerObject = new GameObject("ThrowTestController");
        controllerObject.SetActive(false);
        controller = controllerObject.AddComponent<PotionThrowController>();
        Set("aimInput", aim);
        Set("charges", charges);
        Set("playerHealth", playerHealth);
        Set("enemyHealth", enemyHealth);
        Set("projectilePrefab", source);
        controllerObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(controllerObject);
        foreach (var bomb in Bombs())
            Object.DestroyImmediate(bomb.gameObject);
        Object.DestroyImmediate(template);
        Object.DestroyImmediate(canvas);
        Object.DestroyImmediate(eventObject);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(enemy);
        if (previousEvents != null && previousEvents.isActiveAndEnabled)
            EventSystem.current = previousEvents;
    }

    private void Set(string field, object value)
    {
        typeof(PotionThrowController).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
    }

    private BombProjectile[] Bombs()
    {
        return Object.FindObjectsByType<BombProjectile>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Where(b => b != source &&
                b.name == "ThrowTestBomb(Clone)").ToArray();
    }

    private PointerEventData BeginAim()
    {
        var data = new PointerEventData(events)
        {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };
        aim.OnPointerDown(data);
        Assert.That(aim.IsAiming, Is.True);
        return data;
    }

    private void Release(PointerEventData data, Vector2 drag)
    {
        data.position += drag;
        aim.OnPointerUp(data);
    }

    private void Throw()
    {
        Release(BeginAim(), Vector2.right * 100f);
    }

    private void AssertNoThrow()
    {
        Assert.That(charges.RemainingCharges, Is.EqualTo(3));
        Assert.That(Bombs(), Is.Empty);
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ValidRelease_ConsumesOneChargeAndCreatesProjectile()
    {
        Throw();
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(Bombs(), Has.Length.EqualTo(1));
        Assert.That(Bombs()[0].transform.position, Is.EqualTo(player.transform.position));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
    }

    [Test]
    public void InvalidRelease_DoesNotThrowOrConsume()
    {
        Release(BeginAim(), Vector2.zero);
        AssertNoThrow();
    }

    [Test]
    public void CanceledGesture_ThenRelease_DoesNotThrowOrConsume()
    {
        var data = BeginAim();
        data.position += Vector2.right * 100f;
        aim.OnDrag(data);
        aim.OnCancel(new BaseEventData(events));
        aim.OnPointerUp(data);
        AssertNoThrow();
    }

    [Test]
    public void NoCharges_DoesNotThrow()
    {
        charges.Initialize(0);
        Throw();
        Assert.That(charges.RemainingCharges, Is.Zero);
        Assert.That(Bombs(), Is.Empty);
    }

    [Test]
    public void Destination_UsesReleasePositionDirectionAndDistance_ThenStaysFixed()
    {
        var data = BeginAim();
        player.transform.position = new Vector3(2f, 3f, 1f);
        // Arrastre de 50/100: distancia = Lerp(1, 5, 0.5) = 3, direccion arriba.
        Release(data, Vector2.up * 50f);
        var bomb = Bombs().Single();
        Assert.That(bomb.transform.position, Is.EqualTo(new Vector3(2f, 3f, 1f)));
        player.transform.position = Vector3.one * 20f;
        bomb.Advance(0.25f);
        Assert.That(bomb.transform.position, Is.EqualTo(new Vector3(2f, 4.5f, 1f)));
        bomb.Advance(0.25f);
        Assert.That(bomb.transform.position, Is.EqualTo(new Vector3(2f, 6f, 1f)));
    }

    [UnityTest]
    public IEnumerator Arrival_DealsTenDamageOnce_AndDestroysProjectile()
    {
        Throw();
        var bomb = Bombs().Single();
        int arrivals = 0, damageEvents = 0;
        bomb.Arrived += () => arrivals++;
        enemyHealth.HealthChanged += (current, maximum) =>
        {
            damageEvents++;
            bomb.Advance(1f); // Reentrada durante Resolve.
        };
        bomb.Advance(0.5f);
        bomb.Advance(1f);
        bomb.GetComponent<FireExplosion>().Resolve();
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(90));
        Assert.That(arrivals, Is.EqualTo(1));
        Assert.That(damageEvents, Is.EqualTo(1));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        yield return null; // Solo permite procesar Destroy, no mide el vuelo.
        Assert.That(bomb == null, Is.True);
    }

    [Test]
    public void Miss_ConsumesOneChargeWithoutDamage_AndCannotResolveAgain()
    {
        enemy.transform.position = Vector3.right * 20f;
        Throw();
        var bomb = Bombs().Single();
        bomb.Advance(0.5f);
        enemy.transform.position = Vector3.right * 5f;
        bomb.GetComponent<FireExplosion>().Resolve();
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DeadCombatant_RejectsNewThrows(bool playerDied)
    {
        (playerDied ? playerHealth : enemyHealth).TakeDamage(100);
        Throw();
        Assert.That(charges.RemainingCharges, Is.EqualTo(3));
        Assert.That(Bombs(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator PlayerDiesDuringFlight_NoPendingDamage_AndProjectileIsCleaned()
    {
        Throw();
        var bomb = Bombs().Single();
        bomb.Advance(0.25f);
        playerHealth.TakeDamage(100);
        Throw();
        bomb.Advance(0.25f);
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        yield return null;
        Assert.That(bomb == null, Is.True);
        Assert.That(Bombs(), Is.Empty);
    }

    [Test]
    public void MultipleFlights_VictoryPreventsRemainingDamageAndNewThrows()
    {
        enemyHealth.TakeDamage(90);
        Throw();
        Throw();
        var bombs = Bombs();
        Assert.That(bombs, Has.Length.EqualTo(2));
        int damageEvents = 0;
        enemyHealth.HealthChanged += (current, maximum) => damageEvents++;
        foreach (var bomb in bombs)
            bomb.Advance(0.5f);
        Throw();
        Assert.That(enemyHealth.IsDead, Is.True);
        Assert.That(damageEvents, Is.EqualTo(1));
        Assert.That(charges.RemainingCharges, Is.EqualTo(1));
    }

    [TestCase("aimInput")]
    [TestCase("charges")]
    [TestCase("playerHealth")]
    [TestCase("enemyHealth")]
    [TestCase("projectilePrefab")]
    public void MissingReference_DoesNotConsumeOrThrowExceptions(string field)
    {
        controller.enabled = false;
        Set(field, null);
        controller.enabled = true;
        Assert.DoesNotThrow(Throw);
        AssertNoThrow();
    }

    [Test]
    public void MissingExplosion_DoesNotConsumeOrThrowExceptions()
    {
        Object.DestroyImmediate(template.GetComponent<FireExplosion>());
        Assert.DoesNotThrow(Throw);
        AssertNoThrow();
    }

    [Test]
    public void Reenable_DoesNotDuplicateAimSubscriptions()
    {
        for (int i = 0; i < 3; i++)
        {
            controller.enabled = false;
            controller.enabled = true;
        }
        Throw();
        Assert.That(Bombs(), Has.Length.EqualTo(1));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator Disable_UnsubscribesAimAndPendingArrivals()
    {
        Throw();
        var bomb = Bombs().Single();
        controller.enabled = false;
        Throw();
        bomb.Advance(0.5f);
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        yield return null;
        Assert.That(bomb == null, Is.True);
    }

    [Test]
    public void DestroyController_UnsubscribesAimAndPendingArrivals()
    {
        Throw();
        var bomb = Bombs().Single();
        Object.DestroyImmediate(controllerObject);
        Assert.DoesNotThrow(Throw);
        bomb.Advance(0.5f);
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ZeroDuration_SubscribesBeforeImmediateArrival()
    {
        Set("flightDuration", 0f);
        Throw();
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(90));
    }
}
