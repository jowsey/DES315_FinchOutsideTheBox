using Mirror;
using PrimeTween;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Obstacles
{
    public class SucculentPad2 : MonoBehaviour
    {
        private const float TransitionDuration = 1.25f;

        [SerializeField] private float _jumpForce;

        [SerializeField] private Event _bounceSfx;
        [SerializeField] private LayerMask _bounceLayers;

        private float _lastBounceTime;
        private float _bounceCooldown = 0.35f;

        private void Start()
        {
            Tween.Scale(transform, Vector3.zero, transform.localScale, TransitionDuration, Ease.OutBounce);
        }

        private void OnCollisionStay(Collision collision)
        {
            if ((_bounceLayers.value & (1 << collision.gameObject.layer)) == 0) return;
            
            if (Time.time - _lastBounceTime < _bounceCooldown) return;
            if (collision.rigidbody.isKinematic) return;

            if (!collision.body.TryGetComponent(out NetworkTransformBase identity)) return;
            if (!identity.authority) return;

            _lastBounceTime = Time.time;
            collision.rigidbody.AddForce(transform.up * _jumpForce, ForceMode.Impulse);

            Bounce();
        }

        private void Bounce()
        {
            Sequence.Create()
                .Chain(Tween.ScaleY(transform, 1.2f, 0.18f, Ease.InOutBack))
                .Chain(Tween.ScaleY(transform, 1f, 0.16f, Ease.OutBack));

            _bounceSfx?.Post(gameObject);
        }
    }
}