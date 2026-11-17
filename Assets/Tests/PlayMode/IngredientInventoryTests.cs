using NUnit.Framework;

public class IngredientInventoryTests
{
    private IngredientInventory inventory;

    [SetUp]
    public void SetUp()
    {
        inventory = new IngredientInventory();
    }

    [Test]
    public void NewInventory_AllIngredientsAreEmpty()
    {
        AssertQuantities(0, 0, 0);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void AddingPositiveAmounts_AccumulatesOnlyRequestedIngredient(PotionEffect ingredient)
    {
        Assert.That(inventory.TryAdd(ingredient, 2), Is.True);
        AssertOnlyIngredient(ingredient, 2);
        Assert.That(inventory.TryAdd(ingredient, 3), Is.True);
        AssertOnlyIngredient(ingredient, 5);
    }

    [Test]
    public void RepeatedQueries_ReturnEachQuantityWithoutConsuming()
    {
        Populate();
        for (int query = 0; query < 5; query++)
            AssertQuantities(2, 3, 4);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void Consumption_AcceptsPartialAndExactAmounts(PotionEffect ingredient)
    {
        Assert.That(inventory.TryAdd(ingredient, 5), Is.True);
        Assert.That(inventory.TryConsume(ingredient, 2), Is.True);
        AssertOnlyIngredient(ingredient, 3);
        Assert.That(inventory.TryConsume(ingredient, 3), Is.True);
        AssertQuantities(0, 0, 0);
        Assert.That(inventory.TryConsume(ingredient, 1), Is.False);
        AssertQuantities(0, 0, 0);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void InsufficientAmount_PreservesAllQuantities(PotionEffect ingredient)
    {
        Assert.That(inventory.TryConsume(ingredient, 1), Is.False);
        AssertQuantities(0, 0, 0);
        Populate();
        Assert.That(inventory.TryConsume(ingredient, inventory.GetQuantity(ingredient) + 1), Is.False);
        AssertQuantities(2, 3, 4);
        Assert.That(inventory.TryConsume(ingredient, int.MaxValue), Is.False);
        AssertQuantities(2, 3, 4);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void NonPositiveAmounts_RejectAdditionAndConsumptionWithoutChanges(int amount)
    {
        foreach (PotionEffect ingredient in Ingredients())
        {
            Assert.That(inventory.TryAdd(ingredient, amount), Is.False);
            AssertQuantities(0, 0, 0);
            Assert.That(inventory.TryConsume(ingredient, amount), Is.False);
            AssertQuantities(0, 0, 0);
        }

        Populate();
        foreach (PotionEffect ingredient in Ingredients())
        {
            Assert.That(inventory.TryAdd(ingredient, amount), Is.False);
            AssertQuantities(2, 3, 4);
            Assert.That(inventory.TryConsume(ingredient, amount), Is.False);
            AssertQuantities(2, 3, 4);
        }
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void IntegerLimit_AcceptsBoundaryAndRejectsOverflowWithoutChanges(PotionEffect ingredient)
    {
        Populate();
        int initial = inventory.GetQuantity(ingredient);
        Assert.That(inventory.TryAdd(ingredient, int.MaxValue - initial - 1), Is.True);
        AssertPopulatedExcept(ingredient, int.MaxValue - 1);
        Assert.That(inventory.TryAdd(ingredient, 2), Is.False);
        AssertPopulatedExcept(ingredient, int.MaxValue - 1);
        Assert.That(inventory.TryAdd(ingredient, 1), Is.True);
        AssertPopulatedExcept(ingredient, int.MaxValue);
        Assert.That(inventory.TryAdd(ingredient, 1), Is.False);
        AssertPopulatedExcept(ingredient, int.MaxValue);
        Assert.That(inventory.TryAdd(ingredient, int.MaxValue), Is.False);
        AssertPopulatedExcept(ingredient, int.MaxValue);
        Assert.That(inventory.TryConsume(ingredient, int.MaxValue), Is.True);
        AssertPopulatedExcept(ingredient, 0);
        Assert.That(inventory.TryAdd(ingredient, int.MaxValue), Is.True);
        AssertPopulatedExcept(ingredient, int.MaxValue);
    }

    [TestCase(PotionEffect.Fuego)]
    [TestCase(PotionEffect.Escarcha)]
    [TestCase(PotionEffect.Savia)]
    public void ChangesToOneIngredient_PreserveOtherPopulatedIngredients(PotionEffect ingredient)
    {
        Populate();
        int initial = inventory.GetQuantity(ingredient);
        Assert.That(inventory.TryAdd(ingredient, 7), Is.True);
        AssertPopulatedExcept(ingredient, initial + 7);
        Assert.That(inventory.TryConsume(ingredient, 1), Is.True);
        AssertPopulatedExcept(ingredient, initial + 6);
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void UnknownIngredient_QueriesZeroAndRejectsChanges(int unknown)
    {
        Populate();
        var ingredient = (PotionEffect)unknown;
        Assert.That(inventory.GetQuantity(ingredient), Is.Zero);
        AssertQuantities(2, 3, 4);
        Assert.That(inventory.TryAdd(ingredient, 1), Is.False);
        AssertQuantities(2, 3, 4);
        Assert.That(inventory.TryConsume(ingredient, 1), Is.False);
        AssertQuantities(2, 3, 4);
    }

    [Test]
    public void RepeatedCollectionAndConsumption_KeepsCorrectQuantities()
    {
        for (int cycle = 0; cycle < 10; cycle++)
        {
            Populate();
            AssertQuantities(2, 3, 4);
            Assert.That(inventory.TryConsume(PotionEffect.Escarcha, 2), Is.True);
            AssertQuantities(2, 1, 4);
            Assert.That(inventory.TryConsume(PotionEffect.Fuego, 2), Is.True);
            AssertQuantities(0, 1, 4);
            Assert.That(inventory.TryConsume(PotionEffect.Fuego, 1), Is.False);
            AssertQuantities(0, 1, 4);
            Assert.That(inventory.TryConsume(PotionEffect.Savia, 4), Is.True);
            AssertQuantities(0, 1, 0);
            Assert.That(inventory.TryConsume(PotionEffect.Escarcha, 1), Is.True);
            AssertQuantities(0, 0, 0);
        }
    }

    private void Populate()
    {
        Assert.That(inventory.TryAdd(PotionEffect.Fuego, 2), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Escarcha, 3), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Savia, 4), Is.True);
    }

    private static PotionEffect[] Ingredients()
    {
        return new[] { PotionEffect.Fuego, PotionEffect.Escarcha, PotionEffect.Savia };
    }

    private void AssertOnlyIngredient(PotionEffect ingredient, int expected)
    {
        AssertQuantities(ingredient == PotionEffect.Fuego ? expected : 0,
            ingredient == PotionEffect.Escarcha ? expected : 0,
            ingredient == PotionEffect.Savia ? expected : 0);
    }

    private void AssertPopulatedExcept(PotionEffect ingredient, int expected)
    {
        AssertQuantities(ingredient == PotionEffect.Fuego ? expected : 2,
            ingredient == PotionEffect.Escarcha ? expected : 3,
            ingredient == PotionEffect.Savia ? expected : 4);
    }

    private void AssertQuantities(int fire, int frost, int sap)
    {
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(fire), "Fuego");
        Assert.That(inventory.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(frost), "Escarcha");
        Assert.That(inventory.GetQuantity(PotionEffect.Savia), Is.EqualTo(sap), "Savia");
    }
}
