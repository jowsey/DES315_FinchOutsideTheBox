using UnityEngine;

public class SpinningCylinderSounds : MonoBehaviour
{

    public AK.Wwise.Event SpinningCylinder;

    private void Start()
    {
        SpinningCylinder.Post(gameObject);
    }

    private void OnDestroy()
    {
        //Stops playing sounds when out of use to not overload the game
        SpinningCylinder.Stop(gameObject);
    }
}
