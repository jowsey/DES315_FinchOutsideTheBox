using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ObstructionDitherer : MonoBehaviour
{
    [SerializeField] private float _fadeStartTime;
    [SerializeField] private float _fadeEndTime;
    [SerializeField] private float _fadeStartValue;
    [SerializeField] private float _fadeEndValue;
    [SerializeField] private LayerMask _obstructionMask;
    private Camera _camera;
    public static Transform PlayerTransform; //Set in PlayerController.OnStartLocalPlayer()
    private readonly Dictionary<Renderer, float> _activeRenderers = new(); //All renderers currently active (value = time that they have been active)
    private readonly HashSet<Renderer> _hitsThisFrame = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];
    private readonly Dictionary<Renderer, Material> _materialInstances = new();

    private void Start()
    {
        _camera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (!PlayerTransform) { return; }
        if (CameraZoomController.FirstPerson) { return; }

        _hitsThisFrame.Clear();

        //Sphere cast from camera towards player
        Vector3 dir = PlayerTransform.position - _camera.transform.position;
        float dist = dir.magnitude;
        int hits = Physics.SphereCastNonAlloc(_camera.transform.position, 0.25f, dir.normalized, _hitBuffer, dist, _obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; ++i)
        {
            if (_hitBuffer[i].transform.IsChildOf(PlayerTransform)) { continue; }

            Renderer r = _hitBuffer[i].collider.GetComponent<Renderer>();
            if (!r) { r = _hitBuffer[i].collider.GetComponentInChildren<Renderer>(); }
            if (!r) { continue; }
            if (r.sharedMaterial.shader.name != "Shader Graphs/Dithered" && r.sharedMaterial.shader.name != "Shader Graphs/DitheredPBR") { continue; }

            _hitsThisFrame.Add(r);

            //Get or create material instance
            if (!_materialInstances.TryGetValue(r, out Material mat))
            {
                mat = r.material;
                _materialInstances[r] = mat;
            }

            //Update time if entry already exists, or create new one if it doesn't
            if (_activeRenderers.TryGetValue(r, out float _))
            {
                _activeRenderers[r] += Time.deltaTime;
                _activeRenderers[r] = Mathf.Clamp(_activeRenderers[r], 0.0f, _fadeEndTime);
            }
            else
            {
                _activeRenderers.Add(r, 0.0f);
            }
        }

        //Restore renderers not being hit
        List<Renderer> keys = new(_activeRenderers.Keys); //because we're modifying the values (?) (weird language...)
        List<Renderer> toRemove = new(); //shoutout iterator invalidation for this one
        foreach (Renderer r in keys)
        {
            //Skip if renderer was hit this frame
            if (_hitsThisFrame.Contains(r)) { continue; }
            
            //Remove if the renderer doesn't exist anymore
            if (!r)
            {
                toRemove.Add(r);
                continue;
            }

            //Update time to unfade
            _activeRenderers[r] -= Time.deltaTime;

            //If time has reached <= 0, fade has disappeared (no dither), remove
            if (_activeRenderers[r] <= 0.0f)
            {
                toRemove.Add(r);
                continue;
            }
        }

        foreach (Renderer r in toRemove)
        {
            _activeRenderers.Remove(r);
            _materialInstances.Remove(r);
        }

        //Calculate dither based on active time
        foreach (KeyValuePair<Renderer, float> r in _activeRenderers)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(_fadeStartTime, _fadeEndTime, _activeRenderers[r.Key]));
            float dither = Mathf.Lerp(_fadeStartValue, _fadeEndValue, t);

            Color colour = r.Key.material.GetColor("_BaseColour");
            colour.a = dither;
            _materialInstances[r.Key].SetColor("_BaseColour", colour);
        }
    }

    //Called when first person mode is entered
    public void RemoveAllActiveDithers()
    {
        foreach (KeyValuePair<Renderer, Material> r in _materialInstances)
        {
            Color colour = r.Key.material.GetColor("_BaseColour");
            colour.a = 1.0f;
            r.Value.SetColor("_BaseColour", colour);
        }
        _materialInstances.Clear();
        _activeRenderers.Clear();
    }
}
