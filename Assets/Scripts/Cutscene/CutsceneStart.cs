using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

public class CutsceneStart : NetworkBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Cart _cart;
    [SerializeField] private GameObject _crosshair;
    [SerializeField] private Transform _cartStartTransform;
    
    [SerializeField] private GameObject[] _disabledWhilePlaying;
    
    //todo: maybe remove if we decide to have the cutscene triggered by button prompt instead? so that players can watch it multiple times
    private bool _played;


    private void Awake()
    {
        _played = false;
        _director.played += OnCutsceneStarted;
        _director.stopped += OnCutsceneStopped;
    }

    private void OnDestroy()
    {
        _director.played -= OnCutsceneStarted;
        _director.stopped -= OnCutsceneStopped;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_played)
        {
            Physics.SyncTransforms();
            Dictionary<Flask, (Vector3 localPos, Quaternion rot)> flaskSnapshots = new Dictionary<Flask, (Vector3 localPos, Quaternion rot)>();
            foreach (Flask flask in _cart.CarriedFlasks)
            {
                flaskSnapshots[flask] = (_cart.transform.InverseTransformPoint(flask.transform.position), flask.transform.rotation);
            }
            _cart.transform.position = _cartStartTransform.position;
            _cart.transform.rotation = _cartStartTransform.rotation;
            _cart.Rb.position = _cartStartTransform.position;
            _cart.Rb.rotation = _cartStartTransform.rotation;
            Physics.SyncTransforms();
            foreach (var kvp in flaskSnapshots)
            {
                Vector3 worldPos = _cart.transform.TransformPoint(kvp.Value.localPos);
                Quaternion worldRot = kvp.Value.rot;
                kvp.Key.transform.position = worldPos;
                kvp.Key.transform.rotation = worldRot;
                Rigidbody rb = kvp.Key.GetComponent<Rigidbody>();
                rb.position = worldPos;
                rb.rotation = worldRot;
            }
            Physics.SyncTransforms();

            Camera.main.GetComponent<CameraZoomController>().OnForceThirdPersonActionStarted();

            _director.Play();
            _played = true;
        }
    }

    //Wasn't sure where to chuck these
    private void OnCutsceneStarted(PlayableDirector _)
    {
        foreach (GameObject obj in _disabledWhilePlaying) obj.SetActive(false);

        _crosshair.SetActive(false);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = false;
        Camera.main.GetComponent<CrosshairDetection>().enabled = false;
        Camera.main.GetComponent<AkAudioListener>().enabled = true;
        PlayerController.ControlBlockerFlags flags = PlayerController.ControlBlockerFlags.All;
        flags &= ~PlayerController.ControlBlockerFlags.Pause;
        PlayerController.AddControlBlockerFlags(this, flags);
    }
    
    private void OnCutsceneStopped(PlayableDirector _)
    {
        foreach (GameObject obj in _disabledWhilePlaying) obj.SetActive(true);

        _crosshair.SetActive(true);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = true;
        Camera.main.GetComponent<CrosshairDetection>().enabled = true;
        Camera.main.GetComponent<AkAudioListener>().enabled = false;

        if (isServer)
        {
            _cart.CmdInvokeRespawnEvent(_cart.CurrentCheckpointIndex);
        }
    }
}
