using System.Collections.Generic;
using UnityEngine;

public class ObstructionDitherer : MonoBehaviour
{
    private static readonly int BaseColour = Shader.PropertyToID("_BaseColour");
    
    [SerializeField] private float _fadeStartTime;
    [SerializeField] private float _fadeEndTime;
    [SerializeField] private float _fadeStartValue;
    [SerializeField] private float _fadeEndValue;
    [SerializeField] private float _castThickness;
    [SerializeField] private LayerMask _obstructionMask;
    private Camera _camera;
    public static Transform PlayerTransform; //Set in PlayerController.OnStartLocalPlayer()
    private readonly Dictionary<Material, float> _activeMaterials = new(); //All materials currently active (value = time that they have been active)
    private readonly HashSet<Material> _hitsThisFrame = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];
    
    private readonly List<Material> _activeKeys = new();
    private readonly List<Material> _sharedMaterialBuffer = new();
    private readonly List<Material> _materialBuffer = new();
    private readonly List<Material> _toRemove = new();

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
        int hits = Physics.SphereCastNonAlloc(_camera.transform.position, _castThickness, dir.normalized, _hitBuffer, dist, _obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; ++i)
        {
            //prevent over-extending slightly past player from sphere-cast radius
            float hitDist = Vector3.Dot(_hitBuffer[i].point - _camera.transform.position, dir.normalized);
            if (hitDist >= dist) { continue; }
            
            if (_hitBuffer[i].transform.IsChildOf(PlayerTransform)) { continue; }

            Renderer r = _hitBuffer[i].collider.GetComponentInChildren<Renderer>();
            if (!r) { continue; }

            r.GetSharedMaterials(_sharedMaterialBuffer);
            bool hasDitheredMaterial = false;
            foreach (Material sharedMat in _sharedMaterialBuffer)
            {
                if (sharedMat && sharedMat.shader.name is "Shader Graphs/Dithered" or "Shader Graphs/DitheredPBR" or "Shader Graphs/DitheredPBRTriplanar")
                {
                    hasDitheredMaterial = true;
                    break;
                }
            }
            if (!hasDitheredMaterial) { continue; }

            //Get or create material instances
            r.GetMaterials(_materialBuffer);
            foreach (Material mat in _materialBuffer)
            {
                if (!mat || mat.shader.name is not ("Shader Graphs/Dithered" or "Shader Graphs/DitheredPBR" or "Shader Graphs/DitheredPBRTriplanar")) { continue; }

                _hitsThisFrame.Add(mat);

                //Update time if entry already exists, or create new one if it doesn't
                if (_activeMaterials.TryGetValue(mat, out float _))
                {
                    _activeMaterials[mat] += Time.deltaTime;
                    _activeMaterials[mat] = Mathf.Clamp(_activeMaterials[mat], 0.0f, _fadeEndTime);
                }
                else
                {
                    _activeMaterials.Add(mat, 0.0f);
                }
            }
        }

        //Restore materials not being hit
        _activeKeys.Clear();
        _activeKeys.AddRange(_activeMaterials.Keys); //because we're modifying the values (?) (weird language...)
        _toRemove.Clear();
        foreach (Material mat in _activeKeys)
        {
            //Skip if material was hit this frame
            if (_hitsThisFrame.Contains(mat)) { continue; }

            //Remove if the material doesn't exist anymore
            if (!mat)
            {
                _toRemove.Add(mat);
                continue;
            }

            //Update time to unfade
            _activeMaterials[mat] -= Time.deltaTime;

            //If time has reached <= 0, fade has disappeared (no dither), remove
            if (_activeMaterials[mat] <= 0.0f)
            {
                _toRemove.Add(mat);
                continue;
            }
        }

        foreach (Material mat in _toRemove)
        {
            _activeMaterials.Remove(mat);
            if (mat) { RestoreAlpha(mat); }
        }

        //Calculate dither based on active time
        foreach (KeyValuePair<Material, float> mat in _activeMaterials)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(_fadeStartTime, _fadeEndTime, _activeMaterials[mat.Key]));
            float dither = Mathf.Lerp(_fadeStartValue, _fadeEndValue, t);

            Color colour = mat.Key.GetColor(BaseColour);
            colour.a = dither;
            mat.Key.SetColor(BaseColour, colour);
        }
    }

    public void RemoveAllActiveDithers()
    {
        _activeKeys.Clear();
        _activeKeys.AddRange(_activeMaterials.Keys);
        foreach (Material mat in _activeKeys)
        {
            if (mat) { RestoreAlpha(mat); }
        }
        _activeMaterials.Clear();
        _hitsThisFrame.Clear();
    }

    private static void RestoreAlpha(Material mat)
    {
        Color colour = mat.GetColor(BaseColour);
        colour.a = 1.0f;
        mat.SetColor(BaseColour, colour);
    }
}
