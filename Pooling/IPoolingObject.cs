using UnityEngine;

namespace qtLib.Pooling
{
    public interface IPoolingObject
    {
        public object ObjectPoolID { get; }
        public GameObject GameObject { get; }
        public void OnGet();
        public void OnRelease();
    }
}