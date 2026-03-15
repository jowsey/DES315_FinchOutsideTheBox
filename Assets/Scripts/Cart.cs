using System.Collections.Generic;
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
    [field: SerializeField] public List<Checkpoint> Checkpoints { get; private set; }

    [field: SerializeField] public int CurrentCheckpointIndex { get; private set; } = -1;

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;

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
    public HashSet<Flask> CarriedFlasks = new();
    private Dictionary<Rigidbody, Vector3>[] _flasksAtCheckpoint;
    // The number of flasks we'll respawn with
    public int FlasksOnRespawn => _flasksAtCheckpoint[Mathf.Clamp(CurrentCheckpointIndex, 0, _flasksAtCheckpoint.Length - 1)].Count;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;

        _flasksAtCheckpoint = new Dictionary<Rigidbody, Vector3>[Checkpoints.Count];
        for (int i = 0; i < _flasksAtCheckpoint.Length; ++i)
        {
            _flasksAtCheckpoint[i] = new Dictionary<Rigidbody, Vector3>();
        }
        
        // First checkpoint runs on Frame 0 before flasks run OnTriggerEnter so we need to manually init
        // - Bounds check isn't perfectly accurate, but we can reasonably assume
        // there won't be flasks in the level that are both within the bounds of
        // the flask carrier on scene start yet not meant to be in the flask
        var allFlasks = FindObjectsByType<Flask>(FindObjectsSortMode.None);
        foreach (var flask in allFlasks)
        {
            if (_flaskBounds.bounds.Contains(flask.transform.position))
            {
                CarriedFlasks.Add(flask);
            }
        }
    }

    private void Start()
    {
        Checkpoint.RespawnEvent.AddListener(OnRespawn);
    }

    private void OnDestroy()
    {
        Checkpoint.RespawnEvent.RemoveListener(OnRespawn);
    }

    private void Update()
    {
        if (_devCheckpointBackAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex - 1);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != Checkpoints.Count - 1)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex + 1);
        }
    }

    private void FixedUpdate()
    {
        // Re-center rotation around local Z axis
        var rot = Mathf.DeltaAngle(transform.eulerAngles.z, 0);
        var rotExp = Mathf.Sign(rot) * Mathf.Pow(Mathf.Abs(rot), _tiltCorrectionScaling);
        _rb.AddTorque(_tiltCorrection * rotExp * transform.forward);
    }

    // Records the local positions of all CarriedFlasks and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointFlasksSnapshot()
    {
        _flasksAtCheckpoint[CurrentCheckpointIndex].Clear();
        
        Physics.SyncTransforms();
        foreach (Flask flask in CarriedFlasks)
        {
            Rigidbody flaskRb = flask.GetComponent<Rigidbody>();
            _flasksAtCheckpoint[CurrentCheckpointIndex][flaskRb] = transform.InverseTransformPoint(flask.transform.position);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            var newIndex = Checkpoints.IndexOf(checkpoint);

            if (newIndex > CurrentCheckpointIndex)
            {
                // New checkpoint reached
                CurrentCheckpointIndex = newIndex;
                Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");
                var checkpointBanner = Instantiate(_checkpointBannerPrefab, _uiCanvas.transform);
                checkpointBanner.Checkpoint = checkpoint;
                checkpointBanner.IsFirst = newIndex == 0;
                
                CaptureCheckpointFlasksSnapshot();
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

            ResetFlasks();
        }
    }

    public void ResetFlasks()
    {
        foreach (KeyValuePair<Rigidbody, Vector3> flaskState in _flasksAtCheckpoint[CurrentCheckpointIndex])
        {
            Rigidbody rb = flaskState.Key;
            rb.gameObject.SetActive(true);
            rb.position = transform.TransformPoint(flaskState.Value);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
        if (newCheckpointIndex < 0 || newCheckpointIndex >= Checkpoints.Count)
        {
            Debug.LogWarning($"Tried to respawn at invalid checkpoint index {newCheckpointIndex}");
            return;
        }

        CurrentCheckpointIndex = newCheckpointIndex;
        CaptureCheckpointFlasksSnapshot();
        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }
}