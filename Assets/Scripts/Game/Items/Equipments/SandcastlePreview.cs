using UnityEngine;

namespace Game.Items.Equipments
{
    public class SandcastlePreview : PlaceablePreview
    {
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        private readonly Collider[] _hits = new Collider[64];
        private MaterialPropertyBlock _mpb;

        [SerializeField] private BoxCollider _cartBoundsCollider;
        [SerializeField] private Renderer _cartBoundsRenderer;
        [SerializeField] private LayerMask _collisionLayers;

        private const float GroundedTolerance = 0.8f;

        public bool ValidPosition { get; private set; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private Vector3 GetScaledCenter() => _cartBoundsCollider.transform.TransformPoint(_cartBoundsCollider.center);
        private Vector3 GetScaledHalfExtents() => Vector3.Scale(_cartBoundsCollider.size, _cartBoundsCollider.transform.lossyScale) * 0.5f;

        private bool GetIsGrounded()
        {
            var rotation = _cartBoundsCollider.transform.rotation;
            var bottom = GetScaledCenter() - (rotation * Vector3.up * GetScaledHalfExtents().y);
            var forward = rotation * Vector3.forward * GetScaledHalfExtents().z;

            var center = Physics.Raycast(bottom, Vector3.down, out _, GroundedTolerance, _collisionLayers, QueryTriggerInteraction.Ignore);
            var front = Physics.Raycast(bottom + forward, Vector3.down, out _, GroundedTolerance, _collisionLayers, QueryTriggerInteraction.Ignore);
            var back = Physics.Raycast(bottom - forward, Vector3.down, out _, GroundedTolerance, _collisionLayers, QueryTriggerInteraction.Ignore);

            return center && front && back;
        }

        private int GetNumOverlapHits()
        {
            return Physics.OverlapBoxNonAlloc(
                GetScaledCenter(),
                GetScaledHalfExtents(),
                _hits,
                _cartBoundsCollider.transform.rotation,
                _collisionLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        private void Update()
        {
            ValidPosition = GetIsGrounded() && GetNumOverlapHits() == 0;

            _cartBoundsRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, ValidPosition ? Color.softGreen : Color.softRed);
            _cartBoundsRenderer.SetPropertyBlock(_mpb);
        }
    }
}