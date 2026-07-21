using System;
using System.Collections.Generic;
using qtLib.CustomDebug;
using UnityEngine;

namespace qtLib.Helper
{
    [DefaultExecutionOrder(-100)]
    public class qtDependencyInjection : MonoBehaviour
    {
        #region ----- Variable -----

        private static bool _shuttingDown = false;
        private static object _lock = new object();
        private static qtDependencyInjection _instance;

        // Used to track any global components added at runtime.
        [SerializeField] private Dictionary<Type, Dictionary<uint, object>> _dictionary
            = new Dictionary<Type, Dictionary<uint, object>>();

        #endregion

        #region ----- Public Function
        
        private static qtDependencyInjection Instance
        {
            get
            {
                if (_shuttingDown)
                {
                    qtDebug.LogWarning("[Singleton] Instance '" + typeof(qtDependencyInjection) +
                                     "' already destroyed. Returning null.");
                    return null;
                }

                lock (_lock)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
                {
                    if (_instance != null)
                    {
                        return _instance;
                    }

                    // Search for existing instance.
                    _instance = FindFirstObjectByType<qtDependencyInjection>();

                    // Create new instance if one doesn't already exist.
                    if (_instance != null)
                    {
                        return _instance;
                    }

                    // Need to create a new GameObject to attach the singleton to.
                    var singletonObject = new GameObject("qtDependencyInjection");
                    _instance = singletonObject.AddComponent<qtDependencyInjection>();

                    return _instance;
                }
            }
        }

        public static void Add<T>(T obj, uint id = 0)
        {
            var typeDictionary = Instance._dictionary;
            var type = obj.GetType();

            if (typeDictionary.ContainsKey(type))
            {
                var objDictionary = typeDictionary[type];
                
                if (objDictionary.TryGetValue(id, out var value))
                {
                    objDictionary[id] = obj;
                    if (value != null)
                    {
                        qtDebug.LogWarning("[qtDependencyInjection] Global component of type <" + typeof(T).Name +
                                         "> ID \""
                                         + id + "\" already exist!");
                        return;
                    }

                }

                objDictionary.Add(id, obj);
                typeDictionary[type] = objDictionary;
                return;
            }

            var newDictionary = new Dictionary<uint, object>();
            newDictionary.Add(id, obj);

            typeDictionary.Add(type, newDictionary);
        }
        
        public static void Add<T>(Type type, T obj, uint id = 0)
        {
            var typeDictionary = Instance._dictionary;

            if (typeDictionary.ContainsKey(type))
            {
                var objDictionary = typeDictionary[type];
                
                if (objDictionary.TryGetValue(id, out var value))
                {
                    objDictionary[id] = obj;
                    if (value != null)
                    {
                        qtDebug.LogWarning("[qtDependencyInjection] Global component of type <" + typeof(T).Name +
                                         "> ID \""
                                         + id + "\" already exist!");
                        return;
                    }

                }

                objDictionary.Add(id, obj);
                typeDictionary[type] = objDictionary;
                return;
            }

            var newDictionary = new Dictionary<uint, object>();
            newDictionary.Add(id, obj);

            typeDictionary.Add(type, newDictionary);
        }

        public static T Get<T>(uint id = 0)
        {
            var typeDictionary = Instance._dictionary;

            var type = typeof(T);

            if (typeDictionary.ContainsKey(type) == false)
            {
                qtDebug.LogWarning("[qtDependencyInjection] Global component of type <" + typeof(T).Name + "> ID \""
                                 + id + "\" doesn't exist! Typo?");
                return default;
            }

            var objDictionary = typeDictionary[type];
            if (objDictionary.ContainsKey(id) == false)
            {
                qtDebug.LogWarning("[qtDependencyInjection] Global component of type <" + typeof(T).Name + "> ID \""
                                 + id + "\" doesn't exist! Typo?");
                return default;
            }

            return (T)objDictionary[id];
        }

        #endregion

        #region ----- Unity Event -----

        private void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _dictionary = new Dictionary<Type, Dictionary<uint, object>>();
        }


        #endregion

        #region ----- Private Function -----

        private void OnApplicationQuit()
        {
            _shuttingDown = true;
        }


        #endregion
        
    }
}