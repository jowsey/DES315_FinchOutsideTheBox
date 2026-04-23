using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;

public class CutsceneStart : MonoBehaviour
{
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private Cart _cart;

    private void Awake()
    {
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
        _director.Play();
    }

    //Wasn't sure where to chuck these
    private void OnCutsceneStarted(PlayableDirector _)
    {
        Camera.main.GetComponent<ObstructionDitherer>().enabled = false;
        Camera.main.GetComponent<CrosshairDetection>().enabled = false;
    }
    private void OnCutsceneStopped(PlayableDirector _)
    {
        Camera.main.GetComponent<ObstructionDitherer>().enabled = true;
        Camera.main.GetComponent<CrosshairDetection>().enabled = true;
    }
}
