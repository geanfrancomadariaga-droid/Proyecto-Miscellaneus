using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// Registra eventos que realmente entrega el modulo, sin sustituir su procesamiento.
public sealed class PotionAimPointerProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int PointerId;
    public int Ups;
    public void OnPointerDown(PointerEventData data) => PointerId = data.pointerId;
    public void OnPointerUp(PointerEventData data) => Ups++;
}

public class PotionAimInputTests
{
    private GameObject canvasObject, eventObject, aimObject;
    private EventSystem events, previousEvents;
    private InputSystemUIInputModule module;
    private InputActionAsset actions;
    private InputActionReference pointReference, clickReference;
    private Touchscreen screen;
    private Mouse mouse;
    private PotionAimInput aim;
    private PotionAimPointerProbe probe;
    private int finished, canceled, updated;
    private PotionAimCalculator.Result result;
    private Vector2 origin;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        finished = canceled = updated = 0;
        result = default;
        previousEvents = EventSystem.current;
        screen = InputSystem.AddDevice<Touchscreen>();
        mouse = InputSystem.AddDevice<Mouse>();
        actions = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = actions.AddActionMap("AimTestUI");
        var point = map.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
        point.AddBinding("<Touchscreen>/touch*/position");
        point.AddBinding("<Mouse>/position");
        var click = map.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
        click.AddBinding("<Touchscreen>/touch*/press");
        click.AddBinding("<Mouse>/leftButton");
        actions.devices = new InputDevice[] { screen, mouse };
        pointReference = InputActionReference.Create(point);
        clickReference = InputActionReference.Create(click);
        eventObject = new GameObject("AimTestEvents");
        eventObject.SetActive(false);
        events = eventObject.AddComponent<EventSystem>();
        // Debe seguir habilitado para registrarse en OnEnable al activar el GameObject.
        module = eventObject.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = actions;
        module.point = pointReference;
        module.leftClick = clickReference;
        eventObject.SetActive(true);
        EventSystem.current = events;
        canvasObject = new GameObject("AimTestCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        aimObject = new GameObject("AimTestArea", typeof(RectTransform), typeof(Image));
        aimObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)aimObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        aim = aimObject.AddComponent<PotionAimInput>();
        probe = aimObject.AddComponent<PotionAimPointerProbe>();
        aim.AimFinished += value => { finished++; result = value; };
        aim.AimCanceled += () => canceled++;
        aim.AimUpdated += value => updated++;
        // EventSystem.Update selecciona y activa el modulo; el Canvas tambien
        // necesita estar preparado antes del primer contacto, no despues de Began.
        yield return null;
        Canvas.ForceUpdateCanvases();
        origin = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        AssertUIReady();
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = origin }, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), "El raycast inicial no encontro ningun Graphic.");
        Assert.That(hits[0].gameObject, Is.SameAs(aimObject),
            "AimTestArea debe ser el primer destino del raycast.");
        Assert.That(hits[0].module, Is.SameAs(canvasObject.GetComponent<GraphicRaycaster>()));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(eventObject);
        Object.DestroyImmediate(pointReference);
        Object.DestroyImmediate(clickReference);
        Object.DestroyImmediate(actions);
        if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        // OnEnable/OnDisable mantienen el registro de los EventSystem activos.
        if (previousEvents != null && previousEvents.isActiveAndEnabled)
            EventSystem.current = previousEvents;
    }

    private void QueueTouch(int id, TouchPhase phase, float x)
    {
        InputSystem.QueueStateEvent(screen, new TouchState
        {
            touchId = id, phase = phase, position = origin + Vector2.right * x
        });
    }

    private void AssertUIReady()
    {
        Assert.That(EventSystem.current, Is.SameAs(events));
        Assert.That(events.isActiveAndEnabled, Is.True);
        Assert.That(module.IsActive(), Is.True);
        Assert.That(events.currentInputModule, Is.SameAs(module),
            "EventSystem.Update debe haber seleccionado el modulo antes del contacto.");
        Assert.That(module.actionsAsset, Is.SameAs(actions));
        Assert.That(module.point, Is.SameAs(pointReference));
        Assert.That(module.leftClick, Is.SameAs(clickReference));
        Assert.That(pointReference.action.enabled, Is.True);
        Assert.That(clickReference.action.enabled, Is.True);
    }

    private void Pump()
    {
        AssertUIReady();
        InputSystem.Update();
        AssertUIReady();
        // Ejecuta el Update real: TickModules y Process del modulo seleccionado.
        // No avanza otro frame de input entre Began/Ended y su entrega a la UI.
        events.SendMessage("Update", SendMessageOptions.RequireReceiver);
    }

    private void Touch(int id, TouchPhase phase, float x = 0f)
    {
        QueueTouch(id, phase, x);
        Pump();
    }

    private void BeginDrag()
    {
        Touch(41, TouchPhase.Began);
        Assert.That(aim.IsAiming, Is.True, "El PointerDown debe llegar por raycast y modulo UI.");
        Assert.That(probe.PointerId, Is.Not.EqualTo(41), "pointerId no debe confundirse con touchId.");
        Touch(41, TouchPhase.Moved, 50f);
        Assert.That(aim.CurrentResult.IsValid, Is.True);
    }

    private PointerEventData Pointer(int id, float x = 50f) => new PointerEventData(events)
    {
        pointerId = id, button = PointerEventData.InputButton.Left,
        position = origin + Vector2.right * x
    };

    private void AssertCanceled()
    {
        Assert.That(canceled, Is.EqualTo(1));
        Assert.That(finished, Is.Zero);
        Assert.That(aim.IsAiming, Is.False);
        Assert.That(aim.CurrentResult.IsValid, Is.False);
    }

    [Test]
    public void TouchEnded_ValidDrag_FinishesOnceWithOriginalCalculation()
    {
        BeginDrag();
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(probe.Ups, Is.EqualTo(1));
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(canceled, Is.Zero);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Direction, Is.EqualTo(Vector2.right));
        Assert.That(result.Intensity, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(result.Distance, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(aim.IsAiming, Is.False);
        aim.OnPointerUp(Pointer(probe.PointerId));
        aim.OnCancel(new BaseEventData(events));
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(canceled, Is.Zero);
    }

    [TestCase(0f)]
    [TestCase(10f)]
    [TestCase(15f)]
    public void TouchEnded_InsideDeadZone_FinishesWithInvalidResult(float x)
    {
        Touch(41, TouchPhase.Began);
        Touch(41, TouchPhase.Ended, x);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(result.IsValid, Is.False);
        Assert.That(canceled, Is.Zero);
    }

    [Test]
    public void TouchDraggedBackIntoDeadZone_FinishesInvalid()
    {
        BeginDrag();
        Touch(41, TouchPhase.Moved, 5f);
        Touch(41, TouchPhase.Ended, 5f);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void OtherTouchMovingAndEnding_DoesNotChangeOriginalGesture()
    {
        BeginDrag();
        int updates = updated;
        Touch(72, TouchPhase.Began, -60f);
        Touch(72, TouchPhase.Moved, -100f);
        Touch(72, TouchPhase.Ended, -100f);
        Assert.That(aim.IsAiming, Is.True);
        Assert.That(updated, Is.EqualTo(updates));
        Assert.That(finished + canceled, Is.Zero);
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(result.Direction, Is.EqualTo(Vector2.right));
    }

    [Test]
    public void OtherTouchCanceled_DoesNotCancelOriginalGesture()
    {
        BeginDrag();
        Touch(72, TouchPhase.Began, -60f);
        Touch(72, TouchPhase.Canceled, -60f);
        Assert.That(aim.IsAiming, Is.True);
        Assert.That(canceled, Is.Zero);
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(finished, Is.EqualTo(1));
    }

    [Test]
    public void DifferentPointerDragAndUp_AreIgnored()
    {
        BeginDrag();
        int updates = updated;
        var other = Pointer(probe.PointerId + 1, -100f);
        aim.OnDrag(other);
        aim.OnPointerUp(other);
        Assert.That(updated, Is.EqualTo(updates));
        Assert.That(aim.IsAiming, Is.True);
        Assert.That(finished + canceled, Is.Zero);
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(finished, Is.EqualTo(1));
    }

    [Test]
    public void ClickActionDisabled_DoesNotProduceValidFinish()
    {
        BeginDrag();
        // Esta accion pertenece solo al fixture, nunca al jugador o al joystick.
        module.leftClick.action.Disable();
        module.Process();
        Assert.That(finished, Is.Zero);
        // PassThrough puede no emitir canceled ni PointerUp al deshabilitarse.
        // El caso PointerUpWhileTouchStillPressed prueba esa frontera por separado.
    }

    [Test]
    public void TouchCanceled_ProducesPointerUpButNeverAimFinished()
    {
        BeginDrag();
        Touch(41, TouchPhase.Canceled, 50f);
        Assert.That(probe.Ups, Is.EqualTo(1));
        AssertCanceled();
        aim.OnPointerUp(Pointer(probe.PointerId));
        AssertCanceled();
    }

    [Test]
    public void CancelAndReusedSlotInOneInputUpdate_CannotFinishOldGesture()
    {
        BeginDrag();
        QueueTouch(41, TouchPhase.Canceled, 50f);
        QueueTouch(72, TouchPhase.Began, 60f);
        QueueTouch(72, TouchPhase.Ended, 100f);
        // Solo procesar estado: la cancelacion debe detectarse antes de la UI.
        InputSystem.Update();
        AssertCanceled();
        aim.OnPointerUp(Pointer(probe.PointerId));
        AssertCanceled();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TouchDeviceReset_CancelsWithoutFinishing(bool hardReset)
    {
        BeginDrag();
        // Se reinicia exclusivamente el dispositivo creado por esta prueba.
        InputSystem.ResetDevice(screen, hardReset);
        module.Process();
        AssertCanceled();
        aim.OnPointerUp(Pointer(probe.PointerId));
        AssertCanceled();
    }

    [Test]
    public void TouchDeviceRemoved_CancelsWithoutFinishing()
    {
        BeginDrag();
        InputSystem.RemoveDevice(screen);
        module.Process();
        AssertCanceled();
    }

    [Test]
    public void TouchDeviceDisabled_CancelsWithoutFinishing()
    {
        BeginDrag();
        InputSystem.DisableDevice(screen);
        module.Process();
        AssertCanceled();
    }

    [Test]
    public void PointerUpWhileTouchStillPressed_CancelsInsteadOfFinishing()
    {
        BeginDrag();
        // Simula la frontera UI cuando llega un Up sintetico sin Ended fisico.
        aim.OnPointerUp(Pointer(probe.PointerId));
        AssertCanceled();
        Touch(41, TouchPhase.Ended, 50f);
        AssertCanceled();
    }

    [Test]
    public void ExplicitUICancel_IsConsumedAndCannotFinishLater()
    {
        BeginDrag();
        var data = new BaseEventData(events);
        ExecuteEvents.Execute(aimObject, data, ExecuteEvents.cancelHandler);
        Assert.That(data.used, Is.True);
        aim.OnCancel(new BaseEventData(events));
        Touch(41, TouchPhase.Ended, 50f);
        AssertCanceled();
    }

    [Test]
    public void DisableDuringGesture_CancelsOnceAndAllowsNewGestureAfterEnable()
    {
        BeginDrag();
        aim.enabled = false;
        Touch(41, TouchPhase.Ended, 50f);
        AssertCanceled();
        aim.enabled = true;
        BeginDrag();
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(canceled, Is.EqualTo(1));
    }

    [TestCase("OnApplicationFocus", false)]
    [TestCase("OnApplicationPause", true)]
    public void LifecycleInterruption_CancelsOnce(string message, bool value)
    {
        BeginDrag();
        // Invoca el callback; no simula el sistema operativo Android.
        aim.SendMessage(message, value);
        aim.SendMessage(message, value);
        Touch(41, TouchPhase.Ended, 50f);
        AssertCanceled();
    }

    [Test]
    public void NewGestureAfterCancel_CanFinishNormally()
    {
        BeginDrag();
        Touch(41, TouchPhase.Canceled, 50f);
        AssertCanceled();
        BeginDrag();
        Touch(41, TouchPhase.Ended, 50f);
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(canceled, Is.EqualTo(1));
    }

    [TestCase(50f, true)]
    [TestCase(5f, false)]
    public void MouseRelease_PreservesValidAndInvalidResults(float x, bool valid)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = origin }.WithButton(MouseButton.Left));
        Pump();
        Assert.That(aim.IsAiming, Is.True);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + Vector2.right * x }.WithButton(MouseButton.Left));
        Pump();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + Vector2.right * x });
        Pump();
        Assert.That(probe.Ups, Is.EqualTo(1));
        Assert.That(finished, Is.EqualTo(1));
        Assert.That(result.IsValid, Is.EqualTo(valid));
        Assert.That(canceled, Is.Zero);
    }
}
