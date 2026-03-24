using UnityEngine;

public class PostWwiseFootstep : MonoBehaviour
{
    public AK.Wwise.Event Footstep;
    public AK.Wwise.Event Jump;

    public AK.Wwise.Event Fall;
    public int fallCount;



    public void Start()
    {

    }


    public void PlayFootstepSound()
    {
        Footstep.Post(gameObject);
    }

    public void PlayJumpSound()
    {
        Jump.Post(gameObject);
    }

    public void PlayFalling()
    {
        if(fallCount < 1)
        {
            Fall.Post(gameObject);
            fallCount += 1;
        }
        
    }
}