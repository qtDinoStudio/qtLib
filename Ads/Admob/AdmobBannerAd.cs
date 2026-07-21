#if ENABLE_ADS

using System;
using _Scripts.Data;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using GoogleMobileAds.Api;

namespace qtLib.Ads.Admob
{
    public class AdmobBannerAd : IAdUnit
    {
        private BannerView _bannerView;
        private AdsManager.EvtAdPaid _onAdPaid;

        public AdmobBannerAd(string adUnitId, AdsManager.EvtAdPaid onAdPaid)
        {
            qtDebug.Log("AdmobBannerAd: Constructor");
            _onAdPaid = onAdPaid;
            
            _bannerView = new BannerView(adUnitId, AdSize.Banner, GoogleMobileAds.Api.AdPosition.Bottom);
            
            _bannerView.OnBannerAdLoaded += OnBannerAdLoaded;
            _bannerView.OnBannerAdLoadFailed += OnBannerAdLoadFailed;
            _bannerView.OnAdPaid += OnAdPaid;
            _bannerView.OnAdImpressionRecorded += OnAdImpressionRecorded;
            _bannerView.OnAdClicked += OnAdClicked;
            _bannerView.OnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
            _bannerView.OnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
            LoadAd();
        }
        
        public void LoadAd()
        {
            qtDebug.Log("AdmobBannerAd: LoadAd.");
            _bannerView.Hide();
            qtDebug.Log("AdmobBannerAd: Loading.");
            AdRequest adRequest = new AdRequest();
            _bannerView.LoadAd(adRequest);
        }

        public bool IsReady()
        {
            return true;
        }

        public void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            _bannerView.Show();
        }

        public void HideAds(Action callback = null)
        {
            _bannerView.Hide();
            _bannerView.Destroy();
            _bannerView = null;
        }

        #region ----- Callback -----

        private void OnBannerAdLoaded()
        {
            qtDebug.Log($"Banner view loaded an ad with response : {_bannerView.GetResponseInfo()}");
            LoadNewBannerAd();
            if (UserData.Instance.IsRemoveAds())
            {
                return;
            }
            ShowAds();
        }

        private void OnBannerAdLoadFailed(LoadAdError error)
        {
            qtDebug.LogError($"Banner view failed to load an ad with error : {error}");
        }

        private void OnAdPaid(AdValue adValue)
        {
            qtDebug.Log($"Banner view paid {adValue.Value} {adValue.CurrencyCode}.");
            _onAdPaid?.Invoke((AdsManager.AdType.Banner, AdsManager.AdPosition.All), adValue.Value, adValue.CurrencyCode);
        }
        
        private void OnAdImpressionRecorded()
        {
            qtDebug.Log("Banner view recorded an impression.");
        }
        
        private void OnAdClicked()
        {
            qtDebug.Log("Banner view was clicked.");
        }

        private void OnAdFullScreenContentOpened()
        {
            qtDebug.Log("Banner view full screen content opened.");
        }

        private void OnAdFullScreenContentClosed()
        {
            qtDebug.Log("Banner view full screen content closed.");
        }

        #endregion

        #region ----- Private Function -----

        private async UniTaskVoid LoadNewBannerAd()
        {
            await UniTask.Delay(TimeSpan.FromHours(1));
            HideAds();
            LoadAd();
        }

        #endregion
    }
}

#endif