using UnityEngine;
using UnityEngine.EventSystems;

namespace FastLink
{
    public class DragControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform target = null!;
        public bool shouldReturn;
        private bool isMouseDown;
        private Vector3 startMousePosition;
        private Vector3 startPosition;

        private void Start()
        {
            target = (RectTransform)transform;
        }

        private void Update()
        {
            if (!isMouseDown) return;
            Vector3 currentPosition = Input.mousePosition;
            Vector3 diff = currentPosition - startMousePosition;
            Vector3 pos = startPosition + diff;

            Vector2 Transform = new(target.rect.width * transform.root.lossyScale.x,
                target.rect.height * transform.root.lossyScale.y);

            Vector2 OffsetMin, OffsetMax;
            OffsetMin.x = pos.x - target.pivot.x * Transform.x;
            OffsetMin.y = pos.y - target.pivot.y * Transform.y;
            OffsetMax.x = pos.x + (1 - target.pivot.x) * Transform.x;
            OffsetMax.y = pos.y + (1 - target.pivot.y) * Transform.y;
            if (OffsetMin.x < 0)
                pos.x = target.pivot.x * Transform.x;
            else if (OffsetMax.x > Screen.width) pos.x = Screen.width - (1 - target.pivot.x) * Transform.x;
            if (OffsetMin.y < 0)
                pos.y = target.pivot.y * Transform.y;
            else if (OffsetMax.y > Screen.height) pos.y = Screen.height - (1 - target.pivot.y) * Transform.y;
            target.position = pos;
        }

        public void OnPointerDown(PointerEventData dt)
        {
            isMouseDown = true;
            Vector3 position = target.position;
            FastLinkPlugin.UIAnchor.Value = target.localPosition;
            startPosition = position;
            startMousePosition = Input.mousePosition;
        }

        public void OnPointerUp(PointerEventData dt)
        {
            isMouseDown = false;
            if (shouldReturn)
            {
                target.position = startPosition;
            }
        }
    }
}