using qtLib.UIScripts.Base.Object.SubScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace qtLib.Extension.UI
{
    [DisallowMultipleComponent]
    public class UISwipeNestedDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private enum NestedDragMode
        {
            None,
            PageSwipe,
            ScrollView
        }

        [Header("References")]
        [SerializeField] private UISubSceneSwiper _swipe;
        [SerializeField] private ScrollRect _scrollRect;

        [Header("Optional Conflict Fix")]
        [Tooltip("Nếu ScrollView con có Horizontal = true, tạm tắt horizontal scroll khi user swipe ngang page.")]
        [SerializeField] private bool _disableChildHorizontalScrollWhenPageSwipe = true;

        [Tooltip("Nếu user đã được detect là page swipe ngang, tạm tắt vertical scroll để không kéo ngược sang hướng scroll.")]
        [SerializeField] private bool _disableChildVerticalScrollWhenPageSwipe = true;

        [SerializeField] private float _detectDirectionPixels = 8f;
        [SerializeField] private float _horizontalDominance = 1.1f;

        [Header("Click Guard")]
        [Tooltip("Khi bắt đầu drag/swipe, hủy pending click để tránh trigger Button/action lúc thả tay.")]
        [SerializeField] private bool _cancelClickAfterDrag = true;

        private Vector2 _startPointer;

        private bool _directionResolved;
        // private bool _startedForwardingToSwipe;
        private bool _swipeDragStarted;
        private bool _originalHorizontal;
        private bool _originalVertical;
        private bool _hasCachedScrollRectState;

        private NestedDragMode _dragMode = NestedDragMode.None;

        private void Reset()
        {
            _swipe = GetComponentInParent<UISubSceneSwiper>();
            _scrollRect = GetComponent<ScrollRect>();
        }

        private void Awake()
        {
            if (_swipe == null)
            {
                _swipe = GetComponentInParent<UISubSceneSwiper>();
            }

            if (_scrollRect == null)
            {
                _scrollRect = GetComponent<ScrollRect>();
            }
        }

        private void OnDisable()
        {
            RestoreScrollRectState();

            _directionResolved = false;
            _swipeDragStarted = false;
            _dragMode = NestedDragMode.None;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _startPointer = eventData.position;

            _directionResolved = false;
            _dragMode = NestedDragMode.None;
            _swipeDragStarted = false;

            CacheScrollRectState();

            CancelPendingClick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            CancelPendingClick(eventData);

            ResolveScrollRectConflict(eventData);

            if (_swipe == null || _swipe.IsTransitioning)
                return;

            if (_dragMode != NestedDragMode.PageSwipe)
                return;

            if (!_swipeDragStarted)
            {
                _swipeDragStarted = true;
                _swipe.ForwardBeginDrag(eventData);
            }

            _swipe.ForwardDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CancelPendingClick(eventData);

            if (_swipe != null &&
                _swipeDragStarted &&
                !_swipe.IsTransitioning)
            {
                _swipe.ForwardEndDrag(eventData);
            }

            RestoreScrollRectState();

            _directionResolved = false;
            _dragMode = NestedDragMode.None;
            _swipeDragStarted = false;
        }

        private void ResolveScrollRectConflict(PointerEventData eventData)
        {
            if (_directionResolved)
                return;

            Vector2 delta = eventData.position - _startPointer;

            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);

            if (absX < _detectDirectionPixels &&
                absY < _detectDirectionPixels)
                return;

            if (absX >= absY * _horizontalDominance)
            {
                _dragMode = NestedDragMode.PageSwipe;
                _directionResolved = true;

                DisableChildScrollForPageSwipe();
                return;
            }

            _dragMode = NestedDragMode.ScrollView;
            _directionResolved = true;
        }

        private void DisableChildScrollForPageSwipe()
        {
            if (_scrollRect == null)
            {
                return;
            }

            if (_disableChildHorizontalScrollWhenPageSwipe)
            {
                _scrollRect.horizontal = false;
            }

            if (_disableChildVerticalScrollWhenPageSwipe)
            {
                _scrollRect.vertical = false;
            }

            _scrollRect.StopMovement();
        }

        private void CacheScrollRectState()
        {
            if (_scrollRect == null)
            {
                _hasCachedScrollRectState = false;
                return;
            }

            _originalHorizontal = _scrollRect.horizontal;
            _originalVertical = _scrollRect.vertical;
            _hasCachedScrollRectState = true;
        }

        private void RestoreScrollRectState()
        {
            if (_scrollRect == null)
            {
                return;
            }

            if (!_hasCachedScrollRectState)
            {
                return;
            }

            _scrollRect.horizontal = _originalHorizontal;
            _scrollRect.vertical = _originalVertical;

            _hasCachedScrollRectState = false;
        }

        private void CancelPendingClick(PointerEventData eventData)
        {
            if (!_cancelClickAfterDrag)
                return;

            if (eventData == null)
                return;

            eventData.eligibleForClick = false;
            eventData.clickCount = 0;
            eventData.clickTime = 0;

            // KHÔNG clear pointerPress.
        }
    }
}