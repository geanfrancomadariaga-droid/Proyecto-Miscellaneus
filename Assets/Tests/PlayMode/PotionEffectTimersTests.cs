using NUnit.Framework;

public class PotionEffectTimersTests
{
    private readonly PotionEffectTimers timers = new PotionEffectTimers();

    [SetUp]
    public void SetUp() => timers.Clear();

    [TestCase(PotionModifier.Ninguno, 5)]
    [TestCase(PotionModifier.Doble, 10)]
    public void Fire_RefreshesDurationAndRoundsFiveToEight(PotionModifier modifier, int duration)
    {
        var vial = new PotionFormula(PotionBase.Vial, PotionEffect.Fuego, modifier);
        timers.Apply(vial, 20);
        Assert.That(timers.MeleeDamage(5, 20), Is.EqualTo(8));
        timers.Apply(vial, 23);
        Assert.That(timers.MeleeDamage(5, 23 + duration - .001), Is.EqualTo(8));
        Assert.That(timers.MeleeDamage(5, 23 + duration), Is.EqualTo(5));
    }

    [TestCase(PotionModifier.Ninguno, 5)]
    [TestCase(PotionModifier.Doble, 10)]
    public void Shield_PreservesPriorSlowAndBlocksNewDosesUntilExpiry(PotionModifier modifier, int duration)
    {
        var bomb = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha);
        var vial = new PotionFormula(PotionBase.Vial, PotionEffect.Escarcha, modifier);
        timers.Apply(bomb, 10);
        timers.Apply(vial, 11);
        Assert.That(timers.SpeedFactor(11), Is.EqualTo(.75f));
        timers.Apply(bomb, 11.4);
        Assert.That(timers.SpeedFactor(11.5), Is.EqualTo(1f));
        timers.Apply(vial, 12);
        double end = 12 + duration;
        timers.Apply(bomb, end - .001);
        Assert.That(timers.IsProtected(end - .001), Is.True);
        Assert.That(timers.SpeedFactor(end - .001), Is.EqualTo(1f));
        timers.Apply(bomb, end);
        Assert.That(timers.IsProtected(end), Is.False);
        Assert.That(timers.SpeedFactor(end), Is.EqualTo(.75f));
    }

    [TestCase(PotionModifier.Ninguno, 1.5)]
    [TestCase(PotionModifier.Doble, 3.0)]
    public void Slow_RefreshesWithoutStackingAndEndsAtBoundary(PotionModifier modifier, double duration)
    {
        var bomb = new PotionFormula(PotionBase.Bomba, PotionEffect.Escarcha, modifier);
        timers.Apply(bomb, 10);
        timers.Apply(bomb, 11);
        Assert.That(timers.SpeedFactor(11), Is.EqualTo(.75f));
        Assert.That(timers.SpeedFactor(11 + duration - .001), Is.EqualTo(.75f));
        Assert.That(timers.SpeedFactor(11 + duration), Is.EqualTo(1f));
    }
}
