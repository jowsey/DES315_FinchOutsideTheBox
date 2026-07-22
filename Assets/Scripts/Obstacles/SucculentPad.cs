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

        private float _lastBounceTime;
        private float _bounceCooldown = 0.35f;

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

        private void OnCollisionStay(Collision collision)
        {
            if (Time.time - _lastBounceTime < _bounceCooldown) return;
            _lastBounceTime = Time.time;

            PlayerController.LocalPlayer.Rb.AddForce(transform.up * _jumpForce, ForceMode.Impulse);

            CmdReportBounce();
        }

        [Command(requiresAuthority = false)]
        private void CmdReportBounce()
        {
            // todo this really really sucks
            RpcPlayerBounce();
        }

        [ClientRpc]
        private void RpcPlayerBounce()
        {
            Sequence.Create()
                .Chain(Tween.ScaleY(transform, 1.35f, 0.18f, Ease.InOutBack))
                .Chain(Tween.ScaleY(transform, 1f, 0.16f, Ease.OutBack));

            _bounceSfx?.Post(gameObject);
        }
    }
}