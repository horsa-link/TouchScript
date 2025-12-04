using TouchScript.Debugging.Loggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool LockHorizontal;
    public bool LockVertical;

    [SerializeField] private UnityEvent<Draggable> onDragBegin;
    [SerializeField] private UnityEvent<Draggable> onDragged;
    [SerializeField] private UnityEvent<Draggable> onDragEnd;

    public UnityEvent<Draggable> OnDragBegin => onDragBegin ??= new UnityEvent<Draggable>();
    public UnityEvent<Draggable> OnDragged => onDragged ??= new UnityEvent<Draggable>();
    public UnityEvent<Draggable> OnDragEnd => onDragEnd ??= new UnityEvent<Draggable>();

    public bool IsBeingDragged => isBeingDragged;
    public Vector3 OriginalPosition => originalPosition;

    private Vector2 delta = Vector2.zero;
    private bool isBeingDragged;
    private int pointerId = -1;

    private Transform originalParent;
    private Vector3 originalPosition;
    private float dragScaleFactor = 1;

    public void SetParent(Transform dragLayer)
    {
        if (originalParent == null)
        {
            originalParent = transform.parent;
            originalPosition = transform.position;
            transform.SetParent(dragLayer, true);
            UpdateDragFactor();
        }
    }

    public void ReturnToOriginalParent()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            originalParent = null;
        }
    }

    void IBeginDragHandler.OnBeginDrag(PointerEventData data)
    {
        if (!isBeingDragged)
        {
            pointerId = data.pointerId;
            isBeingDragged = true;
            onDragBegin?.Invoke(this);
            UpdateDragFactor();
        }
    }

    void IDragHandler.OnDrag(PointerEventData data)
    {
        if (isBeingDragged && pointerId == data.pointerId)
        {
            delta = Vector2.Scale(data.delta, transform.lossyScale / dragScaleFactor);
            if (LockHorizontal)
            {
                delta.x = 0;
            }
            if (LockVertical)
            {
                delta.y = 0;
            }
            transform.Translate(delta, Space.World);
            onDragged?.Invoke(this);
        }
    }

    void IEndDragHandler.OnEndDrag(PointerEventData data)
    {
        if (isBeingDragged && pointerId == data.pointerId)
        {
            pointerId = -1;
            isBeingDragged = false;
            onDragEnd?.Invoke(this);
        }
    }

    /// <summary>
    /// Calculates the factor needed to make dragging work correctly with different Canvas Scale modes.
    /// </summary>
    private void UpdateDragFactor()
    {
        dragScaleFactor = 1;

        var canvasScaler = GetComponentInParent<CanvasScaler>();
        Debug.Assert(canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPhysicalSize,
            "Unsupported Canvas Scale mode. Switch to \"Scale With Screen Size\" or \"Constant Pixel Size\"!");

        if (canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            return;
        }

        var displaySize = canvasScaler.GetComponent<Canvas>().renderingDisplaySize;
        var referenceResolution = canvasScaler.referenceResolution;
        var widthRatio = displaySize.x / referenceResolution.x;
        var heightRatio = displaySize.y / referenceResolution.y;

        switch (canvasScaler.screenMatchMode)
        {
            case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
                dragScaleFactor = Mathf.Lerp(widthRatio, heightRatio, canvasScaler.matchWidthOrHeight);
                break;
            case CanvasScaler.ScreenMatchMode.Expand:
                dragScaleFactor = Mathf.Min(widthRatio, heightRatio);
                break;
            case CanvasScaler.ScreenMatchMode.Shrink:
                dragScaleFactor = Mathf.Max(widthRatio, heightRatio);
                break;
        }

        ConsoleLogger.Log($"{this}: Calculated scale factor is {dragScaleFactor:F2}");
    }
}
