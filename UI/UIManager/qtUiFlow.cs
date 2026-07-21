using System;
using System.Collections.Generic;
using System.Threading;
using qtLib.CustomDebug;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.UIManager
{
    [Serializable]
    public class CurrentSession
    {
        // Public field is retained for compatibility with existing inspectors/tools.
        public List<qtMediator> allMediators = new List<qtMediator>();

        public void Add(qtMediator mediator)
        {
            if (mediator != null && !allMediators.Contains(mediator))
            {
                allMediators.Add(mediator);
            }
        }

        public void Remove(qtMediator mediator)
        {
            if (mediator != null)
            {
                allMediators.Remove(mediator);
            }
        }

        public void Clear()
        {
            allMediators.Clear();
        }
    }

    [DefaultExecutionOrder(-49)]
    public class qtUiFlow : MonoBehaviour
    {
        [SerializeField] protected CurrentSession _session = new CurrentSession();

        private readonly List<qtMediator> _mediatorCreated = new List<qtMediator>();
        private static int _busyOperationCount;
        private static int _raycastBlockingEnabled = 1;
        private static int _publishedRaycastBlockState;

        // Busy is implementation detail. Public callers should use TryRequest instead
        // of reading or assigning a global flag.
        private static bool IsBusy => Volatile.Read(ref _busyOperationCount) > 0;

        internal static bool ShouldBlockRaycasts =>
            IsBusy && Volatile.Read(ref _raycastBlockingEnabled) != 0;

        internal static event Action<bool> RaycastBlockStateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Interlocked.Exchange(ref _busyOperationCount, 0);
            Interlocked.Exchange(ref _raycastBlockingEnabled, 1);
            Interlocked.Exchange(ref _publishedRaycastBlockState, 0);
            RaycastBlockStateChanged = null;
        }

        private void Awake()
        {
            qtDependencyInjection.Add(this);
            _session ??= new CurrentSession();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Start()
        {
            var uiEvent = GetComponent<IUIEvent>();
            if (uiEvent == null)
            {
                throw new NotImplementedException("No IUIEvent component found.");
            }

            uiEvent.OnStart();
        }

        private void OnDestroy()
        {
            foreach (var mediator in _mediatorCreated)
            {
                mediator?.Dispose();
            }

            _mediatorCreated.Clear();
            _session?.Clear();
            ResetBusyCounter();
        }
        //
        // /// <summary>
        // /// Returns the newest reusable mediator. A mediator whose request is still
        // /// running is always reused, so repeated clicks join its shared Move operation.
        // /// </summary>
        // public static TMediator Request<TMediator>()
        //     where TMediator : qtMediator
        // {
        //     return Request<TMediator>();
        // }

        /// <summary>
        /// Set allowDuplicate only for a flow that intentionally supports concurrent
        /// instances of the same mediator/UI type.
        /// </summary>
        public static TMediator Request<TMediator>()
            where TMediator : qtMediator
        {
            var uiFlow = RequireInstance();
            var mediator = uiFlow.GetOrAdd<TMediator>();

            uiFlow._session ??= new CurrentSession();
            uiFlow._session.Add(mediator);
            return mediator;
        }

        // /// <summary>
        // /// Explicit escape hatch for screens that really need another concurrent
        // /// instance of the same mediator/UI type.
        // /// </summary>
        // public static TMediator RequestNew<TMediator>(bool needRequestNewData = true)
        //     where TMediator : qtMediator
        // {
        //     return Request<TMediator>(true);
        // }

        /// <summary>
        /// Button-handler friendly request. It rejects the action when another UI
        /// transition is busy or when the same mediator is already active/pending.
        /// </summary>
        public static bool TryRequest<TMediator>(
            out TMediator mediator)
            where TMediator : qtMediator
        {
            mediator = null;

            var uiFlow = qtDependencyInjection.Get<qtUiFlow>();
            if (uiFlow == null || IsBusy)
            {
                return false;
            }

            var current = uiFlow.Get<TMediator>();
            if (current != null &&
                (current.IsRequestInProgress || current.IsActive()))
            {
                return false;
            }

            mediator = uiFlow.GetOrAdd<TMediator>();

            uiFlow._session ??= new CurrentSession();
            uiFlow._session.Add(mediator);
            return mediator != null;
        }

        public static TMediator GetNewest<TMediator>() where TMediator : qtMediator
        {
            var uiFlow = qtDependencyInjection.Get<qtUiFlow>();
            return uiFlow != null ? uiFlow.Get<TMediator>() : null;
        }

        public static bool IsRequestInProgress<TMediator>()
            where TMediator : qtMediator
        {
            var mediator = GetNewest<TMediator>();
            return mediator != null && mediator.IsRequestInProgress;
        }

        /// <summary>
        /// Controls only the optional full-screen raycast blocker. The internal busy
        /// state and duplicate-request protection continue to work when this is false.
        /// </summary>
        public static void SetRaycastBlockingEnabled(bool enabled)
        {
            Interlocked.Exchange(ref _raycastBlockingEnabled, enabled ? 1 : 0);
            PublishRaycastBlockState();
        }

        public static void DisableRaycastBlocking()
        {
            SetRaycastBlockingEnabled(false);
        }

        public static void EnableRaycastBlocking()
        {
            SetRaycastBlockingEnabled(true);
        }

        internal static void BeginBusyOperation()
        {
            Interlocked.Increment(ref _busyOperationCount);
            PublishRaycastBlockState();
        }

        internal static void EndBusyOperation()
        {
            int current;
            int remaining;

            do
            {
                current = Volatile.Read(ref _busyOperationCount);
                if (current <= 0)
                {
                    Interlocked.Exchange(ref _busyOperationCount, 0);
                    PublishRaycastBlockState();
                    return;
                }

                remaining = current - 1;
            }
            while (Interlocked.CompareExchange(
                       ref _busyOperationCount,
                       remaining,
                       current) != current);

            PublishRaycastBlockState();
        }

        private TMediator GetOrAdd<TMediator>()
            where TMediator : qtMediator
        {
            var mediator = Get<TMediator>();

            if (mediator != null)
            {
                // Check the in-flight request before IsActive. The UI GameObject is
                // enabled before show animation completes, so IsActive alone is not a
                // safe double-click guard.
                if (mediator.IsRequestInProgress || mediator.IsActive())
                {
                    return mediator;
                }
            }

            return CreateMediator<TMediator>();
        }

        private TMediator CreateMediator<TMediator>() where TMediator : qtMediator
        {
            var mediator = Activator.CreateInstance(typeof(TMediator)) as TMediator;
            if (mediator == null)
            {
                throw new InvalidOperationException(
                    $"Could not create mediator '{typeof(TMediator).FullName}'. " +
                    "A public parameterless constructor is required.");
            }

            _mediatorCreated.Add(mediator);
            return mediator;
        }

        private TMediator Get<TMediator>() where TMediator : qtMediator
        {
            for (var i = _mediatorCreated.Count - 1; i >= 0; i--)
            {
                var mediator = _mediatorCreated[i];
                if (mediator != null && mediator.GetType() == typeof(TMediator))
                {
                    return mediator as TMediator;
                }
            }

            return null;
        }

        private static qtUiFlow RequireInstance()
        {
            var uiFlow = qtDependencyInjection.Get<qtUiFlow>();
            if (uiFlow == null)
            {
                throw new InvalidOperationException("qtUiFlow is not registered.");
            }

            return uiFlow;
        }

        private static void ResetBusyCounter()
        {
            Interlocked.Exchange(ref _busyOperationCount, 0);
            PublishRaycastBlockState();
        }

        private static void PublishRaycastBlockState()
        {
            // Publish the derived raycast state rather than exposing the busy flag.
            // Re-reading current values avoids an End/Begin race publishing stale data.
            var shouldBlock = ShouldBlockRaycasts;
            var nextState = shouldBlock ? 1 : 0;
            var previousState = Interlocked.Exchange(
                ref _publishedRaycastBlockState,
                nextState);

            if (previousState == nextState)
            {
                return;
            }

            var callbacks = RaycastBlockStateChanged;
            if (callbacks == null)
            {
                return;
            }

            foreach (var callbackDelegate in callbacks.GetInvocationList())
            {
                try
                {
                    ((Action<bool>)callbackDelegate).Invoke(shouldBlock);
                }
                catch (Exception exception)
                {
                    qtDebug.LogError(
                        $"qtUiFlow - raycast blocker callback failed: {exception}");
                }
            }
        }
    }
}