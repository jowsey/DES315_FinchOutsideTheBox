using System.Collections.Generic;
using System.Linq;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    private Rigidbody _rb;
    
    [ValidateInput("@gameObject.scene.isLoaded ? $value.Count > 0 : true", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [SerializeField] private List<Checkpoint> _checkpoints;
    [field: SerializeField] public int CurrentCheckpointIndex { get; private set; }

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;
    
    [SerializeField] [Required] private InputActionReference _respawnAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointBackAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointForwardAction;

    [Tooltip("Base amount of tilt-correct to apply. Higher reduces overall amount of tilting.")]
    [SerializeField] private float _tiltCorrection = 1.1f;
    [Tooltip("Exponent for how much the amount of tilt-correction increases in response to tilting. 1 means consistent, higher makes it kick in far more when tilting more.")]
    [SerializeField] private float _tiltCorrectionScaling = 2f;
    
    // UI
    private Transform _uiCanvas;
    
    // Flask carrying
    [SerializeField] [Required] private Collider _flaskBounds;
    
    private Dictionary<GameObject, Vector3> _initialFlaskPositions = new();
    
    [field: SerializeField] [field: Sirenix.OdinInspector.ReadOnly] public int CarriedFlasks { get; private set; }
    
    [ValidateInput("@$value.Count > 0", "Cart doesn't have any flasks linked.", InfoMessageType.Warning)]
    [SerializeField] private List<GameObject> _trackedFlasks = new();
    
    public int MaxFlasks => _trackedFlasks.Count;
    
    // Ratio of flasks currently being carried
    public float FlasksRemainingRatio => (float)CarriedFlasks / _trackedFlasks.Count;

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
    }
    
    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
        
        foreach (var flask in _trackedFlasks)
        {
            _initialFlaskPositions[flask] = transform.InverseTransformPoint(flask.transform.position);
        }
    }
    
    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
    }
    
    private void Update()
    {
        if (_devCheckpointBackAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex - 1);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != _checkpoints.Count - 1)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex + 1);
        }
        
        // todo bad perf, should can probably just track in bounds trigger enter/exit
        CarriedFlasks = _trackedFlasks.Count(f => _flaskBounds.bounds.Contains(f.transform.position));
        if (isServer && CarriedFlasks == 0 && MaxFlasks > 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex);
        }
    }

    private void FixedUpdate()
    {
        // Re-center rotation around local Z axis
        var rot = Mathf.DeltaAngle(transform.eulerAngles.z, 0);
        var rotExp = Mathf.Sign(rot) * Mathf.Pow(Mathf.Abs(rot), _tiltCorrectionScaling);
        _rb.AddTorque(_tiltCorrection * rotExp * transform.forward);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            var newIndex = _checkpoints.IndexOf(checkpoint);
            
            if (newIndex > CurrentCheckpointIndex)
            {
                // New checkpoint reached
                CurrentCheckpointIndex = newIndex;
                Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");

                var checkpointBanner = Instantiate(_checkpointBannerPrefab, _uiCanvas.transform);
                checkpointBanner.Checkpoint = checkpoint;
            }
        }
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        if (authority)
        {
            Transform newTransform = checkpoint.cartRespawnLocalTransform;
            gameObject.SetActive(false);
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb.isKinematic) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.position = newTransform.position;
            transform.rotation = newTransform.rotation;
            gameObject.SetActive(true);

            ResetFlasks(true);
        }
    }
    
    public void ResetFlasks(bool includeOutOfBounds = false)
    {
        foreach (var flask in _trackedFlasks)
        {
            if (includeOutOfBounds || _flaskBounds.bounds.Contains(flask.transform.position))
            {
                var rb = flask.GetComponent<Rigidbody>();
                rb.position = transform.TransformPoint(_initialFlaskPositions[flask]);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
    
    [Command(requiresAuthority = false)]
    public void CmdInvokeRespawnEvent(int newCheckpointIndex)
    {
        RpcInvokeRespawnEvent(newCheckpointIndex);
    }

    [ClientRpc]
    private void RpcInvokeRespawnEvent(int newCheckpointIndex)
    {
        if (newCheckpointIndex < 0 || newCheckpointIndex >= _checkpoints.Count)
        {
            Debug.LogWarning($"Tried to respawn at invalid checkpoint index {newCheckpointIndex}");
            return;
        }

        CurrentCheckpointIndex = newCheckpointIndex;
        Checkpoint.respawnEvent.Invoke(_checkpoints[CurrentCheckpointIndex]);
    }
}