#if ENABLE_ADS

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using PimDeWitte.UnityMainThreadDispatcher;

namespace qtLib.Ads.Admob
{
    public class AdmobAppOpenAd : AdmobAdUnit, IAdUnit
    {
        private AppOpenAd _appOpenAd;
        private DateTime _expireTime;

        protected sealed override string _AdType() => "AdmobAppOpenAd";

        public AdmobAppOpenAd(string adUnitID, AdsManager.EvtAdPaid onAdPaid) : base(adUnitID, onAdPaid)
        {
            qtDebug.Log($"{_AdType()}: Constructor");
            AppStateEventNotifier.AppStateChanged += OnAppStateChanged;

            LoadAd();
        }

        ~AdmobAppOpenAd()
        {
            AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
        }
        
        public sealed override void LoadAd()
        {
            qtDebug.Log($"{_AdType()}: LoadAd");
            if (_appOpenAd != null)
            {
                _appOpenAd.Destroy();
                _appOpenAd = null;
            }
            _expireTime = DateTime.Now + TimeSpan.FromHours(4);
            qtDebug.Log($"{_AdType()}: Loading");
            AdRequest adRequest = new AdRequest();

            AppOpenAd.Load(_adUnitID, adRequest,
                async (AppOpenAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        qtDebug.LogError($"{_AdType()}: Failed to load an ad with error : {error}");
                        _countReload++;

                        if (_countReload > MaxReloadTime)
                        {
                            return;
                        }
                        
                        try
                        {
                            await UniTask.Delay(_countReload * TimeTryToReload, cancellationToken: _cancellationTokenSource.Token);
                            await UniTask.SwitchToMainThread();
                            LoadAd();
                        }
                        catch (TaskCanceledException)
                        {
                            
                        }
                        catch (OperationCanceledException)                        
                        {
                            //ignore
                        }
                        
                        return;
                    }

                    qtDebug.Log($"{_AdType()}: Loaded with response : {ad.GetResponseInfo()}");
                    _countReload = 0;
                    _appOpenAd = ad;
                    
                    ad.OnAdPaid += OnAdPaid;
                    ad.OnAdImpressionRecorded += OnAdImpressionRecorded;
                    ad.OnAdClicked += OnAdClicked;
                    ad.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
                    ad.OnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
                    ad.OnAdFullScreenContentFailed += OnAdFullScreenContentFailed;
                });
        }

        public override void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            base.ShowAds(onCompleteCallback, onCloseCallback);
            qtDebug.Log($"{_AdType()}: ShowAds");
            _appOpenAd.Show();
        }

        public void HideAds(Action callback = null)
        {
            throw new NotImplementedException();
        }

        public bool IsReady()
        {
            return _appOpenAd != null
                   && _appOpenAd.CanShowAd()
                   && DateTime.Now < _expireTime;
        }

        private void OnAppStateChanged(AppState state)
        {
// #if UNITY_EDITOR
//             return;
// #endif
            qtDebug.Log($"{_AdType()}: App State changed to " + state);

            // if the app is Foregrounded and the ad is available, show it.
            if (state == AppState.Foreground)
            {
                if (IsReady())
                {
                    ShowAds();
                }
            }
        }
        
        #region ----- Callback -----
        
        protected override void OnAdFullScreenContentClosed()
        {
            base.OnAdFullScreenContentClosed();
            
            _onCompleteCallback?.Invoke();
            _onCompleteCallback = null;
            _onAdReady?.Invoke(_adInfor, _appOpenAd.CanShowAd());
            
            LoadAd();
        }

        protected override void OnAdFullScreenContentFailed(AdError error)
        {
            base.OnAdFullScreenContentFailed(error);
            
            LoadAd();
            _onCloseCallback?.Invoke();
            _onCloseCallback = null;
        }

        #endregion
    }
}

#endif