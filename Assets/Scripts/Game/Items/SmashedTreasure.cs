using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Items
{
    public class SmashedTreasure : MonoBehaviour
    {
        private float _smashImpulseForce = 0.2f;
        private DecalProjector _decalProjector;
        [SerializeField] private float _maxDecalDiameter;
        [SerializeField] private float _decalSizeIncreaseSpeed;

        void Awake()
        {
            _decalProjector = GetComponentInChildren<DecalProjector>();
            _decalProjector.transform.parent = null;
            _decalProjector.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void Start()
        {
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                rb.AddExplosionForce(_smashImpulseForce, transform.position, 2.0f, 0.1f, ForceMode.Impulse);
            }
        }

        void Update()
        {
            if (_decalProjector.size.x >= _maxDecalDiameter)
            {
                _decalProjector.size = new Vector3(_maxDecalDiameter, _maxDecalDiameter, 1.0f);
                return;
            }
            float newDiameter = _decalProjector.size.x + Time.deltaTime * _decalSizeIncreaseSpeed;
            _decalProjector.size = new Vector3(newDiameter, newDiameter, 1.0f);
        }
    }
}
