using Game;
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
        [SerializeField] private Transform _innerRotator;

        [SerializeField] private float _bounceScaling = 1.35f;
        [SerializeField] private Event _bounceSfx;
        [SerializeField] private LayerMask _bounceLayers;

        private float _lastBounceTime;
        private float _bounceCooldown = 0.35f;

        public override void OnStartServer()
        {
            base.OnStartServer();
            RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnRespawn.AddListener(OnRespawn);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            Tween.Scale(transform, Vector3.zero, transform.localScale, TransitionDuration, Ease.OutBounce);

            if (_innerRotator)
            {
                Tween.LocalEulerAngles(
                    _innerRotator,
                    _innerRotator.localEulerAngles,
                    _innerRotator.localEulerAngles + (Vector3.up * 360f * 1.5f),
                    TransitionDuration,
                    Ease.OutCubic,
                    startDelay: TransitionDuration * 0.25f
                );
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * _jumpForce * 0.01f);
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
                .Chain(Tween.ScaleY(transform, _bounceScaling, 0.18f, Ease.InOutBack))
                .Chain(Tween.ScaleY(transform, 1f, 0.16f, Ease.OutBack));

            _bounceSfx?.Post(gameObject);
        }

        private void OnBuildRespawnSnapshot(RespawnTarget.RespawnSnapshot snapshot)
        {
            snapshot.PlacedObjects.Add(gameObject);
        }

        private void OnRespawn(RespawnTarget target)
        {
            if (!target.Snapshot.PlacedObjects.Contains(gameObject))
            {
                NetworkServer.Destroy(gameObject);
            }
        }
    }
}