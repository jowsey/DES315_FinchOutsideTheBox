using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FlaskCarrier : MonoBehaviour
{
    [SerializeField] private List<GameObject> _flasks = new();
    [SerializeField] private Collider _carryingBounds;

    [SerializeField] [Sirenix.OdinInspector.ReadOnly]
    private int _carriedFlasks;

    private Dictionary<GameObject, Vector3> _initialRelativePositions = new();

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
        _carriedFlasks = _flasks.Count(f => _carryingBounds.bounds.Contains(f.transform.position));
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