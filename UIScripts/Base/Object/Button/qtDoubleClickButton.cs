// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Cysharp.Threading.Tasks;
// using qtLib.UI.Base;
// using UnityEngine;
//
// namespace qtLib.UIScripts.Base.Object.Button
// {
//     [RequireComponent(typeof(UnityEngine.UI.Button))]
//     public class qtDoubleClickButton : qtButton
//     {
//         #region ----- Component Config -----
//
//         private bool _isFirstClick = false;
//         private bool _isDoubleClick = false;
//
//         private const int TimeBetweenClicks = 250;
//         
//         [HideInInspector] public UnityEngine.UI.Button.ButtonClickedEvent onDoubleClick;
//         private CancellationTokenSource _doubleClickCts;
//         
//         private bool _isEnableDoubleClick = true;
//
//         #endregion
//
//         public void SetEnableDoubleClick(bool isEnableDoubleClick)
//         {
//             _isEnableDoubleClick = isEnableDoubleClick;
//         }
//         
//         protected override async void _OnButtonClick()
//         {
//             try
//             {
//                 _PlaySfx();
//                 if (qtUiFlow.IsBusy)
//                 {
//                     return;
//                 }
//
//                 if (!_isEnableDoubleClick)
//                 {
//                     onClick?.Invoke();
//                     return;
//                 }
//
//                 if (_isFirstClick)
//                 {
//                     onDoubleClick?.Invoke();
//
//                     _isDoubleClick = false;
//                     _isFirstClick = false;
//
//                     _doubleClickCts?.Cancel();
//                     return;
//                 }
//                 else
//                 {
//                     _isFirstClick = true;
//                 }
//             
//                 // SoundController.Instance.PlayClickSfx();
//                 _doubleClickCts = new CancellationTokenSource();
//                 await UniTask.Delay(TimeBetweenClicks, cancellationToken: _doubleClickCts.Token);
//             
//                 if (!_isDoubleClick)
//                 {
//                     _PlaySfx();
//                     onClick?.Invoke();
//                 }
//                 _isDoubleClick = false;
//                 _isFirstClick = false;
//                 
//                 _doubleClickCts = null;
//             }
//             catch (TaskCanceledException)
//             {
//                             
//             }
//             catch (OperationCanceledException)                        
//             {
//                 
//             }
//         }
//     }
// }