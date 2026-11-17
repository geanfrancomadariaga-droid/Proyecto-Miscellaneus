/// <summary>
/// Cantidades de Brasa fungica (Fuego), Cristal de escarcha (Escarcha)
/// y Gota de savia (Savia). No administra recetas ni desbloqueos.
/// </summary>
public sealed class IngredientInventory
{
    private int fireQuantity;
    private int frostQuantity;
    private int sapQuantity;

    /// <summary>Consulta sin consumir; un identificador desconocido devuelve cero.</summary>
    public int GetQuantity(PotionEffect ingredient)
    {
        switch (ingredient)
        {
            case PotionEffect.Fuego: return fireQuantity;
            case PotionEffect.Escarcha: return frostQuantity;
            case PotionEffect.Savia: return sapQuantity;
            default: return 0;
        }
    }

    /// <summary>Anade una cantidad positiva si cabe en int; un rechazo no modifica nada.</summary>
    public bool TryAdd(PotionEffect ingredient, int amount)
    {
        if (amount <= 0)
            return false;

        int current = GetQuantity(ingredient);
        // Comprobar antes de sumar evita desbordamientos, incluso con int.MaxValue.
        if (amount > int.MaxValue - current)
            return false;

        return TrySetQuantity(ingredient, current + amount);
    }

    /// <summary>Consume solo una cantidad positiva disponible; no permite saldos negativos.</summary>
    public bool TryConsume(PotionEffect ingredient, int amount)
    {
        if (amount <= 0)
            return false;

        int current = GetQuantity(ingredient);
        if (amount > current)
            return false;

        return TrySetQuantity(ingredient, current - amount);
    }

    private bool TrySetQuantity(PotionEffect ingredient, int quantity)
    {
        switch (ingredient)
        {
            case PotionEffect.Fuego: fireQuantity = quantity; return true;
            case PotionEffect.Escarcha: frostQuantity = quantity; return true;
            case PotionEffect.Savia: sapQuantity = quantity; return true;
            default: return false;
        }
    }
}
