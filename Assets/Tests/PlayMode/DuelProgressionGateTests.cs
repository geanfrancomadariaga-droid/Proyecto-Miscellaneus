using System;
using NUnit.Framework;

public class DuelProgressionGateTests
{
    private static readonly PotionFormula Fire = new PotionFormula(PotionBase.Bomba, PotionEffect.Fuego);
    private static readonly PotionFormula Sap = new PotionFormula(PotionBase.Vial, PotionEffect.Savia);
    private static readonly PotionFormula DoubleFrost = new PotionFormula(
        PotionBase.Bomba, PotionEffect.Escarcha, PotionModifier.Doble);

    [Test]
    public void OnlySuccessOfTheRequiredStageReadiesTheNextDuel()
    {
        var gate = new DuelProgressionGate();
        Assert.That(gate.Prepared || gate.SapUnlocked || gate.DoubleUnlocked, Is.False);
        Assert.That(gate.RecordCraft(null) || gate.RecordCraft(Sap) || gate.RecordCraft(DoubleFrost), Is.False);
        gate.RecordVictory(2);
        Assert.That(gate.SapUnlocked || gate.DoubleUnlocked, Is.False);
        Assert.That(gate.RecordCraft(Fire), Is.True);
        gate.RecordVictory(1);
        Assert.That(gate.SapUnlocked, Is.True);
        Assert.That(gate.Prepared || gate.DoubleUnlocked, Is.False);
        Assert.That(gate.RecordCraft(Fire) || gate.RecordCraft(DoubleFrost), Is.False);
        Assert.That(gate.RecordCraft(Sap), Is.True);
        gate.RecordVictory(1);
        Assert.That(gate.Prepared, Is.True, "Una notificación repetida no borra la preparación.");
        gate.RecordVictory(2);
        Assert.That(gate.DoubleUnlocked, Is.True);
        Assert.That(gate.Prepared || gate.RecordCraft(Sap), Is.False);
        Assert.That(gate.RecordCraft(DoubleFrost), Is.True);
        gate.RecordVictory(3);
        gate.RecordVictory(4);
        Assert.That(gate.Prepared || gate.RecordCraft(DoubleFrost), Is.False);
    }

    [TestCase(PotionBase.Vial, PotionEffect.Fuego)]
    [TestCase(PotionBase.Bomba, PotionEffect.Escarcha)]
    [TestCase(PotionBase.Vial, PotionEffect.Escarcha)]
    public void DoubleScalesTemporalDurationOnly(PotionBase basis, PotionEffect effect)
    {
        Assert.That(PotionEffectDuration.For(new PotionFormula(basis, effect), 2.5f), Is.EqualTo(2.5f));
        Assert.That(PotionEffectDuration.For(new PotionFormula(basis, effect, PotionModifier.Doble), 2.5f),
            Is.EqualTo(5f));
    }

    [TestCase(PotionBase.Bomba, PotionEffect.Fuego)]
    [TestCase(PotionBase.Bomba, PotionEffect.Savia)]
    [TestCase(PotionBase.Vial, PotionEffect.Savia)]
    public void UnlockDoesNotMakeForbiddenDoubleFormulasValid(PotionBase basis, PotionEffect effect)
    {
        var gate = new DuelProgressionGate();
        gate.RecordVictory(1);
        gate.RecordVictory(2);
        Assert.Throws<ArgumentException>(() => new PotionFormula(basis, effect, PotionModifier.Doble));
        Assert.That(gate.RecordCraft(null), Is.False);
    }
}
