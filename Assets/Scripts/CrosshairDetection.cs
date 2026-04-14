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
        Destroy(_interactPromptInstance.gameObject);
        _interactPromptInstance = null;
    }

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var didHit = Physics.Raycast(ray, out RaycastHit hit, _maxDistance, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);

        // If no hit, cleanup old target
        if (!didHit || !hit.transform.TryGetComponent(out Interactable interactable))
        {
            TargetedTransform = null;

            if (_interactPromptInstance) CleanupInteractPrompt();

            return;
        }

        if (hit.transform == TargetedTransform) return;

        // New target
        TargetedTransform = interactable.InteractedTransform;

        // Interaction UI
        var viewingFlask = PlayerController.LocalPlayer.FlaskPickupAllowed && TargetedTransform.TryGetComponent(out Flask flask) && flask.State == Flask.FlaskState.Idle;
        var viewingPutdownTarget = PlayerController.LocalPlayer.FlaskPutdownAllowed && TargetedTransform.CompareTag("FlaskCarrier");

        var showPrompt = viewingFlask || viewingPutdownTarget;
        if (showPrompt)
        {
            if (!_interactPromptInstance)
            {
                _interactPromptInstance = Instantiate(_interactPromptPrefab, _uiCanvas);
            }

            if (viewingFlask)
            {
                _interactPromptInstance.Build(InteractPrompt.InteractionType.PickUp);

                // Position to right of flask
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform;
                ((RectTransform)_interactPromptInstance.transform).pivot = new Vector2(0, 0.5f);
                _interactPromptInstance.WorldFollowUI.UIPositionOffset = new Vector2(32, 0);
            }
            else if (viewingPutdownTarget)
            {
                _interactPromptInstance.Build(InteractPrompt.InteractionType.PutDown);

                // Position to top of target
                _interactPromptInstance.WorldFollowUI.TrackingTarget = TargetedTransform.GetComponentInChildren<FlaskPutdownTarget>().transform;
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