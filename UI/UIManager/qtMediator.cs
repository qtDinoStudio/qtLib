using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using qtLib.CustomDebug;
using qtLib.Helper;

namespace qtLib.UI.UIManager
{
    public abstract class qtMediator : IDisposable
    {
        protected CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        protected CancellationToken _cancellationToken =>
            _cancellationTokenSource != null
                ? _cancellationTokenSource.Token
                : CancellationToken.None;

        protected object _param;

        private sealed class MoveOperation
        {
            public readonly UniTaskCompletionSource<bool> ShowCompletionSource =
                new UniTaskCompletionSource<bool>();

            public readonly UniTaskCompletionSource<ParamOutput> ResultCompletionSource =
                new UniTaskCompletionSource<ParamOutput>();
        }

        private readonly object _moveGate = new object();
        private MoveOperation _moveOperation;
        private int _openingOperationCount;
        private bool _isDisposed;

        /// <summary>
        /// True from the first Move call until the UI returns a result or is closed.
        /// Repeated Move calls share the same operation instead of opening another UI.
        /// </summary>
        public bool IsRequestInProgress
        {
            get
            {
                lock (_moveGate)
                {
                    return _moveOperation != null;
                }
            }
        }

        /// <summary>
        /// True only while load/config/show/UISetup/animation is running. Waiting for
        /// a UI result is not an opening operation and does not keep the internal busy
        /// counter active.
        /// </summary>
        public bool IsOpening => Volatile.Read(ref _openingOperationCount) > 0;

        public abstract bool IsActive();

        public async UniTaskVoid Move()
        {
            try
            {
                await MoveAsync();
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - {exception}");
            }
        }

        /// <summary>
        /// Opens the UI and completes as soon as load/config/show/UISetup/animation has
        /// finished. Calls made while the same request is running await the same show.
        /// </summary>
        public async UniTask MoveAsync()
        {
            try
            {
                if (!IsRequestInProgress && IsActive())
                {
                    return;
                }

                var operation = GetOrStartSharedMove<NoOutput>();

                await operation.ShowCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - {exception}");
            }
        }

        /// <summary>
        /// Opens the UI once and completes when it produces a result or closes. Every
        /// concurrent caller joins the same operation and receives the same completion.
        /// </summary>
        public async UniTask<TOutput> Move<TOutput>()
            where TOutput : ParamOutput, new()
        {
            try
            {
                if (!IsRequestInProgress && IsActive())
                {
                    return null;
                }

                var operation = GetOrStartSharedMove<ParamOutput>();

                var result = await operation.ResultCompletionSource.Task;
                if (result == null)
                {
                    return null;
                }

                if (result is TOutput typedResult)
                {
                    return typedResult;
                }

                qtDebug.LogError(
                    $"{this} - Expected result '{typeof(TOutput).Name}', " +
                    $"but received '{result.GetType().Name}'.");
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - {exception}");
                return null;
            }
        }

        protected virtual UniTask<TOutput> WorkingForResult<TOutput>()
            where TOutput : ParamOutput, new()
        {
            return UniTask.FromResult(default(TOutput));
        }

        protected void _RefreshToken()
        {
            var previousSource = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
            CancelAndDispose(previousSource);
        }

        protected void BeginOpeningOperation()
        {
            Interlocked.Increment(ref _openingOperationCount);
        }

        protected void EndOpeningOperation()
        {
            var remaining = Interlocked.Decrement(ref _openingOperationCount);
            if (remaining < 0)
            {
                Interlocked.Exchange(ref _openingOperationCount, 0);
            }
        }

        /// <summary>
        /// Releases Move()/MoveAsync() after the UI has finished showing. The shared
        /// result operation can continue waiting in the background for Move&lt;TOutput&gt;.
        /// </summary>
        protected void CompleteSharedShow()
        {
            MoveOperation operation;

            lock (_moveGate)
            {
                operation = _moveOperation;
            }

            operation?.ShowCompletionSource.TrySetResult(true);
        }

        public virtual void Close()
        {
        }

        public virtual void Dispose()
        {
            DisposeCore();
            GC.SuppressFinalize(this);
        }

        protected abstract void RemoveEvent();

        private MoveOperation GetOrStartSharedMove<TOutPut>() where TOutPut : ParamOutput, new()
        {
            MoveOperation operation;
            var shouldStart = false;

            lock (_moveGate)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                operation = _moveOperation;
                if (operation == null)
                {
                    operation = new MoveOperation();
                    _moveOperation = operation;
                    shouldStart = true;
                }
            }

            // Start outside the lock. A custom implementation may complete
            // synchronously, and completion also needs the same gate.
            if (shouldStart)
            {
                ExecuteSharedMove<TOutPut>(operation).Forget();
            }

            return operation;
        }

