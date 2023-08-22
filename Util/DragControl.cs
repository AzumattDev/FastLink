using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FastLink.Util;

public class DragControl : MonoBehaviour, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform dragRectTransform = new();

    private void Start()
    {
        dragRectTransform = GetComponent<RectTransform>();
        dragRectTransform.anchoredPosition = FastLinkPlugin.UIAnchor.Value;
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragRectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        FastLinkPlugin.UIAnchor.Value = dragRectTransform.anchoredPosition;
    }
}

public class MerchAreaDragControl : MonoBehaviour, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform dragRectTransform = new();
    [SerializeField] private Button button = null!;

    private void Start()
    {
        dragRectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        dragRectTransform.anchoredPosition = FastLinkPlugin.MerchUIAnchor.Value;
    }

    public void OnDrag(PointerEventData eventData)
    {
        button.interactable = false;
        dragRectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        button.interactable = true;
        FastLinkPlugin.MerchUIAnchor.Value = dragRectTransform.anchoredPosition;
    }
}