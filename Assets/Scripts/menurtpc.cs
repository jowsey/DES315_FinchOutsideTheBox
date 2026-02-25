using UnityEngine;

public class menurtpc : MonoBehaviour


{
    //Menu makes music go through filter
    public AK.Wwise.RTPC RTPCMenu;
    public float RTPCMenuNum;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RTPCMenuNum = 0;
        RTPCMenu.SetGlobalValue(RTPCMenuNum);
    }
}