        private async UniTask ExecuteSharedMove<TOutPut>(MoveOperation operation) where TOutPut : ParamOutput, new()
        {
            try
            {
                // Always run a result-capable operation. Move()/MoveAsync() is released
                // earlier by CompleteSharedShow(), while Move<TOutput>() can keep waiting.
                var result = await WorkingForResult<TOutPut>();

                // Fallback for a custom mediator override that does not explicitly call
                // CompleteSharedShow().
                operation.ShowCompletionSource.TrySetResult(true);

                ClearSharedMove(operation);
                operation.ResultCompletionSource.TrySetResult(result);
            }
            catch (OperationCanceledException exception)
            {
                ClearSharedMove(operation);
                operation.ShowCompletionSource.TrySetCanceled(exception.CancellationToken);
                operation.ResultCompletionSource.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                ClearSharedMove(operation);
                operation.ShowCompletionSource.TrySetException(exception);
                operation.ResultCompletionSource.TrySetException(exception);
            }
        }

        private void ClearSharedMove(MoveOperation operation)
        {
            lock (_moveGate)
            {
                if (ReferenceEquals(_moveOperation, operation))
                {
                    _moveOperation = null;
                }
            }
        }

        private void DisposeCore()
        {
            MoveOperation pendingOperation = null;

            lock (_moveGate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                pendingOperation = _moveOperation;
                _moveOperation = null;
            }

            Interlocked.Exchange(ref _openingOperationCount, 0);
            CancelAndDispose(_cancellationTokenSource);
            _cancellationTokenSource = null;
            pendingOperation?.ShowCompletionSource.TrySetCanceled();
            pendingOperation?.ResultCompletionSource.TrySetCanceled();
        }

