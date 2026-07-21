#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace qtLib.CustomDebug.Editor
{
    [InitializeOnLoad]
    public class IAPEditor
    {
        private const string EnableDefineSymbol = "ENABLE_IAP";
        private const string MenuEnableIAPPath = "qtTools/Defines/ENABLE_IAP";

        static IAPEditor()
        {
            EditorApplication.delayCall += RefreshMenuCheckmark;
        }

        [MenuItem(MenuEnableIAPPath)]
        private static void ToggleEnableAdsLog()
        {
            bool isEnabled = HasDefine(EnableDefineSymbol);

            SetDefine(
                EnableDefineSymbol,
                !isEnabled
            );

            RefreshMenuCheckmark();
        }
        
        // Hàm validate được Unity gọi để cập nhật dấu check của menu.
        [MenuItem(MenuEnableIAPPath, true)]
        private static bool ValidateToggleEnableLog()
        {
            RefreshMenuCheckmark();
            return true;
        }

        private static void RefreshMenuCheckmark()
        {
            Menu.SetChecked(
                MenuEnableIAPPath,
                HasDefine(EnableDefineSymbol)
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
