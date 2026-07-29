using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public class YarnSegment : NetworkBehaviour
    {
        public const float MaxPullAckTimeout = 1f;
        [SerializeField] private float _pullForce = 600f;

        private Rigidbody _rb;
        private ConfigurableJoint _joint;

        private NetworkIdentity _activePuller;
        private float _lastPullTime = -Mathf.Infinity;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _joint = GetComponent<ConfigurableJoint>();
        }

        [Command(requiresAuthority = false)]
        public void CmdContinuePull(NetworkConnectionToClient sender = null)
        {
            _activePuller = sender!.identity;
            _lastPullTime = Time.time;
        }

        [Command(requiresAuthority = false)]
        public void CmdStopPull(NetworkConnectionToClient sender = null)
        {
            if (sender!.identity != _activePuller) return;
            _lastPullTime = -Mathf.Infinity;
        }

        private void FixedUpdate()
        {
            if (Time.time - _lastPullTime < MaxPullAckTimeout)
            {
                var direction = (transform.position - _joint.connectedBody.position).normalized;
                // _rb.AddForce(direction * _pullForce, ForceMode.Force);
                _rb.AddForce(direction * (_pullForce * Time.fixedDeltaTime), ForceMode.VelocityChange);
            }
        }
    }
}