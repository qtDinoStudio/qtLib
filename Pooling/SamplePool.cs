using System;
using UnityEngine;

namespace qtLib.Pooling
{
    public class SamplePool : qtPool<qtPoolingObject>
    {
        #region ----- Component Config -----

        private qtPoolingObject _prefab;

        #endregion
        
        #region ----- Constructor -----

        public SamplePool(int id) : base()
        {
            throw new NotImplementedException();
        }

        #endregion
        protected override qtPoolingObject CreatePooledItem()
        {
            qtPoolingObject go = GameObject.Instantiate(_prefab);
            go.transform.position = Vector3.zero;
            return go;
        }
    }
}