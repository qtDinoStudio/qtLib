using UnityEngine;

namespace qtLib.Extension.Camera
{
    public class CameraYSortSetup : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera _camera;

        private void Awake()
        {
            _camera.transparencySortMode = TransparencySortMode.CustomAxis;
            _camera.transparencySortAxis = Vector3.up;
        }
    }
}