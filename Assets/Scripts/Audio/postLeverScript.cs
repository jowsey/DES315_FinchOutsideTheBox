using UnityEngine;

public class postLeverScript : MonoBehaviour
{
    public AK.Wwise.Event leverSound;
    public AK.Wwise.Event leverUp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created


    public void PlayLeverSound()
    {
        var leverMovement = GetComponentInChildren<LeverMovement>();
        if (leverMovement?.Forward == true)
        {
            leverSound?.Post(gameObject);
            leverUp?.Post(gameObject);
        }
    }

    public void PlayLeverUp()
    {
        var leverMovement = GetComponentInChildren<LeverMovement>();
        if (leverMovement?.Forward != true)
        {
            leverUp?.Post(gameObject);
        }
    }

}
