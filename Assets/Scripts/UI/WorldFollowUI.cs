using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldFollowUI : MonoBehaviour
    {
        public Transform TrackingTarget;
        public Vector3 TrackingOffset;
        public bool ApplyTrackingOffsetLocally = false;

        public Vector2 UIPositionOffset;

        private Camera _camera;
        private Canvas _parentCanvas;
        private CanvasGroup _canvasGroup;

        private float _followSpeed = 50f;
        
        private void Awake()
        {
            _camera = Camera.main;
            _parentCanvas = GetComponentInParent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>();
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
            
            var newPos = _camera.WorldToScreenPoint(trackingPosition) + (Vector3)UIPositionOffset * _parentCanvas.scaleFactor;
            transform.position = Vector3.Lerp(transform.position, newPos, 1 - Mathf.Exp(-_followSpeed * Time.deltaTime));

            var visible = newPos.z >= 0;
            _canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }
    }
}