using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using UnityEngine;

namespace qtLib.UI.Base
{
    public abstract class qtMediator : IDisposable
    {
        protected CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        protected CancellationToken _cancellationToken => _cancellationTokenSource.Token;
        protected object _param;
        private UniTask<ParamOutput>? _taskCache;
        private CancellationTokenSource _waitingToken;
        
        public abstract bool IsActive();
        
        public async UniTaskVoid Move(bool inactivePreviousScene = true, bool setAsLastSibling = true)
        {
            try
            {
                await WorkingForResult<NoOutput>(inactivePreviousScene, setAsLastSibling);
            }
            catch (Exception e)
            {
                qtDebug.LogError($"{this} - {e.Message}");
            }
        }
        
        public async UniTask<TOutput> Move<TOutput>(bool inactivePreviousScene = true, bool setAsLastSibling = true) where TOutput : ParamOutput, new()
        {
            TOutput _result = null;

            if (_taskCache == null)
            {
                _taskCache = WorkingForResult<ParamOutput>(inactivePreviousScene, setAsLastSibling);

                _result = await _taskCache.Value as TOutput;
            }
            else
            {
                if (_waitingToken == null)
                {
                    _waitingToken = new CancellationTokenSource();
                }

                _waitingToken = CancellationTokenSource.CreateLinkedTokenSource(_waitingToken.Token, _cancellationToken);

                _result = await _taskCache.Value as TOutput;
            }

            if (_waitingToken != null)
            {
                _waitingToken.Cancel();
                _waitingToken.Dispose();
                _waitingToken = null;
            }

            _taskCache = null;
            return _result;
        }

        protected virtual UniTask<TOutput> WorkingForResult<TOutput>(bool inactivePreviousScene, bool setAsLastSibling) where TOutput : ParamOutput, new()
        {
            return UniTask.FromResult(default(TOutput));
        }

        protected void _RefreshToken()
        {
            _cancellationTokenSource?.Cancel();

            _cancellationTokenSource = new CancellationTokenSource();
            // _cancellationTokenSource.Token.Register(RemoveEvent);
            //
            // UniTask.Create(async () =>
            // {
            //     await cancellationToken.Token.WaitUntilCanceled();
            //     state = EStateMediator.M_CANCELED;
            // }).Forget();
        }

        ~qtMediator()
        {
            Dispose();
        }
        
        public virtual void Dispose()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }
            if (_cancellationTokenSource.IsCancellationRequested == false)
                _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
        
        public virtual void Close(){}
        
        private void DoProcessing(CancellationToken token, Action action)
        {
            action?.Invoke();
        }

