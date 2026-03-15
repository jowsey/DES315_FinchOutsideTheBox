using System.Collections;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tools
{
    public class SkinScreenshotter : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private Renderer[] _renderers;
        private int _currentSkinIndex;

        [Button("Use next skin")]
        private void UseNextSkin() => IterateSkinMaterial();

        private void IterateSkinMaterial(int? forceIndex = null)
        {
            PlayerController.SkinMaterials ??= Resources.LoadAll<Material>("PlayerSkins/Materials");

            _currentSkinIndex = PlayerController.SkinMaterials.ToList().IndexOf(_renderers[0].sharedMaterial);
            var newIndex = forceIndex ?? (_currentSkinIndex + 1) % PlayerController.SkinMaterials.Length;

            foreach (var ren in _renderers)
            {
                ren.sharedMaterial = PlayerController.SkinMaterials[newIndex];
            }

            _currentSkinIndex = newIndex;
        }

        [Button("Take screenshot")]
        private void TakeScreenshot() => StartCoroutine(TakeScreenshotRoutine());

        private IEnumerator TakeScreenshotRoutine()
        {
            var path = $"Assets/Resources/PlayerSkins/Icons/Skin_{_currentSkinIndex + 1}.png";
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            UnityEditor.AssetDatabase.ImportAsset(path);
        }

        [Button("Full send it")]
        private void CaptureAllSkins() => StartCoroutine(CaptureAllSkinsRoutine());

        private IEnumerator CaptureAllSkinsRoutine()
        {
            PlayerController.SkinMaterials ??= Resources.LoadAll<Material>("PlayerSkins/Materials");

            for (var i = 0; i < PlayerController.SkinMaterials.Length; i++)
            {
                Debug.Log(PlayerController.SkinMaterials[i].name);
                
                IterateSkinMaterial(i);
                yield return new WaitForEndOfFrame();
                TakeScreenshot();
                yield return new WaitForEndOfFrame();
            }
        }
#endif
    }
}