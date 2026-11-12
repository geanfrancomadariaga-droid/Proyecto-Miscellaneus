using System;
using System.Reflection;
using NUnit.Framework;

public class ElementPotionSlotTests
{
    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void NewSlot_IsEmptyAndAcceptsItsElement(PotionEffect element)
    {
        var slot = new ElementPotionSlot(element);
        AssertState(slot, element, null, 0);

        var formula = new PotionFormula(PotionBase.Vial, element);
        Assert.That(slot.TryAdd(formula, 3), Is.True);
        AssertState(slot, element, formula, 3);
    }

    [Test]
    public void AddingIdenticalFormula_AccumulatesCharges()
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        var formula = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        Assert.That(slot.TryAdd(formula, 2), Is.True);
        Assert.That(slot.TryAdd(formula, 3), Is.True);
        AssertState(slot, PotionEffect.Fuego, formula, 5);
    }

    [Test]
    public void SeparateEqualFormulas_CanAccumulateAndConsume()
    {
        var slot = new ElementPotionSlot(PotionEffect.Escarcha);
        var first = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);
        var second = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);
        Assert.That(ReferenceEquals(first, second), Is.False);
        Assert.That(slot.TryAdd(first, 2), Is.True);
        Assert.That(slot.TryAdd(second, 3), Is.True);
        Assert.That(slot.Charges, Is.EqualTo(5));
        Assert.That(slot.ActiveFormula.Equals(first), Is.True);
        Assert.That(slot.TryConsume(first), Is.True);
        Assert.That(slot.Charges, Is.EqualTo(4));
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia, PotionModifier.Ninguno)]
    public void DifferentFormula_CannotAddOrConsumeAndPreservesState(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        var active = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        var different = new PotionFormula(potionBase, effect, modifier);
        Assert.That(slot.TryAdd(active, 2), Is.True);
        Assert.That(slot.TryAdd(different, 3), Is.False);
        AssertState(slot, PotionEffect.Fuego, active, 2);
        Assert.That(slot.TryConsume(different), Is.False);
        AssertState(slot, PotionEffect.Fuego, active, 2);
    }

    [TestCase(PotionEffect.Fuego, PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Fuego, PotionEffect.Savia)]
    [TestCase(PotionEffect.Escarcha, PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha, PotionEffect.Savia)]
    [TestCase(PotionEffect.Savia, PotionEffect.Fuego)]
    [TestCase(PotionEffect.Savia, PotionEffect.Escarcha)]
    public void EmptySlot_RejectsOtherElements(PotionEffect element, PotionEffect other)
    {
        var slot = new ElementPotionSlot(element);
        Assert.That(slot.TryAdd(new PotionFormula(PotionBase.Vial, other), 1), Is.False);
        AssertState(slot, element, null, 0);
    }

    [TestCase(0, 0)]
    [TestCase(0, -1)]
    [TestCase(0, int.MinValue)]
    [TestCase(2, 0)]
    [TestCase(2, -1)]
    [TestCase(2, int.MinValue)]
    public void NonPositiveAmount_IsRejectedWithoutChanges(int initialCharges, int amount)
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        var formula = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        if (initialCharges > 0)
            Assert.That(slot.TryAdd(formula, initialCharges), Is.True);
        var previous = slot.ActiveFormula;
        Assert.That(slot.TryAdd(formula, amount), Is.False);
        AssertState(slot, PotionEffect.Fuego, previous, initialCharges);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void NullFormula_CannotAddOrConsume(int initialCharges)
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        if (initialCharges > 0)
            Assert.That(slot.TryAdd(new PotionFormula(PotionBase.Vial, PotionEffect.Fuego), initialCharges), Is.True);
        var previous = slot.ActiveFormula;
        Assert.That(slot.TryAdd(null, 1), Is.False);
        AssertState(slot, PotionEffect.Fuego, previous, initialCharges);
        Assert.That(slot.TryConsume(null), Is.False);
        AssertState(slot, PotionEffect.Fuego, previous, initialCharges);
    }

    [Test]
    public void Consumption_RemovesExactlyOneAndNeverGoesBelowZero()
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        var formula = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        Assert.That(slot.TryConsume(formula), Is.False);
        AssertState(slot, PotionEffect.Fuego, null, 0);
        Assert.That(slot.TryAdd(formula, 3), Is.True);
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            Assert.That(slot.TryConsume(formula), Is.True);
            AssertState(slot, PotionEffect.Fuego, remaining == 0 ? null : formula, remaining);
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(slot.TryConsume(formula), Is.False);
            AssertState(slot, PotionEffect.Fuego, null, 0);
        }
    }

    [Test]
    public void ExhaustedSlot_AcceptsDifferentFormulaOfSameElement()
    {
        var slot = new ElementPotionSlot(PotionEffect.Fuego);
        var first = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        var next = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
        Assert.That(slot.TryAdd(first, 1), Is.True);
        Assert.That(slot.TryConsume(first), Is.True);
        AssertState(slot, PotionEffect.Fuego, null, 0);
        Assert.That(slot.TryAdd(next, 2), Is.True);
        AssertState(slot, PotionEffect.Fuego, next, 2);
        Assert.That(slot.TryConsume(first), Is.False);
        AssertState(slot, PotionEffect.Fuego, next, 2);
    }

    [Test]
    public void IntLimit_IsAcceptedButOverflowPreservesState()
    {
        var slot = new ElementPotionSlot(PotionEffect.Savia);
        var formula = new PotionFormula(PotionBase.Vial, PotionEffect.Savia);
        Assert.That(slot.TryAdd(formula, int.MaxValue - 1), Is.True);
        Assert.That(slot.TryAdd(formula, 2), Is.False);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue - 1);
        Assert.That(slot.TryAdd(formula, 1), Is.True);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue);
        Assert.That(slot.TryAdd(formula, 1), Is.False);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue);
        Assert.That(slot.TryAdd(formula, int.MaxValue), Is.False);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue);
        Assert.That(slot.TryConsume(formula), Is.True);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue - 1);
    }

    [Test]
    public void EmptySlot_AcceptsIntMaxValue()
    {
        var slot = new ElementPotionSlot(PotionEffect.Savia);
        var formula = new PotionFormula(PotionBase.Vial, PotionEffect.Savia);
        Assert.That(slot.TryAdd(formula, int.MaxValue), Is.True);
        AssertState(slot, PotionEffect.Savia, formula, int.MaxValue);
    }

    [TestCase(-1)]
    [TestCase(3)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void UnknownElement_IsRejected(int element)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ElementPotionSlot((PotionEffect)element));
    }

    [Test]
    public void PublicApi_CannotSetStateAndUsesNoSceneComponent()
    {
        Type type = typeof(ElementPotionSlot);
        Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
        Assert.That(type.IsSealed, Is.True);
        foreach (string name in new[] { "Element", "ActiveFormula", "Charges" })
        {
            PropertyInfo property = type.GetProperty(name);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetGetMethod(), Is.Not.Null);
            Assert.That(property.GetSetMethod(), Is.Null, name);
        }
        Assert.That(type.GetProperty("Element").GetSetMethod(true), Is.Null);
        Assert.That(type.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
    }

    private static void AssertState(ElementPotionSlot slot, PotionEffect element,
        PotionFormula formula, int charges)
    {
        Assert.That(slot.Element, Is.EqualTo(element));
        Assert.That(slot.ActiveFormula, Is.SameAs(formula));
        Assert.That(slot.Charges, Is.EqualTo(charges));
        Assert.That(slot.Charges, Is.GreaterThanOrEqualTo(0));
    }
}
