#if ENABLE_ADS
using System;
using _Scripts.Entities;
using qtLib.Extension;
using _Scripts.GameFirebase;
using UnityEngine;

namespace qtLib.Ads
{
    public class AdsTimer : MonoBehaviour
    {
        #region ----- Variables -----
        
        private static long _adsTimer;
        private static float _time;
        
        private static float _timeBetweenAds = Definition.InterstitialInterval;
        
        private static bool _canShowAds = false;
        public static bool CanShowAds => _canShowAds;
        
        public static event Action OnShowAds;
        
        #endregion
        
        private void Start()
        {
            FirebaseManager.DoAction(nameof(Start), firebase =>
            {
                _timeBetweenAds = firebase.GetLongValue(FirebaseManager.RemoteConfigKey.TimeBetweenAds);
                return FirebaseManager.EErrorCode.OK;
            });
        
            _adsTimer = GameTimer.RegisterTimer(_timeBetweenAds, true, OnAdsTimeChanged, OnAdsTimerComplete);
        }
        
        private static void OnAdsTimeChanged(float time)
        {
            _time = time;
        }
        
        private static void OnAdsTimerComplete()
        {
            _canShowAds = true;
            OnShowAds?.Invoke();
        }
        
        public static void Pause(bool isPause)
        {
            GameTimer.PauseTimer(_adsTimer, isPause);
        }
        
        public static void ResetTimer()
        {
            GameTimer.UnRegisterTimer(_adsTimer);
            _adsTimer = GameTimer.RegisterTimer(_timeBetweenAds, true, OnAdsTimeChanged, OnAdsTimerComplete);
            _canShowAds = false;
        }
        
        public static void ExpandsTime()
        {
            if (_time <= 0.5f * _timeBetweenAds)
            {
                GameTimer.UnRegisterTimer(_adsTimer);
                _adsTimer = GameTimer.RegisterTimer(_timeBetweenAds * 0.5f, true, OnAdsTimeChanged, OnAdsTimerComplete);
            }
        }
    }
}
#endif
