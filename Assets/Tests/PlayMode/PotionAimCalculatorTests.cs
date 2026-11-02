using NUnit.Framework;
using UnityEngine;

public class PotionAimCalculatorTests
{
    private const float Tolerance = 0.00001f;

    private static PotionAimCalculator.Result Calculate(Vector2 drag)
    {
        return PotionAimCalculator.Calculate(drag, 100f, 0.15f, 1f, 5f);
    }

    private static void AssertInvalid(PotionAimCalculator.Result result)
    {
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Direction, Is.EqualTo(Vector2.zero));
        Assert.That(result.Intensity, Is.Zero);
        Assert.That(result.Distance, Is.Zero);
    }

    [TestCase(0f)]
    [TestCase(10f)]
    [TestCase(15f)]
    public void DragAtOrBelowDeadZone_IsInvalid(float dragX)
    {
        AssertInvalid(Calculate(new Vector2(dragX, 0f)));
    }

    [Test]
    public void HorizontalDrag_HasCorrectDirectionAndDistance()
    {
        var result = Calculate(new Vector2(50f, 0f));
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Direction, Is.EqualTo(Vector2.right));
        Assert.That(result.Intensity, Is.EqualTo(0.5f).Within(Tolerance));
        Assert.That(result.Distance, Is.EqualTo(3f).Within(Tolerance));
    }

    [Test]
    public void DiagonalDrag_HasNormalizedDirection()
    {
        var result = Calculate(new Vector2(-30f, 40f));
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Direction.x, Is.EqualTo(-0.6f).Within(Tolerance));
        Assert.That(result.Direction.y, Is.EqualTo(0.8f).Within(Tolerance));
        Assert.That(result.Direction.magnitude, Is.EqualTo(1f).Within(Tolerance));
    }

    [TestCase(100f)]
    [TestCase(200f)]
    [TestCase(float.MaxValue)]
    public void DragAtOrAboveMaximum_ClampsIntensityAndRange(float dragX)
    {
        var result = Calculate(new Vector2(dragX, 0f));
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Intensity, Is.EqualTo(1f));
        Assert.That(result.Distance, Is.EqualTo(5f));
        Assert.That(result.Direction, Is.EqualTo(Vector2.right));
    }

    [Test]
    public void SmallValidDrag_HasDistanceWithinLimits()
    {
        var result = Calculate(new Vector2(16f, 0f));
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Intensity, Is.EqualTo(0.16f).Within(Tolerance));
        Assert.That(result.Distance, Is.InRange(1f, 5f));
        Assert.That(result.Distance, Is.EqualTo(1.64f).Within(Tolerance));
    }

    [TestCase(0f, 0.15f, 1f, 5f)]
    [TestCase(-100f, 0.15f, 1f, 5f)]
    [TestCase(float.NaN, 0.15f, 1f, 5f)]
    [TestCase(float.PositiveInfinity, 0.15f, 1f, 5f)]
    [TestCase(100f, -0.1f, 1f, 5f)]
    [TestCase(100f, 1f, 1f, 5f)]
    [TestCase(100f, float.NaN, 1f, 5f)]
    [TestCase(100f, float.PositiveInfinity, 1f, 5f)]
    [TestCase(100f, 0.15f, -1f, 5f)]
    [TestCase(100f, 0.15f, 5f, 1f)]
    [TestCase(100f, 0.15f, float.NaN, 5f)]
    [TestCase(100f, 0.15f, float.PositiveInfinity, 5f)]
    [TestCase(100f, 0.15f, 1f, float.NaN)]
    [TestCase(100f, 0.15f, 1f, float.PositiveInfinity)]
    public void InvalidParameters_ReturnZeroInvalidResult(
        float radius, float deadZone, float minRange, float maxRange)
    {
        AssertInvalid(PotionAimCalculator.Calculate(
            Vector2.right * 50f, radius, deadZone, minRange, maxRange));
    }

    [TestCase(float.NaN, 0f)]
    [TestCase(0f, float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity, 0f)]
    public void NonFiniteDrag_ReturnsZeroInvalidResult(float x, float y)
    {
        AssertInvalid(Calculate(new Vector2(x, y)));
    }

    [Test]
    public void CustomParameters_AreUsedByCalculation()
    {
        var result = PotionAimCalculator.Calculate(Vector2.up * 10f, 20f, 0f, 2f, 10f);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Direction, Is.EqualTo(Vector2.up));
        Assert.That(result.Intensity, Is.EqualTo(0.5f).Within(Tolerance));
        Assert.That(result.Distance, Is.EqualTo(6f).Within(Tolerance));
    }
}
