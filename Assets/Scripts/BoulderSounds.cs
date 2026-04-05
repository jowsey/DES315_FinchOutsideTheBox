using Sirenix.OdinInspector;
using UnityEngine;

public class BoulderSounds : MonoBehaviour
{
    //Velocity doesn't exist on non-authed client, so we use this to calculate our own rough speed
    private Vector3 _positionLastFrame;

     public AK.Wwise.Event BoulderSFX;
     public AK.Wwise.RTPC BoulderRTPC;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        BoulderSFX.Post(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        // manually calculate velocity since we don't have the luxury of knowing it on all clients
        var linearVelocity = (transform.position - _positionLastFrame) / Time.fixedDeltaTime;
        _positionLastFrame = transform.position;

        BoulderRTPC.SetGlobalValue(linearVelocity.magnitude * 20);
        
    }
}
