using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace qtLib.UIScripts.Base.Object.SubScene
{
    public class UISubSceneSwiper : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private enum DragMode
        {
            None,
            Horizontal,
            Vertical
        }

        [Header("UI References")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform[] uiObjects;

        [Header("Initial State")]
        [SerializeField] private int initialIndex = 0;

        [Header("Swipe Settings")]
        [SerializeField] private float detectDirectionPixels = 8f;

        [Tooltip("Càng cao thì càng khó bị nhận nhầm swipe ngang khi đang scroll dọc.")]
        [SerializeField] private float horizontalDominance = 1.1f;

        [SerializeField] private float swipeThresholdPercent = 0.2f;
        [SerializeField] private float snapDuration = 0.18f;

        [Header("Click Guard")]
        [Tooltip("Khi bắt đầu drag/swipe, hủy pending click để tránh trigger Button/action lúc thả tay.")]
        [SerializeField] private bool cancelClickAfterDrag = true;

        /// <summary>
        /// Bắn khi target subscene được SetActive(true).
        /// Bắn cho cả swipe và GoToIndex.
        /// Param: fromIndex, toIndex.
        /// </summary>
        public event Action<int, int> onNextSubSceneActivated;

        /// <summary>
        /// Bắn khi previous subscene đã bị SetActive(false).
        /// Bắn cho cả swipe và GoToIndex.
        /// Param: fromIndex, toIndex.
        /// </summary>
        public event Action<int, int> onPreviousSubSceneHidden;

        /// <summary>
        /// Bắn sau khi transition hoàn tất.
        /// Bắn cho cả swipe và GoToIndex.
        /// Param: fromIndex, toIndex.
        /// </summary>
        public event Action<int, int> onTransitionCompleted;

        private int currentIndex;
        private int targetIndex = -1;

        // +1 = target nằm bên phải current, user kéo trái.
        // -1 = target nằm bên trái current, user kéo phải.
        private int targetOffset = 0;

        private float screenWidth;
        private Vector2 startPointer;

        private bool isDragging;
        private bool isAnimating;

        private DragMode dragMode = DragMode.None;

        private CancellationTokenSource animationCts;

        public int CurrentIndex => currentIndex;
        public int PageCount => uiObjects == null ? 0 : uiObjects.Length;

        /// <summary>
        /// True khi đang chạy animation transition.
        /// User swipe sẽ bị ignore khi property này true.
        /// </summary>
        public bool IsTransitioning => isAnimating || animationCts != null;

        private void Reset()
        {
            viewport = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (viewport == null)
            {
                viewport = GetComponent<RectTransform>();
            }

            currentIndex = initialIndex;

            UpdatePageSizes();
            ClampCurrentIndex();
            ShowOnlyCurrent();
        }

        private void OnEnable()
        {
            UpdatePageSizes();
            ClampCurrentIndex();
            ShowOnlyCurrent();
        }

        private void OnDisable()
        {
            CancelAnimation();

            isDragging = false;
            isAnimating = false;

            dragMode = DragMode.None;
            targetIndex = -1;
            targetOffset = 0;
        }

        private void OnDestroy()
        {
            CancelAnimation();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (viewport == null)
            {
                viewport = GetComponent<RectTransform>();
            }

            UpdatePageSizes();

            if (!isDragging && !IsTransitioning)
            {
                ShowOnlyCurrent();
            }
        }

        private void UpdatePageSizes()
        {
            if (viewport == null || uiObjects == null || uiObjects.Length == 0)
            {
                return;
            }

            screenWidth = viewport.rect.width;

            if (screenWidth <= 0f)
            {
                screenWidth = Screen.width;
            }

            if (screenWidth <= 0f)
            {
                screenWidth = 1f;
            }

            for (int i = 0; i < uiObjects.Length; i++)
            {
                RectTransform page = uiObjects[i];

                if (page == null)
                {
                    continue;
                }

                // Mỗi subscene full theo viewport.
                page.anchorMin = new Vector2(0f, 0f);
                page.anchorMax = new Vector2(1f, 1f);
                page.pivot = new Vector2(0.5f, 0.5f);
                page.sizeDelta = Vector2.zero;

                page.anchoredPosition = new Vector2(page.anchoredPosition.x, 0f);
            }
        }

        private void ClampCurrentIndex()
        {
            if (uiObjects == null || uiObjects.Length == 0)
            {
                currentIndex = 0;
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, uiObjects.Length - 1);
        }

        /// <summary>
        /// Chỉ reset visual.
        /// Không notify trong function này để tránh bắn sai lúc Awake, OnEnable, BeginDrag, swipe fail.
        /// </summary>
        private void ShowOnlyCurrent()
        {
            if (uiObjects == null || uiObjects.Length == 0)
            {
                return;
            }

            targetIndex = -1;
            targetOffset = 0;

            for (int i = 0; i < uiObjects.Length; i++)
            {
                RectTransform page = uiObjects[i];

                if (page == null)
                {
                    continue;
                }

                page.gameObject.SetActive(i == currentIndex);
                SetPageX(i, 0f);
            }
        }

        /// <summary>
        /// Dùng sau khi transition thành công.
        /// Tại đây previous scene thật sự bị hide.
        /// </summary>
        private void ShowCurrentAndHidePrevious(
            int previousIndex,
            int newIndex,
            bool notifyPreviousHidden
        )
        {
            if (uiObjects == null || uiObjects.Length == 0)
            {
                return;
            }

            targetIndex = -1;
            targetOffset = 0;

            for (int i = 0; i < uiObjects.Length; i++)
            {
                RectTransform page = uiObjects[i];

                if (page == null)
                {
                    continue;
                }

                if (i == newIndex)
                {
                    page.gameObject.SetActive(true);
                    SetPageX(i, 0f);
                    continue;
                }

                bool wasActive = page.gameObject.activeSelf;

                page.gameObject.SetActive(false);
                SetPageX(i, 0f);

                if (
                    notifyPreviousHidden &&
                    i == previousIndex &&
                    previousIndex != newIndex &&
                    wasActive
                )
                {
                    NotifyPreviousSubSceneHidden(previousIndex, newIndex);
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginDragInternal(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DragInternal(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndDragInternal(eventData);
        }

        public void ForwardBeginDrag(PointerEventData eventData)
        {
            BeginDragInternal(eventData);
        }

        public void ForwardDrag(PointerEventData eventData)
        {
            DragInternal(eventData);
        }

        public void ForwardEndDrag(PointerEventData eventData)
        {
            EndDragInternal(eventData);
        }
        
        private void BeginDragInternal(PointerEventData eventData)
        {
            CancelPendingClick(eventData);

            if (IsTransitioning)
                return;

            if (uiObjects == null || uiObjects.Length <= 1)
                return;

            if (!IsValidIndex(currentIndex))
                return;

            UpdatePageSizes();
            ShowOnlyCurrent();

            startPointer = GetLocalPointer(eventData);

            dragMode = DragMode.None;
            isDragging = true;
        }

        private void DragInternal(PointerEventData eventData)
        {
            CancelPendingClick(eventData);

            if (!isDragging)
            {
                return;
            }

            if (IsTransitioning)
            {
                return;
            }

            if (!IsValidIndex(currentIndex))
            {
                return;
            }

            Vector2 currentPointer = GetLocalPointer(eventData);
            Vector2 delta = currentPointer - startPointer;

            ResolveDragMode(delta);

            // Đã detect vertical thì không ghi nhận horizontal swipe nữa.
            if (dragMode == DragMode.Vertical)
            {
                return;
            }

            if (dragMode == DragMode.None)
            {
                return;
            }
            // Horizontal đã được lock theo trục,
            // nhưng vẫn cho đổi chiều trái/phải trong cùng một lần drag.
            if (Mathf.Abs(delta.x) < detectDirectionPixels)
            {
                ClearTargetPreview();
                return;
            }

            int desiredOffset = delta.x < 0f ? +1 : -1;

            if (!TryActivateTarget(desiredOffset, notifyActivated: true))
            {
                SetPageX(currentIndex, 0f);
                return;
            }

            float clampedDeltaX = ClampDeltaByTarget(delta.x);

            SetPageX(currentIndex, clampedDeltaX);
            SetPageX(targetIndex, targetOffset * screenWidth + clampedDeltaX);
        }

        private void EndDragInternal(PointerEventData eventData)
        {
            CancelPendingClick(eventData);

            if (!isDragging)
            {
                return;
            }

            isDragging = false;

            if (IsTransitioning)
            {
                isDragging = false;
                dragMode = DragMode.None;
                return;
            }

            Vector2 endPointer = GetLocalPointer(eventData);
            Vector2 delta = endPointer - startPointer;

            ResolveDragMode(delta);

            // Chỉ horizontal mới được xử lý page swipe.
            // Vertical hoặc None thì reset, không trigger transition.
            if (dragMode == DragMode.Vertical)
            {
                dragMode = DragMode.None;
                ShowOnlyCurrent();
                return;
            }

            if (dragMode == DragMode.None)
            {
                ShowOnlyCurrent();
                return;
            }

            if (Mathf.Abs(delta.x) < detectDirectionPixels)
            {
                dragMode = DragMode.None;
                ShowOnlyCurrent();
                return;
            }

            // Lấy chiều cuối cùng lúc thả tay.
            // User kéo trái rồi đổi sang phải thì kết quả cuối là phải.
            int desiredOffset = delta.x < 0f ? +1 : -1;

            if (!TryActivateTarget(desiredOffset, notifyActivated: true))
            {
                dragMode = DragMode.None;
                ShowOnlyCurrent();
                return;
            }

            float clampedDeltaX = ClampDeltaByTarget(delta.x);

            SetPageX(currentIndex, clampedDeltaX);
            SetPageX(targetIndex, targetOffset * screenWidth + clampedDeltaX);

            float threshold = screenWidth * swipeThresholdPercent;
            bool shouldSwitch = Mathf.Abs(clampedDeltaX) >= threshold;

            dragMode = DragMode.None;

            StartSnap(
                shouldSwitch: shouldSwitch,
                notifyTransitionCompleted: true,
                notifyPreviousHidden: true
            );
        }

        private void ResolveDragMode(Vector2 delta)
        {
            if (dragMode != DragMode.None)
                return;

            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);

            if (absX < detectDirectionPixels &&
                absY < detectDirectionPixels)
                return;

            if (absX >= absY * horizontalDominance)
            {
                dragMode = DragMode.Horizontal;
            }
            else
            {
                dragMode = DragMode.Vertical;
                ClearTargetPreview();
            }
        }

        private bool TryActivateTarget(int desiredOffset, bool notifyActivated)
        {
            int desiredIndex = currentIndex + desiredOffset;

            if (!IsValidIndex(desiredIndex))
            {
                ClearTargetPreview();
                return false;
            }

            if (targetIndex == desiredIndex && targetOffset == desiredOffset)
            {
                return true;
            }

            // Cho phép đổi chiều trong Horizontal:
            // target cũ bị tắt, target mới được bật.
            ClearTargetPreview();

            targetOffset = desiredOffset;
            targetIndex = desiredIndex;

            SetPageX(currentIndex, 0f);

            uiObjects[targetIndex].gameObject.SetActive(true);
            SetPageX(targetIndex, targetOffset * screenWidth);

            if (notifyActivated)
            {
                NotifyNextSubSceneActivated(currentIndex, targetIndex);
            }

            return true;
        }

        private void ClearTargetPreview()
        {
            if (targetIndex >= 0 &&
                targetIndex != currentIndex &&
                IsValidIndex(targetIndex))
            {
                // Đây chỉ là target preview bị tắt.
                // Không gọi onPreviousSubSceneHidden ở đây.
                uiObjects[targetIndex].gameObject.SetActive(false);
                SetPageX(targetIndex, 0f);
            }

            targetIndex = -1;
            targetOffset = 0;

            if (IsValidIndex(currentIndex))
            {
                SetPageX(currentIndex, 0f);
            }
        }

        private float ClampDeltaByTarget(float deltaX)
        {
            if (targetOffset == +1)
            {
                // Target ở bên phải, kéo từ 0 tới -screenWidth.
                return Mathf.Clamp(deltaX, -screenWidth, 0f);
            }

            if (targetOffset == -1)
            {
                // Target ở bên trái, kéo từ 0 tới +screenWidth.
                return Mathf.Clamp(deltaX, 0f, screenWidth);
            }

            return 0f;
        }

        private void StartSnap(
            bool shouldSwitch,
            bool notifyTransitionCompleted,
            bool notifyPreviousHidden
        )
        {
            if (!IsValidIndex(currentIndex))
            {
                ShowOnlyCurrent();
                return;
            }

            if (!IsValidIndex(targetIndex))
            {
                ShowOnlyCurrent();
                return;
            }

            CancellationTokenSource cts = RestartAnimationToken();

            SnapAsync(
                fromIndex: currentIndex,
                toIndex: targetIndex,
                offset: targetOffset,
                shouldSwitch: shouldSwitch,
                notifyTransitionCompleted: notifyTransitionCompleted,
                notifyPreviousHidden: notifyPreviousHidden,
                cts: cts
            ).Forget();
        }

        private async UniTask SnapAsync(
            int fromIndex,
            int toIndex,
            int offset,
            bool shouldSwitch,
            bool notifyTransitionCompleted,
            bool notifyPreviousHidden,
            CancellationTokenSource cts
        )
        {
            bool shouldNotifyTransitionCompletedAfterCleanup = false;
            int notifyFromIndex = -1;
            int notifyToIndex = -1;

            isAnimating = true;

            try
            {
                if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
                {
                    return;
                }

                CancellationToken token = cts.Token;

                RectTransform fromPage = uiObjects[fromIndex];
                RectTransform toPage = uiObjects[toIndex];

                float width = screenWidth;

                float fromStartX = fromPage.anchoredPosition.x;
                float toStartX = toPage.anchoredPosition.x;

                float fromEndX;
                float toEndX;

                if (shouldSwitch)
                {
                    fromEndX = -offset * width;
                    toEndX = 0f;
                }
                else
                {
                    fromEndX = 0f;
                    toEndX = offset * width;
                }

                if (snapDuration <= 0f)
                {
                    SetPageX(fromIndex, fromEndX);
                    SetPageX(toIndex, toEndX);
                }
                else
                {
                    float timer = 0f;

                    while (timer < snapDuration)
                    {
                        token.ThrowIfCancellationRequested();

                        timer += Time.unscaledDeltaTime;

                        float t = Mathf.Clamp01(timer / snapDuration);

                        // Smooth step.
                        t = t * t * (3f - 2f * t);

                        SetPageX(fromIndex, Mathf.Lerp(fromStartX, fromEndX, t));
                        SetPageX(toIndex, Mathf.Lerp(toStartX, toEndX, t));

                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                }

                token.ThrowIfCancellationRequested();

                SetPageX(fromIndex, fromEndX);
                SetPageX(toIndex, toEndX);

                int oldIndex = currentIndex;

                if (shouldSwitch)
                {
                    currentIndex = toIndex;

                    ShowCurrentAndHidePrevious(
                        previousIndex: oldIndex,
                        newIndex: currentIndex,
                        notifyPreviousHidden: notifyPreviousHidden
                    );
                }
                else
                {
                    // Swipe không đủ threshold:
                    // target preview bị tắt, không notify hide previous, không notify completed.
                    ShowOnlyCurrent();
                }

                if (shouldSwitch && notifyTransitionCompleted && oldIndex != currentIndex)
                {
                    shouldNotifyTransitionCompletedAfterCleanup = true;
                    notifyFromIndex = oldIndex;
                    notifyToIndex = currentIndex;
                }
            }
            catch (OperationCanceledException)
            {
                // Bị cancel bởi GoToIndex mới, Disable object, Destroy object, v.v.
            }
            catch (ObjectDisposedException)
            {
                // Có thể xảy ra nếu CTS bị dispose trong lúc await.
            }
            finally
            {
                if (animationCts == cts)
                {
                    animationCts = null;
                    isAnimating = false;

                    cts.Dispose();
                }
            }

            if (shouldNotifyTransitionCompletedAfterCleanup)
            {
                NotifyTransitionCompleted(notifyFromIndex, notifyToIndex);
            }
        }

        private CancellationTokenSource RestartAnimationToken()
        {
            CancelAnimation();

            animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            return animationCts;
        }

        private void CancelAnimation()
        {
            if (animationCts == null)
            {
                isAnimating = false;
                return;
            }

            CancellationTokenSource cts = animationCts;
            animationCts = null;

            try
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
            isAnimating = false;
        }

        private void NotifyNextSubSceneActivated(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }

            onNextSubSceneActivated?.Invoke(fromIndex, toIndex);
        }

        private void NotifyPreviousSubSceneHidden(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }

            onPreviousSubSceneHidden?.Invoke(fromIndex, toIndex);
        }

        private void NotifyTransitionCompleted(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }

            onTransitionCompleted?.Invoke(fromIndex, toIndex);
        }
        
        private void CancelPendingClick(PointerEventData eventData)
        {
            if (!cancelClickAfterDrag)
                return;

            if (eventData == null)
                return;

            eventData.eligibleForClick = false;
            eventData.clickCount = 0;
            eventData.clickTime = 0f;
        }

        private void SetPageX(int index, float x)
        {
            if (!IsValidIndex(index))
            {
                return;
            }

            RectTransform page = uiObjects[index];
            page.anchoredPosition = new Vector2(x, 0f);
        }

        private Vector2 GetLocalPointer(PointerEventData eventData)
        {
            if (viewport == null)
            {
                return eventData.position;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );

            return localPoint;
        }

        private bool IsValidIndex(int index)
        {
            return uiObjects != null
                   && index >= 0
                   && index < uiObjects.Length
                   && uiObjects[index] != null;
        }

        public void GoToIndex(int index)
        {
            GoToIndex(index, true);
        }

        /// <summary>
        /// Manual GoToIndex cũng notify:
        /// 1. onNextSubSceneActivated
        /// 2. onPreviousSubSceneHidden
        /// 3. onTransitionCompleted
        ///
        /// User swipe bị block khi đang transition,
        /// nhưng GoToIndex bằng code vẫn được phép override transition hiện tại.
        /// </summary>
        public void GoToIndex(int index, bool animated)
        {
            if (uiObjects == null || uiObjects.Length == 0)
            {
                return;
            }

            int newIndex = Mathf.Clamp(index, 0, uiObjects.Length - 1);

            if (!IsValidIndex(newIndex))
            {
                return;
            }

            // Manual call được phép override transition hiện tại.
            CancelAnimation();

            isDragging = false;
            dragMode = DragMode.None;

            UpdatePageSizes();
            ClampCurrentIndex();

            if (newIndex == currentIndex)
            {
                ShowOnlyCurrent();
                return;
            }

            int oldIndex = currentIndex;

            ShowOnlyCurrent();

            targetIndex = newIndex;

            // Index lớn hơn: target nằm bên phải, trượt sang trái.
            // Index nhỏ hơn: target nằm bên trái, trượt sang phải.
            targetOffset = targetIndex > currentIndex ? +1 : -1;

            SetPageX(currentIndex, 0f);

            uiObjects[targetIndex].gameObject.SetActive(true);
            SetPageX(targetIndex, targetOffset * screenWidth);

            NotifyNextSubSceneActivated(oldIndex, targetIndex);

            if (!animated)
            {
                currentIndex = targetIndex;

                ShowCurrentAndHidePrevious(
                    previousIndex: oldIndex,
                    newIndex: currentIndex,
                    notifyPreviousHidden: true
                );

                NotifyTransitionCompleted(oldIndex, currentIndex);
                return;
            }

            StartSnap(
                shouldSwitch: true,
                notifyTransitionCompleted: true,
                notifyPreviousHidden: true
            );
        }

        public void GoNext()
        {
            GoToIndex(currentIndex + 1);
        }

        public void GoPrevious()
        {
            GoToIndex(currentIndex - 1);
        }

        public int GetCurrentIndex()
        {
            return currentIndex;
        }
    }
}