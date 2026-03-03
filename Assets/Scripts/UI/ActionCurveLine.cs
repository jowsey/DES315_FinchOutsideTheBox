using UnityEngine;

namespace UI
{
    [ExecuteAlways]
    public class ActionCurveLine : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;

        public float HeightCurveMultiplier = 1f;
        public Vector3 EndPoint;

        [Min(0)] public int Midpoints = 5;

        public Transform StartFollowTarget;
        public Transform EndFollowTarget;

        public Vector3 StartTrackingOffset;
        public Vector3 EndTrackingOffset;

        public System.Func<bool> ShouldDestroy;

        private void OnValidate()
        {
            Rerender();
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
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                if (ShouldDestroy != null && ShouldDestroy())
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
    }
}