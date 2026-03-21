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
        // Resolve無効時（BlurResolutionScale=1.0）にフル影計算を行う。
        // Resolve有効時はFloorDisplayシェーダーに切り替わるため、このパスは使用されない。
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

            #include "Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl"

            half4 frag_display(Varyings input) : SV_TARGET
            {
                return ComputeFloorShadow(input);
            }
            ENDHLSL
        }

        // ==========================================
        // Pass 1: Per-light Resolve pass（ShadowOnlyShadowResolvePassがライトごとに描画）
        // 1ライトずつ描画し、加算ブレンドで合成することで
        // Texture2DArrayの複数スライス同時アクセスによるキャッシュスラッシングを回避する。
        // ==========================================
        Pass
        {
            Name "ShadowOnlyResolve"

            // ライトごとの寄与を加算合成（RTはクリア済み黒から開始）
            Blend One One
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_resolve_single

            // ブラー品質プリセット切り替え用キーワード
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            #include "Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl"

            // ResolvePassからライトごとに設定されるグローバル変数
            int _ResolveLightIndex;

            half4 frag_resolve_single(Varyings input) : SV_TARGET
            {
                return ComputeSingleLightContribution(input, _ResolveLightIndex);
            }
            ENDHLSL
        }
    }

    // フォールバックなし - このシェーダーはShadowOnlyManagerから専用で使用される
}
