using UnityEngine;

namespace qtLib.Singleton
{
    public abstract class qtSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        public static T Instance => _instance;
    
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
            }
            else
            {
                _instance = this as T;
            }
            _Init();
        }
        
        protected virtual void _Init(){}
    }
}
