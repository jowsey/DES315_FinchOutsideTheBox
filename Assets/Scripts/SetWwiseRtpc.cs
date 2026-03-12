using UnityEngine;

public class SetWwiseRtpc : MonoBehaviour
{


    [Range(0.0f, 30.0f)]
    public float musicFloat;

    [Range(0.0f, 30.0f)]
    public float sfxFloat;

    public AK.Wwise.RTPC musicVolume;
    public AK.Wwise.RTPC sfxVolume;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        musicFloat = 15.0f;
        musicVolume.SetGlobalValue(musicFloat);
        sfxFloat = 15.0f;
        sfxVolume.SetGlobalValue(sfxFloat);
    }

    // Update is called once per frame
    void Update()
    {
        musicVolume.SetGlobalValue(musicFloat);
        sfxVolume.SetGlobalValue(sfxFloat);
    }
}
