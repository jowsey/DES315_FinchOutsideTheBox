using UnityEngine;

public class BoulderSounds : MonoBehaviour
{
    //Velocity doesn't exist on non-authed client, so we use this to calculate our own rough speed
    private Vector3 _positionLastFrame;

    public AK.Wwise.Event BoulderSFX;
    public AK.Wwise.RTPC BoulderRTPC;

    private void Start()
    {
        BoulderSFX.Post(gameObject);
    }

    private void Update()
    {
        // manually calculate velocity since we don't have the luxury of knowing it on all clients
        var linearVelocity = (transform.position - _positionLastFrame) / Time.fixedDeltaTime;
        _positionLastFrame = transform.position;

        BoulderRTPC.SetGlobalValue(linearVelocity.magnitude * 20);
    }

    private void OnDestroy()
    {
        //Stops playing sounds when out of use to not overload the game
        BoulderSFX.Stop(gameObject);    
    }
}