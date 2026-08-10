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

        private Tween _alphaTween;

        private const float MaxedOutOscillateAmplitude = 0.012f;
        private const float MaxedOutOscillateSpeed = 60f;

        private float _maxedOutTime;

        private void OnEnable()
        {
            _alphaTween = Tween.Custom(Color.clear, _line.sharedMaterial.color, 0.75f, col => _line.sharedMaterial.color = col, Ease.OutCubic);
        }

        private void OnDestroy()
        {
            _alphaTween.Stop();
        }

        public void Build(Rigidbody target, Vector3 impulse, bool maxedOut = false)
        {
            _targetRb = target;
            _targetImpulse = impulse;

            if (maxedOut)
            {
                _maxedOutTime += Time.deltaTime;
                var multiplier = 1 + Mathf.Sin(_maxedOutTime * MaxedOutOscillateSpeed) * MaxedOutOscillateAmplitude;
                _targetImpulse *= multiplier;
            }
            else
            {
                _maxedOutTime = 0f;
            }

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