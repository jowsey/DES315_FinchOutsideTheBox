using UnityEngine;

public class PostPillarEventWwise : MonoBehaviour
{
    public AK.Wwise.Event pillar1;
    public AK.Wwise.Event pillar2;
    public AK.Wwise.Event pillarRumble;
    public AK.Wwise.Event pillarCrash;

    public void PostPillar1()
    {
        pillar1.Post(gameObject);
    }

    public void PostPillar2()
    {
        pillar2.Post(gameObject);
    }

    public void PostPillarRumble()
    {
        pillarRumble.Post(gameObject);
    }

    public void PostPillarCrash()
    {
        pillarCrash.Post(gameObject);
    }
}
