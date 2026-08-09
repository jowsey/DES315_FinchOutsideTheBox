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

        [SerializeField] private LayerMask _placeMask;

        [SerializeField] protected GameObject _placePrefab;
        protected GameObject _placeInstance { get; private set; }

        [SerializeField] protected PlaceablePreview _previewPrefab;
        protected PlaceablePreview _previewInstance { get; private set; }

        [SerializeField, Range(0, 180f), SuffixLabel("degs")] private float _maxPlacementAngle = 30;

        private bool _previewVisible = true;
        private MaterialPropertyBlock _mpb;
        private Camera _camera;

        private bool _previewBelievedValid;

        protected override void Awake()
        {
            base.Awake();
            _camera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        }

        protected override void UpdateState(ItemStateData oldState, ItemStateData newState)
        {
            switch (oldState)
            {
                case HeldStateData:
                {
                    if (_previewInstance)
                    {
                        Destroy(_previewInstance.gameObject);
                    }

                    break;
                }
            }

            switch (newState)
            {
                case HeldStateData heldData:
                {
                    if (heldData.Holder.isLocalPlayer && !_previewInstance)
                    {
                        _previewInstance = Instantiate(_previewPrefab);

                        _mpb = new MaterialPropertyBlock();
                        _mpb.SetColor(BaseColorID, ValidPositionColour);
                        _mpb.SetColor(BaseColourID, ValidPositionColour);

                        foreach (var rnd in _previewInstance.ColouredRenderers)
                        {
                            rnd.SetPropertyBlock(_mpb);
                            rnd.enabled = _previewVisible;
                        }
                    }

                    break;
                }
            }

            base.UpdateState(oldState, newState);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (_previewInstance && StateData is HeldStateData heldData)
            {
                var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
                if (Physics.Raycast(ray, out var hit, 100f, _placeMask, QueryTriggerInteraction.Ignore))
                {
                    var lerpT = 1 - Mathf.Exp(-20f * Time.deltaTime);
                    _previewInstance.transform.position = Vector3.Lerp(_previewInstance.transform.position, hit.point, lerpT);
                    _previewInstance.transform.rotation = Quaternion.Slerp(
                        _previewInstance.transform.rotation,
                        Quaternion.LookRotation(_previewInstance.transform.position - heldData.Holder.transform.position, hit.normal),
                        lerpT
                    );

                    var distanceToPlayer = Vector3.Distance(heldData.Holder.transform.position, hit.point);
                    var validPos = distanceToPlayer <= MaxPlaceDistance && Vector3.Angle(hit.normal, Vector3.up) <= _maxPlacementAngle;

                    foreach (var rnd in _previewInstance.ColouredRenderers)
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
            if (StateData is not HeldStateData heldData) return false;
            if (player != heldData.Holder) return false;

            var distanceToPlayer = Vector3.Distance(heldData.Holder.transform.position, position);
            if (distanceToPlayer > MaxPlaceDistance) return false;

            var normalAngle = Vector3.Angle(Vector3.up, rotation * Vector3.up);
            if (normalAngle > _maxPlacementAngle) return false;

            _placeInstance = Instantiate(_placePrefab, position, rotation);
            return true;
        }

        [Command(requiresAuthority = false)]
        private void CmdPlace(Vector3 position, Quaternion rotation, NetworkConnectionToClient sender = null)
        {
            if (OnServerTryPlace(position, rotation, sender!.identity.GetComponent<PlayerController>()))
            {
                OnServerUse();
                NetworkServer.Spawn(_placeInstance);
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

            foreach (var rnd in _previewInstance.ColouredRenderers)
            {
                rnd.enabled = visible;
            }
        }
    }
}