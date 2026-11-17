using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class PotionFormulaSelectionTests
{
    [Test]
    public void InitialSelection_IsFireBombWithoutModifier()
    {
        var selection = new PotionFormulaSelection();
        AssertSelection(selection, PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno);
        Assert.That(selection.TryGetFormula(out var formula), Is.True);
        Assert.That(formula, Is.EqualTo(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego)));
    }

    [Test]
    public void SetBase_ChangesOnlyBase()
    {
        var selection = CreateSelection(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);
        Assert.That(selection.TrySetBase(PotionBase.Vial), Is.True);
        AssertSelection(selection, PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble);
    }

    [Test]
    public void SetEffect_ChangesOnlyEffectEvenWhenCombinationBecomesInvalid()
    {
        var selection = CreateSelection(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble);
        Assert.That(selection.TrySetEffect(PotionEffect.Savia), Is.True);
        AssertSelection(selection, PotionBase.Vial, PotionEffect.Savia, PotionModifier.Doble);
    }

    [Test]
    public void SetModifier_ChangesOnlyModifierEvenWhenCombinationBecomesInvalid()
    {
        var selection = new PotionFormulaSelection();
        Assert.That(selection.TrySetModifier(PotionModifier.Doble), Is.True);
        AssertSelection(selection, PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Doble);
    }

    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Doble)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble)]
    public void ValidCombination_RepeatedQueriesReturnExpectedFormulaWithoutChangingSelection(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var selection = CreateSelection(potionBase, effect, modifier);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.That(selection.TryGetFormula(out var formula), Is.True);
            Assert.That(formula, Is.Not.Null);
            Assert.That(formula.Base, Is.EqualTo(potionBase));
            Assert.That(formula.Effect, Is.EqualTo(effect));
            Assert.That(formula.Modifier, Is.EqualTo(modifier));
            AssertSelection(selection, potionBase, effect, modifier);
        }
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia)]
    public void InvalidCombination_RejectsWithoutExceptionOrChanges_ThenCanBeCorrected(
        PotionBase potionBase, PotionEffect effect)
    {
        var selection = CreateSelection(potionBase, effect, PotionModifier.Doble);
        // Iniciar con una formula demuestra que el rechazo tambien limpia el parametro out.
        PotionFormula formula = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.DoesNotThrow(() => Assert.That(selection.TryGetFormula(out formula), Is.False));
            Assert.That(formula, Is.Null);
            AssertSelection(selection, potionBase, effect, PotionModifier.Doble);
        }
        Assert.That(selection.TrySetModifier(PotionModifier.Ninguno), Is.True);
        Assert.That(selection.TryGetFormula(out formula), Is.True);
        Assert.That(formula.Base, Is.EqualTo(potionBase));
        Assert.That(formula.Effect, Is.EqualTo(effect));
        Assert.That(formula.Modifier, Is.EqualTo(PotionModifier.Ninguno));
    }

    [TestCase(-1, PotionEffect.Escarcha)]
    [TestCase(99, PotionEffect.Escarcha)]
    [TestCase(-1, PotionEffect.Savia)]
    [TestCase(99, PotionEffect.Savia)]
    public void UnknownEnums_RejectWithoutCorruptingValidOrInvalidSelection(int unknown, PotionEffect effect)
    {
        Assert.That(Enum.IsDefined(typeof(PotionBase), unknown), Is.False);
        Assert.That(Enum.IsDefined(typeof(PotionEffect), unknown), Is.False);
        Assert.That(Enum.IsDefined(typeof(PotionModifier), unknown), Is.False);
        var selection = CreateSelection(PotionBase.Vial, effect, PotionModifier.Doble);
        Assert.That(selection.TrySetBase((PotionBase)unknown), Is.False);
        AssertSelection(selection, PotionBase.Vial, effect, PotionModifier.Doble);
        Assert.That(selection.TrySetEffect((PotionEffect)unknown), Is.False);
        AssertSelection(selection, PotionBase.Vial, effect, PotionModifier.Doble);
        Assert.That(selection.TrySetModifier((PotionModifier)unknown), Is.False);
        AssertSelection(selection, PotionBase.Vial, effect, PotionModifier.Doble);
    }

    [Test]
    public void Selection_IsPlainDataAndNeedsNoSceneOrPlayer()
    {
        Type type = typeof(PotionFormulaSelection);
        Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
        Assert.That(type.GetConstructors(), Has.Length.EqualTo(1));
        Assert.That(type.GetConstructors()[0].GetParameters(), Is.Empty);
        foreach (FieldInfo field in type.GetFields(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Assert.That(field.FieldType.IsEnum, Is.True, field.Name);
        var selection = new PotionFormulaSelection();
        Assert.That(selection.TryGetFormula(out _), Is.True);
    }

    [Test]
    public void EditingAndQuerying_DoesNotConsumeIngredientsOrModifyEquipment()
    {
        // Solo esta prueba crea un objeto para observar recursos reales; la seleccion no lo recibe.
        var player = new GameObject("PotionFormulaSelectionTests");
        try
        {
            var inventory = player.AddComponent<PlayerIngredientInventory>();
            var equipment = player.AddComponent<EquippedPotionCharges>();
            var elements = new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };
            var quantities = new int[elements.Length];
            var charges = new int[elements.Length];
            var formulas = new PotionFormula[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.That(inventory.TryAdd(elements[i], i + 2), Is.True);
                Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Bomba, elements[i]), i + 1), Is.True);
                quantities[i] = inventory.GetQuantity(elements[i]);
                charges[i] = equipment.GetCharges(elements[i]);
                formulas[i] = equipment.GetActiveFormula(elements[i]);
            }
            var selectedElement = equipment.SelectedElement;
            var selectedFormula = equipment.SelectedFormula;
            int remainingCharges = equipment.RemainingCharges;
            var selection = new PotionFormulaSelection();
            Assert.That(selection.TryGetFormula(out _), Is.True);
            Assert.That(selection.TrySetModifier(PotionModifier.Doble), Is.True);
            Assert.That(selection.TryGetFormula(out _), Is.False);
            Assert.That(selection.TrySetBase(PotionBase.Vial), Is.True);
            Assert.That(selection.TryGetFormula(out _), Is.True);
            Assert.That(selection.TrySetEffect(PotionEffect.Escarcha), Is.True);
            Assert.That(selection.TryGetFormula(out _), Is.True);
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.That(inventory.GetQuantity(elements[i]), Is.EqualTo(quantities[i]));
                Assert.That(equipment.GetCharges(elements[i]), Is.EqualTo(charges[i]));
                Assert.That(equipment.GetActiveFormula(elements[i]), Is.SameAs(formulas[i]));
            }
            Assert.That(equipment.SelectedElement, Is.EqualTo(selectedElement));
            Assert.That(equipment.SelectedFormula, Is.SameAs(selectedFormula));
            Assert.That(equipment.RemainingCharges, Is.EqualTo(remainingCharges));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static PotionFormulaSelection CreateSelection(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var selection = new PotionFormulaSelection();
        Assert.That(selection.TrySetBase(potionBase), Is.True);
        Assert.That(selection.TrySetEffect(effect), Is.True);
        Assert.That(selection.TrySetModifier(modifier), Is.True);
        return selection;
    }

    private static void AssertSelection(PotionFormulaSelection selection,
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        Assert.That(selection.SelectedBase, Is.EqualTo(potionBase));
        Assert.That(selection.SelectedEffect, Is.EqualTo(effect));
        Assert.That(selection.SelectedModifier, Is.EqualTo(modifier));
    }
}
