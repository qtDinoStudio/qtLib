using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using qtLib.Helper;
using qtLib.Singleton;

namespace qtLib.Ads
{
    public partial class AdsManager : qtSingleton<AdsManager>, IManualInit
    {
        public enum AdPosition
        {
            All,
            LevelTransition,
        }

        public enum AdType
        {
            Banner,
            ImageInterstitial,
            RewardedInterstitial,
            RewardedVideo,
            AOA,
            Native,
            MediumRectangle,
            VideoInterstitial,
        }

        #region ----- Event -----
        
        public delegate void EvtAdReadyCallback(bool isReady);

        public delegate void EvtAdReady((AdType adType, AdPosition adPosition) adInfor, bool isReady);

        public delegate void EvtAdPaid((AdType adType, AdPosition adPosition) adInfor, float value, string code);
        
        #endregion

        private Dictionary<(AdType, AdPosition), List<EvtAdReadyCallback>> _dictAdEvent;

        #region ----- Implement Function -----

        protected override void _Init()
        {
            _dictAdEvent = new ();
            
            ManualInit();
        }

        #endregion

        #region ----- Public Function -----
        
        public void Subscribe((AdType adType, AdPosition adPosition) adInfor, EvtAdReadyCallback listener)
        {
            _dictAdEvent.TryAdd(adInfor, new List<EvtAdReadyCallback>());
            _dictAdEvent[adInfor].Add(listener);            
        }

        public void UnSubscribe((AdType adType, AdPosition adPosition) adInfor, EvtAdReadyCallback listener)
        {
            if (_dictAdEvent.ContainsKey(adInfor))
            {
                _dictAdEvent[adInfor].Remove(listener);
            }
        }

        #endregion
    }
}