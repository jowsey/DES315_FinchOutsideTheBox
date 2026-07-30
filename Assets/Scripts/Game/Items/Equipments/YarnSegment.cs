using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public class YarnSegment : NetworkBehaviour
    {
        public YarnRope ParentRope;

        public const float MaxPullAckTimeout = 1f;
        [SerializeField] private float _pullSpeed = 15f;

        public Rigidbody Rb { get; private set; }
        public ConfigurableJoint Joint { get; private set; }

        private NetworkIdentity _activePuller;
        private float _lastPullTime = -Mathf.Infinity;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();
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

        [Command(requiresAuthority = false)]
        public void CmdDetachRope(NetworkConnectionToClient sender = null)
        {
            ParentRope.ServerDetach(Rb.position + transform.up * 0.5f);
        }

        private void FixedUpdate()
        {
            if (!isServer) return;

            if (Time.time - _lastPullTime < MaxPullAckTimeout)
            {
                var magnitudeDifference = _pullSpeed - Rb.linearVelocity.magnitude;

                var direction = (Rb.position - Joint.connectedBody.position).normalized;
                Rb.AddForce(direction * magnitudeDifference, ForceMode.VelocityChange);
            }
        }
    }
}