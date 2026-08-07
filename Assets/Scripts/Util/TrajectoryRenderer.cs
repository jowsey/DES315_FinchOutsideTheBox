using PrimeTween;
using UnityEngine;

namespace Util
{
    public class TrajectoryRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer _line;

        private Rigidbody _targetRb;
        private Vector3 _targetImpulse;

        private readonly Vector3[] _points = new Vector3[80];

        private void OnEnable()
        {
            Tween.Custom(Color.clear, _line.startColor, 0.5f, col => _line.startColor = col, Ease.OutCubic);
        }

        public void Build(Rigidbody target, Vector3 impulse)
        {
            _targetRb = target;
            _targetImpulse = impulse;

            RenderTrajectory();
        }

        private void RenderTrajectory()
        {
            var pointInterval = Time.fixedDeltaTime;

            var currentPosition = _targetRb.position;
            var currentVelocity = _targetRb.linearVelocity + _targetImpulse / _targetRb.mass;

            for (var i = 0; i < _points.Length; i++)
            {
                _points[i] = currentPosition;

                currentVelocity += Physics.gravity * pointInterval;
                currentVelocity *= Mathf.Max(0f, 1f - _targetRb.linearDamping * pointInterval);

                currentPosition += currentVelocity * pointInterval;
            }

            _line.positionCount = _points.Length;
            _line.SetPositions(_points);
        }
    }
}