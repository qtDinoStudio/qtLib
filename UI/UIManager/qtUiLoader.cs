using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;
using VInspector;

namespace qtLib.UI.UIManager
{
    public abstract partial class qtUiLoader<TUI> : qtUiLoader where TUI : qtUiObject
    {
        [ReadOnly] [SerializeField] protected RectTransform _rectCanvas;
        
        private readonly Dictionary<string, TUI> _uiElements = new Dictionary<string, TUI>();
        private readonly Dictionary<string, TUI> _loadedPrefabs = new Dictionary<string, TUI>();
        private readonly Dictionary<string, UniTaskCompletionSource<TUI>> _pendingPrefabLoads =
            new Dictionary<string, UniTaskCompletionSource<TUI>>();

        [SerializeField] protected List<TUI> _allElemental = new List<TUI>();

        public delegate void OnAddNew(qtUiLoader<TUI> loader, TUI newUI);
        public OnAddNew onAdd;

        public delegate void OnShow(qtUiLoader<TUI> loader, TUI newUI);
        public OnShow onAfterShow;
        public OnShow onAfterHided;

        public delegate UniTask OnBeforeShow(
            qtUiLoader<TUI> loader,
            TUI newUI);

        public OnBeforeShow onBeforeShow;

        public delegate UniTask OnBeforeHide();
        public OnBeforeHide onBeforeHide;

        public async UniTask<TUI> GetOrLoad(string uiName)
        {
            if (string.IsNullOrWhiteSpace(uiName))
            {
                throw new ArgumentException("UI resource name cannot be empty.", nameof(uiName));
            }

            var cachedView = Get(uiName);
            if (cachedView)
            {
                cachedView.PreInit(_rectCanvas);
                return cachedView;
            }

            // Share only the Resources request. Each concurrent caller still receives its
            // own UI instance, which is required for multiple popups of the same type.
            var prefab = await GetOrLoadPrefab(uiName);
            if (!prefab)
            {
                return null;
            }

            var view = await InstantiateUiObject(prefab, uiName);
            InvokeAdded(view);
            return view;
        }

        public TUI Get(string name)
        {
            if (_uiElements.TryGetValue(name, out var cachedView))
            {
                if (!cachedView)
                {
                    _uiElements.Remove(name);
                }
                else if (!cachedView.IsActive)
                {
                    return cachedView;
                }
            }

            // When several copies of the same popup were needed concurrently, reuse any
            // inactive instance instead of allocating another copy forever.
            for (var i = _allElemental.Count - 1; i >= 0; i--)
            {
                var candidate = _allElemental[i];
                if (!candidate)
                {
                    _allElemental.RemoveAt(i);
                    continue;
                }

                if (!candidate.IsActive && string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    Add(name, candidate);
                    return candidate;
                }
            }

            return null;
        }

        public async UniTask Hide(
            TUI ui,
            Action<TUI> anotherCallback = null)
        {
            if (!ui)
            {
                return;
            }

            await ui.Hide();

            // The old implementation waited for
            // `inactivePrevious && !activeInHierarchy`, which can never become true
            // when inactivePrevious is false.
            InvokeAfterHidden(ui);
            anotherCallback?.Invoke(ui);
        }

        internal void InvokeAfterShow(TUI view)
        {
            var callbacks = onAfterShow;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                try
                {
                    ((OnShow)callbackDelegate).Invoke(this, view);
                }
                catch (Exception exception)
                {
                    qtDebug.LogError($"{GetType().Name} - After-show callback failed: {exception}");
                }
            }
        }

        internal async UniTask InvokeBeforeShow(
            TUI view)
        {
            var callbacks = onBeforeShow;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                await ((OnBeforeShow)callbackDelegate)
                    .Invoke(this, view);
            }
        }

        internal async UniTask InvokeBeforeHide()
        {
            var callbacks = onBeforeHide;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                await ((OnBeforeHide)callbackDelegate).Invoke();
            }
        }

        private void InvokeAdded(TUI view)
        {
            var callbacks = onAdd;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                try
                {
                    ((OnAddNew)callbackDelegate).Invoke(this, view);
                }
                catch (Exception exception)
                {
                    qtDebug.LogError($"{GetType().Name} - Add callback failed: {exception}");
                }
            }
        }

        private void InvokeAfterHidden(TUI view)
        {
            var callbacks = onAfterHided;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                try
                {
                    ((OnShow)callbackDelegate).Invoke(this, view);
                }
                catch (Exception exception)
                {
                    qtDebug.LogError($"{GetType().Name} - After-hide callback failed: {exception}");
                }
            }
        }

        private UniTask<TUI> GetOrLoadPrefab(string uiName)
        {
            if (_loadedPrefabs.TryGetValue(uiName, out var cachedPrefab) && cachedPrefab)
            {
                return UniTask.FromResult(cachedPrefab);
            }

            // UniTaskCompletionSource supports multiple awaiters, unlike a raw UniTask.
            if (_pendingPrefabLoads.TryGetValue(uiName, out var pendingLoad))
            {
                return pendingLoad.Task;
            }

            var completionSource = new UniTaskCompletionSource<TUI>();
            _pendingPrefabLoads.Add(uiName, completionSource);
            LoadPrefab(uiName, completionSource).Forget();
            return completionSource.Task;
        }

        private async UniTask LoadPrefab(
            string uiName,
            UniTaskCompletionSource<TUI> completionSource)
        {
            TUI prefab = null;

            try
            {
                await UniTask.SwitchToMainThread();

                var request = Resources.LoadAsync<TUI>(uiName);
                while (!request.isDone)
                {
                    // ResourceRequest.asset is normally null until the request is complete.
                    await UniTask.Yield();
                }

                prefab = request.asset as TUI;
                if (!prefab)
                {
                    throw new UnityException($"Failed to load UI resource '{uiName}'.");
                }

                _loadedPrefabs[uiName] = prefab;
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{uiName} - {exception}");
            }

            RemovePendingPrefabLoad(uiName, completionSource);
            completionSource.TrySetResult(prefab);
        }

        private async UniTask<TUI> InstantiateUiObject(TUI prefab, string uiName)
        {
            if (!prefab)
            {
                throw new ArgumentNullException(nameof(prefab), $"Failed to instantiate '{uiName}'.");
            }

            var view = Instantiate(prefab, transform);
            view.gameObject.SetActive(false);
            view.PreInit(_rectCanvas);

            await UniTask.Yield();

            view.name = prefab.name;
            _allElemental.Add(view);
            Add(uiName, view);

            // Preserve the original lookup behavior for callers that use an explicit
            // Resources path but later query the prefab's asset name.
            if (!string.Equals(prefab.name, uiName, StringComparison.Ordinal))
            {
                Add(prefab.name, view);
            }

            return view;
        }

        private void Add(string key, TUI view)
        {
            _uiElements[key] = view;
        }

        private void RemovePendingPrefabLoad(
            string uiName,
            UniTaskCompletionSource<TUI> completionSource)
        {
            if (_pendingPrefabLoads.TryGetValue(uiName, out var current) &&
                ReferenceEquals(current, completionSource))
            {
                _pendingPrefabLoads.Remove(uiName);
            }
        }
    }

    public class qtUiLoader : MonoBehaviour
    {
    }
}
