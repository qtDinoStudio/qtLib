using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.UIManager
{
    [DefaultExecutionOrder(-50)]
    public class qtUiManager : MonoBehaviour
    {
        [SerializeField] private UILoader _mainCanvas;
        [SerializeField] private UILoader _overlaySceneCanvas;
        [SerializeField] private UILoader _canvasOnTop;
        [SerializeField] private CanvasGroup _imgSceneFading;
        [SerializeField] private CanvasGroup _imgPopupFading;

        private readonly List<CanvasGroup> _imgPopupFadings = new List<CanvasGroup>();

        public delegate void OnShowed(qtScene view);
        public OnShowed onShow;

        public delegate void OnHided(qtScene view);
        public OnHided onHide;

        private void Awake()
        {
            qtDependencyInjection.Add(this);
        }

        private void OnDestroy()
        {
            if (_imgSceneFading)
            {
                _imgSceneFading.DOKill();
            }

            foreach (var fading in _imgPopupFadings)
            {
                if (fading)
                {
                    fading.DOKill();
                }
            }
        }

        public async UniTask BeforeUIHide<TUI>() where TUI : qtUiObject
        {
            var uiName = typeof(TUI).Name;

            try
            {
                var loader = RequireLoader(typeof(TUI));
                await loader.InvokeBeforeHide();
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{uiName} - Before-hide callback failed: {exception}");
            }
        }

        public async UniTask<TUI> Show<TUI>(
            object param = null,
            Action<TUI> result = null,
            TUI preparedView = null)
            where TUI : qtUiObject
        {
            // Manager-level scope also protects callers that use qtUiManager.Show
            // directly instead of going through a qtMediator. Nested scopes are safe
            // because qtUiFlow tracks them with a counter.
            qtUiFlow.BeginBusyOperation();

            try
            {
                var uiName = typeof(TUI).Name;
                var loader = RequireLoader(typeof(TUI));
                TUI view = null;

                try
                {
                    if (preparedView)
                    {
                        view = preparedView;
                    }
                    else
                    {
                        view = (await loader.GetOrLoad(uiName)) as TUI;
                    }

                    if (!view)
                    {
                        qtDebug.LogError($"{uiName} - Loader returned no compatible UI instance.");
                        return null;
                    }

                    view.transform.SetAsLastSibling();

                    // This is idempotent when qtFlowTransition already prepared the same view.
                    view.PrepareForShow(param);
                    result?.Invoke(view);
                    await loader.InvokeBeforeShow(view);
                    await view.Show(param);
                    loader.InvokeAfterShow(view);

                    if (view is qtScene scene)
                    {
                        onShow?.Invoke(scene);
                    }

                    return view;
                }
                catch (Exception exception)
                {
                    if (view)
                    {
                        view.AbortPreparedShow();
                    }

                    qtDebug.LogError($"{uiName} - Show/load/animation failed: {exception}");
                    return null;
                }
            }
            finally
            {
                qtUiFlow.EndBusyOperation();
            }
        }

        public async UniTask<TUI> Load<TUI>(object param = null) where TUI : qtUiObject
        {
            // Keep direct preload calls inside the same global input lock as Show/Hide.
            // Nested calls from qtFlowTransition are safe because the lock is a counter.
            qtUiFlow.BeginBusyOperation();

            try
            {
                var loader = RequireLoader(typeof(TUI));
                var view = await loader.GetOrLoad(typeof(TUI).Name);
                return view as TUI;
            }
            finally
            {
                qtUiFlow.EndBusyOperation();
            }
        }

        public UILoader GetLoader<TUI>()
        {
            return GetLoader(typeof(TUI));
        }

        public UniTask BeforeUIHide(qtUiObject view)
        {
            return view == null ? UniTask.CompletedTask : OnBeforeUIHide(view);
        }

        protected virtual UniTask OnBeforeUIHide(qtUiObject view)
        {
            return UniTask.CompletedTask;
        }

        public async UniTask Hide(qtUiObject view)
        {
            if (!view)
            {
                return;
            }

            qtUiFlow.BeginBusyOperation();
            try
            {
                var loader = RequireLoader(view.GetType());
                await loader.Hide(view);

                if (view is qtScene scene)
                {
                    onHide?.Invoke(scene);
                }
            }
            finally
            {
                qtUiFlow.EndBusyOperation();
            }
        }

        public async UniTask SceneFadingIn(qtScene scene)
        {
            if (!_imgSceneFading || !scene)
            {
                return;
            }

            _imgSceneFading.DOKill();
            _imgSceneFading.gameObject.SetActive(true);
            _imgSceneFading.transform.SetAsLastSibling();

            await _imgSceneFading
                .DOFade(1f, scene.animOutTime)
                .SetUpdate(UpdateType.Normal, true)
                .ToUniTask();
        }

        public async UniTask SceneFadingOut(qtScene scene)
        {
            if (!_imgSceneFading || !scene)
            {
                return;
            }

            _imgSceneFading.DOKill();

            if (!_imgSceneFading.gameObject.activeInHierarchy)
            {
                _imgSceneFading.alpha = 1f;
                _imgSceneFading.gameObject.SetActive(true);
            }

            await _imgSceneFading
                .DOFade(0f, scene.animInTime)
                .SetUpdate(UpdateType.Normal, true)
                .OnComplete(() =>
                {
                    if (_imgSceneFading)
                    {
                        _imgSceneFading.gameObject.SetActive(false);
                    }
                })
                .ToUniTask();
        }

        public CanvasGroup PopupFadeIn(qtPopup popup)
        {
            if (!_imgPopupFading || !popup)
            {
                qtDebug.LogError("Popup fading template or popup is missing.");
                return null;
            }

            var fading = _imgPopupFadings.Find(item => item && !item.gameObject.activeInHierarchy);
            if (!fading)
            {
                fading = Instantiate(_imgPopupFading, _imgPopupFading.transform.parent);
                _imgPopupFadings.Add(fading);
            }

            fading.DOKill();
            fading.transform.SetAsLastSibling();
            popup.transform.SetAsLastSibling();
            fading.alpha = 0f;
            fading.gameObject.SetActive(true);

            fading
                .DOFade(0.8f, popup.animInTime)
                .SetUpdate(UpdateType.Normal, true);

            return fading;
        }

        public void PopupFadeOut(CanvasGroup fading, qtPopup popup)
        {
            if (!fading || !popup)
            {
                return;
            }

            fading.DOKill();

            if (!fading.gameObject.activeInHierarchy)
            {
                fading.alpha = 0.8f;
                fading.gameObject.SetActive(true);
            }

            fading
                .DOFade(0f, popup.animOutTime)
                .SetUpdate(UpdateType.Normal, true)
                .OnComplete(() =>
                {
                    if (fading)
                    {
                        fading.gameObject.SetActive(false);
                    }
                });
        }

        private UILoader GetLoader(Type uiType)
        {
            if (typeof(qtOverlayScene).IsAssignableFrom(uiType))
            {
                return _overlaySceneCanvas;
            }

            if (typeof(qtScene).IsAssignableFrom(uiType))
            {
                return _mainCanvas;
            }

            if (typeof(qtPopup).IsAssignableFrom(uiType))
            {
                return _canvasOnTop;
            }

            return null;
        }

        private UILoader RequireLoader(Type uiType)
        {
            var loader = GetLoader(uiType);
            if (!loader)
            {
                throw new InvalidOperationException(
                    $"No UI loader is configured for '{uiType?.FullName ?? "<null>"}'.");
            }

            return loader;
        }
    }
}
