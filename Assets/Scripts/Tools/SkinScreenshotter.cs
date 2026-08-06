using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using Util;

namespace Tools
{
    public class SkinScreenshotter : MonoBehaviour
    {
#if UNITY_EDITOR
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        [SerializeField] private Renderer[] _skinnedRenderers;
        [SerializeField] private bool _overwriteImages = true;
        [SerializeField] private Vector2 _accentSampleUV = new(0.552f, 0.945f);

        private Color GetLatestSkinColour() => PlayerController.LoadedSkins?.Length >= _finishedSkinCount && _finishedSkinCount > 0
            ? PlayerController.LoadedSkins[_finishedSkinCount - 1].AccentColor
            : Color.white;

        private int _numSkins;

        [ShowInInspector, HideLabel, HideIf("_numSkins", 0)]
        [ProgressBar(0, "_numSkins", ColorGetter = nameof(GetLatestSkinColour), Segmented = true)]
        private int _finishedSkinCount;

        [Button("Full send it")]
        private void GenerateSkinData() => StartCoroutine(GenerateSkinDataRoutine());

        private IEnumerator GenerateSkinDataRoutine()
        {
            var materials = Resources.LoadAll<Material>("PlayerSkins/Materials");

            _numSkins = materials.Length;
            _finishedSkinCount = 0;

            PlayerController.LoadedSkins = new SkinData[materials.Length];

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                foreach (var ren in _skinnedRenderers)
                {
                    ren.sharedMaterial = material;
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                yield return new WaitForEndOfFrame();

                var path = $"Assets/Resources/PlayerSkins/Icons/Skin_{i + 1}.png";
                if (_overwriteImages || !System.IO.File.Exists(path))
                {
                    ScreenCapture.CaptureScreenshot(path);

                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    yield return new WaitForEndOfFrame();
                }

                UnityEditor.AssetDatabase.ImportAsset(path);

                yield return null;

                var skinData = ScriptableObject.CreateInstance<SkinData>();
                skinData.Material = material;
                skinData.Icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                skinData.VCIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/PlayerSkins/VCIcons/Skin_{i + 1}.png");
                skinData.AccentColor = ((Texture2D)material.GetTexture(MainTex)).GetPixelBilinear(_accentSampleUV.x, _accentSampleUV.y);
                UnityEditor.AssetDatabase.CreateAsset(skinData, $"Assets/Resources/PlayerSkins/Skin_{i + 1}.asset");
                UnityEditor.EditorUtility.SetDirty(skinData);

                PlayerController.LoadedSkins[i] = skinData;
                _finishedSkinCount = i + 1;
            }

            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}