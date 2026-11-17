using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class PotionCraftingServiceTests
{
    private static readonly PotionEffect[] Elements =
        { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };

    private GameObject player;
    private PlayerIngredientInventory inventory;
    private EquippedPotionCharges equipment;
    private PotionCraftingService service;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("PotionCraftingServiceTests");
        inventory = player.AddComponent<PlayerIngredientInventory>();
        equipment = player.AddComponent<EquippedPotionCharges>();
        // Awake ya se ejecuto; solo se vacia el equipamiento de este objeto de prueba.
        equipment.Initialize(0);
        service = new PotionCraftingService();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
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
    public void ValidRecipes_ConsumeOneAndAccumulateBeyondThree(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        PopulateIngredients(6);
        foreach (PotionEffect other in Elements)
            if (other != effect)
                Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Bomba, other), 2), Is.True);
        var before = Capture();

        for (int count = 1; count <= 5; count++)
        {
            // Instancias diferentes con igualdad por valor tambien deben acumular.
            var formula = new PotionFormula(potionBase, effect, modifier);
            AssertCraft(formula, PotionCraftingResult.Success);
            foreach (PotionEffect element in Elements)
            {
                int index = (int)element;
                Assert.That(inventory.GetQuantity(element), Is.EqualTo(element == effect ? 6 - count : 6));
                Assert.That(equipment.GetCharges(element), Is.EqualTo(element == effect ? count : 2));
                if (element == effect)
                    Assert.That(equipment.GetActiveFormula(element), Is.EqualTo(formula));
                else
                    Assert.That(equipment.GetActiveFormula(element), Is.SameAs(before.Formulas[index]));
            }
            Assert.That(equipment.SelectedElement, Is.EqualTo(before.SelectedElement));
            Assert.That(equipment.SelectedFormula, Is.SameAs(before.SelectedFormula));
        }
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia)]
    public void ForbiddenDouble_ConstructorRejectsBeforeCraftingWithoutChanges(
        PotionBase potionBase, PotionEffect effect)
    {
        Populate();
        var before = Capture();
        // No es posible entregar esta combinacion al servicio por la API publica.
        Assert.Throws<ArgumentException>(() =>
            new PotionFormula(potionBase, effect, PotionModifier.Doble));
        AssertUnchanged(before);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void MissingIngredient_RepeatedRejectionsPreserveEntireState(PotionEffect effect)
    {
        Populate();
        Assert.That(inventory.TryConsume(effect, 4), Is.True);
        var before = Capture();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            AssertCraft(new PotionFormula(PotionBase.Bomba, effect), PotionCraftingResult.InsufficientIngredients);
            AssertUnchanged(before);
        }
    }

    [Test]
    public void NullFormula_IsInvalidAndPreservesEntireState()
    {
        Populate();
        var before = Capture();
        AssertCraft(null, PotionCraftingResult.InvalidFormula);
        AssertUnchanged(before);
    }

    [TestCase(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia, PotionModifier.Ninguno)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble)]
    public void DifferentBaseOrModifier_RejectsWithoutChanges(
        PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        Populate();
        var before = Capture();
        AssertCraft(new PotionFormula(potionBase, effect, modifier), PotionCraftingResult.IncompatibleFormula);
        AssertUnchanged(before);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void ExhaustedElement_AcceptsNewFormulaAndPreservesOtherReserves(PotionEffect effect)
    {
        Populate();
        var oldFormula = equipment.GetActiveFormula(effect);
        Assert.That(equipment.TryConsume(oldFormula), Is.True);
        Assert.That(equipment.TryConsume(oldFormula), Is.True);
        Assert.That(equipment.GetActiveFormula(effect), Is.Null);
        var before = Capture();
        var replacement = new PotionFormula(PotionBase.Vial, effect);

        AssertCraft(replacement, PotionCraftingResult.Success);
        Assert.That(inventory.GetQuantity(effect), Is.EqualTo(3));
        Assert.That(equipment.GetCharges(effect), Is.EqualTo(1));
        Assert.That(equipment.GetActiveFormula(effect), Is.SameAs(replacement));
        AssertOtherElementsUnchanged(before, effect);
        Assert.That(equipment.SelectedElement, Is.EqualTo(before.SelectedElement));
        Assert.That(equipment.SelectedFormula, Is.SameAs(before.SelectedFormula));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void MissingDependencies_RejectWithoutChanges(bool missingInventory, bool missingEquipment)
    {
        Populate();
        var before = Capture();
        Assert.That(service.TryCraft(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego),
            missingInventory ? null : inventory, missingEquipment ? null : equipment, out var result), Is.False);
        Assert.That(result, Is.EqualTo(PotionCraftingResult.MissingDependency));
        AssertUnchanged(before);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DestroyedUnityDependency_IsRejected(bool destroyInventory)
    {
        Populate();
        var before = Capture();
        var disposable = new GameObject("DestroyedCraftingDependency");
        var lostInventory = disposable.AddComponent<PlayerIngredientInventory>();
        var lostEquipment = disposable.AddComponent<EquippedPotionCharges>();
        Object.DestroyImmediate(disposable);
        Assert.That(service.TryCraft(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego),
            destroyInventory ? lostInventory : inventory,
            destroyInventory ? equipment : lostEquipment, out var result), Is.False);
        Assert.That(result, Is.EqualTo(PotionCraftingResult.MissingDependency));
        AssertUnchanged(before);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void IntegerBoundary_SucceedsThenRealTryAddRejectionRestoresIngredient(PotionEffect effect)
    {
        Populate();
        var formula = equipment.GetActiveFormula(effect);
        Assert.That(equipment.TryAdd(formula, int.MaxValue - 3), Is.True);
        Assert.That(inventory.TryAdd(effect, int.MaxValue - 4), Is.True);
        var beforeSuccess = Capture();
        AssertCraft(formula, PotionCraftingResult.Success);
        Assert.That(equipment.GetCharges(effect), Is.EqualTo(int.MaxValue));
        Assert.That(inventory.GetQuantity(effect), Is.EqualTo(int.MaxValue - 1));
        AssertOtherElementsUnchanged(beforeSuccess, effect);
        Assert.That(equipment.SelectedElement, Is.EqualTo(beforeSuccess.SelectedElement));

        // Partir del maximo tambien verifica que la devolucion no se desborda.
        Assert.That(inventory.TryAdd(effect, 1), Is.True);
        var beforeFailure = Capture();
        Assert.That(equipment.TryAdd(formula, 1), Is.False);
        AssertUnchanged(beforeFailure);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            AssertCraft(formula, PotionCraftingResult.ChargeAdditionRejected);
            AssertUnchanged(beforeFailure);
        }
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void FirstCraft_UsesExistingAutomaticSelection(PotionEffect effect)
    {
        Assert.That(equipment.SelectedElement, Is.Null);
        Assert.That(inventory.TryAdd(effect, 1), Is.True);
        var formula = new PotionFormula(PotionBase.Bomba, effect);
        AssertCraft(formula, PotionCraftingResult.Success);
        Assert.That(inventory.GetQuantity(effect), Is.Zero);
        Assert.That(equipment.GetCharges(effect), Is.EqualTo(1));
        Assert.That(equipment.SelectedElement, Is.EqualTo(effect));
        Assert.That(equipment.SelectedFormula, Is.SameAs(formula));
        var before = Capture();
        AssertCraft(formula, PotionCraftingResult.InsufficientIngredients);
        AssertUnchanged(before);
    }

    [Test]
    public void DefaultFiveCharges_CraftingAddsOneWithoutReinitializing()
    {
        var other = new GameObject("DefaultCraftingEquipment");
        try
        {
            var defaultEquipment = other.AddComponent<EquippedPotionCharges>();
            Assert.That(defaultEquipment.RemainingCharges, Is.EqualTo(5));
            Assert.That(inventory.TryAdd(PotionEffect.Fuego, 1), Is.True);
            Assert.That(service.TryCraft(new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego),
                inventory, defaultEquipment, out var result), Is.True);
            Assert.That(result, Is.EqualTo(PotionCraftingResult.Success));
            Assert.That(defaultEquipment.RemainingCharges, Is.EqualTo(6));
            Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(other);
        }
    }

    private void AssertCraft(PotionFormula formula, PotionCraftingResult expected)
    {
        bool crafted = service.TryCraft(formula, inventory, equipment, out var result);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(crafted, Is.EqualTo(expected == PotionCraftingResult.Success));
    }

    private void PopulateIngredients(int amount)
    {
        foreach (PotionEffect element in Elements)
            Assert.That(inventory.TryAdd(element, amount), Is.True);
    }

    private void Populate()
    {
        PopulateIngredients(4);
        foreach (PotionEffect element in Elements)
            Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Bomba, element), 2), Is.True);
        Assert.That(equipment.TrySelectNext(), Is.True);
        Assert.That(equipment.SelectedElement, Is.EqualTo(PotionEffect.Escarcha));
    }

    private sealed class Snapshot
    {
        public readonly int[] Ingredients = new int[3];
        public readonly int[] Charges = new int[3];
        public readonly PotionFormula[] Formulas = new PotionFormula[3];
        public PotionEffect? SelectedElement;
        public PotionFormula SelectedFormula;
    }

    private Snapshot Capture()
    {
        var snapshot = new Snapshot
        {
            SelectedElement = equipment.SelectedElement,
            SelectedFormula = equipment.SelectedFormula
        };
        foreach (PotionEffect element in Elements)
        {
            int index = (int)element;
            snapshot.Ingredients[index] = inventory.GetQuantity(element);
            snapshot.Charges[index] = equipment.GetCharges(element);
            snapshot.Formulas[index] = equipment.GetActiveFormula(element);
        }
        return snapshot;
    }

    private void AssertElementUnchanged(Snapshot before, PotionEffect element)
    {
        int index = (int)element;
        Assert.That(inventory.GetQuantity(element), Is.EqualTo(before.Ingredients[index]), element.ToString());
        Assert.That(equipment.GetCharges(element), Is.EqualTo(before.Charges[index]), element.ToString());
        Assert.That(equipment.GetActiveFormula(element), Is.SameAs(before.Formulas[index]), element.ToString());
    }

    private void AssertOtherElementsUnchanged(Snapshot before, PotionEffect changed)
    {
        foreach (PotionEffect element in Elements)
            if (element != changed)
                AssertElementUnchanged(before, element);
    }

    private void AssertUnchanged(Snapshot before)
    {
        foreach (PotionEffect element in Elements)
            AssertElementUnchanged(before, element);
        Assert.That(equipment.SelectedElement, Is.EqualTo(before.SelectedElement));
        Assert.That(equipment.SelectedFormula, Is.SameAs(before.SelectedFormula));
    }
}
