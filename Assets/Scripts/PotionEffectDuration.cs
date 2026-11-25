/// <summary>Escala exclusivamente el tiempo de un efecto temporal de una fórmula válida.</summary>
public static class PotionEffectDuration
{
    public static float For(PotionFormula formula, float baseSeconds) =>
        formula != null && formula.Modifier == PotionModifier.Doble ? 2f * baseSeconds : baseSeconds;
}
