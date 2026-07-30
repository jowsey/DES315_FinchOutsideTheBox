using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class PlaceableEquipment : Equipment
    {
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColourID = Shader.PropertyToID("_BaseColour");
        private const float MaxPlaceDistance = 6f;

        private static readonly Color ValidPositionColour = new(0f, 1f, 0f, 0.5f);
        private static readonly Color InvalidPositionColour = new(1f, 0f, 0f, 0.5f);

        [SerializeField] protected GameObject _placePrefab;
        protected GameObject _placeInstance { get; private set; }

        [SerializeField] protected GameObject _previewPrefab;
        protected GameObject _previewInstance { get; private set; }

        [SerializeField, Range(0, 180f), SuffixLabel("degs")] private float _maxPlacementAngle = 30;

        private Renderer[] _previewRenderers;
        private bool _previewVisible = true;
        private MaterialPropertyBlock _mpb;
        private LayerMask _placeMask;
        private Camera _camera;

        private bool _previewBelievedValid;

        protected override void Awake()
        {
            base.Awake();
            _camera = Camera.main;
            _placeMask = ~LayerMask.GetMask("Player", "Cart", "Item");
        }

        protected override void OnStateChanged(ItemState oldState, ItemState newState)
        {
            switch (oldState)
            {
                case ItemState.Held:
                {
                    if (_previewInstance)
                    {
                        Destroy(_previewInstance);
                    }

                    break;
                }
            }

            switch (newState)
            {
                case ItemState.Held:
                {
                    if (_holder.isLocalPlayer && !_previewInstance)
                    {
                        _previewInstance = Instantiate(_previewPrefab);

                        _mpb = new MaterialPropertyBlock();
                        _mpb.SetColor(BaseColorID, ValidPositionColour);
                        _mpb.SetColor(BaseColourID, ValidPositionColour);

                        _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>();
                        foreach (var rnd in _previewRenderers)
                        {
                            rnd.SetPropertyBlock(_mpb);
                            rnd.enabled = _previewVisible;
                        }
                    }

                    break;
                }
            }

            base.OnStateChanged(oldState, newState);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (_previewInstance)
            {
                var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
                if (Physics.Raycast(ray, out var hit, 100f, _placeMask, QueryTriggerInteraction.Ignore))
                {
                    _previewInstance.transform.position = hit.point;
                    _previewInstance.transform.up = hit.normal;

                    var distanceToPlayer = Vector3.Distance(_holder.transform.position, hit.point);
                    var validPos = distanceToPlayer <= MaxPlaceDistance && Vector3.Angle(hit.normal, Vector3.up) <= _maxPlacementAngle;

                    foreach (var rnd in _previewRenderers)
                    {
                        rnd.GetPropertyBlock(_mpb);
                        var colour = validPos ? ValidPositionColour : InvalidPositionColour;
                        _mpb.SetColor(BaseColorID, colour);
                        _mpb.SetColor(BaseColourID, colour);
                        rnd.SetPropertyBlock(_mpb);
                    }

                    _previewBelievedValid = validPos;
                }
            }
        }

        protected virtual bool OnServerTryPlace(Vector3 position, Quaternion rotation, PlayerController player)
        {
            if (State != ItemState.Held) return false;
            if (player != _holder) return false;

            var distanceToPlayer = Vector3.Distance(_holder.transform.position, position);
            if (distanceToPlayer > MaxPlaceDistance) return false;

            var normalAngle = Vector3.Angle(Vector3.up, rotation * Vector3.up);
            if (normalAngle > _maxPlacementAngle) return false;

            _placeInstance = Instantiate(_placePrefab, position, rotation);
            NetworkServer.Spawn(_placeInstance);
            return true;
        }

        [Command(requiresAuthority = false)]
        private void CmdPlace(Vector3 position, Quaternion rotation, NetworkConnectionToClient sender = null)
        {
            if (OnServerTryPlace(position, rotation, sender!.identity.GetComponent<PlayerController>()))
            {
                OnServerUse();
            }
        }

        public override void TryUse()
        {
            base.TryUse();
            if (!_previewInstance || !_previewBelievedValid) return;

            CmdPlace(_previewInstance.transform.position, _previewInstance.transform.rotation);
        }

        protected void SetPreviewVisible(bool visible)
        {
            _previewVisible = visible;
            if (!_previewInstance) return;

            foreach (var rnd in _previewRenderers)
            {
                rnd.enabled = visible;
            }
        }
    }
}