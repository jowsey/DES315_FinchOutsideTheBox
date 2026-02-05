using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    void Awake()
    {
        if (!FindAnyObjectByType<Mirror.NetworkManager>())
        {
            SceneManager.LoadScene("GameList");
        }
    }
}
