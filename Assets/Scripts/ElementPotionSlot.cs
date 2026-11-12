using System;

/// <summary>Reserva de un elemento fijo: conserva una sola formula mientras tenga cargas.</summary>
public sealed class ElementPotionSlot
{
    public PotionEffect Element { get; }
    public PotionFormula ActiveFormula { get; private set; }
    public int Charges { get; private set; }

    public ElementPotionSlot(PotionEffect element)
    {
        if (element != PotionEffect.Fuego && element != PotionEffect.Escarcha &&
            element != PotionEffect.Savia)
            throw new ArgumentOutOfRangeException(nameof(element));

        Element = element;
    }

    public bool TryAdd(PotionFormula formula, int amount)
    {
        if (formula == null || amount <= 0 || formula.Effect != Element)
            return false;
        if (Charges > 0 && ActiveFormula != formula)
            return false;

        // Comprobar el espacio disponible antes de sumar evita desbordar int.
        if (amount > int.MaxValue - Charges)
            return false;

        ActiveFormula = formula;
        Charges += amount;
        return true;
    }

    public bool TryConsume(PotionFormula expectedFormula)
    {
        // La igualdad de PotionFormula compara Base, Effect y Modifier.
        if (Charges == 0 || expectedFormula != ActiveFormula)
            return false;

        Charges--;
        if (Charges == 0)
            ActiveFormula = null;
        return true;
    }
}
