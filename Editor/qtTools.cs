#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace qtLib.Editor
{
    [InitializeOnLoad]
    public static class qtTools
    {
        private const string MenuName = "qtTools/Always Start From First Scene";
        private static bool enabled;

        static qtTools()
        {
            // Đọc trạng thái khi Unity mở lên
            enabled = EditorPrefs.GetBool(MenuName, false);
        }

        [MenuItem(MenuName, priority = 20)]
        private static void Toggle()
        {
            enabled = !enabled;
            EditorPrefs.SetBool(MenuName, enabled);
            Menu.SetChecked(MenuName, enabled);
        }

        [MenuItem(MenuName, true, priority = 20)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuName, enabled);
            return true;
        }
        
        [MenuItem("qtTools/Clear/Clear PlayerPrefs", priority = 0)]
        public static void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("<color=yellow>✔ PlayerPrefs cleared.</color>");
        }

        [MenuItem("qtTools/Clear/Clear PersistentDataPath", priority = 0)]
        public static void ClearPersistentData()
        {
            string path = Application.persistentDataPath;

            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true); // Xoá cả folder + sub
                    Debug.Log($"<color=yellow>✔ Deleted persistentDataPath folder: {path}</color>");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error deleting persistentDataPath: {e.Message}");
                }
            }
            else
            {
                Debug.Log("<color=orange>No persistentDataPath found.</color>");
            }

            // Refresh lại để update Project view (nếu có file tạo lại)
            AssetDatabase.Refresh();
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadFirstSceneOnPlay()
        {
            if (!enabled)
            {
                return;
            }

            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                return;
            }

            string startScene = SceneUtility.GetScenePathByBuildIndex(0);

            if (!string.IsNullOrEmpty(startScene))
            {           
                var gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (gameObjects != null)
                {
                    foreach (GameObject gameObject in gameObjects)
                    {
                        UnityEngine.Object.DestroyImmediate(gameObject);
                    }
                }

                GC.Collect();

                SceneManager.LoadScene(startScene);
            }
        }
    }
}

#endif