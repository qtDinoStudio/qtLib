using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using qtLib.CustomDebug;
using qtLib.Helper;

namespace qtLib.UI.UIManager
{
    /// <summary>
    /// Non-MonoBehaviour helper shared by the scene and overlay-scene mediators.
    /// </summary>
    internal sealed class qtLayerViewRegistry<TView> : IDisposable where TView : qtUiObject
    {
        private readonly qtUiManager _uiManager;
        private readonly UILoader _viewLoader;
        private readonly List<TView> _allViews = new List<TView>();
        private bool _isDisposed;

        public qtLayerViewRegistry(qtUiManager uiManager, UILoader viewLoader)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _viewLoader = viewLoader
                ? viewLoader
                : throw new ArgumentNullException(nameof(viewLoader));

            _viewLoader.onBeforeShow += BeforeViewShow;
            _viewLoader.onAfterHided += AfterViewHidden;
            _viewLoader.onBeforeHide += BeforeViewHide;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _viewLoader.onBeforeShow -= BeforeViewShow;
            _viewLoader.onAfterHided -= AfterViewHidden;
            _viewLoader.onBeforeHide -= BeforeViewHide;
            _allViews.Clear();
        }

        private async UniTask BeforeViewShow(
            qtUiLoader<qtUiObject> loader,
            qtUiObject newUI)
        {
            if (newUI is not TView newView)
            {
                return;
            }

            qtDebug.Log($"<color=yellow>Show: {newView.GetType()}</color>");
            await UniTask.SwitchToMainThread();

            for (var i = _allViews.Count - 1; i >= 0; i--)
            {
                var previousView = _allViews[i];
                if (!previousView || previousView == newView)
                {
                    _allViews.RemoveAt(i);
                    continue;
                }

                await _uiManager.Hide(previousView);
            }

            if (!_allViews.Contains(newView))
            {
                _allViews.Add(newView);
            }
        }

        private void AfterViewHidden(
            qtUiLoader<qtUiObject> loader,
            qtUiObject hiddenUI)
        {
            if (hiddenUI is not TView view)
            {
                return;
            }

            // inactivePrevious=false intentionally leaves the GameObject active.
            if (!view || !view.gameObject.activeInHierarchy)
            {
                _allViews.Remove(view);
            }
        }

        private async UniTask BeforeViewHide()
        {
            await UniTask.SwitchToMainThread();

            for (var i = _allViews.Count - 1; i >= 0; i--)
            {
                var view = _allViews[i];
                if (!view || !view.gameObject)
                {
                    _allViews.RemoveAt(i);
                    continue;
                }

                await _uiManager.BeforeUIHide(view);
            }
        }
    }
}
