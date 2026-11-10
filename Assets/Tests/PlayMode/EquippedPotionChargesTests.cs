using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EquippedPotionChargesTests
{
    private GameObject testObject;
    private EquippedPotionCharges charges;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("EquippedPotionChargesTests");
        testObject.SetActive(false);
        charges = testObject.AddComponent<EquippedPotionCharges>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObject);
    }

    [Test]
    public void Awake_DefaultStartsWithFiveTestCharges()
    {
        testObject.SetActive(true);

        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
    }

    [TestCase(0, 0)]
    [TestCase(12, 12)]
    [TestCase(-1, 0)]
    [TestCase(int.MinValue, 0)]
    public void Awake_UsesConfiguredAmount_ClampingNegativeValues(int configured, int expected)
    {
        // Simula el valor serializado del Inspector antes de ejecutar Awake.
        typeof(EquippedPotionCharges).GetField("initialCharges",
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(charges, configured);

        testObject.SetActive(true);

        Assert.That(charges.RemainingCharges, Is.EqualTo(expected));
    }

    [Test]
    public void OneInvocation_ReturnsTrue_AndConsumesExactlyOneCharge()
    {
        testObject.SetActive(true);

        bool consumed = charges.TryConsumeCharge();

        Assert.That(consumed, Is.True);
        Assert.That(charges.RemainingCharges, Is.EqualTo(4));
    }

    [Test]
    public void SuccessiveConsumption_ReachesZero_AndRepeatedRejectionNeverGoesNegative()
    {
        testObject.SetActive(true);

        for (int expected = 4; expected >= 0; expected--)
        {
            Assert.That(charges.TryConsumeCharge(), Is.True);
            Assert.That(charges.RemainingCharges, Is.EqualTo(expected));
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(charges.TryConsumeCharge(), Is.False);
            Assert.That(charges.RemainingCharges, Is.Zero);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Initialize_NonPositiveAmount_LeavesZeroAndRejectsConsumption(int amount)
    {
        testObject.SetActive(true);

        charges.Initialize(amount);

        Assert.That(charges.RemainingCharges, Is.Zero);
        Assert.That(charges.TryConsumeCharge(), Is.False);
        Assert.That(charges.RemainingCharges, Is.Zero);
    }

    [Test]
    public void Initialize_AfterConsumption_ReplacesRemainingAmount()
    {
        testObject.SetActive(true);
        Assert.That(charges.TryConsumeCharge(), Is.True);

        charges.Initialize(2);

        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(charges.TryConsumeCharge(), Is.True);
        Assert.That(charges.RemainingCharges, Is.EqualTo(1));

        charges.Initialize(0);
        charges.Initialize(7);
        Assert.That(charges.RemainingCharges, Is.EqualTo(7));
    }

    [Test]
    public void Initialize_LargeAmount_DoesNotApplyAGameplayCap()
    {
        testObject.SetActive(true);

        charges.Initialize(int.MaxValue);

        Assert.That(charges.RemainingCharges, Is.EqualTo(int.MaxValue));
        Assert.That(charges.TryConsumeCharge(), Is.True);
        Assert.That(charges.RemainingCharges, Is.EqualTo(int.MaxValue - 1));
    }

    [Test]
    public void Reactivation_DoesNotRefillCharges()
    {
        testObject.SetActive(true);
        Assert.That(charges.TryConsumeCharge(), Is.True);

        charges.enabled = false;
        charges.enabled = true;
        testObject.SetActive(false);
        testObject.SetActive(true);

        Assert.That(charges.RemainingCharges, Is.EqualTo(4));
    }
}
