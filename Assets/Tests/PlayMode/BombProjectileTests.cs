using NUnit.Framework;
using UnityEngine;

public class BombProjectileTests
{
    private GameObject testObject;
    private BombProjectile projectile;
    private int arrivalCount;
    private readonly Vector3 origin = new Vector3(2f, -4f, 1f);
    private readonly Vector3 destination = new Vector3(10f, 8f, 3f);

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("BombProjectileTests");
        projectile = testObject.AddComponent<BombProjectile>();
        // Solo Advance controla el tiempo de estas pruebas, nunca Update.
        projectile.enabled = false;
        arrivalCount = 0;
        projectile.Arrived += () => arrivalCount++;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObject);
    }

    [Test]
    public void Initialize_PlacesProjectileAtOrigin_WithoutArrival()
    {
        Assert.That(projectile.Initialize(origin, destination, 0.5f), Is.True);

        Assert.That(testObject.transform.position, Is.EqualTo(origin));
        Assert.That(arrivalCount, Is.Zero);
    }

    [TestCase(0.5f)]
    [TestCase(2f)]
    public void Advance_PartialTime_InterpolatesLinearly(float duration)
    {
        projectile.Initialize(origin, destination, duration);

        projectile.Advance(duration / 4f);
        Assert.That(testObject.transform.position, Is.EqualTo(new Vector3(4f, -1f, 1.5f)));
        projectile.Advance(duration / 4f);
        Assert.That(testObject.transform.position, Is.EqualTo(new Vector3(6f, 2f, 2f)));
        Assert.That(arrivalCount, Is.Zero);
    }

    [TestCase(0.5f)]
    [TestCase(2f)]
    public void Advance_ReachingOrExceedingDuration_ArrivesExactly(float deltaTime)
    {
        projectile.Initialize(origin, destination, 0.5f);
        Vector3 positionAtArrival = Vector3.zero;
        projectile.Arrived += () => positionAtArrival = testObject.transform.position;

        projectile.Advance(deltaTime);

        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(positionAtArrival, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [Test]
    public void Initialize_CopiesDestination_IndependentOfMovingTarget()
    {
        GameObject target = new GameObject("MovingTarget");
        try
        {
            target.transform.position = destination;
            projectile.Initialize(origin, target.transform.position, 0.5f);
            projectile.Advance(0.25f);

            target.transform.position = new Vector3(100f, 200f, 0f);
            projectile.Advance(0.25f);

            Assert.That(testObject.transform.position, Is.EqualTo(destination));
            Assert.That(arrivalCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void Advance_AfterArrival_DoesNotMoveNotifyAgainOrDestroyObject()
    {
        projectile.Initialize(origin, destination, 0.5f);
        projectile.Advance(0.5f);

        for (int i = 0; i < 3; i++)
            projectile.Advance(1f);

        Assert.That(testObject != null, Is.True);
        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [TestCase(0f)]
    [TestCase(-0.5f)]
    public void Initialize_NonPositiveDuration_ArrivesImmediatelyOnlyOnce(float duration)
    {
        Assert.That(projectile.Initialize(origin, destination, duration), Is.True);
        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));

        projectile.Advance(1f);

        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [TestCase(-0.25f)]
    [TestCase(0f)]
    public void Advance_NonPositiveDelta_DoesNotReverseOrDelayArrival(float deltaTime)
    {
        projectile.Initialize(origin, destination, 0.5f);
        projectile.Advance(0.25f);
        Vector3 halfway = testObject.transform.position;

        projectile.Advance(deltaTime);

        Assert.That(testObject.transform.position, Is.EqualTo(halfway));
        Assert.That(arrivalCount, Is.Zero);
        projectile.Advance(0.25f);
        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [Test]
    public void Advance_BeforeInitialization_DoesNothing()
    {
        testObject.transform.position = origin;

        projectile.Advance(1f);

        Assert.That(testObject.transform.position, Is.EqualTo(origin));
        Assert.That(arrivalCount, Is.Zero);
    }

    [Test]
    public void Initialize_DuringFlight_RejectsNewTrajectoryAndDuration()
    {
        projectile.Initialize(origin, destination, 0.5f);
        projectile.Advance(0.25f);
        Vector3 halfway = testObject.transform.position;

        Assert.That(projectile.Initialize(Vector3.zero, Vector3.one, 0f), Is.False);

        Assert.That(testObject.transform.position, Is.EqualTo(halfway));
        Assert.That(arrivalCount, Is.Zero);
        projectile.Advance(0.25f);
        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    public void Initialize_AfterArrival_RejectsReuseWithoutDuplicateEvent(float duration)
    {
        projectile.Initialize(origin, destination, duration);
        projectile.Advance(0.5f);

        Assert.That(projectile.Initialize(Vector3.zero, Vector3.one, 0f), Is.False);
        projectile.Advance(1f);

        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }

    [Test]
    public void Arrived_CallbackCannotRestartOrEmitArrivalAgain()
    {
        bool reinitializationAccepted = true;
        projectile.Arrived += () =>
        {
            projectile.Advance(1f);
            reinitializationAccepted = projectile.Initialize(Vector3.zero, Vector3.one, 0f);
        };
        projectile.Initialize(origin, destination, 0.5f);

        projectile.Advance(0.5f);

        Assert.That(reinitializationAccepted, Is.False);
        Assert.That(testObject.transform.position, Is.EqualTo(destination));
        Assert.That(arrivalCount, Is.EqualTo(1));
    }
}
