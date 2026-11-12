using System;

public enum PotionBase { Vial, Bomba }
public enum PotionEffect { Fuego, Escarcha, Savia }
public enum PotionModifier { Ninguno, Doble }

/// <summary>Formula valida e inmutable; identifica una receta sin ejecutar sus efectos.</summary>
public sealed class PotionFormula : IEquatable<PotionFormula>
{
    public PotionBase Base { get; }
    public PotionEffect Effect { get; }
    public PotionModifier Modifier { get; }

    /// <summary>
    /// Rechaza identificadores desconocidos y combinaciones no permitidas por el GDD.
    /// Doble solo corresponde a efectos temporales; no multiplica dano ni proyectiles.
    /// </summary>
    public PotionFormula(PotionBase potionBase, PotionEffect effect,
        PotionModifier modifier = PotionModifier.Ninguno)
    {
        if (potionBase != PotionBase.Vial && potionBase != PotionBase.Bomba)
            throw new ArgumentOutOfRangeException(nameof(potionBase));
        if (effect != PotionEffect.Fuego && effect != PotionEffect.Escarcha &&
            effect != PotionEffect.Savia)
            throw new ArgumentOutOfRangeException(nameof(effect));
        if (modifier != PotionModifier.Ninguno && modifier != PotionModifier.Doble)
            throw new ArgumentOutOfRangeException(nameof(modifier));

        bool allowsDouble = effect == PotionEffect.Escarcha ||
            (potionBase == PotionBase.Vial && effect == PotionEffect.Fuego);
        if (modifier == PotionModifier.Doble && !allowsDouble)
            throw new ArgumentException("Esta formula no admite Doble.", nameof(modifier));

        Base = potionBase;
        Effect = effect;
        Modifier = modifier;
    }

    public bool Equals(PotionFormula other)
    {
        return !ReferenceEquals(other, null) && Base == other.Base &&
            Effect == other.Effect && Modifier == other.Modifier;
    }

    public override bool Equals(object obj)
    {
        return obj is PotionFormula other && Equals(other);
    }

    public override int GetHashCode()
    {
        // Los mismos tres componentes siempre producen el mismo hash.
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)Base;
            hash = hash * 31 + (int)Effect;
            return hash * 31 + (int)Modifier;
        }
    }

    public static bool operator ==(PotionFormula left, PotionFormula right)
    {
        return ReferenceEquals(left, right) ||
            (!ReferenceEquals(left, null) && left.Equals(right));
    }

    public static bool operator !=(PotionFormula left, PotionFormula right)
    {
        return !(left == right);
    }
}
