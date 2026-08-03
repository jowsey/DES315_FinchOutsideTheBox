using Sirenix.OdinInspector;
using UnityEngine;

namespace Obstacles
{
    [InfoBox("Identical behaviour to SpinningCylinder but is intentionally not synced over the network, for use in the menu and such")]
    public class SpinningCylinderNonSynced : MonoBehaviour
    {
        [SuffixLabel("deg/s")]
        [SerializeField] private float _spinSpeed;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Quaternion totalRotation = Quaternion.Euler(0, 0, _spinSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(_rb.rotation * totalRotation);
        }
    }
}