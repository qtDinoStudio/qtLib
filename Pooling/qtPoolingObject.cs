using UnityEngine;

namespace qtLib.Pooling
{
    public abstract class qtPoolingObject : MonoBehaviour, IPoolingObject
    {
        public abstract object ObjectPoolID { get; }

        public GameObject GameObject => gameObject;
        
        public virtual void OnGet()
        {
        }

        public virtual void OnRelease()
        {
        }
    }
}
