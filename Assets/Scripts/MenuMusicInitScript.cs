using UnityEngine;

public class MenuMusicInitScript : MonoBehaviour
{
    public AK.Wwise.Event menuMusicInit;

    private static bool _hasStartedMusic;

    private void Start()
    {
        if (!_hasStartedMusic)
        {
            menuMusicInit.Post(gameObject);
            _hasStartedMusic = true;
        }
    }
}