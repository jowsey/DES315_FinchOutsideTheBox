using Obstacles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tools
{
    public class HexTileGridBuilder : MonoBehaviour
    {
        [SerializeField] [Required] private GameObject _hexTilePrefab;

        [SerializeField] [Min(1)] private uint _width;
        [SerializeField] [Min(1)] private uint _length;

        [SerializeField] [Min(0)] private float _tileSize = 1;
        [SerializeField] [Min(0)] private float _tileGap = 0.1f;

        private Vector3 GetPosition(int x, int z)
        {
            // flat-top row-major
            return new Vector3(
                x * 1.5f,
                0,
                z * Mathf.Sqrt(3) + (x % 2 == 0 ? 0 : Mathf.Sqrt(3) / 2)
            ) * _tileSize + new Vector3(x, 0, z) * _tileGap;
        }

        [Button]
        private void RebuildGrid()
        {
            if (Application.isPlaying || !gameObject) return;
            
            var tiles = GetComponentsInChildren<HexTile>();

            foreach (var tile in tiles)
            {
                if (!tile) continue;

                DestroyImmediate(tile.gameObject);
            }

            if (!_hexTilePrefab) return;

#if UNITY_EDITOR
            for (var x = 0; x < _width; x++)
            {
                for (var z = 0; z < _length; z++)
                {
                    // var tile = Instantiate(_hexTilePrefab, transform.TransformPoint(GetPosition(x, z)), Quaternion.identity, transform);
                    // instantiate as prefab
                    var tile = UnityEditor.PrefabUtility.InstantiatePrefab(_hexTilePrefab, transform) as GameObject;
                    tile!.name = $"HexTile_{x}_{z}";
                    tile!.transform.localPosition = GetPosition(x, z);
                    tile!.transform.localScale = new Vector3(_tileSize, 1, _tileSize);

                    UnityEditor.EditorUtility.SetDirty(tile);
                }
            }
#endif
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            UnityEditor.EditorApplication.delayCall += RebuildGrid;
#endif
        }
    }
}