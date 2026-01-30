using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class SplashScreen : MonoBehaviour
{
    [ValueDropdown(nameof(GetSceneNames))] [InfoBox("Scenes won't show up here if they're not included in the Build Settings.")]
    [SerializeField] private string _nextSceneName;

    private VideoPlayer _videoPlayer;

    private IEnumerable<string> GetSceneNames()
    {
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            yield return System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }

    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
    }

    private void Start()
    {
        _videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (string.IsNullOrEmpty(_nextSceneName))
        {
            Debug.LogWarning("Splash screen's next scene name is not set.");
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(_nextSceneName);
    }
}