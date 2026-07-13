using UnityEngine.Pool;

namespace qtLib.Pooling
{
    public abstract class qtPool<T> : IObjectPool<T> where T : qtPoolingObject
    {
        #region ----- Component Config -----

        // Collection checks will throw errors if we try to release an item that is already in the pool.
        protected virtual bool CollectionChecks() => true;
        protected virtual int MaxPoolSize() => 20;
        protected virtual int DefaultPoolSize() => 20;

        private IObjectPool<T> _objectPool;
        public int CountInactive => _objectPool.CountInactive;

        #endregion
        
        #region ----- Property -----

        protected IObjectPool<T> ObjectPool
        {
            get
            {
                if (_objectPool == null)
                {
                    _objectPool = new ObjectPool<T>(CreatePooledItem, _OnTakeFromPool, _OnReturnedToPool,
                        _OnDestroyPoolObject, CollectionChecks(), DefaultPoolSize(), MaxPoolSize());
                }

                return _objectPool;
            }
        }

        #endregion

        #region ----- Private Function -----

        protected abstract T CreatePooledItem();

        // Called when an item is returned to the pool using Release
        private void _OnReturnedToPool(T obj)
        {
            obj.OnRelease();
        }

        // Called when an item is taken from the pool using Get
        private void _OnTakeFromPool(T obj)
        {
            obj.OnGet();
        }

        // If the pool capacity is reached then any items returned will be destroyed.
        // We can control what the destroy behavior does, here we destroy the GameObject.
        private void _OnDestroyPoolObject(T obj)
        {
            obj.OnRelease();
            UnityEngine.Object.Destroy(obj.GameObject);
        }

        #endregion

        #region ----- Implement Function -----

        public T Get()
        {
            return ObjectPool.Get();
        }

        public PooledObject<T> Get(out T v)
        {
            return ObjectPool.Get(out v);
        }

        public void Release(T element)
        {
            lock (ObjectPool)
            {
                ObjectPool.Release(element);
            }
        }

        public void Clear()
        {
            ObjectPool.Clear();
        }


        #endregion
    }
}