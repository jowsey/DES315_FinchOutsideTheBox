using UnityEngine;

public class postLeverScript : MonoBehaviour
{
    public AK.Wwise.Event leverSound;
    public AK.Wwise.Event leverUp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created


    public void PlayLeverSound()
    {
        if (GetComponentInChildren<LeverMovement>().Forward)
        {
            leverSound.Post(gameObject);
            leverUp.Post(gameObject);
        }
    }

    public void PlayLeverUp()
    {
        if (GetComponentInChildren<LeverMovement>().Forward==false)
        {
            leverUp.Post(gameObject);
        }
    }

}
