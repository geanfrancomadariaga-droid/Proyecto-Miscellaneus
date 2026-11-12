using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// Contactos sinteticos, pero procesamiento real del Input System y del modulo UI.
// No reproduce el driver Android: sus cancelaciones se inyectan explicitamente.
public class MovementTouchRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject canvasObject, eventObject, playerObject, enemyObject;
    private EventSystem events, previousEvents;
    private InputSystemUIInputModule module;
    private InputActionAsset actions;
    private InputActionReference pointReference, clickReference, moveReference;
    private Touchscreen screen;
    private RecoverableOnScreenStick stick;
    private PlayerController movement;
    private PlayerMeleeAttack attack;
    private Rigidbody2D body;
    private Health health, enemyHealth;
    private EnemyCombatState combat;
    private EnemyMeleeTelegraph telegraph;
    private EnemyMeleeStrike strike;
    private PlayerDefeatController defeat;
    private PotionAimInput aim;
    private Button button;
    private Vector2 origin, dragged, buttonPosition, aimPosition;
    private int clicks, aimFinished;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        clicks = aimFinished = 0;
        previousEvents = EventSystem.current;
        screen = InputSystem.AddDevice<Touchscreen>();
        actions = ScriptableObject.CreateInstance<InputActionAsset>();
        var ui = actions.AddActionMap("UI");
        var point = ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
        point.AddBinding("<Touchscreen>/touch*/position");
        var click = ui.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
        click.AddBinding("<Touchscreen>/touch*/press");
        ui.devices = new InputDevice[] { screen };
        pointReference = InputActionReference.Create(point);
        clickReference = InputActionReference.Create(click);
        eventObject = new GameObject("MovementRegressionEvents");
        eventObject.SetActive(false);
        events = eventObject.AddComponent<EventSystem>();
        module = eventObject.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = actions;
        module.point = pointReference;
        module.leftClick = clickReference;
        module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;

        canvasObject = new GameObject("MovementRegressionCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var left = Area("Left", new Vector2(.05f, .1f), new Vector2(.4f, .6f));
        left.SetActive(false);
        stick = left.AddComponent<RecoverableOnScreenStick>();
        Set(stick, "touchArea", (RectTransform)left.transform);
        Set(stick, "movementRange", Screen.width * .04f);
        left.SetActive(true);
        var x = Area("X", new Vector2(.6f, .1f), new Vector2(.7f, .4f));
        button = x.AddComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => clicks++);
        var right = Area("Aim", new Vector2(.75f, .1f), new Vector2(.98f, .6f));
        aim = right.AddComponent<PotionAimInput>();
        aim.AimFinished += result => { if (result.IsValid) aimFinished++; };

        var move = actions.AddActionMap("Player").AddAction("Move", InputActionType.Value,
            expectedControlLayout: "Vector2");
        // Ruta del dispositivo virtual creado por ESTE joystick; no usa mandos externos.
        move.AddBinding(stick.control.path);
        moveReference = InputActionReference.Create(move);
        // OnEnable del modulo habilita las acciones UI. Configurar TODOS los
        // mapas y bindings del asset antes de activarlo, incluido Player/Move.
        eventObject.SetActive(true);
        EventSystem.current = events;
        playerObject = new GameObject("MovementRegressionPlayer");
        playerObject.SetActive(false);
        body = playerObject.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        movement = playerObject.AddComponent<PlayerController>();
        Set(movement, "moveActionReference", moveReference);
        health = playerObject.AddComponent<Health>();
        attack = playerObject.AddComponent<PlayerMeleeAttack>();
        Set(attack, "attackButton", button);
        playerObject.AddComponent<CircleCollider2D>().radius = .05f;

        enemyObject = new GameObject("MovementRegressionEnemy");
        enemyObject.SetActive(false);
        enemyObject.transform.position = Vector2.one * 100f;
        enemyHealth = enemyObject.AddComponent<Health>();
        enemyObject.AddComponent<CircleCollider2D>().radius = .1f;
        combat = enemyObject.AddComponent<EnemyCombatState>();
        Set(combat, "playerAttack", attack);
        telegraph = enemyObject.AddComponent<EnemyMeleeTelegraph>();
        Set(telegraph, "player", playerObject.transform);
        strike = enemyObject.AddComponent<EnemyMeleeStrike>();
        Set(strike, "playerHealth", health);
        defeat = playerObject.AddComponent<PlayerDefeatController>();
        Set(defeat, "playerHealth", health);
        Set(defeat, "playerMovement", movement);
        Set(defeat, "playerBody", body);
        Set(defeat, "playerAttack", attack);
        Set(defeat, "potionAimInput", aim);
        Set(defeat, "enemyCombatState", combat);
        Set(defeat, "enemyTelegraph", telegraph);
        Set(defeat, "enemyStrike", strike);
        playerObject.SetActive(true);
        enemyObject.SetActive(true);
        yield return null;
        Canvas.ForceUpdateCanvases();
        origin = new Vector2(Screen.width * .2f, Screen.height * .3f);
        dragged = origin + Vector2.right * (Screen.width * .05f);
        buttonPosition = new Vector2(Screen.width * .65f, Screen.height * .25f);
        aimPosition = new Vector2(Screen.width * .85f, Screen.height * .3f);
        // El callback establece foco para el fixture incluso en ejecucion batch.
        stick.SendMessage("OnApplicationFocus", true);
        Pump();
        Assert.That(events.currentInputModule, Is.SameAs(module));
        AssertRaycast(buttonPosition, button.gameObject);
        AssertRaycast(aimPosition, aim.gameObject);
    }

    private GameObject Area(string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return go;
    }

    private void AssertRaycast(Vector2 position, GameObject expected)
    {
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = position }, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject, Is.SameAs(expected));
    }

    [TearDown]
    public void TearDown()
    {
        // Detener solo la entrada creada por este fixture antes de liberarla.
        if (eventObject != null) eventObject.SetActive(false);
        if (actions != null) actions.Disable();
        Object.DestroyImmediate(playerObject);
        Object.DestroyImmediate(enemyObject);
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(eventObject);
        Object.DestroyImmediate(pointReference);
        Object.DestroyImmediate(clickReference);
        Object.DestroyImmediate(moveReference);
        Object.DestroyImmediate(actions);
        if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
        if (previousEvents != null && previousEvents.isActiveAndEnabled)
            EventSystem.current = previousEvents;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private void Queue(int id, TouchPhase phase, Vector2 position, double time = -1) =>
        InputSystem.QueueStateEvent(screen,
            new TouchState { touchId = id, phase = phase, position = position }, time);

    private void Pump()
    {
        InputSystem.Update();
        events.SendMessage("Update", SendMessageOptions.RequireReceiver);
        // Sigue en Update; no se llama Observe desde onAfterUpdate.
        stick.SendMessage("Update", SendMessageOptions.RequireReceiver);
        // OnScreenControl encola un evento: procesarlo antes de leer Player/Move.
        InputSystem.Update();
        if (movement.isActiveAndEnabled)
        {
            movement.SendMessage("Update", SendMessageOptions.RequireReceiver);
            movement.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
        }
    }

    private void Touch(int id, TouchPhase phase, Vector2 position)
    {
        Queue(id, phase, position);
        Pump();
    }

    private void BeginMovement()
    {
        Touch(11, TouchPhase.Began, origin);
        Touch(11, TouchPhase.Moved, dragged);
        AssertMoving();
    }

    private void AssertMoving()
    {
        Assert.That(stick.State, Is.EqualTo(RecoverableOnScreenStick.GestureState.Active));
        Assert.That(((Gamepad)stick.control.device).leftStick.ReadValue().x, Is.GreaterThan(.9f));
        Assert.That(movement.isActiveAndEnabled, Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(4f).Within(.001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(.001f));
        Assert.That(movement.LastMovementDirection, Is.EqualTo(Vector2.right));
    }

    private void AssertStopped()
    {
        Assert.That(((Gamepad)stick.control.device).leftStick.ReadValue(), Is.EqualTo(Vector2.zero));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    private Vector2 Reversed => origin - Vector2.right * (Screen.width * .05f);

    [UnityTest]
    public IEnumerator RepeatedXReversal_PlayerLoop_PreservesHeldContact()
    {
        // A diferencia de Pump, el PlayerLoop entrega los eventos y ejecuta los
        // Update reales. No inyectar Canceled: X no demuestra una cancelacion.
        Queue(11, TouchPhase.Began, origin);
        yield return null;
        yield return null;
        Queue(11, TouchPhase.Moved, dragged);
        yield return null;
        yield return null;
        yield return new WaitForFixedUpdate();
        AssertMoving();
        double start = screen.touches[0].startTime.ReadValue();
        for (int i = 0; i < 6; i++)
        {
            // Alternar el orden de llegada entre ambos dedos.
            if (i % 2 == 0) Queue(22, TouchPhase.Began, buttonPosition);
            Queue(11, TouchPhase.Moved, Reversed);
            if (i % 2 != 0) Queue(22, TouchPhase.Began, buttonPosition);
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();
            AssertReversed();
            Assert.That(clicks, Is.EqualTo(i));
            Queue(22, TouchPhase.Ended, buttonPosition);
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();
            AssertReversed();
            Assert.That(clicks, Is.EqualTo(i + 1));
            Assert.That(screen.touches[0].touchId.ReadValue(), Is.EqualTo(11));
            Assert.That(screen.touches[0].startTime.ReadValue(), Is.EqualTo(start));
            Assert.That(screen.touches[0].press.isPressed, Is.True);
            Queue(11, TouchPhase.Moved, dragged);
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();
            AssertMoving();
        }
        Queue(11, TouchPhase.Ended, dragged);
        yield return null;
        yield return null;
        yield return new WaitForFixedUpdate();
        AssertStopped();
    }

    private void AssertReversed()
    {
        Assert.That(stick.State, Is.EqualTo(RecoverableOnScreenStick.GestureState.Active));
        Assert.That(((Gamepad)stick.control.device).leftStick.ReadValue().x, Is.LessThan(-.9f));
        Assert.That(Read<InputAction>(movement, "moveAction").ReadValue<Vector2>().x, Is.LessThan(-.9f));
        Assert.That(body.linearVelocity.x, Is.EqualTo(-4f).Within(.001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(.001f));
        Assert.That(movement.LastMovementDirection, Is.EqualTo(Vector2.left));
    }

    [TestCase(0)] // Sin X.
    [TestCase(1)] // Down e inversion en el mismo muestreo.
    [TestCase(2)] // Inversion con X sostenido.
    [TestCase(3)] // Up e inversion en el mismo muestreo.
    [TestCase(4)] // Inversion despues de procesar Up.
    public void RapidReversal_XPhases_PreserveLeftIdentity(int scenario)
    {
        BeginMovement();
        var left = screen.touches[0];
        double start = left.startTime.ReadValue();
        if (scenario >= 2) Touch(22, TouchPhase.Began, buttonPosition);
        if (scenario == 1) Queue(22, TouchPhase.Began, buttonPosition);
        if (scenario == 3) Queue(22, TouchPhase.Ended, buttonPosition);
        if (scenario == 4) Touch(22, TouchPhase.Ended, buttonPosition);
        Queue(11, TouchPhase.Moved, Reversed);
        Pump();
        AssertReversed();
        Assert.That(left.touchId.ReadValue(), Is.EqualTo(11));
        Assert.That(left.startTime.ReadValue(), Is.EqualTo(start));
        Assert.That(left.startPosition.ReadValue(), Is.EqualTo(origin));
        Assert.That(left.press.isPressed, Is.True);
        Assert.That(clicks, Is.EqualTo(scenario >= 3 ? 1 : 0));
        if (scenario == 1 || scenario == 2)
        {
            Touch(22, TouchPhase.Ended, buttonPosition);
            AssertReversed();
            Assert.That(clicks, Is.EqualTo(1));
        }
        Touch(11, TouchPhase.Ended, Reversed);
        AssertStopped();
    }

    [UnityTest]
    public IEnumerator RepeatedXAndReversal_ActuallyDisplacesInBothDirections()
    {
        BeginMovement();
        for (int i = 0; i < 4; i++)
        {
            Queue(22, TouchPhase.Began, buttonPosition);
            Queue(11, TouchPhase.Moved, Reversed);
            Pump();
            AssertReversed();
            Assert.That(clicks, Is.EqualTo(i));
            float before = body.position.x;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(body.position.x, Is.LessThan(before));
            Queue(22, TouchPhase.Ended, buttonPosition);
            Queue(11, TouchPhase.Moved, dragged);
            Pump();
            AssertMoving();
            Assert.That(clicks, Is.EqualTo(i + 1));
            before = body.position.x;
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(body.position.x, Is.GreaterThan(before));
        }
        Touch(11, TouchPhase.Ended, dragged);
        AssertStopped();
    }

    [TestCase(11, false)]
    [TestCase(12, false)]
    [TestCase(11, true)]
    [TestCase(12, true)]
    public void ReplacementBeginsNearby_ThenReversesBeforeObservation(int replacementId, bool gap)
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Assert.That(Vector2.Distance(dragged, Reversed), Is.GreaterThan(40f),
            "Ejecutar con Game View de al menos 401 pixeles de ancho.");
        Queue(11, TouchPhase.Canceled, dragged);
        if (gap)
        {
            Pump();
            AssertStopped();
        }
        Queue(replacementId, TouchPhase.Began, dragged);
        Queue(replacementId, TouchPhase.Moved, Reversed);
        Queue(22, TouchPhase.Ended, buttonPosition);
        Pump();
        AssertReversed();
        Assert.That(clicks, Is.EqualTo(1));
        Pump();
        AssertReversed(); // No requiere levantar y volver a apoyar.
        Touch(replacementId, TouchPhase.Ended, Reversed);
        AssertStopped();
    }

    [TestCase(TouchPhase.Moved)]
    [TestCase(TouchPhase.Stationary)]
    public void CanceledContact_ReturnsWithoutBegan_PreservesIdentity(TouchPhase phase)
    {
        BeginMovement();
        // Alejarse mas de 40 px del origen demuestra que startPosition no es
        // el punto de reaparicion. Se usan eventos reales, no campos del stick.
        var far = new Vector2(Screen.width * .45f, origin.y);
        Touch(11, TouchPhase.Moved, far);
        double start = screen.touches[0].startTime.ReadValue();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(11, TouchPhase.Canceled, far);
        AssertStopped();
        Queue(11, phase, phase == TouchPhase.Moved ? Reversed : far);
        Queue(22, TouchPhase.Ended, buttonPosition);
        Pump();
        Assert.That(screen.touches[0].startTime.ReadValue(), Is.EqualTo(start));
        Assert.That(screen.touches[0].startPosition.ReadValue(), Is.EqualTo(origin));
        if (phase == TouchPhase.Stationary) AssertMoving();
        Touch(11, TouchPhase.Moved, Reversed);
        AssertReversed();
        Touch(11, TouchPhase.Ended, Reversed);
        for (int i = 0; i < 4; i++) Pump();
        AssertStopped();
    }

    [Test]
    public void UnknownIdMovedWithoutBegan_IsIgnoredByTouchscreen()
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(11, TouchPhase.Canceled, dragged);
        Touch(77, TouchPhase.Moved, Reversed);
        AssertStopped(); // El stick no puede recuperar un evento descartado por Touchscreen.
        foreach (var touch in screen.touches)
            Assert.That(touch.press.isPressed && touch.touchId.ReadValue() == 77, Is.False);
    }

    [Test]
    public void RightReplacementStartingOnX_CannotRecoverEvenAfterMovingLeft()
    {
        BeginMovement();
        Queue(11, TouchPhase.Canceled, dragged);
        Queue(22, TouchPhase.Began, buttonPosition);
        Queue(22, TouchPhase.Moved, Reversed);
        Pump();
        AssertStopped();
        Pump();
        AssertStopped();
    }

    [Test]
    public void ReversalAndX_WithRealBombAndFire_ConsumesOneChargeAndHits()
    {
        var template = new GameObject("MovementRegressionBomb");
        var source = template.AddComponent<BombProjectile>();
        source.enabled = false; // Avance determinista, tambien en el clon.
        template.AddComponent<FireExplosion>();
        var throwObject = new GameObject("MovementRegressionThrow");
        throwObject.SetActive(false);
        var thrower = throwObject.AddComponent<PotionThrowController>();
        var charges = playerObject.AddComponent<EquippedPotionCharges>();
        charges.Initialize(3);
        Set(thrower, "aimInput", aim);
        Set(thrower, "charges", charges);
        Set(thrower, "playerHealth", health);
        Set(thrower, "enemyHealth", enemyHealth);
        Set(thrower, "projectilePrefab", source);
        BombProjectile launched = null;
        try
        {
            throwObject.SetActive(true);
            BeginMovement();
            Touch(33, TouchPhase.Began, aimPosition);
            Touch(33, TouchPhase.Moved, aimPosition + Vector2.right * 50f);
            Assert.That(aim.IsAiming && aim.CurrentResult.IsValid, Is.True);
            var result = aim.CurrentResult;
            Queue(22, TouchPhase.Began, buttonPosition);
            Queue(11, TouchPhase.Moved, Reversed);
            Pump();
            AssertReversed();
            Touch(22, TouchPhase.Ended, buttonPosition);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(aim.CurrentResult.Direction, Is.EqualTo(result.Direction));
            Assert.That(charges.RemainingCharges, Is.EqualTo(3));
            // X golpeo lejos del enemigo. Situarlo ahora en el destino de la bomba.
            enemyObject.transform.position = playerObject.transform.position
                + (Vector3)(result.Direction * result.Distance);
            Touch(33, TouchPhase.Ended, aimPosition + Vector2.right * 50f);
            Assert.That(aimFinished, Is.EqualTo(1));
            Assert.That(charges.RemainingCharges, Is.EqualTo(2));
            var bombs = new List<BombProjectile>();
            foreach (var bomb in Object.FindObjectsByType<BombProjectile>(FindObjectsSortMode.None))
                if (bomb.name == "MovementRegressionBomb(Clone)") bombs.Add(bomb);
            Assert.That(bombs.Count, Is.EqualTo(1));
            launched = bombs[0];
            launched.Advance(1f);
            Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(90));
            AssertReversed();
            Touch(11, TouchPhase.Ended, Reversed);
            AssertStopped();
        }
        finally
        {
            Object.DestroyImmediate(throwObject);
            // Limpiar incluso si una asercion fallo antes de encontrar el clon.
            foreach (var bomb in Object.FindObjectsByType<BombProjectile>(FindObjectsSortMode.None))
                if (bomb.name == "MovementRegressionBomb(Clone)")
                    Object.DestroyImmediate(bomb.gameObject);
            Object.DestroyImmediate(template);
        }
    }

    [UnityTest]
    public IEnumerator SingleFinger_ActuallyDisplacesBody_ThenStopsOnRelease()
    {
        BeginMovement();
        float before = body.position.x;
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(body.position.x, Is.GreaterThan(before));
        Touch(11, TouchPhase.Ended, dragged);
        AssertStopped();
        for (int i = 0; i < 4; i++) Pump();
        AssertStopped();
    }

    [Test]
    public void XDown_Up_AndRepeatedClicks_KeepMovement()
    {
        BeginMovement();
        for (int i = 0; i < 8; i++)
        {
            Touch(22, TouchPhase.Began, buttonPosition);
            AssertMoving();
            Assert.That(clicks, Is.EqualTo(i), "X dispara al soltar, no al mantener.");
            Touch(22, TouchPhase.Ended, buttonPosition);
            AssertMoving();
            Assert.That(clicks, Is.EqualTo(i + 1), "Click real entregado por UI.");
        }
        Assert.That(Read<int>(attack, "consecutiveAttacks"), Is.GreaterThan(0));
    }

    [TestCase(1, .4f)]
    [TestCase(2, .4f)]
    [TestCase(3, .8f)]
    [TestCase(4, .8f)]
    [TestCase(5, 1.2f)]
    [TestCase(6, 3f)]
    public void RecoveryAndSixthHitFatigue_BlockOnlyAttack(int hits, float seconds)
    {
        BeginMovement();
        enemyObject.transform.position = Vector2.right;
        Physics2D.SyncTransforms();
        float now = Time.time + 10f;
        for (int i = 0; i < hits; i++)
        {
            Call(attack, "AttackAt", now);
            now = Read<float>(attack, "nextAttackAt");
            Pump();
            AssertMoving();
        }
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100 - hits * 5));
        Assert.That(now - Read<float>(attack, "lastAttackAt"), Is.EqualTo(seconds).Within(.0001f));
        Call(attack, "AttackAt", now - .001f);
        Pump();
        AssertMoving();
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(100 - hits * 5));
        Call(attack, "AttackAt", now);
        Pump();
        AssertMoving();
        Assert.That(enemyHealth.CurrentHealth, Is.EqualTo(95 - hits * 5));
    }

    [TestCase(12)]
    [TestCase(11)]
    public void ReplacementAlreadyPresent_WithXHeld_DoesNotInsertZero(int replacementId)
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Queue(11, TouchPhase.Canceled, dragged);
        Queue(replacementId, TouchPhase.Began, dragged);
        Pump();
        AssertMoving();
        Touch(22, TouchPhase.Ended, buttonPosition);
        AssertMoving();
        Touch(replacementId, TouchPhase.Ended, dragged);
        AssertStopped();
    }

    [TestCase(12)]
    [TestCase(11)]
    public void ReplacementAfterGap_RecoversOriginalOrigin(int replacementId)
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(11, TouchPhase.Canceled, dragged);
        AssertStopped(); // No extrapolar movimiento sin un dedo verificable.
        Assert.That(stick.State, Is.EqualTo(RecoverableOnScreenStick.GestureState.RecoveryPending));
        Touch(replacementId, TouchPhase.Began, dragged);
        AssertMoving();
        Touch(22, TouchPhase.Ended, buttonPosition);
        AssertMoving();
    }

    [Test]
    public void BufferedReplacement_OlderThanObservationButNewerThanContact_Recovers()
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        double eventTime = screen.lastUpdateTime + .000001;
        // La observacion avanza aunque el contacto permanezca quieto.
        Pump();
        Assert.That(eventTime, Is.LessThan(InputState.currentTime));
        Queue(11, TouchPhase.Canceled, dragged, eventTime);
        Queue(12, TouchPhase.Began, dragged, eventTime);
        Pump();
        AssertMoving();
    }

    [Test]
    public void ReleaseWhileXAndAimRemainHeld_DoesNotAdoptEitherContact()
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(33, TouchPhase.Began, aimPosition);
        Touch(11, TouchPhase.Ended, dragged);
        AssertStopped();
        for (int i = 0; i < 4; i++) Pump();
        AssertStopped();
        Touch(22, TouchPhase.Ended, buttonPosition);
        Touch(33, TouchPhase.Ended, aimPosition);
        AssertStopped();
    }

    [Test]
    public void ExistingNearbyFinger_IsNotAReplacement()
    {
        BeginMovement();
        Touch(44, TouchPhase.Began, dragged + Vector2.one);
        Touch(11, TouchPhase.Canceled, dragged);
        Pump();
        AssertStopped();
    }

    [Test]
    public void DistantNewLeftFinger_IsNotAReplacement()
    {
        BeginMovement();
        Queue(11, TouchPhase.Canceled, dragged);
        Queue(44, TouchPhase.Began, dragged + Vector2.up * 60f);
        Pump();
        Pump();
        AssertStopped();
    }

    [Test]
    public void ExistingContactOutsideStartRectangle_RemainsTrackedOnLeft()
    {
        BeginMovement();
        var outside = new Vector2(Screen.width * .45f, dragged.y);
        Touch(11, TouchPhase.Moved, outside);
        AssertMoving();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(22, TouchPhase.Ended, buttonPosition);
        AssertMoving();
        Touch(11, TouchPhase.Ended, outside);
        AssertStopped();
    }

    [Test]
    public void NewContactOutsideStartRectangle_CannotStartMovement()
    {
        Touch(11, TouchPhase.Began, new Vector2(Screen.width * .45f, origin.y));
        Touch(11, TouchPhase.Moved, new Vector2(Screen.width * .46f, origin.y));
        AssertStopped();
    }

    [Test]
    public void MovingIntoRightHalf_CancelsAndCannotStealRightControl()
    {
        BeginMovement();
        Touch(11, TouchPhase.Moved, buttonPosition);
        AssertStopped();
        Touch(11, TouchPhase.Moved, dragged);
        AssertStopped();
    }

    [Test]
    public void RightControlContact_CannotRecoverLostJoystick_EvenIfDraggedLeft()
    {
        BeginMovement();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(11, TouchPhase.Canceled, dragged);
        Touch(22, TouchPhase.Moved, dragged);
        AssertStopped();
        Touch(22, TouchPhase.Ended, dragged);
        AssertStopped();
    }

    [Test]
    public void AmbiguousReplacements_AreNotChosenArbitrarily()
    {
        BeginMovement();
        Queue(11, TouchPhase.Canceled, dragged);
        Queue(12, TouchPhase.Began, dragged);
        Queue(13, TouchPhase.Began, dragged + Vector2.one);
        Pump();
        Pump();
        AssertStopped();
    }

    [UnityTest]
    public IEnumerator ReleaseAndExpiredRecovery_NoPhantomMovement_AndNewGestureWorks()
    {
        BeginMovement();
        Touch(11, TouchPhase.Ended, dragged);
        AssertStopped();
        yield return new WaitForSecondsRealtime(.16f);
        Pump();
        Pump();
        AssertStopped();
        BeginMovement();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RightAim_WithMovementAndOptionalX_RemainsIndependent(bool withX)
    {
        BeginMovement();
        Touch(33, TouchPhase.Began, aimPosition);
        Touch(33, TouchPhase.Moved, aimPosition + Vector2.right * 50f);
        Assert.That(aim.IsAiming, Is.True);
        Assert.That(aim.CurrentResult.IsValid, Is.True);
        var direction = aim.CurrentResult.Direction;
        AssertMoving();
        if (withX)
        {
            Touch(22, TouchPhase.Began, buttonPosition);
            AssertMoving();
            Touch(22, TouchPhase.Ended, buttonPosition);
            AssertMoving();
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(aim.IsAiming, Is.True);
            Assert.That(aim.CurrentResult.Direction, Is.EqualTo(direction));
        }
        Touch(33, TouchPhase.Ended, aimPosition + Vector2.right * 50f);
        Assert.That(aimFinished, Is.EqualTo(1));
        AssertMoving();
        Touch(11, TouchPhase.Ended, dragged);
        AssertStopped();
    }

    [UnityTest]
    public IEnumerator EnemyWarning_AllowsActualDisplacementDuringAttackRecovery()
    {
        BeginMovement();
        enemyObject.transform.position = Vector2.right;
        Physics2D.SyncTransforms();
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(22, TouchPhase.Ended, buttonPosition);
        Assert.That(combat.CurrentState, Is.EqualTo(EnemyCombatState.Mode.DueloCercano));
        Call(telegraph, "TickAt", Time.time);
        Assert.That(telegraph.IsWarning, Is.True);
        float before = body.position.x;
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(body.position.x, Is.GreaterThan(before));
        AssertMoving();
        Assert.That(health.IsDead, Is.False);
    }

    [Test]
    public void NonlethalDamageAllowsMovement_RealDefeatDisablesControls()
    {
        BeginMovement();
        health.TakeDamage(10);
        Pump();
        AssertMoving();
        Touch(33, TouchPhase.Began, aimPosition);
        Assert.That(aim.IsAiming, Is.True);
        health.TakeDamage(90);
        Pump();
        Assert.That(defeat.IsDefeated, Is.True);
        Assert.That(movement.enabled || attack.enabled || aim.enabled, Is.False);
        Assert.That(aim.IsAiming, Is.False);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
        Touch(22, TouchPhase.Began, buttonPosition);
        Touch(22, TouchPhase.Ended, buttonPosition);
        Assert.That(Read<int>(attack, "consecutiveAttacks"), Is.Zero);
    }
}
