using UnityEngine;

public class SetWwiseRtpc : MonoBehaviour
{


    [Range(0.0f, 30.0f)]
    public float Volume;
    
    public AK.Wwise.RTPC musicVolume;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Volume = 5.0f;
        musicVolume.SetGlobalValue(Volume);
    }

    // Update is called once per frame
    void Update()
    {
        musicVolume.SetGlobalValue(Volume);
    }
}
