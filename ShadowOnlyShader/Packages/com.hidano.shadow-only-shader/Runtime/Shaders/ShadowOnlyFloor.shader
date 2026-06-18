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

            // ライトごとの寄与を合成（RTはクリア済み黒から開始）。
            // RGB（影色）は加算、アルファ（影の濃さ）は Screen合成にする。
            //   アルファ: src.a + dst.a*(1 - src.a) = 1 - (1-dst.a)(1-src.a)
            //   → 各ライトを順に合成しても finalAlpha = 1 - Π(1 - a_i) となり、
            //     単一描画パス(ComputeFloorShadow)と一致する。重なりが加算で濃く
            //     なる問題を回避する（src.a は ComputeSingleLightContribution 側で
            //     [0,1] に saturate 済み）。
            Blend One One, One OneMinusSrcAlpha
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
        // ==========================================
        // Pass 2: Single-draw Resolve pass（小さいRTで全ライトを一括描画）
        // RTが十分小さい場合、テクスチャキャッシュに全スライスが収まるため
        // per-light分割の必要がない。ドローコール1回で済みGPUオーバーヘッドを最小化する。
        // ==========================================
        Pass
        {
            Name "ShadowOnlyResolveSingleDraw"

            // 全ライトの結果を一度に書き込み
            Blend Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_resolve_all

            // ブラー品質プリセット切り替え用キーワード
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            #include "Packages/com.hidano.shadow-only-shader/Runtime/Shaders/ShadowOnlyFloorCommon.hlsl"

            half4 frag_resolve_all(Varyings input) : SV_TARGET
            {
                return ComputeFloorShadow(input);
            }
            ENDHLSL
        }
    }

    // フォールバックなし - このシェーダーはShadowOnlyManagerから専用で使用される
}
