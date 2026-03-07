using UnityEngine;

namespace UI
{
    public class WorldFollowUI : MonoBehaviour
    {
        public Transform TrackingTarget;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            transform.position = _camera.WorldToScreenPoint(TrackingTarget.position);
            transform.localScale = transform.position.z >= 0 ? Vector3.one : Vector3.zero;
        }
    }
}