        protected abstract void RemoveEvent();
    }
    
    public class qtMediator<TUI, TLogic> : qtMediator where TUI : qtUiObject where TLogic : qtLogic, new()
    {
        protected TUI _ui;
        protected TLogic _logic;
        
        protected List<UniTask<ParamOutput>> _defaultConditions;
        protected List<UniTask<ParamOutput>> _conditions = new ();
        
        public delegate UniTask fncVLM(TUI ui, TLogic logic, qtMediator<TUI, TLogic> mediator);
        
        protected fncVLM _configUI, _beforeUIShow, _afterUIShow, _beforeUIHide;
        protected fncVLM _addBeforeConfigUI, _addBeforeUIShow, _addAfterUIShow;
        protected fncVLM _addBeforeUIHide;
        protected fncVLM _removeEvent;
        protected fncResult_VLM _forceResult;

        public override bool IsActive()
        {
            if (_ui == null)
            {
                return false;
            }
            
            return _ui.gameObject.activeInHierarchy;
        }

        // public qtMediator<TUI, TLogic> ConfigUI(fncVLM func)
        // {
        //     _configUI = func;        
        //     return this;
        // }
        //
        // public qtMediator<TUI, TLogic> BeforeUIShow(fncVLM func)
        // {
        //     _beforeUIShow = func;     
        //     return this;
        // }
        //
        // public qtMediator<TUI, TLogic> AfterUIShow(fncVLM func)
        // {
        //     _afterUIShow = func;     
        //     return this;
        // }
        
        public qtMediator<TUI, TLogic> SetAdditionSetUp(fncVLM addBeforeConfigUI = null,fncVLM addBeforeUIShow = null, fncVLM addAfterUIShow = null, fncVLM addBeforeUIHide = null)
        {
            _addBeforeConfigUI = addBeforeConfigUI;
            _addBeforeUIShow = addBeforeUIShow;
            _addAfterUIShow = addAfterUIShow;
            _addBeforeUIHide = addBeforeUIHide; 
            return this;
        }

        public qtMediator<TUI, TLogic> RemoveEvent(fncVLM func)
        {
            _removeEvent = func;
            return this;
        }

        public qtMediator<TUI, TLogic> SetResult(fncResult_VLM func)
        {
            _forceResult = func; return this;
        }   

        public delegate UniTask<ParamOutput> fncResult_VLM(TUI ui, TLogic logic, qtMediator<TUI, TLogic> mediator);
        
        public qtMediator() : base(){}

        protected virtual async UniTask<(bool, (TUI, TLogic))> Load(bool inactivePreviousScene, bool setAsLastSibling)
        {
            using (qtFlowTransition<TUI, TLogic>
                   transition = new qtFlowTransition<TUI, TLogic>(_logic ?? new TLogic()))
            {
                //Config
                transition.BeforeUIShow(async (ui, logic) =>
                {
                    _ui = ui;
                    _logic = logic;

                    _ui.onBeforeUIHide = _OnBeforeUIHide;
                    if (_addBeforeConfigUI != null)
                    {
                        qtDebug.Log($"{this} - _addBeforeConfigUI");
#if UNITY_EDITOR && !ENABLE_LOG
                        Debug.Log($"{this} - _addBeforeConfigUI");
#endif
                        await _addBeforeConfigUI.Invoke(ui, logic, this);
                    }

                    if (_configUI != null)
                    {
                        qtDebug.Log($"{this} - _configUI");
#if UNITY_EDITOR && !ENABLE_LOG
                        Debug.Log($"{this} - _configUI");
#endif
                        await _configUI.Invoke(ui, logic, this);
                    }

                    if (_addBeforeUIShow != null)
                    {
                        qtDebug.Log($"{this} - _addBeforeUIShow");
#if UNITY_EDITOR && !ENABLE_LOG
                        Debug.LogError($"{this} - _addBeforeUIShow");
#endif
                        await _addBeforeUIShow.Invoke(ui, logic, this);
                        _addBeforeUIShow = null;
                    }

                    if (_beforeUIShow != null)
                    {
                        qtDebug.Log($"{this} - _beforeUIShow");
#if UNITY_EDITOR && !ENABLE_LOG
                        Debug.LogError($"{this} - _beforeUIShow");
#endif
                        await _beforeUIShow.Invoke(ui, logic, this);
                    }
                });

                //Đoạn này là để hide scene cũ
                (bool isSuccess, (TUI ui, TLogic logic) data) result = new();
                try
                {
                    result.data = await transition.Move(inactivePreviousScene, _param, setAsLastSibling);
                    result.isSuccess = false;
                }
                catch (Exception e)
                {
                    qtDebug.LogError($"{this} - {e.Message}");
#if UNITY_EDITOR && !ENABLE_LOG
                    Debug.LogError(e.Message);
#endif
                    result.isSuccess = true;
                    throw;
                }

                return result;
            }
        }
        
        protected virtual async UniTask<(TUI, TLogic)> Show(bool inactivePreviousScene = true, bool setAsLastSibling = true)
        {
            var (isCancel, mediator) = await Load(inactivePreviousScene, setAsLastSibling);
            if (isCancel)
            {
                throw new OperationCanceledException();
            }
            
            await SetupAfterLoaded(mediator);
            
            if (_ui)
            {
                await UISetup(_ui, _logic, _cancellationToken);
            }

            return mediator;
        }

        public override void Close()
        {
            _ui.ControllerHide().Forget();
        }

        protected virtual void _OnBeforeUIHide()
        {
            _RefreshToken();
            _RemoveEvent().Forget();

            if (_beforeUIHide != null)
            {
                qtDebug.Log($"{this} - _beforeUIHide");
#if UNITY_EDITOR && !ENABLE_LOG
                Debug.Log($"{this} - _beforeUIHide");
#endif
                _beforeUIHide?.Invoke(_ui, _logic, this);
                _beforeUIHide = null;
            }

            if (_addBeforeUIHide != null)
            {
                qtDebug.Log($"{this} - _addBeforeUIHide");
#if UNITY_EDITOR && !ENABLE_LOG
                Debug.Log($"{this} - _addBeforeUIHide");
#endif

                _addBeforeUIHide?.Invoke(_ui, _logic, this);
                _addBeforeUIHide = null;
            }
        }
        
        public qtMediator<TUI, TLogic> SetParam(object param) { this._param = param; return this; }


        protected virtual async UniTask UISetup(TUI ui, TLogic logic, CancellationToken token)
        {
            await UniTask.Yield();
        }

        protected virtual async UniTask SetupAfterLoaded((TUI ui, TLogic logic) mediator)
        {
            if (_ui)
            {
                if (_addAfterUIShow != null)
                {
                    qtDebug.Log($"{this} - _addAfterUIShow");
#if UNITY_EDITOR && !ENABLE_LOG
                    Debug.Log($"{this} - _addAfterUIShow");
#endif
                    await _addAfterUIShow.Invoke(_ui, _logic, this);
                    _addAfterUIShow = null;
                }
                
                if (_afterUIShow != null)
                {
                    qtDebug.Log($"{this} - _afterUIShow");
#if UNITY_EDITOR && !ENABLE_LOG
                    Debug.Log($"{this} - _afterUIShow");
#endif
                    await _afterUIShow.Invoke(_ui, _logic, this);
                    _afterUIShow = null;
                }
            }
        }

        protected override async UniTask<TOutput> WorkingForResult<TOutput>(bool inactivePreviousScene, bool setAsLastSibling)
        {
            qtUiFlow.IsBusy = true;
            try
            {
                var mediator = await Show(inactivePreviousScene, setAsLastSibling);
                qtUiFlow.IsBusy = false;
                if (typeof(TOutput) == typeof(NoOutput))
                {
                    qtDebug.Log("<color=yellow>Finish show: " +  _ui.GetType().ToString() + "</color>");
                    return null;
                }
                var result = await Conditions(_ui, _logic, _cancellationToken) as TOutput;
                qtDebug.Log("<color=yellow>Finish show: " +  _ui.GetType().ToString() + "</color>");
                return result;
            }
            catch (Exception e)
            {
                qtDebug.Log("<color=red>Error show: " + this + " - " +e.Message + "</color>");
                return null;
            }
        }

        protected virtual async UniTask<ParamOutput> Conditions(TUI view, TLogic logic, CancellationToken token)
        {
            int dataTask = 0;
            _conditions = new List<UniTask<ParamOutput>>();

            if (_defaultConditions != null)
            {
                _conditions.AddRange(_defaultConditions);
            }

            if (view)
            {
                dataTask = _conditions.Count;
                _conditions.Add(view.uiResult.Task);
                _conditions.Add(UniTask.Create<ParamOutput>(async () =>
                {
                    await UniTask.WaitUntil(() => view != null && view.gameObject.activeInHierarchy, cancellationToken: token);
                    return null;
                }));
            }

            if (_forceResult != null)
            {
                dataTask = _conditions.Count;
                _conditions.Add(_forceResult.Invoke(view, logic, this));
            }

            if (_conditions.Count > 0)
            {
                ParamOutput[] outputs = await UniTask.WhenAll(_conditions);
                _conditions.Clear();
                return outputs[dataTask];
            }

            return null;
        }

        private void DoProcessing(CancellationToken token, Action action)
        {
            action?.Invoke();
        }

        private async UniTaskVoid _RemoveEvent()
        {
            if (_removeEvent != null)
            {
                qtDebug.Log($"{this} - _removeEvent");
#if UNITY_EDITOR && !ENABLE_LOG
                Debug.Log($"{this} - _removeEvent");
#endif

                await _removeEvent.Invoke(_ui, _logic, this);
            }
            
            _logic.HideScene();
            
            RemoveEvent();
        }

        protected override void RemoveEvent()
        {
        }
    }

    public abstract class qtRequestMediator<TUI, TLogic, TArgs> : qtMediator<TUI, TLogic>, IMRequestData<TArgs>
        where TArgs : class
        where TUI : qtUiObject
        where TLogic : qtLogic,
        new()
    {
        public TArgs Args => _param as TArgs;

        protected override void RemoveEvent()
        {
            _param = null;
            base.RemoveEvent();
        }

        public qtMediator<TUI, TLogic> SetParam(TArgs args)
        {
            _param = args;
            return this;
        }

        protected override async UniTask<(bool, (TUI, TLogic))> Load(bool inactivePreviousScene, bool setAsLastSibling)
        {
            if (_param is not TArgs)
            {
                _param = await RequestData();
            }

            return await base.Load(inactivePreviousScene, setAsLastSibling);
        }

        public abstract UniTask<TArgs> RequestData();
    }


    public interface IMRequestData<TData>
    {
        UniTask<TData> RequestData();
    }
}
