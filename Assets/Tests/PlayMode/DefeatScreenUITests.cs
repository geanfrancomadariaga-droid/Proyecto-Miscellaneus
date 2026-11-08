using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Sustituye solo la frontera con SceneManager para no destruir el Test Runner.
public class DefeatScreenUITestDouble : DefeatScreenUI
{
    public bool Available;
    public int Checks, Requests, RequestedIndex;

    protected override bool CanLoadScene(int buildIndex)
    {
        Checks++;
        return Available;
    }

    protected override void ReloadScene(int buildIndex)
    {
        Requests++;
        RequestedIndex = buildIndex;
    }

    public bool ActualAvailability(int index) => base.CanLoadScene(index);
}

public class DefeatPanelActivationCounter : MonoBehaviour
{
    public int Openings;
    private void OnEnable() => Openings++;
}

public class DefeatScreenUITests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player, canvasObject, panel;
    private Health health;
    private PlayerDefeatController defeat;
    private DefeatScreenUITestDouble ui;
    private Button button;
    private DefeatPanelActivationCounter counter;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("DefeatUITestPlayer");
        player.SetActive(false);
        health = player.AddComponent<Health>();
        defeat = player.AddComponent<PlayerDefeatController>();
        typeof(PlayerDefeatController).GetField("playerHealth", Private).SetValue(defeat, health);
        player.SetActive(true);
        // Tests sincronicos: no avanzan frames ni necesitan los componentes de combate.
        canvasObject = new GameObject("DefeatUITestCanvas", typeof(Canvas));
        canvasObject.SetActive(false);
        panel = new GameObject("DefeatPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasObject.transform, false);
        counter = panel.AddComponent<DefeatPanelActivationCounter>();
        var buttonObject = new GameObject("RetryButton", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        button = buttonObject.GetComponent<Button>();
        ui = canvasObject.AddComponent<DefeatScreenUITestDouble>();
        Set("defeatController", defeat);
        Set("defeatPanel", panel);
        Set("retryButton", button);
        canvasObject.SetActive(true);
        counter.Openings = 0;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(player);
    }

    private void Set(string field, object value) =>
        typeof(DefeatScreenUI).GetField(field, Private).SetValue(ui, value);

    private Action DefeatedHandlers() =>
        (Action)typeof(PlayerDefeatController).GetField("Defeated", Private).GetValue(defeat);

    private void Kill() => health.TakeDamage(health.MaxHealth);

    private static void ExpectUnavailable() => LogAssert.Expect(LogType.Error,
        new Regex("^DefeatScreenUI: no se puede recargar la escena .*Build Profile.*"));

    [Test]
    public void Alive_PanelStartsHidden_AndCanvasStaysActive()
    {
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(canvasObject.activeInHierarchy && ui.isActiveAndEnabled, Is.True);
        button.onClick.Invoke();
        Assert.That(ui.Checks, Is.Zero);
        Assert.That(ui.Requests, Is.Zero);
    }

    [Test]
    public void DefeatedEvent_ShowsPanel_WithoutPausingOrDisablingUI()
    {
        float scale = Time.timeScale;
        Kill();
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(panel.activeInHierarchy, Is.True);
        Assert.That(counter.Openings, Is.EqualTo(1));
        Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
        Assert.That(canvasObject.activeInHierarchy && ui.isActiveAndEnabled, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(scale));
    }

    [Test]
    public void RepeatedNotificationAndReenable_DoNotOpenPanelAgain()
    {
        Kill();
        // Simula una notificacion repetida aunque el controlador ya la previene.
        DefeatedHandlers().Invoke();
        Kill();
        ui.enabled = false;
        ui.enabled = true;
        Assert.That(counter.Openings, Is.EqualTo(1));
        Assert.That(panel.activeSelf, Is.True);
    }

    [Test]
    public void EnabledAfterDefeat_ShowsPanelFromStoredState()
    {
        ui.enabled = false;
        Kill();
        Assert.That(panel.activeSelf, Is.False);
        ui.enabled = true;
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(counter.Openings, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisableOrDestroy_RemovesBothSubscriptions(bool destroy)
    {
        int externalClicks = 0;
        button.onClick.AddListener(() => externalClicks++);
        Assert.That(DefeatedHandlers().GetInvocationList().Length, Is.EqualTo(1));
        if (destroy) Object.DestroyImmediate(ui);
        else ui.enabled = false;
        Assert.That(DefeatedHandlers(), Is.Null);
        Kill();
        Assert.That(panel.activeSelf, Is.False);
        Assert.DoesNotThrow(() => button.onClick.Invoke());
        Assert.That(externalClicks, Is.EqualTo(1));
        // Inspeccionar llamadas preparadas comprueba retirada real, no solo un guard.
        var calls = (System.Collections.IList)typeof(UnityEngine.Events.UnityEventBase)
            .GetMethod("PrepareInvoke", Private).Invoke(button.onClick, null);
        Assert.That(calls.Count, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void RepeatedEnable_DoesNotAccumulateListeners()
    {
        for (int i = 0; i < 3; i++)
        {
            ui.enabled = false;
            ui.enabled = true;
        }
        Assert.That(DefeatedHandlers().GetInvocationList().Length, Is.EqualTo(1));
        Kill();
        // Una validacion fallida no bloquea el siguiente intento: revela duplicados.
        ExpectUnavailable();
        button.onClick.Invoke();
        Assert.That(ui.Checks, Is.EqualTo(1));
        Assert.That(ui.Requests, Is.Zero);
    }

    [Test]
    public void Disable_RemovesListenersFromOriginalReferences()
    {
        Set("defeatController", null);
        Set("retryButton", null);
        ui.enabled = false;
        Assert.That(DefeatedHandlers(), Is.Null);
        var calls = (System.Collections.IList)typeof(UnityEngine.Events.UnityEventBase)
            .GetMethod("PrepareInvoke", Private).Invoke(button.onClick, null);
        Assert.That(calls.Count, Is.Zero);
    }

    [TestCase("defeatController")]
    [TestCase("defeatPanel")]
    [TestCase("retryButton")]
    public void MissingReference_ReportsErrorAndDisablesOnlyComponent(string field)
    {
        ui.enabled = false;
        Set(field, null);
        LogAssert.Expect(LogType.Error, "DefeatScreenUI: asigna Defeat Controller, Defeat Panel y Retry Button " +
            "en el Inspector. El componente se desactivara.");
        Assert.DoesNotThrow(() => ui.enabled = true);
        Assert.That(ui.enabled, Is.False);
        Assert.That(canvasObject.activeInHierarchy, Is.True);
        Assert.That(DefeatedHandlers(), Is.Null);
        Assert.DoesNotThrow(Kill);
        Assert.DoesNotThrow(() => button.onClick.Invoke());
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PanelIsOwnerOrAncestor_ReportsErrorWithoutDeactivatingOwner(bool ancestor)
    {
        ui.enabled = false;
        if (ancestor) canvasObject.transform.SetParent(player.transform, false);
        Set("defeatPanel", ancestor ? player : canvasObject);
        LogAssert.Expect(LogType.Error, "DefeatScreenUI: Defeat Panel no puede ser este objeto ni un ancestro. " +
            "Coloca el componente en el Canvas y el panel como hijo.");
        ui.enabled = true;
        Assert.That(ui.enabled, Is.False);
        Assert.That(canvasObject.activeInHierarchy, Is.True);
        Assert.That(player.activeInHierarchy, Is.True);
    }

    [Test]
    public void Retry_UnavailableScene_LogsErrorWithoutRequest_AndAllowsNextAttempt()
    {
        Kill();
        ExpectUnavailable();
        button.onClick.Invoke();
        Assert.That(ui.Requests, Is.Zero);
        ui.Available = true;
        button.onClick.Invoke();
        Assert.That(ui.Checks, Is.EqualTo(2));
        Assert.That(ui.Requests, Is.EqualTo(1));
    }

    [Test]
    public void Retry_AvailableScene_RequestsActiveIndexOnce_WithoutRestoringHealth()
    {
        Kill();
        ui.Available = true;
        button.onClick.Invoke();
        button.onClick.Invoke();
        ui.enabled = false;
        ui.enabled = true;
        button.onClick.Invoke();
        Assert.That(ui.Checks, Is.EqualTo(1));
        Assert.That(ui.Requests, Is.EqualTo(1));
        Assert.That(ui.RequestedIndex, Is.EqualTo(SceneManager.GetActiveScene().buildIndex));
        Assert.That(health.IsDead && defeat.IsDefeated, Is.True);
    }

    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public void ActualValidation_RejectsInvalidBuildIndex(int index)
    {
        Assert.That(ui.ActualAvailability(index), Is.False);
    }

    [Test]
    public void ActualValidation_AcceptsEnabledBuildScenes_AndRejectsUpperBound()
    {
        int count = SceneManager.sceneCountInBuildSettings;
        Assert.That(count, Is.GreaterThan(0), "Se esperaba SampleScene habilitada en la lista de escenas.");
        for (int i = 0; i < count; i++)
            Assert.That(ui.ActualAvailability(i), Is.True, $"Indice {i}");
        Assert.That(ui.ActualAvailability(count), Is.False);
    }
}
