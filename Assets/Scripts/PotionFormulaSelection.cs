using System;

/// <summary>Seleccion editable de bloques; no elabora pociones ni administra recursos.</summary>
public sealed class PotionFormulaSelection
{
    public PotionBase SelectedBase { get; private set; } = PotionBase.Bomba;
    public PotionEffect SelectedEffect { get; private set; } = PotionEffect.Fuego;
    public PotionModifier SelectedModifier { get; private set; } = PotionModifier.Ninguno;

    /// <summary>Rechaza valores desconocidos sin cambiar la seleccion anterior.</summary>
    public bool TrySetBase(PotionBase potionBase)
    {
        if (!Enum.IsDefined(typeof(PotionBase), potionBase))
            return false;
        SelectedBase = potionBase;
        return true;
    }

    /// <summary>Permite editar el efecto aunque la combinacion sea temporalmente invalida.</summary>
    public bool TrySetEffect(PotionEffect effect)
    {
        if (!Enum.IsDefined(typeof(PotionEffect), effect))
            return false;
        SelectedEffect = effect;
        return true;
    }

    /// <summary>Permite editar el modificador sin corregir los otros bloques.</summary>
    public bool TrySetModifier(PotionModifier modifier)
    {
        if (!Enum.IsDefined(typeof(PotionModifier), modifier))
            return false;
        SelectedModifier = modifier;
        return true;
    }

    /// <summary>
    /// Consulta sin cambiar la seleccion. PotionFormula conserva la autoridad sobre
    /// las combinaciones validas; una combinacion rechazada devuelve false y null.
    /// </summary>
    public bool TryGetFormula(out PotionFormula formula)
    {
        try
        {
            formula = new PotionFormula(SelectedBase, SelectedEffect, SelectedModifier);
            return true;
        }
        catch (ArgumentException)
        {
            formula = null;
            return false;
        }
    }
}
