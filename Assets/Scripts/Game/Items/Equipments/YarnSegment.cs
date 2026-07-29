using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public class YarnSegment : NetworkBehaviour
    {
        public const float MaxPullAckTimeout = 1f;
        [SerializeField] private float _pullSpeed = 3f;

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
            if (!isServer) return;
            
            if (Time.time - _lastPullTime < MaxPullAckTimeout)
            {
                var magnitudeDifference = _pullSpeed - _rb.linearVelocity.magnitude;
                
                var direction = (_rb.position - _joint.connectedBody.position).normalized;
                _rb.AddForce(direction * magnitudeDifference, ForceMode.VelocityChange);
            }
        }
    }
}