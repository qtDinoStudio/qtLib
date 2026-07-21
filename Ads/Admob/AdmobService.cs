#if ENABLE_ADS

using System;
using System.Collections.Generic;
using System.Linq;
using qtLib.Helper;
using GoogleMobileAds.Api;
using UnityEngine;

namespace qtLib.Ads.Admob
{
    public class AdmobService : AdService, IAdService
    {
        private Dictionary<AdsManager.AdType, Dictionary<AdsManager.AdPosition, IAdUnit>> _dictAd = new Dictionary<AdsManager.AdType, Dictionary<AdsManager.AdPosition, IAdUnit>>();
        
        public void Initialize(AdConfigModel ads, AdsManager.EvtAdReady onAdReady = null, AdsManager.EvtAdPaid onAdPaid = null)
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            qtDebug.Log($"Admob Service initialized");
            MobileAds.Initialize(status =>
            {
                qtDebug.Log($"Admob initialized: {status.getAdapterStatusMap().First().Key} {status.getAdapterStatusMap().First().Value}");
                
                for (var i = 0; i < ads.adTypes.Count; i++)
                {
                    AdType ad = ads.adTypes[i];
                    if (!_dictAd.ContainsKey(ad.adType))
                    {
                        _dictAd.Add(ad.adType, new Dictionary<AdsManager.AdPosition, IAdUnit>());
                    }

                    for (var j = 0; j < ad.adPositions.Length; j++)
                    {
                        AdPosition ap = ad.adPositions[j];
                        switch (ad.adType)
                        {
                            case AdsManager.AdType.Banner:
                            {
                                _bannerAdUnit = new AdmobBannerAd(ap.adUnitID, onAdPaid);
                                break;
                            }
                            case AdsManager.AdType.AOA:
                            {
                                _appOpenAdUnit = new AdmobAppOpenAd(ap.adUnitID, onAdPaid);
                                break;
                            }
                            case AdsManager.AdType.VideoInterstitial:
                            case AdsManager.AdType.ImageInterstitial:
                            {
                                _dictAd[ad.adType].Add(ap.adPosition,
                                    new AdmobInterstitialAd(ad.adPositions[j].adUnitID, onAdPaid));
                                break;
                            }
                            case AdsManager.AdType.RewardedInterstitial:
                            {
                                _dictAd[ad.adType].Add(ap.adPosition,
                                    new AdmobRewardedInterstitialAd((AdsManager.AdType.RewardedInterstitial, ad.adPositions[j].adPosition), 
                                        ad.adPositions[j].adUnitID, 
                                        onAdReady,
                                        onAdPaid));
                                break;
                            }
                            case AdsManager.AdType.RewardedVideo:
                            {
                                _dictAd[ad.adType].Add(ap.adPosition,
                                    new AdmobRewardedVideoAd((AdsManager.AdType.RewardedVideo, ad.adPositions[j].adPosition), 
                                        ad.adPositions[j].adUnitID,
                                        onAdReady,
                                        onAdPaid));
                                break;
                            }
                            case AdsManager.AdType.Native:
                            {
                                _dictAd[ad.adType].Add(ap.adPosition,
                                    new AdmobNativeAd((AdsManager.AdType.Native, ad.adPositions[j].adPosition), 
                                        ad.adPositions[j].adUnitID,
                                        onAdReady,
                                        onAdPaid));
                                break;
                            }
                            case AdsManager.AdType.MediumRectangle:
                            {
                                throw new NotImplementedException("Medium rectangle ads not implemented");
                            }
                            default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                        }
                    }
                }
                _isInitialized = true;
            });
        }
        
        public void ShowAppOpenAd()
        {
            if (!_appOpenAdUnit.IsReady())
            {
                return;
            }
            _appOpenAdUnit?.ShowAds();
        }

        #region ----- Banner Ads -----

        public void ShowBannerAd()
        {
            _bannerAdUnit?.ShowAds();
        }

        public void HideBannerAd()
        {
            _bannerAdUnit?.HideAds();
        }
        
        #endregion

        #region ----- Rewared Video Ads -----

        public bool IsAdReady(AdsManager.AdType adType, AdsManager.AdPosition adPosition)
        {
            if (!_dictAd.TryGetValue(adType, out var value))
            {
                return false;
            }

            if (!value.ContainsKey(adPosition))
            {
                return false;
            }
            return _dictAd[adType][adPosition].IsReady();
        }

        public void ShowAd(AdsManager.AdType adType, AdsManager.AdPosition adPosition, Action onCompleteCallback, Action onCloseCallback = null)
        {
            if (!_dictAd.TryGetValue(adType, out var value))
            {
                return;
            }

            if (!value.ContainsKey(adPosition))
            {
                return;
            }
            
            value[adPosition].ShowAds(onCompleteCallback, onCloseCallback);
        }

        #endregion

        #region ----- Native Overlay Ads -----

        public bool IsNativeAdsReady(AdsManager.AdPosition adPosition)
        {
            if (!_dictAd.TryGetValue(AdsManager.AdType.Native, out var value))
            {
                return false;
            }

            if (!value.ContainsKey(adPosition))
            {
                return false;
            }
            return _dictAd[AdsManager.AdType.Native][adPosition].IsReady();
        }
        
        public void ShowNativeOverlayAd(AdsManager.AdPosition adPosition)
        {
            _dictAd[AdsManager.AdType.Native][adPosition].ShowAds();
        }

        public void HideNativeOverlayAd(AdsManager.AdPosition adPosition)
        {
            _dictAd[AdsManager.AdType.Native][adPosition].HideAds();
        }
        
        #endregion
        
        public void RemoveAds()
        {
            _bannerAdUnit?.HideAds();
        }
    }
}

#endif