using UnityEngine;

public class MenuMusicInitScript : MonoBehaviour
{
    public AK.Wwise.Event menuMusicInit;

    private static bool _hasStartedMusic;


    void Start()
    {
        if (!_hasStartedMusic)
        {
            menuMusicInit.Post(gameObject);
           _hasStartedMusic = true;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
