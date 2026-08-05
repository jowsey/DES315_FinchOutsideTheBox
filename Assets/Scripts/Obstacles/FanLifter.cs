using Mirror;
using UnityEngine;

namespace Obstacles
{
    public class FanLifter : NetworkBehaviour
    {
        [SerializeField] private float _jumpForce;
        [SerializeField] private LayerMask _bounceLayers;

        [SerializeField] public AK.Wwise.Event _windVent;

        private void OnTriggerStay(Collider collision)
        {
            if ((_bounceLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            if (collision.attachedRigidbody.isKinematic) return;

            if (!collision.attachedRigidbody.TryGetComponent(out NetworkTransformBase identity)) return;
            if (!identity.authority) return;

            if (PlayerController.LocalPlayer.JumpAction.action.IsPressed())
            {
                collision.attachedRigidbody.AddForce(transform.up * _jumpForce, ForceMode.Force);
            }
        }

        private void Start()
        {
            _windVent.Post(gameObject);
        }
    }
}