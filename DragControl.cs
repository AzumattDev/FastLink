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

            Rect rect = target.rect;
            Vector2 vector2 = new(rect.width * ((Component)this).transform.root.lossyScale.x,
                rect.height * ((Component)this).transform.root.lossyScale.y);

            Vector2 offsetMin, offsetMax;
            Vector2 pivot = target.pivot;
            offsetMin.x = pos.x - pivot.x * vector2.x;
            offsetMin.y = pos.y - pivot.y * vector2.y;
            offsetMax.x = pos.x + (1 - pivot.x) * vector2.x;
            offsetMax.y = pos.y + (1 - pivot.y) * vector2.y;
            if (offsetMin.x < 0)
                pos.x = target.pivot.x * vector2.x;
            else if (offsetMax.x > Screen.width) pos.x = Screen.width - (1 - target.pivot.x) * vector2.x;
            if (offsetMin.y < 0)
                pos.y = target.pivot.y * vector2.y;
            else if (offsetMax.y > Screen.height) pos.y = Screen.height - (1 - target.pivot.y) * vector2.y;
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