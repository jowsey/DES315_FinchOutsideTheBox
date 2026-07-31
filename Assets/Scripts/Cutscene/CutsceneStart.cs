using System.Collections.Generic;
using Game.Items;
using Mirror;
using PrimeTween;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneStart : NetworkBehaviour
{
    public static bool CutsceneActive { get; private set; }

    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Cart _cart;
    [SerializeField] private GameObject _crosshair;
    [SerializeField] private Transform _cartStartTransform;

    [SerializeField] private CanvasGroup[] _hiddenWhilePlaying;

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
            Dictionary<Item, (Vector3 localPos, Quaternion rot)> objectSnapshots = new();
            foreach (Item item in _cart.CarriedItems)
            {
                objectSnapshots[item] = (_cart.transform.InverseTransformPoint(item.transform.position), item.transform.rotation);
            }

            _cart.transform.position = _cartStartTransform.position;
            _cart.transform.rotation = _cartStartTransform.rotation;
            _cart.Rb.position = _cartStartTransform.position;
            _cart.Rb.rotation = _cartStartTransform.rotation;
            Physics.SyncTransforms();
            foreach (var kvp in objectSnapshots)
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
        CutsceneActive = true;
        foreach (CanvasGroup group in _hiddenWhilePlaying) Tween.Alpha(group, 0f, 0.5f, Ease.OutCubic);

        _crosshair.SetActive(false);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = false;
        Camera.main.GetComponent<InteractDetection>().enabled = false;
        Camera.main.GetComponent<AkAudioListener>().enabled = true;

        PlayerController.AddControlBlockerFlags(this, PlayerController.ControlBlockerFlags.All);
    }

    private void OnCutsceneStopped(PlayableDirector _)
    {
        foreach (CanvasGroup group in _hiddenWhilePlaying) Tween.Alpha(group, 1f, 0.5f, Ease.OutCubic);

        _crosshair.SetActive(true);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = true;
        Camera.main.GetComponent<InteractDetection>().enabled = true;
        Camera.main.GetComponent<AkAudioListener>().enabled = false;

        PlayerController.RemoveAllControlBlockerFlags(this);

        if (isServer)
        {
            _cart.CmdInvokeRespawnEvent(_cart.CurrentRespawnTarget);
        }

        CutsceneActive = false;
    }
}