using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// Prototipo sin visuales ni eventos Pointer. Lee contactos reales y escribe solo el stick.
/// Un touchId nuevo NO demuestra que sea el mismo dedo: la recuperacion es heuristica.
/// </summary>
[DisallowMultipleComponent]
public sealed class RecoverableOnScreenStick : OnScreenControl
{
    public enum GestureState { Free, Active, RecoveryPending }

    [InputControl(layout = "Vector2")]
    [SerializeField] private string outputPath = "<Gamepad>/leftStick";
    [Tooltip("Area UI de inicio. Obligatoria; no debe solaparse con controles derechos.")]
    [SerializeField] private RectTransform touchArea;
    [Tooltip("Recorrido en unidades locales del RectTransform, como en un Canvas escalado.")]
    [SerializeField, Min(1f)] private float movementRange = 50f;
    [SerializeField, Range(0.02f, 0.3f)] private float recoverySeconds = 0.12f;
    [Tooltip("Distancia maxima en pixeles de pantalla; provisional para el dispositivo de prueba.")]
    [SerializeField, Min(1f)] private float recoveryRadiusPixels = 40f;
    [Tooltip("Limite horizontal normalizado. Nunca permite capturar la mitad derecha.")]
    [SerializeField, Range(0.05f, 0.5f)] private float leftBoundary = 0.5f;

    public GestureState State => state;
    private GestureState state;
    private Touchscreen screen;
    private Canvas canvas;
    private readonly Dictionary<int, double> previous = new Dictionary<int, double>();
    private int activeId;
    private double activeStart;
    private Vector2 origin;
    private Vector2 lastPosition;
    private double recoveryDeadline;
    private bool baseline = true;
    private bool focused = true;
    private bool paused;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
    // Diagnostico temporal: no se compila ni se evalua en builds normales.
    private readonly Dictionary<TouchControl, (int id, double start, int phase)> diagnosticContacts = new Dictionary<TouchControl, (int, double, int)>();
    private readonly Dictionary<TouchControl, string> diagnosticRejections = new Dictionary<TouchControl, string>();
    private Vector2 diagnosticMovement;
    private Vector2Int diagnosticDirection;
    private double diagnosticNextMovement;
#endif

    protected override string controlPathInternal
    {
        get => outputPath;
        set => outputPath = value;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        focused = Application.isFocused;
        paused = false;
        Clear();
        canvas = touchArea == null ? null : touchArea.GetComponentInParent<Canvas>();
        if (touchArea == null || canvas == null || !(control is InputControl<Vector2>))
        {
            Debug.LogError("RecoverableOnScreenStick requiere Touch Area en un Canvas y una salida Vector2.", this);
            enabled = false;
            return;
        }
    }

