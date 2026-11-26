using System;

/// <summary>Vencimientos en segundos de combate. Una nueva dosis sustituye su vencimiento.</summary>
public sealed class PotionEffectTimers
{
    private double fireUntil, protectionUntil, slowUntil;

    public bool HasFire(double now) => now < fireUntil;
    public bool IsProtected(double now) => now < protectionUntil;
    public float SpeedFactor(double now) => now < slowUntil ? 0.75f : 1f;

    public void Apply(PotionFormula formula, double now)
    {
        if (formula == null) return;
        if (formula.Base == PotionBase.Vial && formula.Effect == PotionEffect.Fuego)
            fireUntil = now + PotionEffectDuration.For(formula, 5f);
        else if (formula.Effect == PotionEffect.Escarcha)
        {
            if (formula.Base == PotionBase.Vial)
                protectionUntil = now + PotionEffectDuration.For(formula, 5f);
            else if (!IsProtected(now))
                slowUntil = now + PotionEffectDuration.For(formula, 1.5f);
        }
    }

    public int MeleeDamage(int baseDamage, double now)
    {
        // Techo de 3d/2 con enteros: 5 se convierte en 8. long evita desbordamientos.
        return HasFire(now) ? (int)Math.Min(int.MaxValue, (3L * baseDamage + 1) / 2) : baseDamage;
    }

    public void Clear() => fireUntil = protectionUntil = slowUntil = 0;
}
