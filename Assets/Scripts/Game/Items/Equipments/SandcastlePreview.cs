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

        public bool ValidPosition { get; private set; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private bool GetIsGrounded()
        {
            var bounds = _cartBoundsCollider.bounds;
            var distance = bounds.extents.y + 0.5f;
            var forward = _cartBoundsCollider.transform.forward;

            var center = Physics.Raycast(bounds.center, Vector3.down, out _, distance, _collisionLayers, QueryTriggerInteraction.Ignore);
            var front = Physics.Raycast(bounds.center + bounds.extents.z * forward, Vector3.down, out _, distance, _collisionLayers, QueryTriggerInteraction.Ignore);
            var back = Physics.Raycast(bounds.center - bounds.extents.z * forward, Vector3.down, out _, distance, _collisionLayers, QueryTriggerInteraction.Ignore);

            return front && center && back;
        }

        private int GetNumOverlapHits()
        {
            return Physics.OverlapBoxNonAlloc(
                _cartBoundsCollider.bounds.center,
                _cartBoundsCollider.bounds.extents,
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