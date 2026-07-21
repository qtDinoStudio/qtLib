#if ENABLE_ADS
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using qtLib.Helper;
using UnityEngine;


namespace qtLib.Ads.Admob
{
    public class AdmobNativeAd : AdmobAdUnit, IAdUnit
    {
        protected sealed override string _AdType() => "AdmobNativeAd";

        private NativeOverlayAd _nativeOverlayAd;
        
        public AdmobNativeAd((AdsManager.AdType adType, AdsManager.AdPosition adPosition) adInfor, string adUnitID, AdsManager.EvtAdReady onAdReady, AdsManager.EvtAdPaid onAdPaid) : base(adInfor, adUnitID, onAdReady, onAdPaid)
        {
            qtDebug.Log($"{_AdType()}: Constructor");
            LoadAd();
        }

        public sealed override void LoadAd()
        {
            qtDebug.Log($"{_AdType()}: LoadAd");
            if (_nativeOverlayAd != null)
            {
                _nativeOverlayAd.Destroy();
                _nativeOverlayAd = null;
            }

            qtDebug.Log($"{_AdType()}: Loading");
            var adRequest = new AdRequest();

            // Optional: Define native ad options.
            var options = new NativeAdOptions
            {
                AdChoicesPlacement = AdChoicesPlacement.TopRightCorner,
                MediaAspectRatio = MediaAspectRatio.Any,
            };

            // Send the request to load the ad.
            NativeOverlayAd.Load(_adUnitID, adRequest, options,
                async (NativeOverlayAd ad, LoadAdError error) =>
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
                    
                    _nativeOverlayAd = ad;
                    _nativeOverlayAd.OnAdClicked += OnAdClicked;
                    _nativeOverlayAd.OnAdFullScreenContentClosed +=  OnAdFullScreenContentClosed;
                    _nativeOverlayAd.OnAdFullScreenContentOpened +=  OnAdFullScreenContentOpened;
                    _nativeOverlayAd.OnAdPaid += OnAdPaid;
                    _nativeOverlayAd.OnAdImpressionRecorded += OnAdImpressionRecorded;
                });
        }

        public override void ShowAds(Action onCompleteCallback = null, Action onCloseCallback = null)
        {
            // Define a native template style with a custom style.
            var style = new NativeTemplateStyle
            {
                TemplateId = NativeTemplateId.Medium,
                MainBackgroundColor = Color.clear,
                CallToActionText = new NativeTemplateTextStyle()
                {
                    BackgroundColor = Color.clear,
                    FontSize = 9,
                    Style = NativeTemplateFontStyle.Bold
                }
            };
            
            // Renders a native overlay ad at the default size
            // and anchored to the bottom of the screen.
            int adSize = 600;
            _nativeOverlayAd.RenderTemplate(style, new AdSize(adSize,adSize), 100, 50);
        }

        public void HideAds(Action callback = null)
        {
            _nativeOverlayAd?.Hide();
        }

        public bool IsReady()
        {
            return _nativeOverlayAd != null;
        }
        
        protected override void OnAdFullScreenContentClosed()
        {
            base.OnAdFullScreenContentClosed();
            
            LoadAd();
        }
    }
}

#endif