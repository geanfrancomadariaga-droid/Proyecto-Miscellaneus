using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PotionCraftingControllerTests
{
    private static readonly PotionEffect[] Elements =
        { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };

    private GameObject player, buttonObject;
    private PlayerIngredientInventory inventory;
    private EquippedPotionCharges equipment;
    private PotionCraftingController controller;
    private Button button;
    private Text label;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("CraftingControllerTestPlayer");
        inventory = player.AddComponent<PlayerIngredientInventory>();
        equipment = player.AddComponent<EquippedPotionCharges>();
        // Conservar las cinco cargas reales de Awake en el objeto temporal.
        Assert.That(equipment.RemainingCharges, Is.EqualTo(5));
        AddIngredient(PotionEffect.Fuego, 2);
        AddIngredient(PotionEffect.Escarcha, 3);
        AddIngredient(PotionEffect.Savia, 4);
        Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Vial,
            PotionEffect.Escarcha, PotionModifier.Doble), 3), Is.True);
        Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Vial,
            PotionEffect.Savia), 4), Is.True);
        Assert.That(equipment.TrySelectNext(), Is.True);
        Assert.That(equipment.SelectedElement, Is.EqualTo(PotionEffect.Escarcha));

        buttonObject = new GameObject("CraftingTestButton", typeof(RectTransform), typeof(Button));
        buttonObject.SetActive(false);
        button = buttonObject.GetComponent<Button>();
        var labelObject = new GameObject("CraftingResult", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        label = labelObject.GetComponent<Text>();
        controller = buttonObject.AddComponent<PotionCraftingController>();
        Set("inventory", inventory);
        Set("equipment", equipment);
        Set("resultText", label);
        // Equivale a un unico enlace desde el Inspector.
        button.onClick.AddListener(controller.CraftFireBomb);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(buttonObject);
        Object.DestroyImmediate(player);
    }

    [UnityTest]
    public IEnumerator Initialization_DoesNotCraft()
    {
        var before = Capture();
        buttonObject.SetActive(true);
        yield return null;
        yield return null;
        AssertUnchanged(before);
        Assert.That(label.text, Is.Empty);
    }

    [Test]
    public void OneIngredient_DefaultFiveCharges_BecomeSixAndShowSuccess()
    {
        Assert.That(inventory.TryConsume(PotionEffect.Fuego, 1), Is.True);
        var before = Capture();
        buttonObject.SetActive(true);
        controller.CraftFireBomb();
        AssertCrafted(before, 1);
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.Zero);
        Assert.That(equipment.RemainingCharges, Is.EqualTo(6));
        Assert.That(label.text, Is.EqualTo("Bomba de Fuego preparada (+1)"));
    }

    [Test]
    public void ConsecutiveRequests_EachConsumesOneAndAddsOne()
    {
        buttonObject.SetActive(true);
        var before = Capture();
        controller.CraftFireBomb();
        AssertCrafted(before, 1);
        controller.CraftFireBomb();
        AssertCrafted(before, 2);
    }

    [Test]
    public void MissingIngredients_PreservesEntireStateAndShowsReason()
    {
        Assert.That(inventory.TryConsume(PotionEffect.Fuego, 2), Is.True);
        buttonObject.SetActive(true);
        var before = Capture();
        controller.CraftFireBomb();
        AssertUnchanged(before);
        Assert.That(label.text, Is.EqualTo("No tienes ingredientes de Fuego."));
    }

    [TestCase(PotionModifier.Ninguno)]
    [TestCase(PotionModifier.Doble)]
    public void OtherFireFormula_RejectsAndPreservesEntireState(PotionModifier modifier)
    {
        equipment.Initialize(0);
        Assert.That(equipment.TryAdd(new PotionFormula(PotionBase.Vial,
            PotionEffect.Fuego, modifier), 2), Is.True);
        buttonObject.SetActive(true);
        var before = Capture();
        controller.CraftFireBomb();
        AssertUnchanged(before);
        Assert.That(label.text, Is.EqualTo("Ya hay otra fórmula de Fuego con cargas."));
    }

    [Test]
    public void ChargeAdditionRejected_RestoresIngredientAndPreservesEntireState()
    {
        Assert.That(equipment.TryAdd(equipment.GetActiveFormula(PotionEffect.Fuego),
            int.MaxValue - 5), Is.True);
        buttonObject.SetActive(true);
        var before = Capture();
        controller.CraftFireBomb();
        AssertUnchanged(before);
        Assert.That(label.text, Is.EqualTo("No se pudo añadir la carga de Fuego."));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void NullDependencies_AreSafeAndPreserveEntireState(bool missingInventory, bool missingEquipment)
    {
        if (missingInventory) Set("inventory", null);
        if (missingEquipment) Set("equipment", null);
        buttonObject.SetActive(true);
        var before = Capture();
        Assert.DoesNotThrow(() => controller.CraftFireBomb());
        AssertUnchanged(before);
        Assert.That(label.text, Is.EqualTo("Falta asignar el inventario o el equipamiento."));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DestroyedDependency_IsSafeAndPreservesEntireState(bool destroyInventory)
    {
        var disposable = new GameObject("DestroyedCraftingReference");
        try
        {
            if (destroyInventory)
                Set("inventory", disposable.AddComponent<PlayerIngredientInventory>());
            else
                Set("equipment", disposable.AddComponent<EquippedPotionCharges>());
        }
        finally
        {
            Object.DestroyImmediate(disposable);
        }
        buttonObject.SetActive(true);
        var before = Capture();
        Assert.DoesNotThrow(() => controller.CraftFireBomb());
        AssertUnchanged(before);
        Assert.That(label.text, Is.EqualTo("Falta asignar el inventario o el equipamiento."));
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrDestroyedText_CraftsAndRejectsSafely(bool destroyText)
    {
        if (destroyText) Object.DestroyImmediate(label);
        else Set("resultText", null);
        buttonObject.SetActive(true);
        var before = Capture();
        Assert.DoesNotThrow(() => controller.CraftFireBomb());
        AssertCrafted(before, 1);
        Assert.DoesNotThrow(() => controller.CraftFireBomb());
        AssertCrafted(before, 2);
        var exhausted = Capture();
        Assert.DoesNotThrow(() => controller.CraftFireBomb());
        AssertUnchanged(exhausted);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ReenableComponentAndObject_DoNotCraftOrDuplicateButtonRequests()
    {
        buttonObject.SetActive(true);
        var before = Capture();
        for (int i = 0; i < 2; i++)
        {
            controller.enabled = false;
            yield return null;
            controller.enabled = true;
            yield return null;
            buttonObject.SetActive(false);
            buttonObject.SetActive(true);
            yield return null;
            AssertUnchanged(before);
        }
        button.onClick.Invoke();
        AssertCrafted(before, 1);
    }

    [Test]
    public void ButtonOnClick_ExecutesExactlyOneRequestPerInvocation()
    {
        buttonObject.SetActive(true);
        var before = Capture();
        button.onClick.Invoke();
        AssertCrafted(before, 1);
        button.onClick.Invoke();
        AssertCrafted(before, 2);
        var exhausted = Capture();
        button.onClick.Invoke();
        AssertUnchanged(exhausted);
    }

    [Test]
    public void EmptyEquipment_FirstCraftUsesNormalAutomaticFireSelection()
    {
        equipment.Initialize(0);
        foreach (var element in new[] { PotionEffect.Escarcha, PotionEffect.Savia })
            while (equipment.GetCharges(element) > 0)
                Assert.That(equipment.TryConsume(equipment.GetActiveFormula(element)), Is.True);
        Assert.That(equipment.SelectedElement, Is.Null);
        buttonObject.SetActive(true);
        var before = Capture();
        controller.CraftFireBomb();
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(1));
        Assert.That(equipment.RemainingCharges, Is.EqualTo(1));
        Assert.That(equipment.SelectedElement, Is.EqualTo(PotionEffect.Fuego));
        Assert.That(equipment.SelectedFormula, Is.EqualTo(new PotionFormula(
            PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno)));
        AssertElementUnchanged(before, PotionEffect.Escarcha);
        AssertElementUnchanged(before, PotionEffect.Savia);
    }

    [Test]
    public void EditableSelection_CyclesEachBlockWithoutChangingOthersOrResources()
    {
        var before = Capture();
        AssertEditableSelection(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno);
        controller.ToggleModifier();
        AssertEditableSelection(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Doble);
        AssertUnchanged(before);
        controller.NextBase();
        AssertEditableSelection(PotionBase.Vial, PotionEffect.Fuego, PotionModifier.Doble);
        AssertUnchanged(before);
        controller.NextBase();
        AssertEditableSelection(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Doble);
        AssertUnchanged(before);
        foreach (var effect in new[] { PotionEffect.Escarcha, PotionEffect.Savia, PotionEffect.Fuego })
        {
            controller.NextEffect();
            AssertEditableSelection(PotionBase.Bomba, effect, PotionModifier.Doble);
            AssertUnchanged(before);
        }
        controller.ToggleModifier();
        AssertEditableSelection(PotionBase.Bomba, PotionEffect.Fuego, PotionModifier.Ninguno);
        AssertUnchanged(before);
    }

    [TestCase(PotionEffect.Escarcha, false)]
    [TestCase(PotionEffect.Savia, false)]
    [TestCase(PotionEffect.Escarcha, true)]
    [TestCase(PotionEffect.Savia, true)]
    public void SelectedFormula_CraftsWithCompatibleOrEmptyReserve(PotionEffect effect, bool emptyReserve)
    {
        SelectCompatibleFormula(effect);
        if (emptyReserve)
            while (equipment.GetCharges(effect) > 0)
                Assert.That(equipment.TryConsume(equipment.GetActiveFormula(effect)), Is.True);
        var before = Capture();
        controller.CraftSelectedFormula();
        AssertSelectedCrafted(before, effect);
        Assert.That(label.text, Is.EqualTo(effect == PotionEffect.Escarcha
            ? "Vial de Escarcha + Doble preparado (+1)" : "Vial de Savia preparado (+1)"));
    }

    [Test]
    public void InvalidSelection_RejectsThenCraftsAfterCorrection()
    {
        SelectCompatibleFormula(PotionEffect.Savia);
        controller.ToggleModifier();
        var before = Capture();
        controller.CraftSelectedFormula();
        AssertUnchanged(before);
        AssertEditableSelection(PotionBase.Vial, PotionEffect.Savia, PotionModifier.Doble);
        Assert.That(label.text, Is.EqualTo("La fórmula no es válida."));
        controller.ToggleModifier();
        controller.CraftSelectedFormula();
        AssertSelectedCrafted(before, PotionEffect.Savia);
    }

    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void SelectedFormula_MissingIngredientsPreservesState(PotionEffect effect)
    {
        SelectCompatibleFormula(effect);
        Assert.That(inventory.TryConsume(effect, inventory.GetQuantity(effect)), Is.True);
        var before = Capture();
        controller.CraftSelectedFormula();
        AssertUnchanged(before);
        AssertCompatibleSelection(effect);
        Assert.That(label.text, Is.EqualTo($"No tienes ingredientes de {effect}."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SelectedFireVial_RejectsAgainstInitialFiveBombs(bool doubleModifier)
    {
        controller.NextBase();
        if (doubleModifier) controller.ToggleModifier();
        var before = Capture();
        controller.CraftSelectedFormula();
        AssertUnchanged(before);
        Assert.That(equipment.RemainingCharges, Is.EqualTo(5));
        AssertEditableSelection(PotionBase.Vial, PotionEffect.Fuego,
            doubleModifier ? PotionModifier.Doble : PotionModifier.Ninguno);
        Assert.That(label.text, Is.EqualTo("Ya hay otra fórmula de Fuego con cargas."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OriginalButton_CraftsFixedBombDespiteDifferentOrInvalidSelection(bool invalid)
    {
        SelectCompatibleFormula(PotionEffect.Savia);
        if (invalid) controller.ToggleModifier();
        buttonObject.SetActive(true);
        var before = Capture();
        button.onClick.Invoke();
        AssertCrafted(before, 1);
        AssertEditableSelection(PotionBase.Vial, PotionEffect.Savia,
            invalid ? PotionModifier.Doble : PotionModifier.Ninguno);
        Assert.That(label.text, Is.EqualTo("Bomba de Fuego preparada (+1)"));
    }

    [Test]
    public void SelectionText_ShowsInitialAndEditedBlocksIncludingInvalidCombination()
    {
        Text selectionLabel = CreateSelectionText();
        buttonObject.SetActive(true);
        Assert.That(selectionLabel.text, Is.EqualTo("Bomba + Fuego + Ninguno"));
        controller.ToggleModifier();
        Assert.That(selectionLabel.text, Is.EqualTo("Bomba + Fuego + Doble"));
        controller.NextBase();
        Assert.That(selectionLabel.text, Is.EqualTo("Vial + Fuego + Doble"));
        controller.NextEffect();
        Assert.That(selectionLabel.text, Is.EqualTo("Vial + Escarcha + Doble"));
        controller.NextEffect();
        Assert.That(selectionLabel.text, Is.EqualTo("Vial + Savia + Doble"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrDestroyedSelectionText_AllowsEditingCraftingAndRejection(bool destroyed)
    {
        if (destroyed) Object.DestroyImmediate(CreateSelectionText());
        buttonObject.SetActive(true);
        var before = Capture();
        Assert.DoesNotThrow(() => SelectCompatibleFormula(PotionEffect.Escarcha));
        Assert.DoesNotThrow(() => controller.CraftSelectedFormula());
        AssertSelectedCrafted(before, PotionEffect.Escarcha);
        controller.NextEffect();
        var after = Capture();
        Assert.DoesNotThrow(() => controller.CraftSelectedFormula());
        AssertUnchanged(after);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Reenable_WithEditedSelectionPreservesSelectionAndResources()
    {
        Text selectionLabel = CreateSelectionText();
        SelectCompatibleFormula(PotionEffect.Savia);
        var before = Capture();
        buttonObject.SetActive(true);
        yield return null;
        controller.enabled = false;
        yield return null;
        controller.enabled = true;
        yield return null;
        buttonObject.SetActive(false);
        buttonObject.SetActive(true);
        yield return null;
        AssertUnchanged(before);
        AssertCompatibleSelection(PotionEffect.Savia);
        Assert.That(selectionLabel.text, Is.EqualTo("Vial + Savia + Ninguno"));
        Assert.That(label.text, Is.Empty);
    }

    private Text CreateSelectionText()
    {
        var textObject = new GameObject("CraftingSelection", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        Text text = textObject.GetComponent<Text>();
        Set("selectionText", text);
        return text;
    }

    private void SelectCompatibleFormula(PotionEffect effect)
    {
        controller.NextBase();
        controller.NextEffect();
        if (effect == PotionEffect.Savia) controller.NextEffect();
        else controller.ToggleModifier();
    }

    private void AssertEditableSelection(PotionBase potionBase, PotionEffect effect, PotionModifier modifier)
    {
        var selection = (PotionFormulaSelection)typeof(PotionCraftingController)
            .GetField("selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.That(selection.SelectedBase, Is.EqualTo(potionBase));
        Assert.That(selection.SelectedEffect, Is.EqualTo(effect));
        Assert.That(selection.SelectedModifier, Is.EqualTo(modifier));
    }

    private void AssertCompatibleSelection(PotionEffect effect) =>
        AssertEditableSelection(PotionBase.Vial, effect,
            effect == PotionEffect.Escarcha ? PotionModifier.Doble : PotionModifier.Ninguno);

    private void AssertSelectedCrafted(Snapshot before, PotionEffect effect)
    {
        int index = (int)effect;
        Assert.That(inventory.GetQuantity(effect), Is.EqualTo(before.Ingredients[index] - 1));
        Assert.That(equipment.GetCharges(effect), Is.EqualTo(before.Charges[index] + 1));
        Assert.That(equipment.GetActiveFormula(effect), Is.EqualTo(new PotionFormula(PotionBase.Vial,
            effect, effect == PotionEffect.Escarcha ? PotionModifier.Doble : PotionModifier.Ninguno)));
        foreach (var other in Elements)
            if (other != effect) AssertElementUnchanged(before, other);
        // La reserva seleccionada puede recibir una instancia nueva de la misma formula.
        Assert.That(equipment.SelectedElement, Is.EqualTo(before.SelectedElement));
        Assert.That(equipment.SelectedFormula, Is.EqualTo(before.SelectedFormula));
        AssertCompatibleSelection(effect);
    }

    private void Set(string field, object value) =>
        typeof(PotionCraftingController).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

    private void AddIngredient(PotionEffect element, int amount) =>
        Assert.That(inventory.TryAdd(element, amount), Is.True);

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

    private void AssertSelectionUnchanged(Snapshot before)
    {
        Assert.That(equipment.SelectedElement, Is.EqualTo(before.SelectedElement));
        Assert.That(equipment.SelectedFormula, Is.SameAs(before.SelectedFormula));
    }

    private void AssertUnchanged(Snapshot before)
    {
        foreach (PotionEffect element in Elements) AssertElementUnchanged(before, element);
        AssertSelectionUnchanged(before);
    }

    private void AssertCrafted(Snapshot before, int count)
    {
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(before.Ingredients[0] - count));
        Assert.That(equipment.GetCharges(PotionEffect.Fuego), Is.EqualTo(before.Charges[0] + count));
        // TryAdd puede sustituir la instancia por otra formula igual por valor.
        Assert.That(equipment.GetActiveFormula(PotionEffect.Fuego), Is.EqualTo(before.Formulas[0]));
        AssertElementUnchanged(before, PotionEffect.Escarcha);
        AssertElementUnchanged(before, PotionEffect.Savia);
        AssertSelectionUnchanged(before);
    }
}
