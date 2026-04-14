using UnityEngine;

namespace UI
{
    public class WorldFollowUI : MonoBehaviour
    {
        public Transform TrackingTarget;
        public Vector3 TrackingOffset;
        public bool ApplyTrackingOffsetLocally = false;

        public Vector2 UIPositionOffset;

        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void Start()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (!TrackingTarget) return;

            var trackingPosition = ApplyTrackingOffsetLocally
                ? TrackingTarget.TransformPoint(TrackingOffset)
                : TrackingTarget.position + TrackingOffset;

            transform.position = _camera.WorldToScreenPoint(trackingPosition) + (Vector3)UIPositionOffset;
            transform.localScale = transform.position.z >= 0 ? Vector3.one : Vector3.zero;
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }
    }
}