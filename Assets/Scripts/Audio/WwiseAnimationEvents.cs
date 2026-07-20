using System;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

public class WwiseAnimationEvents : MonoBehaviour
{
    [Required] public AK.Wwise.Event Footstep;
    [Required] public AK.Wwise.Event Jump;
    [Required] public AK.Wwise.Event Glide;
    [Required] public AK.Wwise.Event headShake;
    [Required] public AK.Wwise.Event headNod;
    [Required] public AK.Wwise.Event spin; 
    [Required] public AK.Wwise.Event Frontflip;

    [NonSerialized, ShowInInspector, ReadOnly] public bool GlideTriggered;
    [NonSerialized, ShowInInspector, ReadOnly] public bool EnableFootsteps = true;
    
    public void ResetGlideTrigger()
    {
        GlideTriggered = false;
    }

    [UsedImplicitly]
    public void PlayFootstepSound()
    {
        if (!EnableFootsteps) return;
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

    [UsedImplicitly]
    public void HeadShake()
    {
        headShake.Post(gameObject);
    }

    [UsedImplicitly]
    public void HeadNod()
    {
        headNod.Post(gameObject);
    }

    [UsedImplicitly]
    public void PlaySpin()
    {
        spin.Post(gameObject);
    }

    [UsedImplicitly]
    public void PlayFlipSound()
    {
        Frontflip.Post(gameObject);
    }
}