using UnityEngine;

public class SetRTPC : MonoBehaviour
{
    public AK.Wwise.RTPC RTPCMenuOnOff;
    public float RTPCMenuValue;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RTPCMenuValue = 0;
        RTPCMenuOnOff.SetGlobalValue(RTPCMenuValue);
    }
}
