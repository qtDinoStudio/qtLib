using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.UIManager
{
    [DefaultExecutionOrder(-49)]
    public class qtSceneMediator : MonoBehaviour
    {
        private qtLayerViewRegistry<qtScene> _viewRegistry;

        private void Awake()
        {
            qtDependencyInjection.Add(this);

            var uiManager = qtDependencyInjection.Get<qtUiManager>();
            var loader = uiManager != null ? uiManager.GetLoader<qtScene>() : null;
            if (uiManager == null || !loader)
            {
                qtDebug.LogError("qtSceneMediator - Scene loader is not configured.");
                return;
            }

            _viewRegistry = new qtLayerViewRegistry<qtScene>(uiManager, loader);
        }

        private void OnDestroy()
        {
            _viewRegistry?.Dispose();
            _viewRegistry = null;
        }
    }
}
