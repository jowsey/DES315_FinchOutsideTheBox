using TMPro;
using UnityEngine;

namespace UI
{
    [ExecuteAlways]
    public class ActionCurveLine : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Transform _promptLabel;

        public float HeightCurveMultiplier = 1f;
        public Vector3 EndPoint;

        [Min(0)] public int Midpoints = 15;

        public Transform StartFollowTarget;
        public Transform EndFollowTarget;

        public Vector3 StartTrackingOffset;
        public Vector3 EndTrackingOffset;

        public string PromptLabel;

        // Fire-and-forget predicate that runs every frame, will destroy self when returns true
        public System.Func<bool> ShouldDestroy;

        private static Canvas _canvas;
        private static Camera _camera;

        private void OnValidate()
        {
            Rerender();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (!_canvas) _canvas = FindAnyObjectByType<Canvas>();
                if (!_camera) _camera = FindAnyObjectByType<Camera>();

                _promptLabel.SetParent(_canvas.transform, false);
                _promptLabel.GetComponentInChildren<TextMeshProUGUI>().text = PromptLabel;
            }
        }

        private void Rerender()
        {
            if (StartFollowTarget) transform.position = StartFollowTarget.position + StartTrackingOffset;
            if (EndFollowTarget) EndPoint = EndFollowTarget.position + EndTrackingOffset;

            if (!_lineRenderer) return;
            _lineRenderer.positionCount = Midpoints + 2;
            _lineRenderer.SetPosition(0, transform.position);
            _lineRenderer.SetPosition(Midpoints + 1, EndPoint);

            var lineForward = (EndPoint - transform.position).normalized;
            var lineRight = Vector3.Cross(lineForward, Vector3.up).normalized;
            var lineUp = Vector3.Cross(lineRight, lineForward).normalized;

            for (var i = 1; i <= Midpoints; i++)
            {
                var t = (float)i / (Midpoints + 1);
                var midPoint = Vector3.Lerp(transform.position, EndPoint, t);
                midPoint += lineUp * (Mathf.Sin(t * Mathf.PI) * HeightCurveMultiplier);
                _lineRenderer.SetPosition(i, midPoint);
            }

            if (Application.isPlaying && _camera)
            {
                var promptWorldPos = Vector3.Lerp(transform.position, EndPoint, 0.5f) + lineUp * (HeightCurveMultiplier + 0.5f);
                _promptLabel.position = _camera.WorldToScreenPoint(promptWorldPos);
                _promptLabel.localScale = _promptLabel.position.z >= 0 ? Vector3.one : Vector3.zero; // todo migrate to WorldFollowUI
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                if (ShouldDestroy())
                {
                    Destroy(gameObject);
                    return;
                }

                Rerender();
            }
            else if (transform.hasChanged)
            {
                Rerender();
                transform.hasChanged = false;
            }
        }

        private void OnDestroy()
        {
            if (_promptLabel && _promptLabel.parent != transform)
            {
                Destroy(_promptLabel.gameObject);
            }
        }
    }
}