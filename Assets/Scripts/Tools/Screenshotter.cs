using UnityEngine;
using UnityEngine.InputSystem;

public class Screenshotter : MonoBehaviour
{
    [SerializeField] private InputActionReference _screenshotAction;

    void Update()
    {
        if (_screenshotAction.action.WasPressedThisFrame())
        {
            ScreenCapture.CaptureScreenshot("screenshot.png");
        }
    }
}
