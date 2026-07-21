#if ENABLE_ADS

using GoogleMobileAds.Api;
using qtLib.Helper;

namespace qtLib.Ads.Admob
{
    public abstract class AdmobAdUnit : AdUnit
    {
        protected abstract string _AdType();
        protected AdmobAdUnit(string adUnitID, AdsManager.EvtAdPaid onAdPaid) : base(adUnitID, onAdPaid)
        {
        }

        protected AdmobAdUnit((AdsManager.AdType adType, AdsManager.AdPosition adPosition) adInfor, string adUnitID, AdsManager.EvtAdReady onAdReady, AdsManager.EvtAdPaid onAdPaid) : base(adInfor, adUnitID, onAdReady, onAdPaid)
        {
        }

        protected virtual void OnAdPaid(AdValue adValue)
        {
            qtDebug.Log($"{_AdType()}: Paid {adValue.Value} {adValue.CurrencyCode}.");
            _onAdPaid?.Invoke(_adInfor, adValue.Value, adValue.CurrencyCode);
        }

        protected virtual void OnAdImpressionRecorded()
        {
            qtDebug.Log($"{_AdType()}: Recorded an impression.");
        }

        protected virtual void OnAdClicked()
        {
            qtDebug.Log($"{_AdType()}: Was clicked.");
        }
        
        protected virtual void OnAdFullScreenContentOpened()
        {
            qtDebug.Log($"{_AdType()}: Full screen content opened.");
        }

        protected virtual void OnAdFullScreenContentClosed()
        {
            qtDebug.Log($"{_AdType()}: Full screen content closed.");
        }

        protected virtual void OnAdFullScreenContentFailed(AdError error)
        {
            qtDebug.LogError($"{_AdType()}: Failed to open full screen content with error : {error}");
        }
    }
}

#endif