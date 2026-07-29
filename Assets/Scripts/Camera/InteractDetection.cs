using Game.Items;
using Game.Items.Equipments;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

public class InteractDetection : MonoBehaviour
{
    private Camera _camera;
    private Transform _uiCanvas;

    [SerializeField] private float _maxInteractDistance = 4.0f;
    [SerializeField] private float _maxPutdownDistance = 8.0f;

    [Header("UI")] [SerializeField] [Required]
    private InteractPrompt _interactPromptPrefab;

    [SerializeField] [Required] private ItemInfoCard _itemInfoCardPrefab;

    private InteractPrompt _interactPromptInstance;
    private ItemInfoCard _itemInfoCardInstance;

    private readonly Collider[] _nearbyHits = new Collider[64];

    //The transform of the object currently being looked at
    public static Transform TargetedTransform { get; private set; }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
    }

    private void CleanupInteractPrompt()
    {
        _interactPromptInstance.Destroy();
        _interactPromptInstance = null;
    }

    private void CleanupItemInfoPrompt()
    {
        _itemInfoCardInstance.Destroy();
        _itemInfoCardInstance = null;
    }

    private void CleanupPrompts()
    {
        TargetedTransform = null;
        if (_interactPromptInstance) CleanupInteractPrompt();
        if (_itemInfoCardInstance) CleanupItemInfoPrompt();
    }
    
    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        if (!PlayerController.LocalPlayer) return;

        var detectMask = LayerMask.GetMask("Item", "Cart", "Rope");
        var maxReach = Mathf.Max(_maxInteractDistance, _maxPutdownDistance);

        var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var didRayHit = Physics.SphereCast(ray, 0.25f, out var rayHit, 100f, detectMask, QueryTriggerInteraction.Ignore);

        Interactable interactable = null;
        var playerPos = PlayerController.LocalPlayer.transform.position;
        var distanceToPlayer = Vector3.Distance(rayHit.point, playerPos);

        // todo spherecast prevents fallback if it hits something it doesnt end up using, do full checks before deciding whether to do fallback 

        // Check if spherecast hit
        if (!didRayHit || distanceToPlayer > maxReach || !rayHit.transform.TryGetComponent(out interactable))
        {
            // If not, check nearby
            var hits = Physics.OverlapSphereNonAlloc(playerPos, maxReach / 2f, _nearbyHits, detectMask);

            var closest = float.MaxValue;
            for (var i = 0; i < hits; i++)
            {
                var hitTransform = _nearbyHits[i].transform;
                if (!hitTransform.TryGetComponent(out Interactable nearbyInteractable)) continue;
                var distance = Vector3.Distance(hitTransform.position, playerPos);

                if (distance >= closest) continue;
                closest = distance;
                interactable = nearbyInteractable;
            }

            if (interactable)
            {
                distanceToPlayer = closest;
            }
            else
            {
                CleanupPrompts();
                return;
            }
        }

        var interactedTransform = interactable.InteractedTransform;

        // New target
        Item item = null;
        var validPickupTarget = distanceToPlayer <= _maxInteractDistance
                                && PlayerController.LocalPlayer.PickupAllowed
                                && interactedTransform.TryGetComponent(out item)
                                && item.Pickuppable
                                && item.State == Item.ItemState.Idle;

        var validPutdownTarget = distanceToPlayer <= _maxPutdownDistance
                                 && PlayerController.LocalPlayer.PutdownAllowed
                                 && interactedTransform.CompareTag("TreasureCarrier");

        var validHookTarget = distanceToPlayer <= _maxInteractDistance
                              && PlayerController.LocalPlayer.UseAllowed
                              && PlayerController.LocalPlayer.HeldObject is YarnEquipment { IsHooking: false }
                              && interactedTransform.CompareTag("YarnHookTarget");
        
        var validPullTarget = distanceToPlayer <= _maxInteractDistance
                              && PlayerController.LocalPlayer.PickupAllowed
                              && interactedTransform.gameObject.layer == LayerMask.NameToLayer("Rope");

        var showInteractPrompt = validPickupTarget || validPutdownTarget || validHookTarget || validPullTarget;
        var showInfoCard = validPickupTarget && item.ShowInfoCard;

        if (!showInteractPrompt && _interactPromptInstance) CleanupInteractPrompt();
        if (!showInfoCard && _itemInfoCardInstance) CleanupItemInfoPrompt();

        if (!(showInteractPrompt || showInfoCard))
        {
            TargetedTransform = null;
            return;
        }

        // Build/update prompts for new state
        if (interactedTransform != TargetedTransform)
        {
            TargetedTransform = interactable.InteractedTransform;

            if (!_interactPromptInstance) _interactPromptInstance = Instantiate(_interactPromptPrefab, _uiCanvas);

            if (validPickupTarget) _interactPromptInstance.Build(InteractPrompt.InteractionType.PickUp);
            else if (validPutdownTarget) _interactPromptInstance.Build(InteractPrompt.InteractionType.PutDown);
            else if (validHookTarget) _interactPromptInstance.Build(InteractPrompt.InteractionType.Attach);
            else if (validPullTarget) _interactPromptInstance.Build(InteractPrompt.InteractionType.Pull);
            _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;

            if (validPickupTarget)
            {
                // Interact prompt to right
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0, 0.5f);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, 0);

                if (showInfoCard)
                {
                    if (!_itemInfoCardInstance) _itemInfoCardInstance = Instantiate(_itemInfoCardPrefab, _uiCanvas);
                    _itemInfoCardInstance.Build(item.Data);

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
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(0, -32);
            }
        }
    }
}