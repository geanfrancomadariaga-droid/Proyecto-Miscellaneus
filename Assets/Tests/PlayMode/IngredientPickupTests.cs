using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class IngredientPickupTests
{
    private Scene testScene;
    private PhysicsScene2D physicsScene;
    private GameObject player;
    private Rigidbody2D playerBody;
    private PlayerIngredientInventory inventory;
    private GameObject pickupObject;
    private IngredientPickup pickup;

    [SetUp]
    public void SetUp()
    {
        // Fisica real y aislada: no simula ni modifica SampleScene.
        testScene = SceneManager.CreateScene("IngredientPickupTests",
            new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        physicsScene = testScene.GetPhysicsScene2D();
        player = CreateObject("CollectorWithoutPlayerTag");
        player.transform.position = Vector2.left * 4f;
        playerBody = player.AddComponent<Rigidbody2D>();
        playerBody.gravityScale = 0f;
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        player.AddComponent<BoxCollider2D>();
        inventory = player.AddComponent<PlayerIngredientInventory>();

        pickupObject = CreateObject("Ingredient");
        pickupObject.AddComponent<BoxCollider2D>().isTrigger = true;
        pickup = pickupObject.AddComponent<IngredientPickup>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (testScene.IsValid() && testScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(testScene);
    }

    [UnityTest]
    public IEnumerator Fire_DefaultAmount_AddsOneAndDestroysPickup()
    {
        Enter();
        AssertQuantities(1, 0, 0);
        yield return null; // Destroy se procesa al terminar el fotograma.
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator Frost_AddsOneAndDestroysPickup()
    {
        Configure(PotionEffect.Escarcha, 1);
        Enter();
        AssertQuantities(0, 1, 0);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator Sap_AddsOneAndDestroysPickup()
    {
        Configure(PotionEffect.Savia, 1);
        Enter();
        AssertQuantities(0, 0, 1);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator ConfiguredAmount_AccumulatesAndPreservesOtherIngredients()
    {
        Populate();
        Configure(PotionEffect.Escarcha, 7);
        Enter();
        AssertQuantities(2, 10, 4);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator MultipleCollidersAndReentryBeforeDestroy_GrantOnlyOnce()
    {
        AddChildCollider();
        player.AddComponent<CircleCollider2D>();
        Enter(); // Varios pares de colliders en el mismo paso fisico.
        AssertQuantities(1, 0, 0);
        MovePlayer(Vector2.left * 4f);
        MovePlayer(Vector2.zero); // Otra entrada sin ceder el fotograma a Destroy.
        AssertQuantities(1, 0, 0);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator RenewablePickupRequiresLeavingWithAllCollidersBeforeCollectingAgain()
    {
        typeof(IngredientPickup).GetField("renewable", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pickup, true);
        Configure(PotionEffect.Savia, 1);
        AddChildCollider();
        Enter();
        Step();
        AssertQuantities(0, 0, 1);
        yield return null;
        Assert.That(pickupObject != null, Is.True);
        MovePlayer(Vector2.left * 4f);
        AssertQuantities(0, 0, 1);
        MovePlayer(Vector2.zero);
        Step();
        AssertQuantities(0, 0, 2);
    }

    [UnityTest]
    public IEnumerator ContactWithoutInventory_PreservesPickupAndUnrelatedInventory()
    {
        Populate();
        GameObject stranger = CreateObject("Player"); // El nombre no autoriza la recogida.
        stranger.AddComponent<BoxCollider2D>();
        stranger.AddComponent<Rigidbody2D>().gravityScale = 0f;
        Step();
        AssertQuantities(2, 3, 4);
        yield return null;
        Assert.That(pickupObject != null, Is.True);
        Object.DestroyImmediate(stranger);
        Enter(); // El contacto invalido no bloquea una recogida posterior valida.
        AssertQuantities(3, 3, 4);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator UnknownIngredient_IsRejectedAndPickupSurvives()
    {
        Populate();
        Configure((PotionEffect)99, 1);
        Enter();
        AssertQuantities(2, 3, 4);
        Assert.That(inventory.GetQuantity((PotionEffect)99), Is.Zero);
        yield return null;
        Assert.That(pickupObject != null, Is.True);
    }

    [UnityTest]
    public IEnumerator ZeroAmount_IsRejectedAndPickupSurvives()
    {
        yield return RejectAmount(0);
    }

    [UnityTest]
    public IEnumerator NegativeAmount_IsRejectedAndPickupSurvives()
    {
        yield return RejectAmount(-1);
    }

    [UnityTest]
    public IEnumerator Overflow_PreservesPickupAndCanRetryAfterConsumption()
    {
        Populate();
        Assert.That(inventory.TryAdd(PotionEffect.Fuego, int.MaxValue - 2), Is.True);
        Enter();
        AssertQuantities(int.MaxValue, 3, 4);
        yield return null;
        Assert.That(pickupObject != null, Is.True);

        Assert.That(inventory.TryConsume(PotionEffect.Fuego, 1), Is.True);
        MovePlayer(Vector2.left * 4f);
        MovePlayer(Vector2.zero);
        AssertQuantities(int.MaxValue, 3, 4);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator ChildCollider_FindsInventoryOnParent()
    {
        player.GetComponent<BoxCollider2D>().enabled = false;
        AddChildCollider();
        Enter();
        AssertQuantities(1, 0, 0);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator Collection_PreservesAllEquippedPotionChargesAndSelection()
    {
        var charges = player.AddComponent<EquippedPotionCharges>();
        charges.Initialize(2);
        var frost = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha);
        var sap = new PotionFormula(PotionBase.Bomba, PotionEffect.Savia);
        Assert.That(charges.TryAdd(frost, 3), Is.True);
        Assert.That(charges.TryAdd(sap, 4), Is.True);
        PotionFormula fire = charges.GetActiveFormula(PotionEffect.Fuego);
        PotionEffect? selection = charges.SelectedElement;
        Enter();
        yield return null;
        AssertQuantities(1, 0, 0);
        Assert.That(pickupObject == null, Is.True);
        Assert.That(charges.GetCharges(PotionEffect.Fuego), Is.EqualTo(2));
        Assert.That(charges.GetCharges(PotionEffect.Escarcha), Is.EqualTo(3));
        Assert.That(charges.GetCharges(PotionEffect.Savia), Is.EqualTo(4));
        Assert.That(charges.RemainingCharges, Is.EqualTo(2));
        Assert.That(charges.GetActiveFormula(PotionEffect.Fuego), Is.EqualTo(fire));
        Assert.That(charges.GetActiveFormula(PotionEffect.Escarcha), Is.EqualTo(frost));
        Assert.That(charges.GetActiveFormula(PotionEffect.Savia), Is.EqualTo(sap));
        Assert.That(charges.SelectedElement, Is.EqualTo(selection));
    }

    [UnityTest]
    public IEnumerator DisabledComponent_IgnoresContactAndCanCollectOnLaterEntry()
    {
        Populate();
        pickup.enabled = false;
        Enter();
        AssertQuantities(2, 3, 4);
        yield return null;
        Assert.That(pickupObject != null, Is.True);
        MovePlayer(Vector2.left * 4f);
        pickup.enabled = true;
        MovePlayer(Vector2.zero);
        AssertQuantities(3, 3, 4);
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator InactivePickup_IgnoresContact()
    {
        Populate();
        pickupObject.SetActive(false);
        Enter();
        yield return null;
        AssertQuantities(2, 3, 4);
        Assert.That(pickupObject != null, Is.True);
    }

    private IEnumerator RejectAmount(int amount)
    {
        Populate();
        Configure(PotionEffect.Fuego, amount);
        Enter();
        AssertQuantities(2, 3, 4);
        yield return null;
        Assert.That(pickupObject != null, Is.True);
    }

    private GameObject CreateObject(string name)
    {
        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, testScene);
        return created;
    }

    private void AddChildCollider()
    {
        GameObject child = CreateObject("ChildCollider");
        child.transform.SetParent(player.transform, false);
        child.AddComponent<BoxCollider2D>();
    }

    private void Configure(PotionEffect ingredient, int amount)
    {
        // Solo configura campos serializados; nunca invoca el callback por reflexion.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(IngredientPickup).GetField("ingredient", flags).SetValue(pickup, ingredient);
        typeof(IngredientPickup).GetField("amount", flags).SetValue(pickup, amount);
    }

    private void Enter()
    {
        Step(); // Primero registra los colliders separados.
        MovePlayer(Vector2.zero);
    }

    private void MovePlayer(Vector2 position)
    {
        playerBody.position = position;
        playerBody.WakeUp();
        Step();
    }

    private void Step()
    {
        Physics2D.SyncTransforms();
        Assert.That(physicsScene.Simulate(0.02f), Is.True);
    }

    private void Populate()
    {
        Assert.That(inventory.TryAdd(PotionEffect.Fuego, 2), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Escarcha, 3), Is.True);
        Assert.That(inventory.TryAdd(PotionEffect.Savia, 4), Is.True);
    }

    private void AssertQuantities(int fire, int frost, int sap)
    {
        Assert.That(inventory.GetQuantity(PotionEffect.Fuego), Is.EqualTo(fire));
        Assert.That(inventory.GetQuantity(PotionEffect.Escarcha), Is.EqualTo(frost));
        Assert.That(inventory.GetQuantity(PotionEffect.Savia), Is.EqualTo(sap));
    }
}
