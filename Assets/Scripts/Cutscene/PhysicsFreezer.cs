using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class PhysicsFreezer : MonoBehaviour
{
    [Tooltip("Drag anything that needs to be frozen here")]
    [SerializeField] private List<Rigidbody> _rigidbodiesToFreeze;

    private PlayableDirector _director;
    private Dictionary<Rigidbody, bool> _originalKinematicStates = new Dictionary<Rigidbody, bool>();

    private void Awake()
    {
        _director = GetComponent<PlayableDirector>();

        _director.played += OnCutsceneStarted;
        _director.stopped += OnCutsceneStopped;
    }

    private void OnDestroy()
    {
        _director.played -= OnCutsceneStarted;
        _director.stopped -= OnCutsceneStopped;
    }

    private void OnCutsceneStarted(PlayableDirector director)
    {
        _originalKinematicStates.Clear();

        foreach (Rigidbody rb in _rigidbodiesToFreeze)
        {
            if (rb != null)
            {
                _originalKinematicStates[rb] = rb.isKinematic;
                rb.isKinematic = true;
            }
        }

        Camera.main.GetComponent<InteractDetection>().enabled = false;
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        foreach (Rigidbody rb in _rigidbodiesToFreeze)
        {
            if (rb != null && _originalKinematicStates.ContainsKey(rb))
            {
                rb.isKinematic = _originalKinematicStates[rb];
            }
        }
        Camera.main.GetComponent<InteractDetection>().enabled = true;
    }
}
