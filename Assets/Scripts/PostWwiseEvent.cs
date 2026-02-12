using UnityEngine;

public class PostWwiseEvent : MonoBehaviour
{
    public AK.Wwise.Event footstepSound;

    
    public void PlayFootstepSound()
    {
        footstepSound.Post(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
