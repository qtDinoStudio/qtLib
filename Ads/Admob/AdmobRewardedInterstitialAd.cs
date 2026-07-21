#if ENABLE_ADS

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using GoogleMobileAds.Api;
using PimDeWitte.UnityMainThreadDispatcher;

namespace qtLib.Ads.Admob
{
    public class AdmobRewardedInterstitialAd : AdmobAdUnit, IAdUnit
    {
        private RewardedInterstitialAd _rewardedInterstitialAd;

        protected sealed override string _AdType() => "AdmobRewardedInterstitialAd";

        public AdmobRewardedInterstitialAd(
            (AdsManager.AdType adType, AdsManager.AdPosition adPosition) adInfor, 
            string adUnitID, 
            AdsManager.EvtAdReady onAdReady,
            AdsManager.EvtAdPaid onAdPaid) 
            : base(adInfor, adUnitID, onAdReady, onAdPaid)
        {
            qtDebug.Log($"{_AdType()}: Constructor");
            LoadAd();
        }
        
        public sealed override void LoadAd()
        {
            qtDebug.Log($"{_AdType()}: LoadAd");
            if (_rewardedInterstitialAd != null)
            {
                _rewardedInterstitialAd.Destroy();
                _rewardedInterstitialAd = null;
            }

            qtDebug.Log($"{_AdType()}: Loading");
            AdRequest adRequest = new AdRequest();

            // send the request to load the ad.
            RewardedInterstitialAd.Load(_adUnitID, adRequest,
                async (RewardedInterstitialAd ad, LoadAdError error) =>
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
                    _onAdReady.Invoke(_adInfor, true);
                    _rewardedInterstitialAd = ad;
                    
                    _rewardedInterstitialAd.OnAdPaid += OnAdPaid;
                    _rewardedInterstitialAd.OnAdImpressionRecorded += OnAdImpressionRecorded;
                    _rewardedInterstitialAd.OnAdClicked += OnAdClicked;
                    _rewardedInterstitialAd.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
                    _rewardedInterstitialAd.OnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
                    _rewardedInterstitialAd.OnAdFullScreenContentFailed += OnAdFullScreenContentFailed;
                });
        }

        public override void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            qtDebug.Log($"{_AdType()}: ShowAds");
            base.ShowAds(onCompleteCallback, onCloseCallback);
            _rewardedInterstitialAd.Show((Reward reward) =>
            {
                // _onCompleteCallback?.Invoke();
                // _onCompleteCallback = null;
            });
        }

        public void HideAds(Action callback = null)
        {
            throw new NotImplementedException();
        }

        public bool IsReady()
        {
            return _rewardedInterstitialAd != null && _rewardedInterstitialAd.CanShowAd();
        }
        
        #region ----- Callback -----
        
        protected override void OnAdFullScreenContentClosed()
        {
            base.OnAdFullScreenContentClosed();
            
            _onCompleteCallback?.Invoke();
            _onCompleteCallback = null;
            _onAdReady.Invoke(_adInfor, _rewardedInterstitialAd.CanShowAd());
            
            LoadAd();
        }

        protected override void OnAdFullScreenContentFailed(AdError error)
        {
            base.OnAdFullScreenContentFailed(error);
            _onCloseCallback?.Invoke();
            _onCloseCallback = null;

            LoadAd();
        }

        #endregion
    }
}

#endif