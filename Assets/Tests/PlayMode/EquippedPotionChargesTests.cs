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

    [Test]
    public void Selection_Awake_SelectsFireWithFiveCharges()
    {
        AssertSelection(null);
        testObject.SetActive(true);
        AssertSelection(PotionEffect.Fuego);
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(5));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Selection_Empty_ReturnsNullAndCannotAdvance(int amount)
    {
        testObject.SetActive(true);
        charges.Initialize(amount);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(charges.TrySelectNext(), Is.False);
            AssertSelection(null);
        }
    }

    [Test]
    public void Selection_AllAvailable_CompletesCircularOrder()
    {
        PopulateSlots();
        AssertSelection(PotionEffect.Fuego);
        foreach (var expected in new[] { PotionEffect.Escarcha, PotionEffect.Savia, PotionEffect.Fuego })
        {
            Assert.That(charges.TrySelectNext(), Is.True);
            AssertSelection(expected);
            AssertPopulated(PotionEffect.Fuego, 3);
        }
    }

    [Test]
    public void Selection_FiveFireAndThreeSap_SkipsEmptyFrost()
    {
        testObject.SetActive(true);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 3), Is.True);
        AssertSelection(PotionEffect.Fuego);
        Assert.That(charges.TrySelectNext(), Is.True);
        AssertSelection(PotionEffect.Savia);
        Assert.That(charges.TrySelectNext(), Is.True);
        AssertSelection(PotionEffect.Fuego);
        AssertSlot(PotionEffect.Fuego, new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego), 5);
        AssertSlot(PotionEffect.Escarcha, null, 0);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Selection_OnlyOneAvailable_SelectsFirstAdditionAndStays(PotionEffect element)
    {
        testObject.SetActive(true);
        charges.Initialize(0);
        var formula = new PotionFormula(PotionBase.Vial, element);
        Assert.That(charges.TryAdd(formula, 2), Is.True);
        AssertSelection(element);
        for (int i = 0; i < 4; i++)
        {
            Assert.That(charges.TrySelectNext(), Is.True);
            AssertSelection(element);
            Assert.That(charges.SelectedFormula, Is.SameAs(formula));
            Assert.That(charges.GetCharges(element), Is.EqualTo(2));
        }
    }

    [Test]
    public void Selection_LastSelectedCharge_AdvancesToFrostThenSapThenNull()
    {
        testObject.SetActive(true);
        charges.Initialize(1);
        var frost = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha);
        var sap = new PotionFormula(PotionBase.Bomba, PotionEffect.Savia);
        Assert.That(charges.TryAdd(frost, 2), Is.True);
        Assert.That(charges.TryAdd(sap, 1), Is.True);
        Assert.That(charges.TryConsumeCharge(), Is.True);
        AssertSelection(PotionEffect.Escarcha);
        Assert.That(charges.TryConsume(frost), Is.True);
        AssertSelection(PotionEffect.Escarcha);
        Assert.That(charges.TryConsume(frost), Is.True);
        AssertSelection(PotionEffect.Savia);
        Assert.That(charges.TryConsume(sap), Is.True);
        AssertSelection(null);
        Assert.That(charges.TrySelectNext(), Is.False);
    }

    [Test]
    public void Selection_ExhaustingUnselectedReserve_PreservesSelection()
    {
        PopulateSlots();
        var sap = new PotionFormula(PotionBase.Bomba, PotionEffect.Savia);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(charges.TryConsume(sap), Is.True);
            AssertSelection(PotionEffect.Fuego);
        }
        AssertSlot(PotionEffect.Savia, null, 0);
    }

    [TestCase(PotionEffect.Fuego, PotionEffect.Savia)]
    [TestCase(PotionEffect.Escarcha, PotionEffect.Fuego)]
    [TestCase(PotionEffect.Savia, PotionEffect.Fuego)]
    [TestCase(PotionEffect.Savia, PotionEffect.Escarcha)]
    public void Selection_Exhaustion_SkipsEmptyReserveAndWraps(PotionEffect selected, PotionEffect next)
    {
        testObject.SetActive(true);
        charges.Initialize(0);
        var formula = new PotionFormula(PotionBase.Bomba, selected);
        Assert.That(charges.TryAdd(formula, 1), Is.True);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, next), 1), Is.True);
        AssertSelection(selected);
        Assert.That(charges.TryConsume(formula), Is.True);
        AssertSelection(next);
        Assert.That(charges.TryConsume(formula), Is.False);
        AssertSelection(next);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Selection_AfterTotalExhaustion_AdditionRestoresSelection(PotionEffect element)
    {
        testObject.SetActive(true);
        charges.Initialize(1);
        Assert.That(charges.TryConsumeCharge(), Is.True);
        AssertSelection(null);
        var formula = new PotionFormula(PotionBase.Vial, element);
        Assert.That(charges.TryAdd(formula, 1), Is.True);
        AssertSelection(element);
        Assert.That(charges.SelectedFormula, Is.SameAs(formula));
    }

    [Test]
    public void Selection_AddingFire_PreservesSelectedFrost()
    {
        PopulateSlots();
        Assert.That(charges.TrySelectNext(), Is.True);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego), 2), Is.True);
        AssertSelection(PotionEffect.Escarcha);
        AssertPopulated(PotionEffect.Fuego, 5);
    }

    [Test]
    public void Selection_RejectedOperations_PreserveSelectionAndReserves()
    {
        PopulateSlots();
        Assert.That(charges.TrySelectNext(), Is.True);
        foreach (var element in new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia })
        {
            var formula = new PotionFormula(PotionBase.Bomba, element);
            foreach (int amount in new[] { 0, -1, int.MinValue, int.MaxValue })
            {
                Assert.That(charges.TryAdd(formula, amount), Is.False);
                AssertSelection(PotionEffect.Escarcha);
            }
            var incompatible = new PotionFormula(PotionBase.Vial, element);
            Assert.That(charges.TryAdd(incompatible, 1), Is.False);
            AssertSelection(PotionEffect.Escarcha);
            Assert.That(charges.TryConsume(incompatible), Is.False);
            AssertSelection(PotionEffect.Escarcha);
        }
        var differentModifier = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);
        Assert.That(charges.TryAdd(differentModifier, 1), Is.False);
        AssertSelection(PotionEffect.Escarcha);
        Assert.That(charges.TryConsume(differentModifier), Is.False);
        AssertSelection(PotionEffect.Escarcha);
        Assert.That(charges.TryAdd(null, 1), Is.False);
        Assert.That(charges.TryConsume(null), Is.False);
        AssertSelection(PotionEffect.Escarcha);
        AssertPopulated(PotionEffect.Fuego, 3);
    }

    [Test]
    public void Selection_RejectionsWhileEmpty_KeepNull()
    {
        charges.Initialize(0);
        Assert.That(charges.TryConsumeCharge(), Is.False);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 0), Is.False);
        Assert.That(charges.TryAdd(null, 1), Is.False);
        Assert.That(charges.TryConsume(null), Is.False);
        AssertSelection(null);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(7)]
    public void Selection_Initialize_PreservesAvailableFrostAndOtherReserves(int amount)
    {
        PopulateSlots();
        Assert.That(charges.TrySelectNext(), Is.True);
        charges.Initialize(amount);
        AssertSelection(PotionEffect.Escarcha);
        AssertPopulated(PotionEffect.Fuego, amount > 0 ? amount : 0);
        Assert.That(charges.RemainingCharges, Is.EqualTo(amount > 0 ? amount : 0));
    }

    [Test]
    public void Selection_Initialize_ReplacesSelectedFormulaAndFindsDeterministicFallback()
    {
        testObject.SetActive(true);
        charges.Initialize(0);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Vial, PotionEffect.Fuego), 2), Is.True);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 3), Is.True);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 3), Is.True);
        charges.Initialize(7);
        AssertSelection(PotionEffect.Fuego);
        Assert.That(charges.SelectedFormula, Is.EqualTo(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego)));
        AssertPopulated(PotionEffect.Fuego, 7);
        charges.Initialize(0);
        AssertSelection(PotionEffect.Escarcha);
        AssertPopulated(PotionEffect.Fuego, 0);
    }

    [Test]
    public void Selection_Initialize_WhenEmpty_RestoresFire()
    {
        charges.Initialize(0);
        AssertSelection(null);
        charges.Initialize(2);
        AssertSelection(PotionEffect.Fuego);
        testObject.SetActive(true);
        AssertSelection(PotionEffect.Fuego);
        Assert.That(charges.RemainingCharges, Is.EqualTo(5));
    }

    [Test]
    public void Selection_Reactivation_PreservesSelectedSapAndAllReserves()
    {
        PopulateSlots();
        Assert.That(charges.TrySelectNext(), Is.True);
        Assert.That(charges.TrySelectNext(), Is.True);
        Assert.That(charges.TryConsume(charges.SelectedFormula), Is.True);
        charges.enabled = false;
        charges.enabled = true;
        AssertSelection(PotionEffect.Savia);
        AssertPopulated(PotionEffect.Savia, 2);
        testObject.SetActive(false);
        testObject.SetActive(true);
        AssertSelection(PotionEffect.Savia);
        AssertPopulated(PotionEffect.Savia, 2);
    }

    [Test]
    public void Selection_LegacyApi_StillConsumesOnlyFireWhileFrostSelected()
    {
        PopulateSlots();
        Assert.That(charges.TrySelectNext(), Is.True);
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            Assert.That(charges.TryConsumeCharge(), Is.True);
            Assert.That(charges.RemainingCharges, Is.EqualTo(remaining));
            AssertSelection(PotionEffect.Escarcha);
            AssertPopulated(PotionEffect.Fuego, remaining);
        }
        Assert.That(charges.TryConsumeCharge(), Is.False);
        AssertSelection(PotionEffect.Escarcha);
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Vial, PotionEffect.Fuego), 2), Is.True);
        Assert.That(charges.RemainingCharges, Is.Zero);
        Assert.That(charges.TryConsumeCharge(), Is.False);
        AssertSelection(PotionEffect.Escarcha);
        AssertSlot(PotionEffect.Fuego, new PotionFormula(PotionBase.Vial, PotionEffect.Fuego), 2);
        AssertSlot(PotionEffect.Escarcha, new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha), 3);
        AssertSlot(PotionEffect.Savia, new PotionFormula(PotionBase.Bomba, PotionEffect.Savia), 3);
    }

    [Test]
    public void Selection_RepeatedCycles_KeepFormulaCountsAndOrderConsistent()
    {
        testObject.SetActive(true);
        charges.Initialize(0);
        var order = new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };
        for (int cycle = 0; cycle < 4; cycle++)
        {
            foreach (var element in order)
                Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Vial, element), 2), Is.True);
            AssertSelection(PotionEffect.Fuego);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(charges.TryConsume(charges.SelectedFormula), Is.True);
                AssertSelection(order[i]);
                Assert.That(charges.GetCharges(order[i]), Is.EqualTo(1));
                Assert.That(charges.TrySelectNext(), Is.True);
                AssertSelection(order[(i + 1) % 3]);
            }
            for (int i = 0; i < 3; i++)
            {
                Assert.That(charges.TryConsume(charges.SelectedFormula), Is.True);
                AssertSlot(order[i], null, 0);
                AssertSelection(i < 2 ? (PotionEffect?)order[i + 1] : null);
            }
            Assert.That(charges.TrySelectNext(), Is.False);
            Assert.That(charges.TryConsumeCharge(), Is.False);
            AssertSelection(null);
        }
    }

    private void AssertSelection(PotionEffect? expected)
    {
        Assert.That(charges.SelectedElement, Is.EqualTo(expected));
        if (!expected.HasValue)
        {
            Assert.That(charges.SelectedFormula, Is.Null);
            return;
        }
        Assert.That(charges.GetCharges(expected.Value), Is.GreaterThan(0));
        Assert.That(charges.SelectedFormula, Is.Not.Null);
        Assert.That(charges.SelectedFormula, Is.SameAs(charges.GetActiveFormula(expected.Value)));
        Assert.That(charges.SelectedFormula.Effect, Is.EqualTo(expected.Value));
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
