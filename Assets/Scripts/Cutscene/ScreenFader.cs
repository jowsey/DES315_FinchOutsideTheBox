using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    private Image _fadeImage;

    void Awake()
    {
        _fadeImage = GetComponent<Image>();
    }

    public void FadeOut(float duration)
    {
        StartCoroutine(Fade(0.0f, 1.0f, duration));
    }

    public void FadeIn(float duration)
    {
        StartCoroutine(Fade(1f, 0f, duration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float timer = 0f;
        Color c = _fadeImage.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, timer / duration);
            _fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        _fadeImage.color = c;
    }
}
