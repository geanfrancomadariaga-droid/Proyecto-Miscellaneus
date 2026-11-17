using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Cuenta asignaciones incluso si Text descartaria una cadena identica.
public class IngredientHUDCountingText : Text
{
    public int Assignments { get; private set; }

    public override string text
    {
        get => base.text;
        set
        {
            Assignments++;
            base.text = value;
        }
    }
}

public class IngredientInventoryHUDTests
{
    private GameObject player;
    private GameObject hudObject;
    private PlayerIngredientInventory inventory;
    private IngredientInventoryHUD hud;
    private IngredientHUDCountingText fire;
    private IngredientHUDCountingText frost;
    private IngredientHUDCountingText sap;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("IngredientHUDTestPlayer");
        inventory = player.AddComponent<PlayerIngredientInventory>();
        hudObject = new GameObject("IngredientHUDTest");
        hudObject.SetActive(false);
        fire = CreateText("Fire");
        frost = CreateText("Frost");
        sap = CreateText("Sap");
        hud = hudObject.AddComponent<IngredientInventoryHUD>();
        Set("inventory", inventory);
        Set("fireText", fire);
        Set("frostText", frost);
        Set("sapText", sap);
        hudObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(hudObject);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void EmptyInventory_InitiallyDisplaysAllZeros()
    {
        AssertDisplay(0, 0, 0);
    }

    [UnityTest]
    public IEnumerator AddFire_UpdatesOnlyFireText()
    {
        int fireWrites = fire.Assignments;
        int frostWrites = frost.Assignments;
        int sapWrites = sap.Assignments;
        Add(PotionEffect.Fuego, 2);
        yield return null;
        AssertDisplay(2, 0, 0);
        Assert.That(fire.Assignments, Is.EqualTo(fireWrites + 1));
        Assert.That(frost.Assignments, Is.EqualTo(frostWrites));
        Assert.That(sap.Assignments, Is.EqualTo(sapWrites));
    }

    [UnityTest]
    public IEnumerator AddFrost_UpdatesFrostQuantity()
    {
        Add(PotionEffect.Escarcha, 3);
        yield return null;
        AssertDisplay(0, 3, 0);
    }

    [UnityTest]
    public IEnumerator AddSap_UpdatesSapQuantity()
    {
        Add(PotionEffect.Savia, 4);
        yield return null;
        AssertDisplay(0, 0, 4);
    }

    [UnityTest]
    public IEnumerator RepeatedAdditions_DisplayAccumulatedTotals()
    {
        Populate();
        yield return null;
        AssertDisplay(2, 3, 4);
        Populate();
        yield return null;
        AssertDisplay(4, 6, 8);
    }

    [UnityTest]
    public IEnumerator Consumption_ReducesCorrespondingCounters()
    {
        Populate();
        yield return null;
        Assert.That(inventory.TryConsume(PotionEffect.Fuego, 1), Is.True);
        yield return null;
        AssertDisplay(1, 3, 4);
        Assert.That(inventory.TryConsume(PotionEffect.Escarcha, 3), Is.True);
        yield return null;
        AssertDisplay(1, 0, 4);
        Assert.That(inventory.TryConsume(PotionEffect.Savia, 2), Is.True);
        yield return null;
        AssertDisplay(1, 0, 2);
    }

