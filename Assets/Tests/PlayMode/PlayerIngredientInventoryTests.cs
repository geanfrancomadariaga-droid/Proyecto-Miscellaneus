using NUnit.Framework;
using UnityEngine;

public class PlayerIngredientInventoryTests
{
    private GameObject player;
    private PlayerIngredientInventory inventory;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("PlayerIngredientInventoryTest");
        inventory = player.AddComponent<PlayerIngredientInventory>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
    }

    [Test]
    public void Awake_StartsWithAllIngredientsEmpty()
    {
        AssertQuantities(0, 0, 0);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Collection_AccumulatesRequestedIngredient(PotionEffect ingredient)
    {
        Assert.That(inventory.TryAdd(ingredient, 2), Is.True);
        Assert.That(inventory.GetQuantity(ingredient), Is.EqualTo(2));
        Assert.That(inventory.TryAdd(ingredient, 3), Is.True);
        Assert.That(inventory.GetQuantity(ingredient), Is.EqualTo(5));
    }

    [Test]
    public void RepeatedQueries_DoNotConsumeOrDuplicateIngredients()
    {
        Populate();
        for (int query = 0; query < 10; query++)
            AssertQuantities(2, 3, 4);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Consumption_AcceptsPartialAndCompleteAmounts(PotionEffect ingredient)
    {
        Assert.That(inventory.TryAdd(ingredient, 5), Is.True);
        Assert.That(inventory.TryConsume(ingredient, 2), Is.True);
        Assert.That(inventory.GetQuantity(ingredient), Is.EqualTo(3));
        Assert.That(inventory.TryConsume(ingredient, 3), Is.True);
        AssertQuantities(0, 0, 0);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void InvalidAmounts_RejectAdditionAndConsumptionWithoutChanges(int amount)
    {
        Populate();
        foreach (PotionEffect ingredient in Ingredients())
        {
            Assert.That(inventory.TryAdd(ingredient, amount), Is.False);
            Assert.That(inventory.TryConsume(ingredient, amount), Is.False);
            AssertQuantities(2, 3, 4);
        }
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void InsufficientStock_RejectsConsumptionWithoutChanges(PotionEffect ingredient)
    {
        Assert.That(inventory.TryConsume(ingredient, 1), Is.False);
        AssertQuantities(0, 0, 0);
        Populate();
        Assert.That(inventory.TryConsume(ingredient, inventory.GetQuantity(ingredient) + 1), Is.False);
        AssertQuantities(2, 3, 4);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void ChangesToOneIngredient_PreserveOtherIngredients(PotionEffect ingredient)
    {
        Populate();
        int initial = inventory.GetQuantity(ingredient);
        Assert.That(inventory.TryAdd(ingredient, 5), Is.True);
        AssertPopulatedExcept(ingredient, initial + 5);
        Assert.That(inventory.TryConsume(ingredient, 1), Is.True);
        AssertPopulatedExcept(ingredient, initial + 4);
    }

    [Test]
    public void DisableAndEnableComponent_PreservesQuantitiesAndAllowsFurtherChanges()
    {
        Populate();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            inventory.enabled = false;
            inventory.enabled = true;
            AssertQuantities(2, 3, 4);
        }
        Assert.That(inventory.TryAdd(PotionEffect.Fuego, 1), Is.True);
        Assert.That(inventory.TryConsume(PotionEffect.Savia, 2), Is.True);
        AssertQuantities(3, 3, 2);
    }

    [Test]
    public void ReactivateGameObject_PreservesQuantities()
    {
        Populate();
        player.SetActive(false);
        player.SetActive(true);
        AssertQuantities(2, 3, 4);
    }

    [Test]
    public void CallsBeforeFirstActivation_AreSafeAndAwakePreservesCollectedIngredients()
    {
        var inactivePlayer = new GameObject("InactiveInventoryTest");
        inactivePlayer.SetActive(false);
        try
        {
            var inactiveInventory = inactivePlayer.AddComponent<PlayerIngredientInventory>();
            Assert.That(inactiveInventory.GetQuantity(PotionEffect.Fuego), Is.Zero);
            Assert.That(inactiveInventory.TryConsume(PotionEffect.Fuego, 1), Is.False);
            Assert.That(inactiveInventory.TryAdd(PotionEffect.Fuego, 3), Is.True);
            inactivePlayer.SetActive(true);
            Assert.That(inactiveInventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(3));
            AssertQuantities(0, 0, 0);
        }
        finally
        {
            Object.DestroyImmediate(inactivePlayer);
        }
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void UnknownIngredient_QueriesZeroAndRejectsChanges(int unknown)
    {
        Populate();
        var ingredient = (PotionEffect)unknown;
        Assert.That(inventory.GetQuantity(ingredient), Is.Zero);
        Assert.That(inventory.TryAdd(ingredient, 1), Is.False);
        Assert.That(inventory.TryConsume(ingredient, 1), Is.False);
        AssertQuantities(2, 3, 4);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void IntegerLimit_RejectsOverflowWithoutChanges(PotionEffect ingredient)
    {
        Populate();
        int initial = inventory.GetQuantity(ingredient);
        Assert.That(inventory.TryAdd(ingredient, int.MaxValue - initial), Is.True);
        Assert.That(inventory.TryAdd(ingredient, 1), Is.False);
        AssertPopulatedExcept(ingredient, int.MaxValue);
        Assert.That(inventory.TryConsume(ingredient, int.MaxValue), Is.True);
        AssertPopulatedExcept(ingredient, 0);
    }

    private static PotionEffect[] Ingredients() =>
        new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };

    private void Populate()
    {
        Assert.That(inventory.TryAdd(PotionEffect.Fuego, 2), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Escarcha, 3), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Savia, 4), Is.True);
    }

    private void AssertPopulatedExcept(PotionEffect ingredient, int expected)
    {
        AssertQuantities(ingredient == PotionEffect.Fuego ? expected : 2,
            ingredient == PotionEffect.Escarcha ? expected : 3,
            ingredient == PotionEffect.Savia ? expected : 4);
    }

    private void AssertQuantities(int fire, int frost, int sap)
    {
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(fire));
        Assert.That(inventory.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(frost));
        Assert.That(inventory.GetQuantity(PotionEffect.Savia), Is.EqualTo(sap));
    }
}
