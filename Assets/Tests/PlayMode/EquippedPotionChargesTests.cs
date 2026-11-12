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

    [Test]
    public void Awake_ConfiguredFive_InitializesOnlyFireBomb()
    {
        typeof(EquippedPotionCharges).GetField("initialCharges",
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(charges, 5);
        testObject.SetActive(true);

        AssertSlot(PotionEffect.Fuego, new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego), 5);
        AssertSlot(PotionEffect.Escarcha, null, 0);
        AssertSlot(PotionEffect.Savia, null, 0);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void AddAndExplicitConsume_AffectOnlySelectedElement(PotionEffect element)
    {
        PopulateSlots();
        var formula = new PotionFormula(PotionBase.Bomba, element);
        Assert.That(charges.TryAdd(formula, 2), Is.True);
        AssertPopulated(element, 5);
        Assert.That(charges.TryConsume(new PotionFormula(PotionBase.Bomba, element)), Is.True);
        AssertPopulated(element, 4);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void IncompatibleFormula_CannotAddOrConsume(PotionEffect element)
    {
        PopulateSlots();
        var incompatible = new PotionFormula(PotionBase.Vial, element);
        Assert.That(charges.TryAdd(incompatible, 2), Is.False);
        AssertPopulated(element, 3);
        Assert.That(charges.TryConsume(incompatible), Is.False);
        AssertPopulated(element, 3);
    }

    [Test]
    public void DifferentModifier_CannotAddOrConsume()
    {
        PopulateSlots();
        var different = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);
        Assert.That(charges.TryAdd(different, 1), Is.False);
        Assert.That(charges.TryConsume(different), Is.False);
        AssertPopulated(PotionEffect.Escarcha, 3);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Exhaustion_ClearsFormulaAndAllowsReplacement(PotionEffect element)
    {
        PopulateSlots();
        var original = new PotionFormula(PotionBase.Bomba, element);
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            Assert.That(charges.TryConsume(original), Is.True);
            AssertPopulated(element, remaining);
        }
        Assert.That(charges.TryConsume(original), Is.False);
        AssertPopulated(element, 0);
        var replacement = new PotionFormula(PotionBase.Vial, element);
        Assert.That(charges.TryAdd(replacement, 2), Is.True);
        AssertSlot(element, replacement, 2);
        Assert.That(charges.TryConsume(original), Is.False);
        AssertSlot(element, replacement, 2);
    }

    [Test]
    public void LegacyConsumption_OnlyConsumesFireBomb()
    {
        PopulateSlots();
        Assert.That(charges.TryConsumeCharge(), Is.True);
        AssertPopulated(PotionEffect.Fuego, 2);
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(charges.TryConsumeCharge(), Is.True);
        Assert.That(charges.TryConsumeCharge(), Is.True);
        var vial = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble);
        Assert.That(charges.TryAdd(vial, 4), Is.True);
        Assert.That(charges.RemainingCharges, Is.Zero);
        Assert.That(charges.TryConsumeCharge(), Is.False);
        AssertSlot(PotionEffect.Fuego, vial, 4);
        AssertSlot(PotionEffect.Escarcha, new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 3);
        AssertSlot(PotionEffect.Savia, new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 3);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    [TestCase(7)]
    [TestCase(int.MaxValue)]
    public void Initialize_ReplacesIncompatibleFireAndPreservesOtherElements(int amount)
    {
        PopulateSlots();
        charges.Initialize(0);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Vial, PotionEffect.Fuego), 2), Is.True);
        charges.Initialize(amount);
        int expected = amount > 0 ? amount : 0;
        AssertPopulated(PotionEffect.Fuego, expected);
        Assert.That(charges.RemainingCharges, Is.EqualTo(expected));
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void InvalidAmountsAndOverflow_PreserveAllSlots(PotionEffect element)
    {
        PopulateSlots();
        var formula = new PotionFormula(PotionBase.Bomba, element);
        foreach (int amount in new[] { 0, -1, int.MinValue, int.MaxValue })
        {
            Assert.That(charges.TryAdd(formula, amount), Is.False);
            AssertPopulated(element, 3);
        }
        Assert.That(charges.TryAdd(formula, int.MaxValue - 3), Is.True);
        AssertPopulated(element, int.MaxValue);
        Assert.That(charges.TryAdd(formula, 1), Is.False);
        AssertPopulated(element, int.MaxValue);
        Assert.That(charges.TryConsume(formula), Is.True);
        AssertPopulated(element, int.MaxValue - 1);
    }

    [Test]
    public void NullFormulaAndInvalidElements_PreserveAllSlots()
    {
        PopulateSlots();
        Assert.That(charges.TryAdd(null, 1), Is.False);
        Assert.That(charges.TryConsume(null), Is.False);
        foreach (int element in new[] { -1, 3, int.MinValue, int.MaxValue })
        {
            Assert.That(charges.GetCharges((PotionEffect)element), Is.Zero);
            Assert.That(charges.GetActiveFormula((PotionEffect)element), Is.Null);
        }
        AssertPopulated(PotionEffect.Fuego, 3);
    }

    [Test]
    public void Reactivation_PreservesAllFormulasAndCharges()
    {
        PopulateSlots();
        Assert.That(charges.TryConsume(new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha)), Is.True);
        charges.enabled = false;
        charges.enabled = true;
        testObject.SetActive(false);
        testObject.SetActive(true);
        AssertPopulated(PotionEffect.Escarcha, 2);
    }

    [Test]
    public void BeforeAwake_IsEmpty_AndAwakeStillAppliesConfiguredAmount()
    {
        Assert.That(charges.RemainingCharges, Is.Zero);
        Assert.That(charges.TryConsumeCharge(), Is.False);
        charges.Initialize(2);
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        testObject.SetActive(true);
        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
    }

    private void PopulateSlots()
    {
        testObject.SetActive(true);
        charges.Initialize(3);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 3), Is.True);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 3), Is.True);
    }

    private void AssertPopulated(PotionEffect changed, int expected)
    {
        foreach (PotionEffect element in new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia })
        {
            int count = element == changed ? expected : 3;
            AssertSlot(element, count == 0 ? null : new PotionFormula(PotionBase.Bomba, element), count);
        }
    }

    private void AssertSlot(PotionEffect element, PotionFormula formula, int expected)
    {
        Assert.That(charges.GetCharges(element), Is.EqualTo(expected), element.ToString());
        Assert.That(charges.GetActiveFormula(element), Is.EqualTo(formula), element.ToString());
    }
}
