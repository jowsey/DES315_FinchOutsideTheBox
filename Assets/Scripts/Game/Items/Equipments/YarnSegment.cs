using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    [RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
    public class YarnSegment : NetworkBehaviour
    {
        public YarnRope ParentRope;

        public const float MaxPullAckTimeout = 1f;
        private const float ReachedDistance = 0.5f;
        [SerializeField] private float _maxPullSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _maxTemporalForceIncrease = 0.1f;
        [SerializeField, Min(0f)] private float _minimumTemporalForceMultiplier = 0.1f;

        public Rigidbody Rb { get; private set; }
        public ConfigurableJoint Joint { get; private set; }

        private PlayerController _activePuller;
        private float _lastPullTime = -Mathf.Infinity;
        private float _appliedForce;
        private bool _reached;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();
        }

        [Command(requiresAuthority = false)]
        public void CmdContinuePull(NetworkConnectionToClient sender = null)
        {
            if (!sender!.identity.TryGetComponent<PlayerController>(out var player)) return;
            ServerStartPull(player);
        }

        [Command(requiresAuthority = false)]
        public void CmdStopPull(NetworkConnectionToClient sender = null)
        {
            if (!_activePuller || sender!.identity.GetComponent<PlayerController>() != _activePuller) return;

            foreach (var segment in ParentRope.Segments)
            {
                segment._activePuller = null;
                segment._lastPullTime = -Mathf.Infinity;
                segment._reached = false;
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdDetachRope(NetworkConnectionToClient sender = null)
        {
            ParentRope.ServerDetach(Rb.position + Vector3.up * 0.75f);
        }

        [Server]
        public void ServerStartPull(PlayerController puller)
        {
            if (_reached)
            {
                var segments = ParentRope.Segments;
                for (var i = segments.IndexOf(this) - 1; i >= 0; i--)
                {
                    var segment = segments[i];
                    if (segment._reached) continue;
                    segment.ServerStartPull(puller);
                    return;
                }

                return;
            }

            _activePuller = puller;
            _lastPullTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (!isServer || _reached) return;
            if (!_activePuller || Time.time - _lastPullTime >= MaxPullAckTimeout)
            {
                _appliedForce = 0f;
                return;
            }

            var target = _activePuller.HeldObjectPickupTarget.position;
            if (Vector3.Distance(Rb.position, target) <= ReachedDistance)
            {
                _appliedForce = 0f;
                _reached = true;

                var segments = ParentRope.Segments;
                var index = segments.IndexOf(this);
                if (index > 0) segments[index - 1].ServerStartPull(_activePuller);
                return;
            }

            var magnitudeDifference = Mathf.Max(0f, _maxPullSpeed - Rb.linearVelocity.magnitude);
            var targetForce = (magnitudeDifference * magnitudeDifference) * (Rb.mass * 0.25f);
            _appliedForce = Mathf.Max(Mathf.Min(targetForce, _appliedForce * (1f + _maxTemporalForceIncrease)), targetForce * _minimumTemporalForceMultiplier);

            Rb.AddForce((target - Rb.position).normalized * _appliedForce, ForceMode.VelocityChange);
        }
    }
}