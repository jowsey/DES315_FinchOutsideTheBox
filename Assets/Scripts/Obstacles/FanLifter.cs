using Mirror;
using PrimeTween;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Obstacles
{
    public class FanLifter : NetworkBehaviour
    {
        private const float TransitionDuration = 1.25f;

        [SerializeField] private float _jumpForce;
        [SerializeField] private Transform _innerMesh;

        [SerializeField] private Event _bounceSfx;

        [SerializeField] private LayerMask _bounceLayers;



        private void OnTriggerStay(Collider collision)
        {
            if ((_bounceLayers.value & (1 << collision.gameObject.layer)) == 0) return;
            
            if (collision.attachedRigidbody.isKinematic) return;

            if (!collision.attachedRigidbody.TryGetComponent(out NetworkTransformBase identity)) return;
            if (!identity.authority) return;

            if (PlayerController.LocalPlayer.JumpAction.action.IsPressed())
            collision.attachedRigidbody.AddForce(transform.up * _jumpForce, ForceMode.Force);

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
            _bounceSfx?.Post(gameObject);
        }
    }
}