        private static void CancelAndDispose(CancellationTokenSource source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                if (!source.IsCancellationRequested)
                {
                    source.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                source.Dispose();
            }
        }
    }

    public class qtMediator<TUI, TLogic> : qtMediator
        where TUI : qtUiObject
        where TLogic : qtLogic, new()
    {
        protected TUI _ui;
        protected TLogic _logic;

        protected List<UniTask<ParamOutput>> _defaultConditions;
        protected List<UniTask<ParamOutput>> _conditions = new List<UniTask<ParamOutput>>();

        public delegate UniTask fncVLM(
            TUI ui,
            TLogic logic,
            qtMediator<TUI, TLogic> mediator);
        
        protected fncVLM _configUI;
        protected fncVLM _beforeUIShow;
        protected fncVLM _afterUIShow;
        protected fncVLM _beforeUIHide;

        protected fncVLM _addBeforeConfigUI;
        protected fncVLM _addBeforeUIShow;
        protected fncVLM _addAfterUIShow;
        protected fncVLM _addBeforeUIHide;
        protected fncVLM _removeEvent;

        private int _isHandlingBeforeHide;
        private int _duplicateConfigurationWarningIssued;

        public override bool IsActive()
        {
            return _ui && _ui.gameObject.activeInHierarchy;
        }

        public qtMediator<TUI, TLogic> ConfigUI(fncVLM func)
        {
            if (CanConfigureRequest(nameof(ConfigUI)))
            {
                _configUI = func;
            }

            return this;
        }

        public qtMediator<TUI, TLogic> BeforeUIShow(fncVLM func)
        {
            if (CanConfigureRequest(nameof(BeforeUIShow)))
            {
                _beforeUIShow = func;
            }

            return this;
        }

        public qtMediator<TUI, TLogic> AfterUIShow(fncVLM func)
        {
            if (CanConfigureRequest(nameof(AfterUIShow)))
            {
                _afterUIShow = func;
            }

            return this;
        }

        public qtMediator<TUI, TLogic> BeforeUIHide(fncVLM func)
        {
            if (CanConfigureRequest(nameof(BeforeUIHide)))
            {
                _beforeUIHide = func;
            }

            return this;
        }

        public qtMediator<TUI, TLogic> SetAdditionSetUp(
            fncVLM addBeforeConfigUI = null,
            fncVLM addBeforeUIShow = null,
            fncVLM addAfterUIShow = null,
            fncVLM addBeforeUIHide = null)
        {
            if (CanConfigureRequest(nameof(SetAdditionSetUp)))
            {
                _addBeforeConfigUI = addBeforeConfigUI;
                _addBeforeUIShow = addBeforeUIShow;
                _addAfterUIShow = addAfterUIShow;
                _addBeforeUIHide = addBeforeUIHide;
            }

            return this;
        }

        public qtMediator<TUI, TLogic> SetAdditionalSetup(
            fncVLM addBeforeConfigUI = null,
            fncVLM addBeforeUIShow = null,
            fncVLM addAfterUIShow = null,
            fncVLM addBeforeUIHide = null)
        {
            return SetAdditionSetUp(
                addBeforeConfigUI,
                addBeforeUIShow,
                addAfterUIShow,
                addBeforeUIHide);
        }

        public qtMediator<TUI, TLogic> RemoveEvent(fncVLM func)
        {
            if (CanConfigureRequest(nameof(RemoveEvent)))
            {
                _removeEvent = func;
            }

            return this;
        }
        
        public qtMediator<TUI, TLogic> SetParam(object param)
        {
            // The first click owns the parameters of the shared operation. A repeated
            // request receives this mediator but must not overwrite the first request.
            if (CanConfigureRequest(nameof(SetParam)))
            {
                _param = param;
            }

            return this;
        }

        protected virtual async UniTask<(bool, (TUI, TLogic))> Load()
        {
            using var transition = new qtFlowTransition<TUI, TLogic>(_logic ?? new TLogic());
            transition.BeforeUIShow(ConfigureBeforeShow);

            try
            {
                var data = await transition.Move(_param);
                return (true, data);
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - {exception}");
                throw;
            }
        }

        protected virtual async UniTask<(TUI, TLogic)> Show()
        {
            var (isSuccess, data) = await Load();

            if (!isSuccess)
            {
                throw new OperationCanceledException();
            }

            await SetupAfterLoaded(data);

            if (_ui)
            {
                await UISetup(_ui, _logic, _cancellationToken);
            }

            return data;
        }

        public override void Close()
        {
            if (_ui)
            {
                _ui.ControllerHide().Forget();
            }
        }

        public override void Dispose()
        {
            if (_ui)
            {
                _ui.onBeforeUIHide -= _OnBeforeUIHide;
            }

            _configUI = null;
            _beforeUIShow = null;
            _afterUIShow = null;
            _beforeUIHide = null;
            _addBeforeConfigUI = null;
            _addBeforeUIShow = null;
            _addAfterUIShow = null;
            _addBeforeUIHide = null;
            _removeEvent = null;

            base.Dispose();
        }

        protected virtual void _OnBeforeUIHide()
        {
            if (Interlocked.Exchange(ref _isHandlingBeforeHide, 1) != 0)
            {
                return;
            }

            HandleBeforeUIHide().Forget();
        }

        /// <summary>
        /// Resolves data for the accepted Move operation before Load(), LogicInit(),
        /// ConfigUI and all other before-show callbacks execute.
        /// </summary>
        protected virtual UniTask PrepareRequestData()
        {
            return UniTask.CompletedTask;
        }

        protected virtual async UniTask UISetup(
            TUI ui,
            TLogic logic,
            CancellationToken token)
        {
            await UniTask.Yield();
        }

        protected virtual async UniTask SetupAfterLoaded((TUI ui, TLogic logic) data)
        {
            if (!_ui)
            {
                return;
            }

            var additionalAfterShow = _addAfterUIShow;
            _addAfterUIShow = null;
            await InvokeHook(nameof(_addAfterUIShow), additionalAfterShow);

            var afterShow = _afterUIShow;
            _afterUIShow = null;
            await InvokeHook(nameof(_afterUIShow), afterShow);
        }

        protected override async UniTask<TOutput> WorkingForResult<TOutput>()
        {
            // Capture the operation token before any await. Hide/Dispose replaces the
            // source after canceling it; reading _cancellationToken later could otherwise
            // return CancellationToken.None and leave the background result wait alive.
            var operationToken = _cancellationToken;

            Interlocked.Exchange(ref _duplicateConfigurationWarningIssued, 0);
            BeginOpeningOperation();
            qtUiFlow.BeginBusyOperation();

            try
            {
                // Request data first. Show() then copies the fresh _param into logic.param
                // before LogicInit and invokes ConfigUI only after loading is complete.
                await PrepareRequestData();

                // The internal busy scope covers request/load/config/show/UISetup/animation.
                // It must end before the opened UI waits for user input.
                await Show();
            }
            catch (Exception exception)
            {
                qtDebug.Log($"<color=red>Error show: {this} - {exception}</color>");
                throw;
            }
            finally
            {
                qtUiFlow.EndBusyOperation();
                EndOpeningOperation();
            }

            // Release Move()/MoveAsync() now. The background shared operation remains
            // alive until result/close so repeated requests still cannot open a duplicate.
            CompleteSharedShow();
            LogShowFinished();

            if (typeof(TOutput) == typeof(NoOutput))
            {
                return null;
            }

            try
            {
                return await Conditions(_ui, _logic, operationToken) as TOutput;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - Result condition failed: {exception}");
                return null;
            }
        }

        protected virtual async UniTask<ParamOutput> Conditions(
            TUI view,
            TLogic logic,
            CancellationToken token)
        {
            int dataTask = 0;
            _conditions.Clear();

            if (_defaultConditions != null)
            {
                _conditions.AddRange(_defaultConditions);
                // UniTask values are single-consumption, so treat these as one-shot.
                _defaultConditions = null;
            }

            if (view)
            {
                dataTask = _conditions.Count;
                _conditions.Add(view.uiResult.Task);
            }

            // Closing the view releases Move<TOutput>() even without an explicit result.
            ParamOutput[] results = await UniTask.WhenAll(_conditions);
            
            _conditions.Clear();
            return results[dataTask];
        }

        protected override void RemoveEvent()
        {
        }

        private bool CanConfigureRequest(string memberName)
        {
            if (!IsRequestInProgress)
            {
                Interlocked.Exchange(ref _duplicateConfigurationWarningIssued, 0);
                return true;
            }

            if (Interlocked.Exchange(
                    ref _duplicateConfigurationWarningIssued,
                    1) == 0)
            {
                qtDebug.Log(
                    $"<color=yellow>{this} - Ignore duplicate {memberName}; " +
                    "the first UI request is still running.</color>");
            }

            return false;
        }

        private async UniTask ConfigureBeforeShow(TUI ui, TLogic logic)
        {
            if (!ui)
            {
                throw new InvalidOperationException(
                    $"Transition returned no '{typeof(TUI).Name}' instance.");
            }

            _ui = ui;
            _logic = logic;

            _ui.onBeforeUIHide -= _OnBeforeUIHide;
            _ui.onBeforeUIHide += _OnBeforeUIHide;

            var additionalBeforeConfig = _addBeforeConfigUI;
            _addBeforeConfigUI = null;
            await InvokeHook(nameof(_addBeforeConfigUI), additionalBeforeConfig);
            await InvokeHook(nameof(_configUI), _configUI);

            var additionalBeforeShow = _addBeforeUIShow;
            _addBeforeUIShow = null;
            await InvokeHook(nameof(_addBeforeUIShow), additionalBeforeShow);
            await InvokeHook(nameof(_beforeUIShow), _beforeUIShow);
        }

        private async UniTask HandleBeforeUIHide()
        {
            try
            {
                _RefreshToken();

                var beforeHide = _beforeUIHide;
                _beforeUIHide = null;
                await InvokeHook(nameof(_beforeUIHide), beforeHide);

                var additionalBeforeHide = _addBeforeUIHide;
                _addBeforeUIHide = null;
                await InvokeHook(nameof(_addBeforeUIHide), additionalBeforeHide);

                await RemoveEventsAsync();
            }
            catch (Exception exception)
            {
                qtDebug.LogError($"{this} - Before-hide cleanup failed: {exception}");
            }
            finally
            {
                Interlocked.Exchange(ref _isHandlingBeforeHide, 0);
            }
        }

        private async UniTask RemoveEventsAsync()
        {
            if (_removeEvent != null)
            {
                await InvokeHook(nameof(_removeEvent), _removeEvent);
            }

            _logic?.HideScene();
            RemoveEvent();
        }

        private async UniTask InvokeHook(string hookName, fncVLM hook)
        {
            if (hook == null)
            {
                return;
            }

            qtDebug.Log($"{this} - {hookName}");
            await hook.Invoke(_ui, _logic, this);
        }

        private static async UniTask<ParamOutput> WaitForCancellation(CancellationToken token)
        {
            await UniTask.WaitUntilCanceled(token);
            return null;
        }

        private void LogShowFinished()
        {
            var viewName = _ui ? _ui.GetType().ToString() : typeof(TUI).ToString();
            qtDebug.Log($"<color=yellow>Finish show: {viewName}</color>");
        }
    }

    public abstract class qtMediator<TUI, TLogic, TArgs> : qtMediator<TUI, TLogic>,
        IRequestData<TArgs>
        where TArgs : class
        where TUI : qtUiObject
        where TLogic : qtLogic, new()
    {
        protected TArgs _args => _param as TArgs;

        public qtMediator<TUI, TLogic> SetParam(TArgs args)
        {
            return base.SetParam(args);
        }

        public abstract UniTask<TArgs> RequestData();

        protected override void RemoveEvent()
        {
            _param = null;
            base.RemoveEvent();
        }

        protected override async UniTask PrepareRequestData()
        {
            // Run once for each accepted Move operation, whether the mediator is reused
            // or newly created. During RequestData(), Args still exposes the input passed
            // through SetParam(). ConfigUI receives the returned fresh data afterwards.
            // Duplicate clicks join the existing shared operation and never execute this
            // hook a second time.
            if (_param == null)
            {
                var requestedData = await RequestData();
                _param = requestedData;
            }
        }
    }

    public interface IRequestData<TData>
    {
        public UniTask<TData> RequestData();
    }
}