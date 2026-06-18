Shader "Hidden/ShadowOnlyShader/FloorDisplay"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ==========================================
        // Resolve結果テクスチャをサンプリングするだけの軽量Display Pass。
        // ShadowOnlyShadowResolvePass が事前計算した低解像度RTから影を読み取る。
        // 影計算コードは一切含まれない。
        // ==========================================
        Pass
        {
            Name "ShadowOnlyFloorDisplay"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Resolve用テクスチャ（グローバルテクスチャとしてResolvePassから設定される）
            TEXTURE2D(_ShadowResolveTex);
            SAMPLER(sampler_ShadowResolveTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                // RenderScale を反映した正しいスクリーンUVを取得する。
                // positionCS.xy / _ScreenParams.xy は RenderScale を考慮しないため、
                // RenderScale != 1 のとき screenUV が [0,1] からずれて影が縮小・隅寄りになる。
                // GetNormalizedScreenSpaceUV は内部で _ScaledScreenParams（実描画解像度）を
                // 使用するため RenderScale に依存せず一致する。
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                return SAMPLE_TEXTURE2D(_ShadowResolveTex, sampler_ShadowResolveTex, screenUV);
            }
            ENDHLSL
        }
    }
}
