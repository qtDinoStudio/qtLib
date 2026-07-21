using Cysharp.Threading.Tasks;
using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.UIManager
{
    public class qtPopupMediator : MonoBehaviour
    {
        private UILoader _viewLoader;

        protected qtUiManager UiManager => qtDependencyInjection.Get<qtUiManager>();

        protected virtual void Awake()
        {
            qtDependencyInjection.Add(this);

            var manager = UiManager;
            if (manager == null)
            {
                qtDebug.LogError("qtPopupMediator - qtUiManager is not registered.");
                return;
            }

            _viewLoader = manager.GetLoader<qtPopup>();
            if (!_viewLoader)
            {
                qtDebug.LogError("qtPopupMediator - Popup loader is not configured.");
                return;
            }

            _viewLoader.onBeforeShow += BeforeViewShow;
            _viewLoader.onAfterShow += AfterViewShow;
            _viewLoader.onAfterHided += AfterViewHidden;
            _viewLoader.onBeforeHide += BeforeViewHide;
        }

        protected virtual void OnDestroy()
        {
            if (!_viewLoader)
            {
                return;
            }

            _viewLoader.onBeforeShow -= BeforeViewShow;
            _viewLoader.onAfterShow -= AfterViewShow;
            _viewLoader.onAfterHided -= AfterViewHidden;
            _viewLoader.onBeforeHide -= BeforeViewHide;
            _viewLoader = null;
        }

        protected virtual UniTask BeforeViewShow(
            qtUiLoader<qtUiObject> loader,
            qtUiObject newUI)
        {
            return UniTask.CompletedTask;
        }

        protected virtual void AfterViewShow(
            qtUiLoader<qtUiObject> loader,
            qtUiObject newUI)
        {
        }

        protected virtual void AfterViewHidden(
            qtUiLoader<qtUiObject> loader,
            qtUiObject newUI)
        {
        }

        protected virtual UniTask BeforeViewHide()
        {
            return UniTask.CompletedTask;
        }
    }
}
