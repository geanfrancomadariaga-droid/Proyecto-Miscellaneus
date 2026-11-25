/// <summary>Reglas de preparación de una partida. Solo el coordinador registra sus resultados.</summary>
public sealed class DuelProgressionGate
{
    private int victories;
    public bool SapUnlocked => victories >= 1;
    public bool DoubleUnlocked => victories >= 2;
    public bool Prepared { get; private set; }

    public bool Allows(PotionFormula formula) => formula != null &&
        (formula.Effect != PotionEffect.Savia || SapUnlocked) &&
        (formula.Modifier != PotionModifier.Doble || DoubleUnlocked);

    public void RecordVictory(int duel)
    {
        if (duel != victories + 1 || duel > 3) return;
        victories = duel;
        Prepared = false;
    }

    public bool RecordCraft(PotionFormula formula)
    {
        if (Prepared || !Allows(formula)) return false;
        Prepared = victories == 0 || (victories == 1 && formula.Effect == PotionEffect.Savia) ||
            (victories == 2 && formula.Modifier == PotionModifier.Doble);
        return Prepared;
    }
}
