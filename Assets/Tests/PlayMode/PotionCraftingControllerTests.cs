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
