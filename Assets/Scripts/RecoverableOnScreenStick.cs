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
    private void OnApplicationFocus(bool hasFocus) { focused = hasFocus; Clear(); }
    private void OnApplicationPause(bool isPaused) { paused = isPaused; Clear(); }

    private void Clear()
    {
        ReleaseGesture();
        previous.Clear();
        screen = null;
        baseline = true;
    }

    private void ReleaseGesture()
    {
        SendValueToControl(Vector2.zero);
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
            Clear();
            screen = Touchscreen.current;
        }
        if (screen == null)
        {
            SendValueToControl(Vector2.zero);
            return;
        }
        // Tras habilitar, volver del fondo o cambiar de dispositivo, ignorar dedos ya apoyados.
        if (baseline)
        {
            RememberContacts();
            baseline = false;
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (state == GestureState.Active)
        {
            TouchControl tracked = null;
            foreach (var touch in screen.touches)
                if (touch.press.isPressed && touch.touchId.ReadValue() == activeId
                    && touch.startTime.ReadValue() == activeStart)
                    tracked = touch;
            if (tracked != null)
            {
                Vector2 position = tracked.position.ReadValue();
                // El rectangulo limita el INICIO, no el recorrido de un dedo ya identificado.
                // La mitad derecha sigue prohibida para no invadir sus controles.
                if (!InLeftSide(position))
                {
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
            // Buscar en este mismo muestreo: un reemplazo verificable no necesita
            // insertar un frame en cero. Sin candidato se detiene al final.
        }

        if (state == GestureState.RecoveryPending && now > recoveryDeadline)
        {
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
            if (!touch.press.isPressed || !IsNew(touch)
                || !InLeftSide(touch.startPosition.ReadValue())
                || !InLeftSide(touch.position.ReadValue()))
                continue;
            if (state == GestureState.Free && !InLeftArea(touch.startPosition.ReadValue()))
                continue;
            if (state == GestureState.RecoveryPending)
            {
                // La identidad es (ID, inicio): Android puede reutilizar un ID.
                // IsNew excluye dedos ya presentes en el ultimo muestreo activo.
                // No comparar el inicio con el reloj de Update: eventos encolados
                // pueden ser anteriores a ese reloj sin ser contactos antiguos.
                if (touch.startTime.ReadValue() < activeStart
                    || (touch.position.ReadValue() - lastPosition).sqrMagnitude
                        > recoveryRadiusPixels * recoveryRadiusPixels
                    || (touch.startPosition.ReadValue() - lastPosition).sqrMagnitude
                        > recoveryRadiusPixels * recoveryRadiusPixels)
                {
                    continue;
                }
            }
            candidate = touch;
            count++;
        }
        if (count > 1)
        {
            // No elegir arbitrariamente entre candidatos plausibles.
            Clear();
            return;
        }
        if (candidate != null)
        {
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
            SendValueToControl(Vector2.zero);
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
        SendValueToControl(Vector2.ClampMagnitude((local - origin) / Mathf.Max(1f, movementRange), 1f));
    }

}
