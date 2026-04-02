using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ObstructionDitherer : MonoBehaviour
{
    [SerializeField] private LayerMask _obstructionMask;
    private Camera _camera;
    public static Transform PlayerTransform; //Set in PlayerController.OnStartLocalPlayer()
    private readonly HashSet<Renderer> _hiddenRenderers = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

    private void Start()
    {
        _camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (!PlayerTransform) { return; }

        foreach (Renderer r in _hiddenRenderers)
        {
            if (r) { r.enabled = true; }
        }
        _hiddenRenderers.Clear();

        //Sphere cast from camera towards player
        Vector3 dir = PlayerTransform.position - _camera.transform.position;
        float dist = dir.magnitude;
        int hits = Physics.SphereCastNonAlloc(_camera.transform.position, 0.25f, dir.normalized, _hitBuffer, dist, _obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; ++i)
        {
            if (_hitBuffer[i].transform.IsChildOf(PlayerTransform)) { continue; }
            Renderer[] renderers = _hitBuffer[i].transform.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r && r.enabled)
                {
                    r.enabled = false;
                    _hiddenRenderers.Add(r);
                }
            }
        }
    }
}
