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
        }
    }
}