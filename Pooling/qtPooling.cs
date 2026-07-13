using System.Collections.Generic;
using qtLib.Singleton;
using UnityEngine;

namespace qtLib.Pooling
{
    public abstract class qtPooling<TInstance, TPool, TObject> : qtPooling<TInstance>
        where TInstance : MonoBehaviour
        where TPool : qtPool<TObject> 
        where TObject : qtPoolingObject
    {
        protected Dictionary<object, TPool> _pools = new();
    }
    
    public abstract class qtPooling<TInstance> : qtSingleton<TInstance>
        where TInstance : MonoBehaviour
    {
    }
}