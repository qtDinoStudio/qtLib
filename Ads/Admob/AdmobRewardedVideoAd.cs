#if ENABLE_ADS

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using GoogleMobileAds.Api;
using PimDeWitte.UnityMainThreadDispatcher;

namespace qtLib.Ads.Admob
{
    public class AdmobRewardedVideoAd : AdmobAdUnit, IAdUnit
    {
        private RewardedAd _rewardedAd;

        protected sealed override string _AdType() => "AdmobRewardedVideoAd";

        public AdmobRewardedVideoAd(
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
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }

            qtDebug.Log($"{_AdType()}: Loading");
            AdRequest adRequest = new AdRequest();
            RewardedAd.Load(_adUnitID, adRequest,
                async (RewardedAd ad, LoadAdError error) =>
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
                            await UniTask.Delay(_countReload * TimeTryToReload,
                                cancellationToken: _cancellationTokenSource.Token);
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
                    _rewardedAd = ad;
                    _onAdReady?.Invoke(_adInfor, _rewardedAd.CanShowAd());
                    _rewardedAd.OnAdPaid += OnAdPaid;
                    _rewardedAd.OnAdImpressionRecorded += OnAdImpressionRecorded;
                    _rewardedAd.OnAdClicked += OnAdClicked;
                    _rewardedAd.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
                    _rewardedAd.OnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
                    _rewardedAd.OnAdFullScreenContentFailed += OnAdFullScreenContentFailed;
                });
        }
        
        public override void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            qtDebug.Log($"{_AdType()}: ShowAds");
            base.ShowAds(onCompleteCallback, onCloseCallback);
            _rewardedAd.Show((Reward reward) =>
            {
                qtDebug.Log($"{_AdType()}: userRewardEarnedCallback");
                _onCompleteCallback?.Invoke();
                _onCompleteCallback = null;
                LoadAd();
            });
        }

        public void HideAds(Action callback = null)
        {
            throw new NotImplementedException();
        }

        public bool IsReady()
        {
            return _rewardedAd != null && _rewardedAd.CanShowAd();
        }

        #region ----- Callback -----
        
        protected override void OnAdFullScreenContentClosed()
        {
            base.OnAdFullScreenContentClosed();
            
            LoadAd();
            _onCloseCallback?.Invoke();
            _onCloseCallback = null;
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