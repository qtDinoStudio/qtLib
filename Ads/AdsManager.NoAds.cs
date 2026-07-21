using System;
using Cysharp.Threading.Tasks;

namespace qtLib.Ads
{
    public partial class AdsManager
    {
#if !ENABLE_ADS

        public UniTask ManualInit()
        {
            return UniTask.CompletedTask;
        }

        #region ----- Banner Ads -----

        public void ShowBannerAds()
        {
        }

        public void ShowAppOpenAd()
        {
        }

        #endregion

        #region ----- Interstitial Ads -----
        
        public bool IsInterstitialAdsReady(AdPosition position)
        {
            return true;
        }
        
        public void ShowInterstitialAds(AdPosition position, Action callback)
        {
            callback?.Invoke();
        }

        #endregion

        #region ----- Rewarded Interstitial Ads -----

        public bool IsRewardedInterstitialAdsReady(AdPosition position)
        {
            return true;
        }

        public void ShowRewardedInterstitialAds(AdPosition position, Action callback)
        {
            callback?.Invoke();
        }
        
        #endregion

        #region ----- Rewared Video Ads -----

        public bool IsRewardedVideoAdsReady(AdPosition position)
        {
            return true;
        }

        public void ShowRewardedVideoAds(AdPosition position, Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            onCompleteCallback?.Invoke();
        }

        #endregion
        
        public void ShowNativeAds(AdPosition adPosition)
        {
            
        }

        public void HideNativeAds(AdPosition  adPosition)
        {
            
        }

        public void RemoveAds()
        {
            
        }
#endif
    }
}

