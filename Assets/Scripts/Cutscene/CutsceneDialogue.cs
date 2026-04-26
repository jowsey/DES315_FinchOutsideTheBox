using UI;
using UnityEngine;

[System.Serializable]
public struct ChatLine
{
    public PlayerController Speaker;
    [TextArea] public string Message;
}

public class CutsceneDialogue : MonoBehaviour
{
    [SerializeField] private TextChatFeed _chatFeed;
    [SerializeField] private ChatLine[] _lines;

    private void Start()
    {
        if (!_chatFeed) _chatFeed = FindAnyObjectByType<TextChatFeed>();
    }

    public void PlayLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Length) return;

        var line = _lines[lineIndex];
        _chatFeed.DisplayLocalMessage(line.Speaker, line.Message);
    }
}