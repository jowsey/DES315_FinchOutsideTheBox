using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

public class WwiseAnimationEvents : MonoBehaviour
{
    [Required] public AK.Wwise.Event Footstep;
    [Required] public AK.Wwise.Event Jump;
    [Required] public AK.Wwise.Event Glide;

    [ReadOnly] public bool GlideTriggered;
    [ReadOnly] public bool DisableFootsteps;

    public void ResetGlideTrigger()
    {
        GlideTriggered = false;
    }

    [UsedImplicitly]
    public void PlayFootstepSound()
    {
        if (DisableFootsteps) return;
        Footstep.Post(gameObject);
    }

    [UsedImplicitly]
    public void PlayJumpSound()
    {
        Jump.Post(gameObject);
    }

    [UsedImplicitly]
    public void PlayGlideSound()
    {
        if (GlideTriggered) return;

        Glide.Post(gameObject);
        GlideTriggered = true;
    }
}