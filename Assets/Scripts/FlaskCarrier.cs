using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class FlaskCarrier : NetworkBehaviour
{
    [SerializeField] private List<GameObject> _flasks = new();
    [SerializeField] private Collider _carryingBounds;
    [SerializeField] private Cart _cart;

    private Dictionary<GameObject, Vector3> _initialRelativePositions = new();
    
    [field: SerializeField] [field: Sirenix.OdinInspector.ReadOnly]
    public int CarriedFlasks { get; private set; }
    
    // Ratio of flasks currently being carried
    public float FlasksRemainingRatio => (float)CarriedFlasks / _flasks.Count;
    
    public int MaxFlasks => _flasks.Count;
    
    private void Awake()
    {
        foreach (var flask in _flasks)
        {
            _initialRelativePositions[flask] = transform.InverseTransformPoint(flask.transform.position);
        }
    }
    
    private void Update()
    {
        // todo can probably just track in trigger enter/exit
        CarriedFlasks = _flasks.Count(f => _carryingBounds.bounds.Contains(f.transform.position));
        if (isServer && CarriedFlasks == 0)
        {
            Checkpoint.respawnEvent.Invoke(_cart.checkpoints[_cart.currentCheckpointIndex]);
        }
    }

    public void ResetFlasks(bool includeOutOfBounds = false)
    {
        foreach (var flask in _flasks)
        {
            if (includeOutOfBounds || _carryingBounds.bounds.Contains(flask.transform.position))
            {
                var rb = flask.GetComponent<Rigidbody>();
                rb.position = transform.TransformPoint(_initialRelativePositions[flask]);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}