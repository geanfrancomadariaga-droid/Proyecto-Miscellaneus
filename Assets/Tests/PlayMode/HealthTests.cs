using NUnit.Framework;
using UnityEngine;

public class HealthTests
{
    private GameObject testObject;
    private Health health;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("HealthTests");
        testObject.SetActive(false);

        health = testObject.AddComponent<Health>();

        testObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
            UnityEngine.Object.DestroyImmediate(testObject);
    }

    [Test]
    public void InitialHealth_Is100()
    {
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(health.MaxHealth, Is.EqualTo(100));
        Assert.That(health.IsDead, Is.False);
    }

    [Test]
    public void TakeDamage_30_Leaves70Health()
    {
        health.TakeDamage(30);

        Assert.That(health.CurrentHealth, Is.EqualTo(70));
    }

    [Test]
    public void Heal_CannotExceedMaxHealth()
    {
        health.TakeDamage(30);
        health.Heal(50);

        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
    }

    [Test]
    public void ExcessiveDamage_LeavesZeroHealth()
    {
        health.TakeDamage(150);

        Assert.That(health.CurrentHealth, Is.EqualTo(0));
        Assert.That(health.IsDead, Is.True);
    }

    [Test]
    public void ZeroAndNegativeAmounts_DoNotChangeHealth()
    {
        // Con salud parcial, tanto una cura como un dano serian observables.
        health.TakeDamage(30);

        health.TakeDamage(0);
        Assert.That(health.CurrentHealth, Is.EqualTo(70));
        health.TakeDamage(-10);
        Assert.That(health.CurrentHealth, Is.EqualTo(70));
        health.Heal(0);
        Assert.That(health.CurrentHealth, Is.EqualTo(70));
        health.Heal(-10);
        Assert.That(health.CurrentHealth, Is.EqualTo(70));
    }

    [Test]
    public void HealthChanged_ReportsCurrentAndMaxHealth()
    {
        int eventCount = 0;
        int reportedCurrent = -1;
        int reportedMax = -1;
        health.HealthChanged += (current, maximum) =>
        {
            eventCount++;
            reportedCurrent = current;
            reportedMax = maximum;
        };

        health.TakeDamage(30);

        Assert.That(eventCount, Is.EqualTo(1));
        Assert.That(reportedCurrent, Is.EqualTo(70));
        Assert.That(reportedMax, Is.EqualTo(100));

        health.Heal(10);

        Assert.That(eventCount, Is.EqualTo(2));
        Assert.That(reportedCurrent, Is.EqualTo(80));
        Assert.That(reportedMax, Is.EqualTo(100));

        health.TakeDamage(150);

        Assert.That(eventCount, Is.EqualTo(3));
        Assert.That(reportedCurrent, Is.EqualTo(0));
        Assert.That(reportedMax, Is.EqualTo(100));
    }

    [Test]
    public void Died_FiresExactlyOnce()
    {
        int deathCount = 0;
        health.Died += () => deathCount++;

        health.TakeDamage(30);
        Assert.That(deathCount, Is.EqualTo(0));

        health.TakeDamage(70);
        Assert.That(deathCount, Is.EqualTo(1));

        health.TakeDamage(10);
        health.TakeDamage(150);
        Assert.That(deathCount, Is.EqualTo(1));
    }

    [Test]
    public void DeadCharacter_CannotHeal()
    {
        health.TakeDamage(100);
        health.Heal(50);

        Assert.That(health.CurrentHealth, Is.EqualTo(0));
        Assert.That(health.IsDead, Is.True);
    }
}
