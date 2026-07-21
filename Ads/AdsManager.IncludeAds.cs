using System;
using qtLib.Helper;
using qtLib.IAP;
using UnityEngine;

#if ENABLE_ADS
using _Scripts.Data;
using _Scripts.GameFirebase;
using _Scripts.Entities;
using qtLib.Ads.Admob;
using Cysharp.Threading.Tasks;
#endif

namespace qtLib.Ads
{
    public partial class AdsManager
    {
        #region ----- Component Config -----

        [SerializeField] private AdConfigModel _adConfigData;

#if ENABLE_ADS
        private IAdService _adService = new AdmobService();
#endif

        #endregion
        
        private float _interstitialTimer = 0;

        #region ----- Properties -----

#if ENABLE_ADS
        public bool IsRemoveAds => UserData.Instance.IsRemoveAds();
#else
        public bool IsRemoveAds => false;
#endif

        #endregion

#if ENABLE_ADS

        #region ------ Implement Function -----

        public override UniTask ManualInit()
        {
            MessageDispatcher.Register(MessageDispatcher.EEvent.IAPPurchaseSucceeded, _IAPPurchaseSucceeded);
            _adService.Initialize(_adConfigData, _OnAdReady, _OnAdPaid);
            
            return base.ManualInit();
        }

        #endregion
        
        #region ----- Banner Ads -----

        public void ShowBannerAds()
        {
            if (IsRemoveAds)
            {
                return;
            }

            _adService.ShowBannerAd();
        }

        #endregion

        #region ----- Interstitial Ads -----

        public bool IsInterstitialAdsReady(AdPosition position)
        {
            return !IsRemoveAds && _adService.IsAdReady(AdType.ImageInterstitial, position) || _adService.IsAdReady(AdType.VideoInterstitial, position);
        }

        public void ShowInterstitialAds(AdPosition position, Action callback)
        {
            if (IsRemoveAds)
            {
                callback?.Invoke();
                return;
            }

            if (Time.time >= _interstitialTimer)
            {
                _interstitialTimer = Time.time + Definition.InterstitialInterval;
            }
            else
            {
                callback?.Invoke();
                return;
            }

            if (_adService.IsAdReady(AdType.ImageInterstitial, position))
            {
                qtDebug.Log("ShowImageInterstitialAds");
                _adService.ShowAd(AdType.ImageInterstitial, position, callback);
            }
            else if (_adService.IsAdReady(AdType.VideoInterstitial, position))
            {
                qtDebug.Log("ShowVideoInterstitialAds");
                _adService.ShowAd(AdType.VideoInterstitial, position, callback);
            }
            else
            {
                callback?.Invoke();
            }
        }

        #endregion

        #region ----- Rewarded Interstitial Ads -----

        public bool IsRewardedInterstitialAdsReady(AdPosition position)
        {
            return _adService.IsAdReady(AdType.RewardedInterstitial, position);
        }

        public void ShowRewardedInterstitialAds(AdPosition position, Action callback)
        {
            if (IsRewardedInterstitialAdsReady(position))
            {
                qtDebug.Log("ShowRewardedInterstitialAds");
                _adService.ShowAd(AdType.RewardedInterstitial, position, callback);
            }
            else
            {
                callback?.Invoke();
            }
        }

        #endregion

        #region ----- Rewared Video Ads -----

        public bool IsRewardedVideoAdsReady(AdPosition position)
        {
            return _adService.IsAdReady(AdType.RewardedVideo, position);
        }

        public void ShowRewardedVideoAds(AdPosition position, Action onCompleteCallback = null, Action onCloseCallback =
 null)
        {
            if (IsRewardedVideoAdsReady(position))
            {
                qtDebug.Log("ShowRewardedVideoAds");
                _adService.ShowAd(AdType.RewardedVideo, position, onCompleteCallback, onCloseCallback);
            }
            else
            {
                onCloseCallback?.Invoke();
            }
        }

        #endregion

        #region ----- Native Ads -----

        public bool IsNativeAdsReady(AdPosition adPosition)
        {
            return !IsRemoveAds && _adService.IsNativeAdsReady(adPosition);
        }

        public void ShowNativeAds(AdPosition adPosition)
        {
            if (!IsNativeAdsReady(adPosition))
            {
                return;
            }
            
            _adService.ShowNativeOverlayAd(adPosition);
        }

        public void HideNativeAds(AdPosition  adPosition)
        {
            _adService.HideNativeOverlayAd(adPosition);
        }

        #endregion

        #region ----- AOA -----

        public void ShowAppOpenAd()
        {
            _adService.ShowAppOpenAd();
        }

        #endregion

        public void RemoveAds()
        {
            _adService.RemoveAds();
        }

        private void _IAPPurchaseSucceeded(MessageDispatcher.MessageObject message)
        {
            if (message is not MessageDispatcher.IAPPurchaseSucceededMessage data)
            {
                return;
            }
            qtDebug.Log($"AdsManager: {data.productId} purchased successfully");
        
            if (data.productId.Equals(IAPManager.IAPProduct.Shop_RemoveAds))
            {
                RemoveAds();
            }
        }
        #region ----- Private Function -----

        private void _OnAdReady((AdType adType, AdPosition adPosition) adInfor, bool isReady)
        {
            lock (_dictAdEvent)
            {
                if (_dictAdEvent.ContainsKey(adInfor))
                {
                    _dictAdEvent[adInfor].ForEach(listener => listener?.Invoke(isReady));
                }
            }
        }

        /// <summary>
        /// The ad's value in micro-units, where 1,000,000 micro-units equal one unit of the currency.
        /// </summary>
        /// <param name="adInfor"></param>
        /// <param name="value"></param>
        /// <param name="currencyCode"></param>
        private void _OnAdPaid((AdType adType, AdPosition adPosition) adInfor, float value, string currencyCode)
        {
            FirebaseManager.DoAction(nameof(_OnAdPaid), firebase =>
            {
                firebase.SendEvent(
                    AnalyticsKey.Ads_Revenue,
                    (AnalyticsParam.AdType, adInfor.adType.ToString()),
                    (AnalyticsParam.RevenueValue, value),
                    (AnalyticsParam.RevenueCurrency, currencyCode));
                return FirebaseManager.EErrorCode.OK;
            });   
        }

        #endregion
#endif
    }
}

