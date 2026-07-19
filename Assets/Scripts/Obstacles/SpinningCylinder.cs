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

        public override void OnStartClient()
        {
            base.OnStartClient();
            _rb = GetComponent<Rigidbody>();

            Quaternion elapsedRotation = Quaternion.Euler(0, 0, (float)(NetworkTime.time * _spinSpeed));
            _rb.MoveRotation(_rb.rotation * elapsedRotation);
        }

        private void FixedUpdate()
        {
            Quaternion totalRotation = Quaternion.Euler(0, 0, _spinSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(_rb.rotation * totalRotation);
        }
    }
}
