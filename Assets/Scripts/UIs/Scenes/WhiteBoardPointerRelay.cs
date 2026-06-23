using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class WhiteBoardPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public event Action<PointerEventData> PointerEnter;
    public event Action<PointerEventData> PointerExit;
    public event Action<PointerEventData> PointerDown;
    public event Action<PointerEventData> PointerUp;
    public event Action<PointerEventData> Drag;

    public void OnPointerEnter(PointerEventData eventData) => PointerEnter?.Invoke(eventData);
    public void OnPointerExit(PointerEventData eventData) => PointerExit?.Invoke(eventData);
    public void OnPointerDown(PointerEventData eventData) => PointerDown?.Invoke(eventData);
    public void OnPointerUp(PointerEventData eventData) => PointerUp?.Invoke(eventData);
    public void OnDrag(PointerEventData eventData) => Drag?.Invoke(eventData);
}
