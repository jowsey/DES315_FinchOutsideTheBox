using System.Collections.Generic;
using System.Linq;
using Game.Items;
using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using Util;

public class CutsceneStart : NetworkBehaviour
{
    public static bool CutsceneActive { get; private set; }

    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Cart _cart;
    [SerializeField] private Transform _cartStartTransform;

    [SerializeField] private CanvasGroup[] _hiddenWhilePlaying;

    [SerializeField] private CutscenePuppeteer _puppeteer;

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
            if (isServer)
            {
                Physics.SyncTransforms();
                Dictionary<Item, (Vector3 localPos, Quaternion rot)> objectSnapshots = new();
                foreach (Item item in _cart.CarriedItems)
                {
                    objectSnapshots[item] = (_cart.transform.InverseTransformPoint(item.transform.position), item.transform.rotation);
                }

                _cart.ServerTeleportTo(_cartStartTransform);

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
            }

            Camera.main.GetComponent<CameraZoomController>().OnForceThirdPersonActionStarted();

            _director.Play();
            _played = true;
        }
    }

    //Wasn't sure where to chuck these
    private void OnCutsceneStarted(PlayableDirector _)
    {
        _puppeteer.BuildPlayer(0, PlayerController.LocalPlayer.PlayerName, PlayerController.LocalPlayer.PlayerSkinIndex);

        var firstOtherPlayer = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(p => !p.IsPuppet && !p.CutscenePlayer && p != PlayerController.LocalPlayer);

        if (firstOtherPlayer)
        {
            _puppeteer.BuildPlayer(1, firstOtherPlayer.PlayerName, firstOtherPlayer.PlayerSkinIndex);
        }
        else
        {
            _puppeteer.BuildPlayer(1, "Cat", (PlayerController.LocalPlayer.PlayerSkinIndex + 1) % PlayerController.LoadedSkins.Length);
        }

        CutsceneActive = true;

        GloballyHiddenGroup.AddHideSource(this);
        PlayerController.AddControlBlockerFlags(this, PlayerController.ControlBlockerFlags.All);

        Camera.main.GetComponent<ObstructionDitherer>().enabled = false;
        Camera.main.GetComponent<InteractDetection>().enabled = false;
        Camera.main.GetComponent<AkAudioListener>().enabled = true;
    }

    private void OnCutsceneStopped(PlayableDirector _)
    {
        GloballyHiddenGroup.RemoveHideSource(this);
        PlayerController.RemoveAllControlBlockerFlags(this);

        Camera.main.GetComponent<ObstructionDitherer>().enabled = true;
        Camera.main.GetComponent<InteractDetection>().enabled = true;
        Camera.main.GetComponent<AkAudioListener>().enabled = false;

        if (isServer)
        {
            _cart.CmdInvokeRespawnEvent(_cart.CurrentRespawnTarget);
        }

        CutsceneActive = false;
    }
}