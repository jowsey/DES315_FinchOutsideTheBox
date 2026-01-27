using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void LoadScene()
    {
        SceneManager.LoadScene("GameList");
    }
}
