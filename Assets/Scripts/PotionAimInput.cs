using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Lee un gesto de apuntado UI; no lanza pociones.</summary>
public sealed class PotionAimInput : MonoBehaviour, IPointerDownHandler,
    IDragHandler, IPointerUpHandler, ICancelHandler
{
    [SerializeField, Min(0.01f)] private float maxDragRadius = 100f;
    [SerializeField, Range(0f, 0.99f)] private float deadZone = 0.15f;
    [SerializeField, Min(0f)] private float minRange = 1f;
    [SerializeField, Min(0f)] private float maxRange = 5f;

    public bool IsAiming { get; private set; }
    public PotionAimCalculator.Result CurrentResult { get; private set; }

    public event Action AimStarted;
    public event Action<PotionAimCalculator.Result> AimUpdated;
    // Puede entregar IsValid == false. El consumidor debe comprobarlo.
    public event Action<PotionAimCalculator.Result> AimFinished;
    public event Action AimCanceled;

    private int activePointerId;
    private RectTransform canvasRect;
    private Camera gestureCamera;
    private Vector2 startPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || IsAiming ||
            eventData.button != PointerEventData.InputButton.Left)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        canvasRect = canvas.rootCanvas.transform as RectTransform;
        gestureCamera = eventData.pressEventCamera;
        if (canvasRect == null || !TryGetLocalPosition(eventData, out startPosition))
        {
            ClearState();
            return;
        }

        activePointerId = eventData.pointerId;
        CurrentResult = default;
        IsAiming = true;
        // El modulo UI dirige Cancel al objeto seleccionado.
        eventData.selectedObject = gameObject;
        AimStarted?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        if (!TryCalculate(eventData, out var result))
        {
            CancelAim();
            return;
        }

        CurrentResult = result;
        AimUpdated?.Invoke(result);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        // Recalcula al soltar, incluso si no hubo eventos de arrastre.
        if (!TryCalculate(eventData, out var result))
        {
            CancelAim();
            return;
        }

        ClearState();
        AimFinished?.Invoke(result);
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!IsAiming)
            return;

        CancelAim();
        eventData.Use();
    }

    private bool IsActivePointer(PointerEventData eventData)
    {
        return IsAiming && eventData.pointerId == activePointerId &&
            eventData.button == PointerEventData.InputButton.Left;
    }

    private bool TryGetLocalPosition(PointerEventData eventData, out Vector2 position)
    {
        position = default;
        return canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, gestureCamera, out position);
    }

    private bool TryCalculate(PointerEventData eventData,
        out PotionAimCalculator.Result result)
    {
        result = default;
        if (!TryGetLocalPosition(eventData, out Vector2 position))
            return false;

        result = PotionAimCalculator.Calculate(position - startPosition,
            maxDragRadius, deadZone, minRange, maxRange);
        return true;
    }

    private void CancelAim()
    {
        if (!IsAiming)
            return;

        ClearState();
        AimCanceled?.Invoke();
    }

    private void ClearState()
    {
        IsAiming = false;
        activePointerId = default;
        CurrentResult = default;
        startPosition = default;
        canvasRect = null;
        gestureCamera = null;
    }

    private void OnDisable()
    {
        CancelAim();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelAim();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            CancelAim();
    }

}
