using Cysharp.Threading.Tasks;
using UnityEngine;

namespace qtLib.Singleton
{
    public class qtHiddenSingleton<T> : MonoBehaviour, IManualInit where T : MonoBehaviour
    {
        protected static T _instance
        {
            get;
            private set;
        }
    
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

        public virtual UniTask ManualInit()
        {
            return UniTask.CompletedTask;
        }
    }
}
