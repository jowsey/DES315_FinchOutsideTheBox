using Mirror;
using PrimeTween;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Obstacles
{
    public class SucculentPad : NetworkBehaviour
    {
        private const float TransitionDuration = 1.25f;

        [SerializeField] private float _jumpForce;
        [SerializeField] private Transform _innerMesh;

        [SerializeField] private Event _bounceSfx;

        private Sequence _bounceTween;

        private void OnEnable()
        {
            Tween.Scale(transform, Vector3.zero, transform.localScale, TransitionDuration, Ease.OutBounce);
            Tween.LocalEulerAngles(
                _innerMesh,
                _innerMesh.localEulerAngles,
                _innerMesh.localEulerAngles + (Vector3.up * 360f * 1.5f),
                TransitionDuration,
                Ease.OutCubic,
                startDelay: TransitionDuration * 0.25f
            );
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * _jumpForce * 0.01f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_bounceTween.isAlive) return;

            _bounceTween = Sequence.Create()
                .Chain(Tween.ScaleY(transform, 1.35f, 0.18f, Ease.InOutBack))
                .Chain(Tween.ScaleY(transform, 1f, 0.16f, Ease.OutBack));

            _bounceSfx?.Post(gameObject);

            // Only apply forces if we have authority over the body
            // Nice easy way to check this is whether it's kinematic on our client
            var body = collision.collider.attachedRigidbody;
            if (!body.isKinematic)
            {
                body.AddForce(transform.up * _jumpForce, ForceMode.Impulse);
            }
        }
    }
}