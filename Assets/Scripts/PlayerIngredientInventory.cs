using UnityEngine;

/// <summary>Asocia al jugador un inventario de ingredientes durante la vida de este componente.</summary>
[DisallowMultipleComponent]
public sealed class PlayerIngredientInventory : MonoBehaviour
{
    private IngredientInventory inventory;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        // Otra instancia puede consultar desde su Awake antes que este componente.
        // No reemplazar el inventario al activarse conserva lo acumulado previamente.
        if (inventory == null)
            inventory = new IngredientInventory();
    }

    /// <summary>Consulta sin consumir; un ingrediente desconocido devuelve cero.</summary>
    public int GetQuantity(PotionEffect ingredient)
    {
        EnsureInitialized();
        return inventory.GetQuantity(ingredient);
    }

    /// <summary>Delega la recoleccion; una cantidad o ingrediente invalido no cambia el inventario.</summary>
    public bool TryAdd(PotionEffect ingredient, int amount)
    {
        EnsureInitialized();
        return inventory.TryAdd(ingredient, amount);
    }

    /// <summary>Delega el consumo; devuelve false si la solicitud no es valida o no hay existencias.</summary>
    public bool TryConsume(PotionEffect ingredient, int amount)
    {
        EnsureInitialized();
        return inventory.TryConsume(ingredient, amount);
    }
}
