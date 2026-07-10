using Game.Treasure;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

public class CrosshairDetection : MonoBehaviour
{
    private Camera _camera;
    private Transform _uiCanvas;

    [SerializeField] private float _maxDistance;

    [Header("UI")]
    [SerializeField] [Required] private InteractPrompt _interactPromptPrefab;

    private InteractPrompt _interactPromptInstance;

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

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        if (!PlayerController.LocalPlayer) return;

        // todo it would be nice if players could pick stuff up through (dithered) walls
        // presumably this means doing a RaycastAll, filtering out non-interactable stuff, and
        // then doing a line-of-sight raycast from the player to the final interactable target?
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var didHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);

        var hitWithinDistance = didHit && Vector3.Distance(PlayerController.LocalPlayer.transform.position, hit.point) <= _maxDistance;

        // If no hit, cleanup old target
        if (!hitWithinDistance || !hit.transform.TryGetComponent(out Interactable interactable))
        {
            TargetedTransform = null;

            if (_interactPromptInstance) CleanupInteractPrompt();

            return;
        }

        if (hit.transform == TargetedTransform) return;

        // New target
        TargetedTransform = interactable.InteractedTransform;

        // Interaction UI
        var viewingTreasure = PlayerController.LocalPlayer.PickupAllowed && TargetedTransform.TryGetComponent(out Treasure treasure) && treasure.State == Treasure.HoldableState.Idle;
        var viewingPutdownTarget = PlayerController.LocalPlayer.PutdownAllowed && TargetedTransform.CompareTag("ObjectCarrier");

        var showPrompt = viewingTreasure || viewingPutdownTarget;
        if (showPrompt)
        {
            if (!_interactPromptInstance)
            {
                _interactPromptInstance = Instantiate(_interactPromptPrefab, _uiCanvas);
            }

            if (viewingTreasure)
            {
                _interactPromptInstance.Build(InteractPrompt.InteractionType.PickUp);

                // Position to right of treasure
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0, 0.5f);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, 0);
            }
            else if (viewingPutdownTarget)
            {
                _interactPromptInstance.Build(InteractPrompt.InteractionType.PutDown);

                // Position to top of target
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform.GetComponentInChildren<HeldObjectPutdownTarget>().transform;
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0.5f, 0);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(0, -32);
            }
        }
        else
        {
            if (_interactPromptInstance) CleanupInteractPrompt();
        }
    }
}