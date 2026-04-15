using UnityEngine;

public class BannerSounds : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public AK.Wwise.Event BannerSfx;

    private void Start()
    {
        BannerSfx.Post(gameObject);
    }

    private void OnDestroy()
    {
        //Stops playing sounds when out of use to not overload the game
        BannerSfx.Stop(gameObject);
    }
}
