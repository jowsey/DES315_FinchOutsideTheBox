using Game;
using Game.Items;
using Game.Items.Equipments;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

public class InteractDetection : MonoBehaviour
{
    private struct InteractionData
    {
        public Transform Transform;
        public Item Item;
        public UpgradeSack Sack;
        public bool ValidPickupTarget;
        public bool ValidPutdownTarget;
        public bool ValidHookTarget;
        public bool ValidPullTarget;

        public bool AnyValid => ValidPickupTarget || ValidPutdownTarget || ValidHookTarget || ValidPullTarget;
    }

    private Camera _camera;

    [SerializeField] private float _maxInteractDistance = 3.0f;
    [SerializeField] private float _fallbackRadiusDistance = 4.0f;
    [SerializeField] private float _maxPutdownDistance = 8.0f;

    [Header("UI")] [SerializeField] [Required]
    private InteractPrompt _interactPromptPrefab;

    [SerializeField] [Required] private ItemInfoCard _itemInfoCardPrefab;

    private InteractPrompt _interactPromptInstance;
    private InteractPrompt _secondaryInteractPromptInstance;
    private ItemInfoCard _itemInfoCardInstance;

    private readonly Collider[] _nearbyHits = new Collider[64];

    [SerializeField] private InteractPrompt.InteractPromptConfiguration _pickupConfig;
    [SerializeField] private InteractPrompt.InteractPromptConfiguration _putdownConfig;
    [SerializeField] private InteractPrompt.InteractPromptConfiguration _storeConfig;
    [SerializeField] private InteractPrompt.InteractPromptConfiguration _attachHookConfig;
    [SerializeField] private InteractPrompt.InteractPromptConfiguration _pullRopeConfig;
    [SerializeField] private InteractPrompt.InteractPromptConfiguration _detachHookConfig;

    //The transform of the object currently being looked at
    public static Transform TargetedTransform { get; private set; }

    private uint _lastTargetMask;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void CleanupInteractPrompt()
    {
        _interactPromptInstance.Destroy();
        _interactPromptInstance = null;
    }

    private void CleanupSecondaryInteractPrompt()
    {
        _secondaryInteractPromptInstance.Destroy();
        _secondaryInteractPromptInstance = null;
    }

    private void CleanupItemInfoPrompt()
    {
        _itemInfoCardInstance.Destroy();
        _itemInfoCardInstance = null;
    }

    private InteractionData EvaluateTarget(Interactable interactable, float dist)
    {
        var interactedTransform = interactable.InteractedTransform;
        var sack = interactedTransform.GetComponent<UpgradeSack>();
        var item = sack?.StoredItem ?? interactedTransform.GetComponent<Item>();

        return new InteractionData
        {
            Transform = interactedTransform,
            Item = item,
            Sack = sack,
            ValidPickupTarget = dist <= _maxInteractDistance
                                && PlayerController.LocalPlayer.PickupAllowed
                                && item
                                && item.Pickuppable
                                && item.StateData is Item.IdleStateData or Item.SackCarriedStateData,
            ValidPutdownTarget = dist <= _maxPutdownDistance
                                 && PlayerController.LocalPlayer.PutdownAllowed
                                 && (interactedTransform.CompareTag("TreasureCarrier") || (sack && !sack.StoredItem)),
            ValidHookTarget = dist <= _maxInteractDistance
                              && PlayerController.LocalPlayer.UseAllowed
                              && PlayerController.LocalPlayer.HeldObject is YarnEquipment { IsHooking: false }
                              && interactedTransform.CompareTag("YarnHookTarget"),
            ValidPullTarget = dist <= _maxInteractDistance
                              && PlayerController.LocalPlayer.PickupAllowed
                              && interactedTransform.gameObject.layer == LayerMask.NameToLayer("Rope")
        };
    }

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        if (!PlayerController.LocalPlayer) return;

        var detectMask = LayerMask.GetMask("Item", "Cart", "Rope");
        var maxReach = Mathf.Max(_maxInteractDistance, _maxPutdownDistance);
        var playerPos = PlayerController.LocalPlayer.transform.position + Vector3.up * 1f;

        var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var didRayHit = Physics.SphereCast(ray, 0.1f, out var rayHit, 100f, detectMask);

        InteractionData target = default;
        var foundTarget = false;

        // Check if spherecast hit
        if (didRayHit && rayHit.transform.TryGetComponent(out Interactable rayInteractable))
        {
            var distance = Vector3.Distance(rayHit.point, playerPos);
            if (distance <= maxReach)
            {
                target = EvaluateTarget(rayInteractable, distance);
                foundTarget = target.AnyValid;
            }
        }

