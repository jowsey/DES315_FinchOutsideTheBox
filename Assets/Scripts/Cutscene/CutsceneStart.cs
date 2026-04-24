using Mirror;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneStart : NetworkBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Cart _cart;
    [SerializeField] private GameObject _crosshair;
    
    //todo: maybe remove if we decide to have the cutscene triggered by button prompt instead? so that players can watch it multiple times
    private bool _played;

    private uint _playersFinishedCount;


    private void Awake()
    {
        _played = false;
        _playersFinishedCount = 0;
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
            _director.Play();
            _played = true;
        }
    }

    //Wasn't sure where to chuck these
    private void OnCutsceneStarted(PlayableDirector _)
    {
        _crosshair.SetActive(false);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = false;
        Camera.main.GetComponent<CrosshairDetection>().enabled = false;
    }
    private void OnCutsceneStopped(PlayableDirector _)
    {
        _crosshair.SetActive(true);
        Camera.main.GetComponent<ObstructionDitherer>().enabled = true;
        Camera.main.GetComponent<CrosshairDetection>().enabled = true;

        CmdPlayerFinishedCutscene();
    }

    [Command]
    private void CmdPlayerFinishedCutscene()
    {
        ++_playersFinishedCount;
        if (_playersFinishedCount == 2)
        {
            //Both players have finished the cutscene, respawn
            _cart.CmdInvokeRespawnEvent(_cart.CurrentCheckpointIndex);
            _playersFinishedCount = 0;
        }
    }
}
