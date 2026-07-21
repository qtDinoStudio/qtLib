using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.UIManager
{
    [DefaultExecutionOrder(-49)]
    public class qtOverlaySceneMediator : MonoBehaviour
    {
        private qtLayerViewRegistry<qtOverlayScene> _viewRegistry;

        private void Awake()
        {
            qtDependencyInjection.Add(this);

            var uiManager = qtDependencyInjection.Get<qtUiManager>();
            var loader = uiManager != null ? uiManager.GetLoader<qtOverlayScene>() : null;
            if (uiManager == null || !loader)
            {
                qtDebug.LogError("qtOverlaySceneMediator - Overlay loader is not configured.");
                return;
            }

            _viewRegistry = new qtLayerViewRegistry<qtOverlayScene>(uiManager, loader);
        }

        private void OnDestroy()
        {
            _viewRegistry?.Dispose();
            _viewRegistry = null;
        }
    }
}