        // If not, check nearby
        if (!foundTarget)
        {
            var hits = Physics.OverlapSphereNonAlloc(playerPos, _fallbackRadiusDistance, _nearbyHits, detectMask);
            var closest = float.MaxValue;

            for (var i = 0; i < hits; i++)
            {
                var hitTransform = _nearbyHits[i].transform;
                if (!hitTransform.TryGetComponent(out Interactable nearbyInteractable)) continue;
                var distance = Vector3.Distance(_nearbyHits[i].ClosestPoint(playerPos), playerPos);

                if (distance >= closest) continue;

                var nearbyTarget = EvaluateTarget(nearbyInteractable, distance);
                if (nearbyTarget.AnyValid)
                {
                    target = nearbyTarget;
                    closest = distance;
                    foundTarget = true;
                }
            }
        }

        if (!foundTarget)
        {
            TargetedTransform = null;
            if (_interactPromptInstance) CleanupInteractPrompt();
            if (_secondaryInteractPromptInstance) CleanupSecondaryInteractPrompt();
            if (_itemInfoCardInstance) CleanupItemInfoPrompt();
            return;
        }

        var showInteractPrompt = target.AnyValid;
        var showInfoCard = target.ValidPickupTarget && target.Item.ShowInfoCard;

        if (!showInteractPrompt && _interactPromptInstance) CleanupInteractPrompt();
        if (!target.ValidPullTarget && _secondaryInteractPromptInstance) CleanupSecondaryInteractPrompt();
        if (!showInfoCard && _itemInfoCardInstance) CleanupItemInfoPrompt();

        if (!(showInteractPrompt || showInfoCard))
        {
            TargetedTransform = null;
            return;
        }

        // check whether the new prompt types are different, even if the transform is the same
        var targetMask = (target.ValidPickupTarget ? 1u : 0u) | (target.ValidPutdownTarget ? 2u : 0u) | (target.ValidHookTarget ? 4u : 0u) | (target.ValidPullTarget ? 8u : 0u);

        // Build/update prompts for new state
        if (target.Transform != TargetedTransform || _lastTargetMask != targetMask)
        {
            TargetedTransform = target.Transform;
            _lastTargetMask = targetMask;

            if (!_interactPromptInstance) _interactPromptInstance = Instantiate(_interactPromptPrefab, UIGlobals.MainCanvas.transform);

            if (target.ValidPickupTarget) _interactPromptInstance.Build(_pickupConfig);
            else if (target.ValidPutdownTarget) _interactPromptInstance.Build(target.Sack ? _storeConfig : _putdownConfig);
            else if (target.ValidHookTarget) _interactPromptInstance.Build(_attachHookConfig);
            else if (target.ValidPullTarget)
            {
                _interactPromptInstance.Build(_pullRopeConfig);

                if (!_secondaryInteractPromptInstance) _secondaryInteractPromptInstance = Instantiate(_interactPromptPrefab, UIGlobals.MainCanvas.transform);
                _secondaryInteractPromptInstance.Build(_detachHookConfig);
                _secondaryInteractPromptInstance.transform.localScale = Vector3.one * 0.75f;
            }

            _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;

            if (target.ValidPickupTarget)
            {
                // Interact prompt to right
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0, 0.5f);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, 0);

                if (showInfoCard)
                {
                    if (!_itemInfoCardInstance) _itemInfoCardInstance = Instantiate(_itemInfoCardPrefab, UIGlobals.MainCanvas.transform);
                    _itemInfoCardInstance.Build(target.Item.Data,
                        target.Item is Treasure ? ItemInfoCard.SubtextDisplayType.BuySpeculate : ItemInfoCard.SubtextDisplayType.UsageHint);

                    // Info card below it
                    _itemInfoCardInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                    ((RectTransform)_itemInfoCardInstance.transform).pivot = new Vector2(0, 1.0f);
                    _itemInfoCardInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, -32);
                }
            }
            else
            {
                // Interact prompt above target
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0.5f, 0);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(0, 16);

                if (target.ValidPullTarget)
                {
                    // Detach prompt below it
                    _secondaryInteractPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                    ((RectTransform)_secondaryInteractPromptInstance.transform).pivot = new Vector2(0.5f, 1.0f);
                    _secondaryInteractPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(0, -16);
                }
            }
        }
    }
}