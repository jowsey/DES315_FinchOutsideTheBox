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
        private Canvas _parentCanvas;
        private CanvasRenderer[] _canvasRenderers;
        
        private void Awake()
        {
            _camera = Camera.main;
            _parentCanvas = GetComponentInParent<Canvas>();
            FindRenderers();
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
            
            transform.position = _camera.WorldToScreenPoint(trackingPosition) + (Vector3)UIPositionOffset * _parentCanvas.scaleFactor;

            var visible = transform.position.z >= 0;
            foreach (var canvasRenderer in _canvasRenderers)
            {
                canvasRenderer.cull = !visible;
            }
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        public void FindRenderers()
        {
            _canvasRenderers = GetComponentsInChildren<CanvasRenderer>();
        }
    }
}