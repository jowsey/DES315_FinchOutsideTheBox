using UnityEngine;
using UnityEngine.SceneManagement;

public class EnsureNetworked : MonoBehaviour
{
    private void Awake()
    {
        if (!FindAnyObjectByType<Mirror.NetworkManager>())
        {
            SceneManager.LoadScene("Menu");
        }
    }
}
