using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class PlaceableEquipment : Equipment
    {
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private const float MaxPlaceDistance = 5f;

        private static readonly Color ValidPositionColour = new(0f, 1f, 0f, 0.5f);
        private static readonly Color InvalidPositionColour = new(1f, 0f, 0f, 0.5f);

        [SerializeField] protected GameObject _placePrefab;
        [SerializeField] protected GameObject _previewPrefab;
        
        private GameObject _previewInstance;
        private Renderer[] _previewRenderers;
        private MaterialPropertyBlock _mpb;

        private Camera _camera;

        private LayerMask _placeMask;

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

                        _previewRenderers = _previewInstance.GetComponentsInChildren<Renderer>();
                        foreach (var rnd in _previewRenderers)
                        {
                            rnd.SetPropertyBlock(_mpb);
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

                    foreach (var rnd in _previewRenderers)
                    {
                        rnd.GetPropertyBlock(_mpb);
                        _mpb.SetColor(BaseColorID, distanceToPlayer <= MaxPlaceDistance ? ValidPositionColour : InvalidPositionColour);
                        rnd.SetPropertyBlock(_mpb);
                    }
                }
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdPlace(Vector3 position, Quaternion rotation, NetworkConnectionToClient sender = null)
        {
            if (State != ItemState.Held) return;
            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != _holder) return;

            var distanceToPlayer = Vector3.Distance(_holder.transform.position, position);
            if (distanceToPlayer > MaxPlaceDistance) return;

            var instance = Instantiate(_placePrefab, position, rotation);
            NetworkServer.Spawn(instance);

            OnServerPlace(instance);
        }

        public override void TryUse()
        {
            base.TryUse();
            if (!_previewInstance) return;

            CmdPlace(_previewInstance.transform.position, _previewInstance.transform.rotation);
        }

        protected virtual void OnServerPlace(GameObject instance)
        {
            OnServerSuccessfulUse();
        }
    }
}