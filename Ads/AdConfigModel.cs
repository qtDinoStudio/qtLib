using System;
using System.Collections.Generic;
using UnityEngine;

namespace qtLib.Ads
{
        [Serializable]
        [CreateAssetMenu(fileName = "Ads Config Data", menuName = "Ads/Ads Config Data")]
        public class AdConfigModel : ScriptableObject
        {
                [SerializeField] private List<AdType> _adTypes = new List<AdType>();
                public List<AdType> adTypes => _adTypes;
        }

        [Serializable]
        public class AdType
        {
                [SerializeField] private AdsManager.AdType _adType;
                [SerializeField] private AdPosition[] _adPositions;

                public AdsManager.AdType adType => _adType;
                public AdPosition[] adPositions => _adPositions;

        }

        [Serializable]
        public class AdPosition
        {
                [SerializeField] private AdsManager.AdPosition _adPosition;

                [Space] 
                [SerializeField] private string _editorAdrAdUnitID;
                [SerializeField] private string _editorIOSAdUnitID;
                [SerializeField] private string _adrAdUnitID;
                [SerializeField] private string _iosAdUnitID;

                public AdsManager.AdPosition adPosition => _adPosition;

#if IS_TEST_ADS
#if UNITY_EDITOR
                public string adUnitID => _editorAdrAdUnitID;
#elif UNITY_ANDROID
            public string adUnitID => _editorAdrAdUnitID;
#elif UNITY_IOS
            public string adUnitID => _editorIOSAdUnitID;
#endif
#else
    #if UNITY_EDITOR
            public string adUnitID => _editorIOSAdUnitID;
    #elif UNITY_ANDROID
            public string adUnitID => _adrAdUnitID;
    #elif UNITY_IOS
            public string adUnitID => _iosAdUnitID;
    #endif
#endif
        }

}