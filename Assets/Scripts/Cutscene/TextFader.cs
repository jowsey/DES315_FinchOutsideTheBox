using TMPro;
using UnityEngine;
using System.Collections;

public class TextFader : MonoBehaviour
{
    private TextMeshProUGUI _fadeText;

    void Awake()
    {
        _fadeText = GetComponent<TextMeshProUGUI>();
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
        Color c = _fadeText.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, timer / duration);
            _fadeText.color = c;
            yield return null;
        }

        c.a = to;
        _fadeText.color = c;
    }
}
