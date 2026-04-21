using UnityEngine;

public class MenuMusicInitScript : MonoBehaviour
{
    public AK.Wwise.Event menuMusicInit;

    private static bool _hasStartedMusic;

    private void Start()
    {
        DontDestroyOnLoad(this);
        if (!_hasStartedMusic)
        {
            menuMusicInit.Post(gameObject);
            _hasStartedMusic = true;
        }
    }
}