    protected override void OnDisable()
    {
        Clear();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        Clear();
    }
    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        Trace(hasFocus ? "foco recuperado" : "foco perdido");
        Clear();
    }
    private void OnApplicationPause(bool isPaused)
    {
        paused = isPaused;
        Trace(isPaused ? "pausa" : "fin de pausa");
        Clear();
    }

    private void Clear()
    {
        Trace("clear (foco/pausa/dispositivo/limite/ambiguedad); ver evento anterior");
        ReleaseGesture();
        previous.Clear();
        screen = null;
        baseline = true;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        diagnosticContacts.Clear();
        diagnosticRejections.Clear();
#endif
    }

    private void ReleaseGesture()
    {
        Trace("release -> Free");
        SendMovement(Vector2.zero);
        state = GestureState.Free;
        activeId = 0;
        activeStart = 0;
        origin = lastPosition = Vector2.zero;
        recoveryDeadline = 0;
    }

    private Camera EventCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay
        ? null : canvas.worldCamera;

    private bool InLeftSide(Vector2 position)
    {
        return touchArea != null && canvas != null && touchArea.gameObject.activeInHierarchy
            && position.x >= 0f && position.x < Screen.width * leftBoundary
            && position.y >= 0f && position.y < Screen.height
            && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera != null);
    }

    private bool InLeftArea(Vector2 position) => InLeftSide(position)
        && RectTransformUtility.RectangleContainsScreenPoint(touchArea, position, EventCamera);

    private bool IsNew(TouchControl touch)
    {
        return !previous.TryGetValue(touch.touchId.ReadValue(), out double start)
            || start != touch.startTime.ReadValue();
    }

    private bool IsTracked(TouchControl touch) => touch.touchId.ReadValue() == activeId
        && touch.startTime.ReadValue() == activeStart;

    private void RememberContacts()
    {
        previous.Clear();
        foreach (var touch in screen.touches)
            if (touch.press.isPressed)
                previous[touch.touchId.ReadValue()] = touch.startTime.ReadValue();
    }

    private void Update()
    {
        Observe();
    }

    private void Observe()
    {
        if (!focused || paused)
        {
            return;
        }
        if (Touchscreen.current != screen)
        {
            Trace("cambio de Touchscreen.current");
            Clear();
            screen = Touchscreen.current;
        }
        if (screen == null)
        {
            SendMovement(Vector2.zero);
            return;
        }
        // Tras habilitar, volver del fondo o cambiar de dispositivo, ignorar dedos ya apoyados.
        if (baseline)
        {
            Trace("baseline: ignorar contactos ya presentes");
            RememberContacts();
            baseline = false;
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        TraceContacts();
        if (state == GestureState.Active)
        {
            TouchControl tracked = null;
            foreach (var touch in screen.touches)
                if (touch.press.isPressed && IsTracked(touch))
                    tracked = touch;
            if (tracked != null)
            {
                Vector2 position = tracked.position.ReadValue();
                // El rectangulo limita el INICIO, no el recorrido de un dedo ya identificado.
                // La mitad derecha sigue prohibida para no invadir sus controles.
                if (!InLeftSide(position))
                {
                    Trace("contacto activo fuera del lado izquierdo", tracked);
                    Clear();
                    return;
                }
                lastPosition = position;
                WriteMovement(position);
                RememberContacts();
                return;
            }
            state = GestureState.RecoveryPending;
            recoveryDeadline = now + recoverySeconds;
            Trace("perdida -> RecoveryPending");
            // Buscar en este mismo muestreo: un reemplazo verificable no necesita
            // insertar un frame en cero. Sin candidato se detiene al final.
        }

        if (state == GestureState.RecoveryPending && now > recoveryDeadline)
        {
            Trace("plazo expirado antes de evaluar candidatos");
            // Terminar la recuperacion no es un cambio de dispositivo/foco.
            // No ignorar el proximo gesto nuevo con otro baseline.
            ReleaseGesture();
            RememberContacts();
            return;
        }

        TouchControl candidate = null;
        int count = 0;
        foreach (var touch in screen.touches)
        {
            bool returning = state == GestureState.RecoveryPending && IsTracked(touch);
            if (!touch.press.isPressed)
                continue;
            if (!returning && !IsNew(touch))
            {
                TraceRejection(touch, "contacto recordado: !IsNew");
                continue;
            }
            if (!InLeftSide(touch.startPosition.ReadValue()) || !InLeftSide(touch.position.ReadValue()))
            {
                TraceRejection(touch, "origen o posicion fuera del lado izquierdo");
                continue;
            }
            if (state == GestureState.Free && !InLeftArea(touch.startPosition.ReadValue()))
            {
                TraceRejection(touch, "origen fuera del area inicial");
                continue;
            }
            if (state == GestureState.RecoveryPending && !returning)
            {
                // La identidad es (ID, inicio): Android puede reutilizar un ID.
                // IsNew excluye dedos ya presentes en el ultimo muestreo activo.
                // Validar donde REAPARECIO, no cuanto se movio despues: Began y
                // Moved pueden procesarse antes del mismo Update al invertir.
                // El mismo (ID, inicio) puede volver con Moved sin otro Began;
                // su startPosition sigue siendo el origen del gesto, no la perdida.
                if (touch.startTime.ReadValue() < activeStart)
                {
                    TraceRejection(touch, "inicio anterior al contacto activo");
                    continue;
                }
                if ((touch.startPosition.ReadValue() - lastPosition).sqrMagnitude
                    > recoveryRadiusPixels * recoveryRadiusPixels)
                {
                    TraceRejection(touch, "distancia de reaparicion mayor al radio");
                    continue;
                }
            }
            candidate = touch;
            count++;
        }
        if (count > 1)
        {
            Trace("multiples candidatos: recuperacion ambigua");
            // No elegir arbitrariamente entre candidatos plausibles.
            Clear();
            return;
        }
        if (candidate != null)
        {
            Trace(state == GestureState.Free ? "inicio -> Active" : "recuperacion -> Active", candidate);
            if (state == GestureState.Free)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(touchArea,
                    candidate.startPosition.ReadValue(), EventCamera, out origin);
            activeId = candidate.touchId.ReadValue();
            activeStart = candidate.startTime.ReadValue();
            lastPosition = candidate.position.ReadValue();
            state = GestureState.Active;
            WriteMovement(lastPosition);
        }
        else
        {
            // Nunca extrapolar movimiento mientras no haya contacto verificable.
            SendMovement(Vector2.zero);
        }
        RememberContacts();
    }

    private void WriteMovement(Vector2 position)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(touchArea,
            position, EventCamera, out Vector2 local))
        {
            Clear();
            return;
        }
        SendMovement(Vector2.ClampMagnitude((local - origin) / Mathf.Max(1f, movementRange), 1f));
    }

    private void SendMovement(Vector2 value)
    {
        SendValueToControl(value);
        TraceMovement(value);
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void Trace(string reason, TouchControl touch = null)
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        string contact = touch == null ? "" : $" candidateId={touch.touchId.ReadValue()} start={touch.startTime.ReadValue():R} press={touch.press.isPressed} phase={touch.phase.ReadValue()} origin={touch.startPosition.ReadValue()} position={touch.position.ReadValue()}";
        Debug.Log($"[JoystickRecovery] t={Time.realtimeSinceStartupAsDouble:F3} state={state} id={activeId} start={activeStart:R} movement={diagnosticMovement} remaining={recoveryDeadline - Time.realtimeSinceStartupAsDouble:F3} reason={reason}{contact}", this);
#endif
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void TraceRejection(TouchControl touch, string reason)
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (diagnosticRejections.TryGetValue(touch, out string previousReason) && previousReason == reason)
            return;
        diagnosticRejections[touch] = reason;
        Trace("rechazo: " + reason, touch);
#endif
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void TraceContacts()
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        foreach (var touch in screen.touches)
        {
            // Moved/Stationary no se registran por frame. Solo identidad y
            // presion; Ended y Canceled se distinguen al perder el contacto.
            var signature = (touch.touchId.ReadValue(), touch.startTime.ReadValue(),
                touch.press.isPressed ? -1 : (int)touch.phase.ReadValue());
            if (diagnosticContacts.TryGetValue(touch, out var old) && old.Equals(signature))
                continue;
            diagnosticContacts[touch] = signature;
            diagnosticRejections.Remove(touch);
            Trace("transicion de contacto " + touch.name, touch);
        }
#endif
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void TraceMovement(Vector2 value)
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        diagnosticMovement = value;
        var direction = new Vector2Int(value.x > .01f ? 1 : value.x < -.01f ? -1 : 0,
            value.y > .01f ? 1 : value.y < -.01f ? -1 : 0);
        // Solo cambios de sentido o parada; maximo cuatro registros/s.
        if (direction == diagnosticDirection || Time.realtimeSinceStartupAsDouble < diagnosticNextMovement)
            return;
        diagnosticDirection = direction;
        diagnosticNextMovement = Time.realtimeSinceStartupAsDouble + .25;
        Trace("vector enviado (cambio de sentido/parada)");
#endif
    }
}
