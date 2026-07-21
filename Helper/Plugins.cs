using UnityEngine;

namespace qtLib.Helper
{
    public class Plugins : MonoBehaviour
    {
        private void Awake()
        {
#if ENABLE_CUSTOM_DEBUG
            // SRDebug.Init();
#endif
        }
    }
}