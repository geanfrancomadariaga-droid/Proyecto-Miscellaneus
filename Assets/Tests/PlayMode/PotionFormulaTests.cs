using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class PotionFormulaTests
{
    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble)]
    public void OfficialRecipe_CreatesFormulaWithRequestedComponents(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var formula = new PotionFormula(potionBase, effect, modifier);
        Assert.That(formula.Base, Is.EqualTo(potionBase));
        Assert.That(formula.Effect, Is.EqualTo(effect));
        Assert.That(formula.Modifier, Is.EqualTo(modifier));
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia)]
    public void ForbiddenDouble_RejectsCombination(PotionBase potionBase, PotionEffect effect)
    {
        Assert.Throws<ArgumentException>(() =>
            new PotionFormula(potionBase, effect, PotionModifier.Doble));
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void UnknownIdentifiers_AreRejected(int unknown)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PotionFormula((PotionBase)unknown, PotionEffect.Fuego));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PotionFormula(PotionBase.Vial, (PotionEffect)unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PotionFormula(PotionBase.Vial, PotionEffect.Fuego, (PotionModifier)unknown));
    }

    [Test]
    public void OmittedModifier_IsNone()
    {
        var formula = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
        Assert.That(formula.Modifier, Is.EqualTo(PotionModifier.Ninguno));
    }

    [Test]
    public void EqualComponents_HaveValueEqualityAndEqualHashes()
    {
        var first = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble);
        var second = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble);
        var third = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble);

        Assert.That(ReferenceEquals(first, second), Is.False);
        Assert.That(first.Equals(first), Is.True);
        Assert.That(first.Equals(second) && second.Equals(first), Is.True);
        Assert.That(second.Equals(third) && first.Equals(third), Is.True);
        Assert.That(first.Equals((object)second), Is.True);
        Assert.That(first == second, Is.True);
        Assert.That(first != second, Is.False);
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        Assert.That(new HashSet<PotionFormula> { first, second }, Has.Count.EqualTo(1));
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble)]
    public void ChangingOneComponent_ChangesFormula(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var original = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego);
        var changed = new PotionFormula(potionBase, effect, modifier);
        Assert.That(original.Equals(changed), Is.False);
        Assert.That(original.Equals((object)changed), Is.False);
        Assert.That(original == changed, Is.False);
        Assert.That(original != changed, Is.True);
    }

    [Test]
    public void Equality_HandlesNullAndUnrelatedObjects()
    {
        var formula = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
        PotionFormula missing = null;
        Assert.That(formula.Equals(missing), Is.False);
        Assert.That(formula.Equals((object)null), Is.False);
        Assert.That(formula.Equals("Bomba + Fuego"), Is.False);
        Assert.That(formula == missing, Is.False);
        Assert.That(missing == formula, Is.False);
        Assert.That(missing == (PotionFormula)null, Is.True);
        Assert.That(formula != missing, Is.True);
    }

    [Test]
    public void Components_AreReadOnlyAndClassCannotBeExtended()
    {
        Type type = typeof(PotionFormula);
        Assert.That(type.IsSealed, Is.True);
        foreach (string name in new[] { "Base", "Effect", "Modifier" })
        {
            PropertyInfo property = type.GetProperty(name);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.GetSetMethod(true), Is.Null);
        }
        foreach (FieldInfo field in type.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Assert.That(field.IsPrivate && field.IsInitOnly, Is.True, field.Name);
            Assert.That(field.FieldType.IsEnum, Is.True, field.Name);
        }
    }

    [Test]
    public void Model_UsesOnlyPlainDataWithoutSceneOrInputObjects()
    {
        Assert.That(typeof(PotionFormula).BaseType, Is.EqualTo(typeof(object)));
        var constructors = typeof(PotionFormula).GetConstructors();
        Assert.That(constructors, Has.Length.EqualTo(1));
        foreach (ParameterInfo parameter in constructors[0].GetParameters())
            Assert.That(parameter.ParameterType.IsEnum, Is.True);

        // Construccion y uso sin GameObject, escena, dispositivo ni inicializacion de Unity.
        var formula = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
        Assert.That(formula.Equals(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego)), Is.True);
    }
}
