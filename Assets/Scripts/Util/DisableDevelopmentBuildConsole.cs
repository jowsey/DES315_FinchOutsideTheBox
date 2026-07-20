using UnityEngine;

namespace Util
{
    public static class DisableDevelopmentBuildConsole
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableDeveloperConsole()
        {
            if (Debug.isDebugBuild) Debug.developerConsoleEnabled = false;
        }
    }
}