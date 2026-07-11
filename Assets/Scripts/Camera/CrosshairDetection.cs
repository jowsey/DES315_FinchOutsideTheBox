using Game.Items;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

public class CrosshairDetection : MonoBehaviour
{
    private Camera _camera;
    private Transform _uiCanvas;

    [SerializeField] private float _maxPickupDistance = 4.0f;
    [SerializeField] private float _maxPutdownDistance = 8.0f;

    [Header("UI")]
    [SerializeField] [Required] private InteractPrompt _interactPromptPrefab;

    [SerializeField] [Required] private ItemInfoCard _itemInfoCardPrefab;

    private InteractPrompt _interactPromptInstance;
    private ItemInfoCard _itemInfoCardInstance;

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
        if (_interactPromptInstance) CleanupInteractPrompt();
        if (_itemInfoCardInstance) CleanupItemInfoPrompt();
    }

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        if (!PlayerController.LocalPlayer) return;
        
        // todo it would be nice if players could pick stuff up through (dithered) walls
        // presumably this means doing a RaycastAll, filtering out non-interactable stuff, and
        // then doing a line-of-sight raycast from the player to the final interactable target?
        var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var didHit = Physics.Raycast(ray, out var hit, 100f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
        
        var maxReach = Mathf.Max(_maxPickupDistance, _maxPutdownDistance);
        
        var playerDistance = Vector3.Distance(hit.point, PlayerController.LocalPlayer.transform.position);
        
        // If no hit, cleanup old target
        if (!didHit || !hit.transform.TryGetComponent(out Interactable interactable) || playerDistance > maxReach)
        {
            TargetedTransform = null;
            CleanupPrompts();
            return;
        }

        var interactedTransform = interactable.InteractedTransform;

        // New target
        Item item = null;
        var validPickupTarget = playerDistance <= _maxPickupDistance
                                && PlayerController.LocalPlayer.PickupAllowed
                                && interactedTransform.TryGetComponent(out item)
                                && item.Pickuppable
                                && item.State == Item.ItemState.Idle;
        var validPutdownTarget = playerDistance <= _maxPutdownDistance
                                 && PlayerController.LocalPlayer.PutdownAllowed
                                 && interactedTransform.CompareTag("ObjectCarrier");

        var validTarget = validPickupTarget || validPutdownTarget;
        if (!validTarget)
        {
            CleanupPrompts();
        }
        else if (interactedTransform != TargetedTransform)
        {
            TargetedTransform = interactable.InteractedTransform;

            if (!_interactPromptInstance) _interactPromptInstance = Instantiate(_interactPromptPrefab, _uiCanvas);

            if (validPickupTarget)
            {
                if (!_itemInfoCardInstance) _itemInfoCardInstance = Instantiate(_itemInfoCardPrefab, _uiCanvas);

                _interactPromptInstance.Build(InteractPrompt.InteractionType.PickUp);
                _itemInfoCardInstance.Build(item.Data);

                // Position interact prompt to right
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0, 0.5f);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, 0);

                // Position item info card below it
                _itemInfoCardInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                ((RectTransform)_itemInfoCardInstance.transform).pivot = new Vector2(0, 1.0f);
                _itemInfoCardInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, -32);
            }
            else if (validPutdownTarget)
            {
                _interactPromptInstance.Build(InteractPrompt.InteractionType.PutDown);

                // Position to top of target
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform.GetComponentInChildren<HeldObjectPutdownTarget>().transform;
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0.5f, 0);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(0, -32);
            }
        }
    }
}