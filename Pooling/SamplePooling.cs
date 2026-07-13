using System.Collections.Generic;
using qtLib.Helper;
using qtLib.Singleton;

namespace qtLib.Pooling
{
    public class SamplePooling : qtSingleton<SamplePooling>
    {
        #region ----- Component Config ------

        private Dictionary<int, SamplePool> _turretPools = new Dictionary<int, SamplePool>();
        private SamplePool _curPool;

        #endregion

        #region ----- Public Function -----

        public qtPoolingObject Get(int id)
        {
            if (!_turretPools.ContainsKey(id))
            {
                _turretPools.Add(id, new SamplePool(id));
            }

            _curPool = _turretPools[id];
            return _curPool.Get();
        }

        public void Release(qtPoolingObject target)
        {
            _curPool.Release(target);
        }
        #endregion
    }
}
