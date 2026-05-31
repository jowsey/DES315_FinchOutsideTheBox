using Mirror;
using System.Collections;
using UnityEngine;

public class Emoter : MonoBehaviour
{
    private NetworkAnimator anim;
    private static int EmoteLayerIndex;

    public void Awake()
    {
        anim = GetComponent<NetworkAnimator>();
        EmoteLayerIndex = anim.animator.GetLayerIndex("EmoteLayer");
    }

    public void PlayEmote(string triggerName)
    {
        StopAllCoroutines();
        StartCoroutine(RunEmote(triggerName));
    }

    private IEnumerator RunEmote(string triggerName)
    {
        //Player shouldn't be able to access controls during emote
        PlayerController.AddControlBlocker(this);

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

        //Animation is complete, remove control blocker and snap back to the locomotion layer
        PlayerController.RemoveControlBlocker(this);
        
    }

    private IEnumerator SetLayerWeight(float target)
    {
        Debug.Log($"Starting SetLayerWeight to {target}");

        float elapsed = 0.0f;
        const float duration = 0.15f; //todo: move to field
        float current = anim.animator.GetLayerWeight(EmoteLayerIndex);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float weight = Mathf.Lerp(current, target, elapsed / duration);
            Debug.Log($"Current weight: {weight}");
            anim.animator.SetLayerWeight(EmoteLayerIndex, weight);
            yield return null;
        }

        //For cleanliness
        anim.animator.SetLayerWeight(EmoteLayerIndex, target);

        Debug.Log($"SetLayerWeight complete, final weight: {anim.animator.GetLayerWeight(EmoteLayerIndex)}");
    }
}
