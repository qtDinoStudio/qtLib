#if ENABLE_ADS

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using GoogleMobileAds.Api;
using PimDeWitte.UnityMainThreadDispatcher;

namespace qtLib.Ads.Admob
{
    public class AdmobInterstitialAd : AdmobAdUnit, IAdUnit
    {
        private InterstitialAd _interstitialAd;
        protected sealed override string _AdType() => "AdmobInterstitialAd";
        
        public AdmobInterstitialAd(string adUnitID, AdsManager.EvtAdPaid onAdPaid) : base(adUnitID, onAdPaid)
        {
            qtDebug.Log($"{_AdType()}: Constructor");
            LoadAd();
        }
        
        public sealed override void LoadAd()
        {
            qtDebug.Log($"{_AdType()}: LoadAd");
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }
            qtDebug.Log($"{_AdType()}: Loading");
            AdRequest adRequest = new AdRequest();
            InterstitialAd.Load(_adUnitID, adRequest,
                async (InterstitialAd ad, LoadAdError error) =>
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
                    _interstitialAd = ad;
                    _onAdReady?.Invoke(_adInfor, true);
                    _interstitialAd.OnAdPaid += OnAdPaid;
                    _interstitialAd.OnAdImpressionRecorded += OnAdImpressionRecorded;
                    _interstitialAd.OnAdClicked += OnAdClicked;
                    _interstitialAd.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
                    _interstitialAd.OnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
                    _interstitialAd.OnAdFullScreenContentFailed += OnAdFullScreenContentFailed;
                });
        }
        
        public bool IsReady()
        {
            return _interstitialAd != null && _interstitialAd.CanShowAd();
        }

        public override void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            qtDebug.Log($"{_AdType()}: ShowAds");
            base.ShowAds(onCompleteCallback, onCloseCallback);
            _interstitialAd.Show();
        }

        public void HideAds(Action callback = null)
        {
            throw new NotImplementedException();
        }

        #region ----- Callback -----
        
        protected override void OnAdFullScreenContentClosed()
        {
            base.OnAdFullScreenContentClosed();
            _onCompleteCallback?.Invoke();
            _onCompleteCallback = null;
            
            _onAdReady?.Invoke(_adInfor, _interstitialAd.CanShowAd());

            LoadAd();
        }

        protected override void OnAdFullScreenContentFailed(AdError error)
        {
            base.OnAdFullScreenContentFailed(error);
            
            _onCloseCallback?.Invoke();
            LoadAd();
        }

        #endregion
    }
}

#endif