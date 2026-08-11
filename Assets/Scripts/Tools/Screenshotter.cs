using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public class Screenshotter : MonoBehaviour
{
    [SerializeField] private InputActionReference _screenshotAction;

    [Button("Take screenshot")]
    private void TakeScreenshot()
    {
        var currentDateTime = System.DateTime.Now;
        ScreenCapture.CaptureScreenshot($"screenshot-{currentDateTime:yyyy-MM-dd_HH-mm-ss}.png");
    }

    void Update()
    {
        if (_screenshotAction.action.WasPressedThisFrame())
        {
            TakeScreenshot();
        }
    }
}