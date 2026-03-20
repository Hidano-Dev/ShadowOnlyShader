Shader "Hidden/ShadowOnlyShader/Floor"
{
    Properties
    {
        // グローバルパラメータ（ShadowOnlyManagerから毎フレーム設定される）
        _BlendMultiplier ("Blend Multiplier", Float) = 1.0
        _VirtualLightCount ("Virtual Light Count", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ==========================================
        // Pass 0: Display pass（URPが通常のTransparent描画で使用）
        // _SHADOW_RESOLVE_ACTIVE キーワード有効時: Resolve結果テクスチャをサンプリング
        // キーワード無効時: 従来通りフル影計算
        // ==========================================
        Pass
        {
            Name "ShadowOnlyFloor"

            // 影以外の部分をalpha=0で透明に保つ
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_display

            // ブラー品質プリセット切り替え用キーワード
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            // Resolve有効/無効の切り替え（ShadowOnlyManagerがマテリアルキーワードで制御）
            #pragma multi_compile _ _SHADOW_RESOLVE_ACTIVE

            #include "Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl"

            // Resolve用テクスチャ（グローバルテクスチャとしてResolvePassから設定される）
            TEXTURE2D(_ShadowResolveTex);
            SAMPLER(sampler_ShadowResolveTex);

            half4 frag_display(Varyings input) : SV_TARGET
            {
                #if defined(_SHADOW_RESOLVE_ACTIVE)
                    // Resolveが有効: 事前計算済みの低解像度テクスチャからサンプリング
                    float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                    return SAMPLE_TEXTURE2D(_ShadowResolveTex, sampler_ShadowResolveTex, screenUV);
                #else
                    // Resolveが無効: 従来通りフル影計算
                    return ComputeFloorShadow(input);
                #endif
            }
            ENDHLSL
        }

        // ==========================================
        // Pass 1: Resolve pass（ShadowOnlyShadowResolvePassが低解像度RTに描画する際に使用）
        // フルシャドウ計算を実行し、結果を直接書き込む（Blend Off）
        // ==========================================
        Pass
        {
            Name "ShadowOnlyResolve"

            // Resolve RTに直接書き込み（ブレンドなし）
            Blend Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_resolve

            // ブラー品質プリセット切り替え用キーワード
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            #include "Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl"

            half4 frag_resolve(Varyings input) : SV_TARGET
            {
                return ComputeFloorShadow(input);
            }
            ENDHLSL
        }
    }

    // フォールバックなし - このシェーダーはShadowOnlyManagerから専用で使用される
}