    [UnityTest]
    public IEnumerator RepeatedUpdates_PreserveInventoryAndDoNotRewriteTexts()
    {
        Populate();
        yield return null;
        int fireWrites = fire.Assignments;
        int frostWrites = frost.Assignments;
        int sapWrites = sap.Assignments;
        for (int frame = 0; frame < 3; frame++)
            yield return null;
        AssertDisplay(2, 3, 4);
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(2));
        Assert.That(inventory.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(3));
        Assert.That(inventory.GetQuantity(PotionEffect.Savia), Is.EqualTo(4));
        Assert.That(fire.Assignments, Is.EqualTo(fireWrites));
        Assert.That(frost.Assignments, Is.EqualTo(frostWrites));
        Assert.That(sap.Assignments, Is.EqualTo(sapWrites));
    }

    [UnityTest]
    public IEnumerator ReenableComponent_ImmediatelyRefreshesChangesWhileDisabled()
    {
        Populate();
        yield return null;
        hud.enabled = false;
        Add(PotionEffect.Fuego, 5);
        yield return null;
        AssertDisplay(2, 3, 4);
        hud.enabled = true;
        AssertDisplay(7, 3, 4);
        hud.enabled = false;
        hud.enabled = true;
        AssertDisplay(7, 3, 4);
    }

    [UnityTest]
    public IEnumerator ReactivateGameObject_RefreshesAllQuantities()
    {
        hudObject.SetActive(false);
        Populate();
        yield return null;
        hudObject.SetActive(true);
        AssertDisplay(2, 3, 4);
    }

    [UnityTest]
    public IEnumerator MissingInventory_ShowsZerosAndCanBeRestored()
    {
        Populate();
        yield return null;
        Set("inventory", null);
        for (int frame = 0; frame < 3; frame++)
            yield return null;
        AssertDisplay(0, 0, 0);
        hud.enabled = false;
        hud.enabled = true;
        AssertDisplay(0, 0, 0);
        Set("inventory", inventory);
        yield return null;
        AssertDisplay(2, 3, 4);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MissingTexts_AllowOtherUpdatesAndRestoration()
    {
        Set("fireText", null);
        Populate();
        yield return null;
        AssertDisplay(0, 3, 4);
        Set("frostText", null);
        Set("sapText", null);
        Set("inventory", null);
        hud.enabled = false;
        hud.enabled = true;
        for (int frame = 0; frame < 3; frame++)
            yield return null;
        Set("inventory", inventory);
        Set("fireText", fire);
        Set("frostText", frost);
        Set("sapText", sap);
        yield return null;
        AssertDisplay(2, 3, 4);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ReplacementText_DisplaysUnchangedQuantity()
    {
        Populate();
        yield return null;
        fire = CreateText("ReplacementFire");
        Set("fireText", fire);
        yield return null;
        AssertDisplay(2, 3, 4);
    }

    [UnityTest]
    public IEnumerator Updates_PreserveEquippedChargesFormulasAndSelection()
    {
        var charges = player.AddComponent<EquippedPotionCharges>();
        charges.Initialize(2);
        var frostFormula = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha);
        var sapFormula = new PotionFormula(PotionBase.Bomba, PotionEffect.Savia);
        Assert.That(charges.TryAdd(frostFormula, 3), Is.True);
        Assert.That(charges.TryAdd(sapFormula, 4), Is.True);
        Assert.That(charges.TrySelectNext(), Is.True);
        PotionEffect? selected = charges.SelectedElement;
        PotionFormula selectedFormula = charges.SelectedFormula;
        PotionFormula fireFormula = charges.GetActiveFormula(PotionEffect.Fuego);
        Populate();
        yield return null;
        Assert.That(inventory.TryConsume(PotionEffect.Fuego, 1), Is.True);
        yield return null;
        hud.enabled = false;
        hud.enabled = true;
        AssertDisplay(1, 3, 4);
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(2));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(3));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(4));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(charges.GetActiveFormula(PotionEffect.Fuego), Is.SameAs(fireFormula));
        Assert.That(charges.GetActiveFormula(PotionEffect.Escarcha), Is.SameAs(frostFormula));
        Assert.That(charges.GetActiveFormula(PotionEffect.Savia), Is.SameAs(sapFormula));
        Assert.That(charges.SelectedElement, Is.EqualTo(selected));
        Assert.That(charges.SelectedFormula, Is.SameAs(selectedFormula));
    }

    private IngredientHUDCountingText CreateText(string name)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(IngredientHUDCountingText));
        textObject.transform.SetParent(hudObject.transform, false);
        return textObject.GetComponent<IngredientHUDCountingText>();
    }

    private void Set(string field, object value) =>
        typeof(IngredientInventoryHUD).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, value);

    private void Add(PotionEffect ingredient, int amount) =>
        Assert.That(inventory.TryAdd(ingredient, amount), Is.True);

    private void Populate()
    {
        Add(PotionEffect.Fuego, 2);
        Add(PotionEffect.Escarcha, 3);
        Add(PotionEffect.Savia, 4);
    }

    private void AssertDisplay(int fireAmount, int frostAmount, int sapAmount)
    {
        Assert.That(fire.text, Is.EqualTo($"Fuego: {fireAmount}"));
        Assert.That(frost.text, Is.EqualTo($"Escarcha: {frostAmount}"));
        Assert.That(sap.text, Is.EqualTo($"Savia: {sapAmount}"));
    }
}
