using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PotionSelectionButtonTests
{
    private GameObject player, buttonObject;
    private EquippedPotionCharges charges;
    private PotionSelectionButton selector;
    private Button button;
    private Text label;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("SelectionTestPlayer");
        charges = player.AddComponent<EquippedPotionCharges>();
        buttonObject = new GameObject("SelectionTestButton", typeof(RectTransform), typeof(Button));
        buttonObject.SetActive(false);
        button = buttonObject.GetComponent<Button>();
        var labelObject = new GameObject("SelectionTestLabel", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        label = labelObject.GetComponent<Text>();
        selector = buttonObject.AddComponent<PotionSelectionButton>();
        Set("charges", charges);
        Set("selectionLabel", label);
        Set("selectionButton", button);
        // Equivale al enlace manual de onClick; el componente no agrega listeners.
        button.onClick.AddListener(selector.SelectNext);
        buttonObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(buttonObject);
        Object.DestroyImmediate(player);
    }

    private void Set(string field, object value) =>
        typeof(PotionSelectionButton).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(selector, value);

    private void Add(PotionEffect element, int amount) =>
        Assert.That(charges.TryAdd(new PotionFormula(PotionBase.Bomba, element), amount), Is.True);

    private void AssertDisplay(PotionEffect element, string text)
    {
        Assert.That(charges.SelectedElement, Is.EqualTo(element));
        Assert.That(label.text, Is.EqualTo(text));
        Assert.That(button.interactable, Is.True);
    }

    [Test]
    public void InitialFire_DisplaysFiveCharges()
    {
        AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 5");
    }

    [Test]
    public void Click_AllElements_SelectsCircularOrder()
    {
        Add(PotionEffect.Escarcha, 3);
        Add(PotionEffect.Savia, 2);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            button.onClick.Invoke();
            AssertDisplay(PotionEffect.Escarcha, "Bomba · Escarcha: 3");
            button.onClick.Invoke();
            AssertDisplay(PotionEffect.Savia, "Bomba · Savia: 2");
            button.onClick.Invoke();
            AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 5");
        }
    }

    [TestCase(PotionEffect.Escarcha, "Bomba · Escarcha: 2")]
    [TestCase(PotionEffect.Savia, "Bomba · Savia: 2")]
    public void Click_SkipsEmptyReserve(PotionEffect other, string text)
    {
        Add(other, 2);
        button.onClick.Invoke();
        AssertDisplay(other, text);
        button.onClick.Invoke();
        AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 5");
    }

    [Test]
    public void Click_OnlyFire_KeepsSelectionAndCharges()
    {
        for (int i = 0; i < 4; i++)
        {
            button.onClick.Invoke();
            AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 5");
            Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(5));
        }
    }

    [UnityTest]
    public IEnumerator LastCharge_AutomaticallyDisplaysNextReserveThenEmpty()
    {
        charges.Initialize(1);
        Add(PotionEffect.Escarcha, 1);
        Assert.That(charges.TryConsumeCharge(), Is.True);
        yield return null;
        AssertDisplay(PotionEffect.Escarcha, "Bomba · Escarcha: 1");
        Assert.That(charges.TryConsume(charges.SelectedFormula), Is.True);
        yield return null;
        Assert.That(label.text, Is.EqualTo("SIN POCIONES"));
        Assert.That(button.interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator ConsumptionWithoutSelectionChange_UpdatesCount()
    {
        Assert.That(charges.TryConsumeCharge(), Is.True);
        yield return null;
        AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 4");
    }

    [UnityTest]
    public IEnumerator Empty_DisplaysNoPotions_AndAdditionRestoresButton()
    {
        charges.Initialize(0);
        yield return null;
        Assert.That(label.text, Is.EqualTo("SIN POCIONES"));
        Assert.That(button.interactable, Is.False);
        Add(PotionEffect.Savia, 2);
        yield return null;
        AssertDisplay(PotionEffect.Savia, "Bomba · Savia: 2");
    }

    [Test]
    public void Click_Empty_IsSafeAndDoesNotCreateCharges()
    {
        charges.Initialize(0);
        // Invoke tambien comprueba llamadas directas aunque el Button este desactivado.
        for (int i = 0; i < 3; i++)
            Assert.DoesNotThrow(() => button.onClick.Invoke());
        Assert.That(charges.SelectedFormula, Is.Null);
        Assert.That(charges.SelectedElement, Is.Null);
        foreach (var element in new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia })
            Assert.That(charges.GetCharges(element), Is.Zero);
        Assert.That(label.text, Is.EqualTo("SIN POCIONES"));
        Assert.That(button.interactable, Is.False);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RepeatedClicks_DoNotConsumeOrReplaceFormulas()
    {
        Add(PotionEffect.Escarcha, 3);
        Add(PotionEffect.Savia, 2);
        var fire = charges.GetActiveFormula(PotionEffect.Fuego);
        var frost = charges.GetActiveFormula(PotionEffect.Escarcha);
        var sap = charges.GetActiveFormula(PotionEffect.Savia);
        for (int i = 0; i < 12; i++) button.onClick.Invoke();
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(5));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(3));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(2));
        Assert.That(charges.GetActiveFormula(PotionEffect.Fuego), Is.SameAs(fire));
        Assert.That(charges.GetActiveFormula(PotionEffect.Escarcha), Is.SameAs(frost));
        Assert.That(charges.GetActiveFormula(PotionEffect.Savia), Is.SameAs(sap));
    }

    [UnityTest]
    public IEnumerator MissingUIReferences_ClickUpdateAndReenableAreSafe()
    {
        Add(PotionEffect.Escarcha, 2);
        Set("selectionLabel", null);
        button.onClick.Invoke();
        yield return null;
        Assert.That(charges.SelectedElement, Is.EqualTo(PotionEffect.Escarcha));
        Set("selectionLabel", label);
        Set("selectionButton", null);
        button.onClick.Invoke();
        yield return null;
        Assert.That(label.text, Is.EqualTo("Bomba · Fuego: 5"));
        Set("selectionLabel", null);
        selector.enabled = false;
        selector.enabled = true;
        button.onClick.Invoke();
        yield return null;
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(5));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(2));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MissingCharges_ShowsEmptyAndIsSafe()
    {
        Set("charges", null);
        button.onClick.Invoke();
        yield return null;
        Assert.That(label.text, Is.EqualTo("SIN POCIONES"));
        Assert.That(button.interactable, Is.False);
        selector.enabled = false;
        selector.enabled = true;
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Reenable_RefreshesAndDoesNotDuplicateClicks()
    {
        Add(PotionEffect.Escarcha, 2);
        Add(PotionEffect.Savia, 3);
        for (int i = 0; i < 3; i++)
        {
            selector.enabled = false;
            button.onClick.Invoke();
            Assert.That(charges.SelectedElement, Is.EqualTo(PotionEffect.Fuego));
            selector.enabled = true;
        }
        selector.enabled = false;
        Assert.That(charges.TryConsumeCharge(), Is.True);
        selector.enabled = true;
        AssertDisplay(PotionEffect.Fuego, "Bomba · Fuego: 4");
        button.onClick.Invoke();
        AssertDisplay(PotionEffect.Escarcha, "Bomba · Escarcha: 2");
        LogAssert.NoUnexpectedReceived();
    }
}
