using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Obstacles
{
    public class SpinningCylinder : NetworkBehaviour
    {
        [SuffixLabel("deg/s")]
        [SerializeField] private float _spinSpeed;

        private Rigidbody _rb;

        private Quaternion _initialRotation;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _initialRotation = _rb.rotation;
        }

        private void FixedUpdate()
        {
            if (netId == 0) return;

            var angle = (float)((_spinSpeed * NetworkTime.time) % 360.0);

            var addedRotation = Quaternion.Euler(0, 0, angle);
            _rb.MoveRotation(_initialRotation * addedRotation);
        }
    }
}