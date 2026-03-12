using UnityEngine;

public class PostWwiseFootstep : MonoBehaviour
{
    public AK.Wwise.Event Footstep;
    public AK.Wwise.Event Jump;

    public WheelSeat Seat;

    public void PlayFootstepSound()
    {
        
            Footstep.Post(gameObject);
        
        
    }

    public void PlayJumpSound()
    {
        Jump.Post(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}
