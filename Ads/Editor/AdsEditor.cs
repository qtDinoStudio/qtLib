#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace qtLib.Ads.Editor
{
    [InitializeOnLoad]
    public class AdsEditor
    {
        private const string EnableDefineSymbol = "ENABLE_ADS";
        private const string TestAdsDefineSymbol = "IS_TEST_ADS";
        private const string MenuEnableAdsPath = "qtTools/Defines/ADS/ENABLE_ADS";
        private const string MenuEnableTestPath = "qtTools/Defines/ADS/ENABLE_TEST_ADS";

        static AdsEditor()
        {
            EditorApplication.delayCall += RefreshMenuCheckmark;
        }

        [MenuItem(MenuEnableAdsPath)]
        private static void ToggleEnableAdsLog()
        {
            bool isEnabled = HasDefine(EnableDefineSymbol);

            SetDefine(
                EnableDefineSymbol,
                !isEnabled
            );

            RefreshMenuCheckmark();
        }
        
        [MenuItem(MenuEnableTestPath)]
        private static void ToggleEnableTestAdsLog()
        {
            bool isEnabled = HasDefine(TestAdsDefineSymbol);

            SetDefine(
                TestAdsDefineSymbol,
                !isEnabled
            );

            RefreshMenuCheckmark();
        }

        // Hàm validate được Unity gọi để cập nhật dấu check của menu.
        [MenuItem(MenuEnableAdsPath, true)]
        private static bool ValidateToggleEnableLog()
        {
            RefreshMenuCheckmark();
            return true;
        }

        private static void RefreshMenuCheckmark()
        {
            Menu.SetChecked(
                MenuEnableAdsPath,
                HasDefine(EnableDefineSymbol)
            );
            Menu.SetChecked(
                MenuEnableTestPath,
                HasDefine(TestAdsDefineSymbol)
            );
        }

        private static bool HasDefine(string symbol)
        {
            BuildTargetGroup targetGroup =
                EditorUserBuildSettings.selectedBuildTargetGroup;

            if (targetGroup == BuildTargetGroup.Unknown)
                return false;

            string defineString =
                PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            return defineString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Contains(symbol);
        }

        private static void SetDefine(string symbol, bool enabled)
        {
            BuildTargetGroup targetGroup =
                EditorUserBuildSettings.selectedBuildTargetGroup;

            if (targetGroup == BuildTargetGroup.Unknown)
                return;

            string defineString =
                PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            HashSet<string> defines = defineString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToHashSet();

            bool changed = enabled
                ? defines.Add(symbol)
                : defines.Remove(symbol);

            if (!changed)
                return;

            string newDefineString = string.Join(
                ";",
                defines.OrderBy(x => x)
            );

            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                targetGroup,
                newDefineString
            );
        }
    }
}

#endif
