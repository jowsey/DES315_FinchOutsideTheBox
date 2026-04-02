using UnityEngine;

namespace UI
{
    public class WorldFollowUI : MonoBehaviour
    {
        public Transform TrackingTarget;
        public Vector3 TrackingOffset;
        public bool ApplyOffsetLocally = false;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            var trackingPosition = ApplyOffsetLocally
                ? TrackingTarget.TransformPoint(TrackingOffset)
                : TrackingTarget.position + TrackingOffset;

            transform.position = _camera.WorldToScreenPoint(trackingPosition);
            transform.localScale = transform.position.z >= 0 ? Vector3.one : Vector3.zero;
        }
    }
}