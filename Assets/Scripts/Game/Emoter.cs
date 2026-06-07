using Mirror;
using System.Collections;
using UnityEngine;

public class Emoter : MonoBehaviour
{
    private NetworkAnimator anim;
    private Rigidbody _rb;
    private CameraZoomController _zoomController;
    private static int EmoteLayerIndex;
    private bool _isEmoting;
    public bool IsEmoting
    {
        get { return _isEmoting; }
        private set
        {
            if (_isEmoting == value) { return; }
            _isEmoting = value;
            if (_isEmoting) { _zoomController.OnForceThirdPersonActionStarted(); }
            else { _zoomController.OnRestorePreActionFirstPersonState(); }
        }
    }

    public void Awake()
    {
        anim = GetComponent<NetworkAnimator>();
        _rb = GetComponent<Rigidbody>();
        _zoomController = Camera.main.GetComponent<CameraZoomController>();
        EmoteLayerIndex = anim.animator.GetLayerIndex("EmoteLayer");
        IsEmoting = false;
    }

    //For testing
    [Sirenix.OdinInspector.Button] private void Spin() { PlayEmote("Emote_Spin"); }
    [Sirenix.OdinInspector.Button] private void Nod() { PlayEmote("Emote_Nod"); }
    [Sirenix.OdinInspector.Button] private void Headshake() { PlayEmote("Emote_Headshake"); }
    [Sirenix.OdinInspector.Button] private void Frontflip() { PlayEmote("Emote_Frontflip"); }

    public void PlayEmote(string triggerName)
    {
        StopAllCoroutines();
        StartCoroutine(RunEmote(triggerName));
    }

    private IEnumerator RunEmote(string triggerName)
    {
        //Player shouldn't be able to access any controls besides look and pause during emote
        PlayerController.ControlBlockerFlags controllerBlockerFlags = PlayerController.ControlBlockerFlags.All;
        controllerBlockerFlags &= ~PlayerController.ControlBlockerFlags.Look;
        controllerBlockerFlags &= ~PlayerController.ControlBlockerFlags.Pause;
        controllerBlockerFlags &= ~PlayerController.ControlBlockerFlags.ToggleTextChat;
        controllerBlockerFlags &= ~PlayerController.ControlBlockerFlags.Respawn;
        PlayerController.AddControlBlockerFlags(this, controllerBlockerFlags);

        IsEmoting = true;

        //We're currently on the locomotion (base) layer, so need to transition to the emote layer
        //We do this via layer blending to create a seamless transition between any currently-playing locomotion animation and the emote animation
        //Start playing the emote animation first so we're actually blending towards something
        anim.SetTrigger(triggerName);
        yield return null; //let the animator update

        //Fade in to emote layer
        yield return StartCoroutine(SetLayerWeight(1.0f));

        //We're now blended entirely on to the emote layer, wait until the animation is complete
        while (!anim.animator.GetCurrentAnimatorStateInfo(EmoteLayerIndex).IsName("Passthrough"))
        {
            yield return null;
        }

        //Animation is complete, blend back to the locomotion layer
        yield return StartCoroutine(SetLayerWeight(0.0f));

        //Now on the locomotion layer, remove control blocker flags
        PlayerController.RemoveAllControlBlockerFlags(this);
        IsEmoting = false;
    }

    private IEnumerator SetLayerWeight(float target)
    {
        float elapsed = 0.0f;
        const float duration = 0.15f; //todo: move to field
        float current = anim.animator.GetLayerWeight(EmoteLayerIndex);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float weight = Mathf.Lerp(current, target, elapsed / duration);
            anim.animator.SetLayerWeight(EmoteLayerIndex, weight);
            yield return null;
        }

        //For cleanliness
        anim.animator.SetLayerWeight(EmoteLayerIndex, target);
        yield return null;
    }
}
