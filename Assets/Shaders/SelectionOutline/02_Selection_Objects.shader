Shader "02_Selection/Objects"
{
    Properties
    {
        _SelectionColor("Selection Color", Color) = (1,1,1,1)
        _MaxDistance("Max Distance", float) = 15
        _OutlineWidthFactor("Outline Width Factor", Float) = 1

        // Transparency
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector]_BlendMode("_BlendMode", Range(0.0, 1.0)) = 0.5
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    // #pragma enable_d3d11_debug_symbols

    //enable GPU instancing support
    #pragma multi_compile_instancing
    #pragma multi_compile _ DOTS_INSTANCING_ON
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
        }
        Pass
        {
            Name "FirstPass"
            Tags
            {
                "LightMode" = "FirstPass"
            }

            Blend Off
            ZWrite On
            ZTest LEqual

            Cull Back

            HLSLPROGRAM
            // Toggle the alpha test
            #define _ALPHATEST_ON

            // Toggle transparency
            // #define _SURFACE_TYPE_TRANSPARENT

            // Toggle fog on transparent
            #define _ENABLE_FOG_ON_TRANSPARENT

            // List all the attributes needed in your shader (will be passed to the vertex shader)
            // you can see the complete list of these attributes in VaryingMesh.hlsl
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT

            // List all the varyings needed in your fragment shader
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_TANGENT_TO_WORLD
            #define VARYINGS_NEED_POSITION_WS

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _SelectionColor;
                float _MaxDistance;
                float _OutlineWidthFactor;
                float _AlphaCutoff;
                float _BlendMode;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassRenderersV2.hlsl"

            // Put the code to render the objects in your custom pass in this function
            void GetSurfaceAndBuiltinData(FragInputs fragInputs, float3 viewDirection, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {
                if (_MaxDistance > 0 && dot(fragInputs.positionRWS, fragInputs.positionRWS) > _MaxDistance * _MaxDistance) discard;

                    ZERO_BUILTIN_INITIALIZE(builtinData);
                    ZERO_INITIALIZE(SurfaceData, surfaceData);

                builtinData.opacity = saturate(_OutlineWidthFactor);
                builtinData.emissiveColor = float3(0, 0, 0);
                surfaceData.color = float3(1, 1, 1) * _SelectionColor.rgb;
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}