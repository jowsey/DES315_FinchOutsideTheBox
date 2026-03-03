using UnityEngine;
using Sirenix.OdinInspector;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
public class Flask : Mirror.NetworkBehaviour
{
    public bool _highlighted;
    private MeshRenderer[] _renderers;
    [SerializeField] [Required] private Color _highlightedColour;
    private Color _unhighlightedColour;
    [SerializeField] [Required] private Transform _flaskPickupTarget;
    private Collider[] _colliders;
    private Rigidbody _rb;
    private bool _moving;

    private void Awake()
    {
        _highlighted = false;
        _renderers = GetComponentsInChildren<MeshRenderer>();
        _unhighlightedColour = _renderers[0].material.GetColor("_BaseColor");
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _moving = false;
    }

    private void Update()
    {
        bool beingLookedAt = (CrosshairDetection._hitTransform == transform) || GetComponentsInChildren<Transform>().Contains(CrosshairDetection._hitTransform);

        if (beingLookedAt && !_highlighted)
        {
            //Object is being looked at but is not highlighted, highlight it
            foreach (MeshRenderer renderer in _renderers)
            {
                renderer.material.SetColor("_BaseColor", _highlightedColour);
            }
            _highlighted = true;
        }
        else if (!beingLookedAt && _highlighted)
        {
            //Object isn't being looked at but is highlighted, unhighlight it
            foreach (MeshRenderer renderer in _renderers)
            {
                renderer.material.SetColor("_BaseColor", _unhighlightedColour);
            }
            _highlighted = false;
        }
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdPickup()
    {
        //Pickup involves disabling colliders and setting rigidbody to kinematic, then moving flask towards the pickup target
        //Movement of flask towards pickup target will be handled by NetworkRigidbody
        //Disabling colliders and setting rigidbody to kinematic needs to be done per-client though, so use an RPC
        RpcInitiatePickup();
        _moving = true;
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdPutdown()
    {
        RpcPutdown();
        _moving = false;
    }

    [Mirror.ClientRpc]
    public void RpcInitiatePickup()
    {
        foreach (Collider collider in _colliders) { collider.enabled = false; }
        _rb.isKinematic = true;
    }

    [Mirror.ClientRpc]
    public void RpcPutdown()
    {
        foreach (Collider collider in _colliders) { collider.enabled = true; }
        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!_moving) { return; }

        float movementSpeed = 10.0f;
        Vector3 newPos = _rb.position + (_flaskPickupTarget.position - _rb.position).normalized * Time.fixedDeltaTime * movementSpeed;
        _rb.MovePosition(newPos);

        if ((_rb.position - _flaskPickupTarget.position).sqrMagnitude < 1e-2)
        {
            CmdPutdown();
        }
    }
}
