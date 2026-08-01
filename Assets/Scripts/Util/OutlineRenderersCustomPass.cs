using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Util
{
    [UsedImplicitly, Serializable]
    public class OutlineRenderersCustomPass : CustomPass
    {
        [SerializeField] private Material _outlineMaterial;

        public OutlineRenderersCustomPass()
        {
            targetColorBuffer = TargetBuffer.Custom;
            targetDepthBuffer = TargetBuffer.Custom;
            clearFlags = ClearFlag.Color | ClearFlag.Depth;
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (!_outlineMaterial) return;

            var cmd = ctx.cmd;
            var propertyBlock = ctx.propertyBlock;
            foreach (var outlineTarget in OutlineTarget.Active)
            {
                if (!outlineTarget) continue;

                var renderer = outlineTarget.Renderer;
                if (!renderer) continue;

                propertyBlock.Clear();
                propertyBlock.SetColor(ShaderIDs.SelectionColor, outlineTarget.Colour);
                propertyBlock.SetFloat(ShaderIDs.MaxDistance, outlineTarget.MaxDrawDistance);
                propertyBlock.SetFloat(ShaderIDs.OutlineWidthFactor, outlineTarget.OutlineWidthFactor);

                if (renderer is MeshRenderer)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    var mesh = meshFilter ? meshFilter.sharedMesh : null;
                    if (mesh)
                    {
                        cmd.DrawMesh(mesh, renderer.localToWorldMatrix, _outlineMaterial, 0, 0, propertyBlock);
                        continue;
                    }
                }

                cmd.DrawRenderer(renderer, _outlineMaterial, 0, 0);
            }
        }

        private static class ShaderIDs
        {
            public static readonly int SelectionColor = Shader.PropertyToID("_SelectionColor");
            public static readonly int MaxDistance = Shader.PropertyToID("_MaxDistance");
            public static readonly int OutlineWidthFactor = Shader.PropertyToID("_OutlineWidthFactor");
        }
    }
}
