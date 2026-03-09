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

        Pass
        {
            Name "ShadowOnlyFloor"

            // 影以外の部分をalpha=0で透明に保つ
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // ブラー品質プリセット切り替え用キーワード（Task 6.2で使用）
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 仮想光源の最大数
            #define MAX_VIRTUAL_LIGHTS 8

            // --- Uniform変数 ---
            // ShadowOnlyManagerから毎フレームMaterial.SetXxxで設定される

            // アクティブな仮想光源数
            int _VirtualLightCount;

            // 合成倍率パラメータ
            float _BlendMultiplier;

            // 各仮想光源のVP行列（ワールド空間からライト射影空間への変換）
            float4x4 _LightVPMatrix_0;
            float4x4 _LightVPMatrix_1;
            float4x4 _LightVPMatrix_2;
            float4x4 _LightVPMatrix_3;
            float4x4 _LightVPMatrix_4;
            float4x4 _LightVPMatrix_5;
            float4x4 _LightVPMatrix_6;
            float4x4 _LightVPMatrix_7;

            // 各仮想光源の深度テクスチャ
            TEXTURE2D(_ShadowDepthTex_0);
            TEXTURE2D(_ShadowDepthTex_1);
            TEXTURE2D(_ShadowDepthTex_2);
            TEXTURE2D(_ShadowDepthTex_3);
            TEXTURE2D(_ShadowDepthTex_4);
            TEXTURE2D(_ShadowDepthTex_5);
            TEXTURE2D(_ShadowDepthTex_6);
            TEXTURE2D(_ShadowDepthTex_7);

            SAMPLER(sampler_ShadowDepthTex_0);
            SAMPLER(sampler_ShadowDepthTex_1);
            SAMPLER(sampler_ShadowDepthTex_2);
            SAMPLER(sampler_ShadowDepthTex_3);
            SAMPLER(sampler_ShadowDepthTex_4);
            SAMPLER(sampler_ShadowDepthTex_5);
            SAMPLER(sampler_ShadowDepthTex_6);
            SAMPLER(sampler_ShadowDepthTex_7);

            // 各仮想光源の影の色（RGBAのうちRGBを使用）
            float4 _ShadowColor_0;
            float4 _ShadowColor_1;
            float4 _ShadowColor_2;
            float4 _ShadowColor_3;
            float4 _ShadowColor_4;
            float4 _ShadowColor_5;
            float4 _ShadowColor_6;
            float4 _ShadowColor_7;

            // 各仮想光源の影の濃さ（アルファ値）
            float _ShadowAlpha_0;
            float _ShadowAlpha_1;
            float _ShadowAlpha_2;
            float _ShadowAlpha_3;
            float _ShadowAlpha_4;
            float _ShadowAlpha_5;
            float _ShadowAlpha_6;
            float _ShadowAlpha_7;

            // 各仮想光源の深度バイアス
            float _DepthBias_0;
            float _DepthBias_1;
            float _DepthBias_2;
            float _DepthBias_3;
            float _DepthBias_4;
            float _DepthBias_5;
            float _DepthBias_6;
            float _DepthBias_7;

            // ブラー関連パラメータ（Task 6.2で使用、ここでは宣言のみ）
            float _BlurRadius_0;
            float _BlurRadius_1;
            float _BlurRadius_2;
            float _BlurRadius_3;
            float _BlurRadius_4;
            float _BlurRadius_5;
            float _BlurRadius_6;
            float _BlurRadius_7;

            float _BlurDistanceFactor_0;
            float _BlurDistanceFactor_1;
            float _BlurDistanceFactor_2;
            float _BlurDistanceFactor_3;
            float _BlurDistanceFactor_4;
            float _BlurDistanceFactor_5;
            float _BlurDistanceFactor_6;
            float _BlurDistanceFactor_7;

            // Hue Shift（Task 6.2で使用、ここでは宣言のみ）
            float _HueShift_0;
            float _HueShift_1;
            float _HueShift_2;
            float _HueShift_3;
            float _HueShift_4;
            float _HueShift_5;
            float _HueShift_6;
            float _HueShift_7;

            // 色収差（Task 6.2で使用、ここでは宣言のみ）
            float _ChromaticAberration_0;
            float _ChromaticAberration_1;
            float _ChromaticAberration_2;
            float _ChromaticAberration_3;
            float _ChromaticAberration_4;
            float _ChromaticAberration_5;
            float _ChromaticAberration_6;
            float _ChromaticAberration_7;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // オブジェクト空間からワールド空間への変換
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // ワールド空間からクリップ空間への変換（通常のカメラVP行列）
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // ヘルパー関数: VP行列のインデックスアクセス
            float4x4 GetLightVPMatrix(int index)
            {
                if (index == 0) return _LightVPMatrix_0;
                if (index == 1) return _LightVPMatrix_1;
                if (index == 2) return _LightVPMatrix_2;
                if (index == 3) return _LightVPMatrix_3;
                if (index == 4) return _LightVPMatrix_4;
                if (index == 5) return _LightVPMatrix_5;
                if (index == 6) return _LightVPMatrix_6;
                return _LightVPMatrix_7;
            }

            // ヘルパー関数: 影色のインデックスアクセス
            float4 GetShadowColor(int index)
            {
                if (index == 0) return _ShadowColor_0;
                if (index == 1) return _ShadowColor_1;
                if (index == 2) return _ShadowColor_2;
                if (index == 3) return _ShadowColor_3;
                if (index == 4) return _ShadowColor_4;
                if (index == 5) return _ShadowColor_5;
                if (index == 6) return _ShadowColor_6;
                return _ShadowColor_7;
            }

            // ヘルパー関数: 影の濃さのインデックスアクセス
            float GetShadowAlpha(int index)
            {
                if (index == 0) return _ShadowAlpha_0;
                if (index == 1) return _ShadowAlpha_1;
                if (index == 2) return _ShadowAlpha_2;
                if (index == 3) return _ShadowAlpha_3;
                if (index == 4) return _ShadowAlpha_4;
                if (index == 5) return _ShadowAlpha_5;
                if (index == 6) return _ShadowAlpha_6;
                return _ShadowAlpha_7;
            }

            // ヘルパー関数: 深度バイアスのインデックスアクセス
            float GetDepthBias(int index)
            {
                if (index == 0) return _DepthBias_0;
                if (index == 1) return _DepthBias_1;
                if (index == 2) return _DepthBias_2;
                if (index == 3) return _DepthBias_3;
                if (index == 4) return _DepthBias_4;
                if (index == 5) return _DepthBias_5;
                if (index == 6) return _DepthBias_6;
                return _DepthBias_7;
            }

            // ヘルパー関数: 深度テクスチャのサンプリング（インデックスアクセス）
            // HLSLではテクスチャ配列のインデックスアクセスがサポートされないため、
            // if分岐で各テクスチャを個別にサンプリングする
            float SampleShadowDepth(int index, float2 uv)
            {
                if (index == 0) return SAMPLE_TEXTURE2D(_ShadowDepthTex_0, sampler_ShadowDepthTex_0, uv).r;
                if (index == 1) return SAMPLE_TEXTURE2D(_ShadowDepthTex_1, sampler_ShadowDepthTex_1, uv).r;
                if (index == 2) return SAMPLE_TEXTURE2D(_ShadowDepthTex_2, sampler_ShadowDepthTex_2, uv).r;
                if (index == 3) return SAMPLE_TEXTURE2D(_ShadowDepthTex_3, sampler_ShadowDepthTex_3, uv).r;
                if (index == 4) return SAMPLE_TEXTURE2D(_ShadowDepthTex_4, sampler_ShadowDepthTex_4, uv).r;
                if (index == 5) return SAMPLE_TEXTURE2D(_ShadowDepthTex_5, sampler_ShadowDepthTex_5, uv).r;
                if (index == 6) return SAMPLE_TEXTURE2D(_ShadowDepthTex_6, sampler_ShadowDepthTex_6, uv).r;
                return SAMPLE_TEXTURE2D(_ShadowDepthTex_7, sampler_ShadowDepthTex_7, uv).r;
            }

            // 影判定: プロジェクティブテクスチャマッピングによる深度比較
            // ワールド位置をライトVP行列で射影空間に変換し、深度テクスチャと比較して
            // 影領域かどうかを判定する
            // 戻り値: 影の強度（0.0 = 影なし、1.0 = 完全な影）
            float ComputeShadow(int lightIndex, float3 positionWS)
            {
                // ワールド位置をライト射影空間に変換
                float4x4 lightVP = GetLightVPMatrix(lightIndex);
                float4 positionLS = mul(lightVP, float4(positionWS, 1.0));

                // 透視除算（クリップ空間からNDCへ）
                float3 ndc = positionLS.xyz / positionLS.w;

                // 射影範囲外の判定（クランプ処理）
                // NDC空間で [-1, 1] の範囲外のフラグメントでは影を描画しない
                if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0)
                {
                    return 0.0;
                }

                // NDCをUV座標に変換 [-1,1] -> [0,1]
                float2 shadowUV = ndc.xy * 0.5 + 0.5;

                // プラットフォームに応じたY座標の反転
                // Unity URPでは一部プラットフォームでUVのY軸が反転する
                #if UNITY_UV_STARTS_AT_TOP
                    shadowUV.y = 1.0 - shadowUV.y;
                #endif

                // 深度テクスチャからサンプリング
                float sampledDepth = SampleShadowDepth(lightIndex, shadowUV);

                // フラグメントの深度値（ライト射影空間でのZ値）
                // プラットフォームに応じた深度範囲の調整
                float fragmentDepth = ndc.z;

                float bias = GetDepthBias(lightIndex);
                float shadow = 0.0;

                #if UNITY_REVERSED_Z
                    // Reversed-Z: 近い = 1.0、遠い = 0.0
                    // 床面フラグメントは光源から見てキャスターの向こう側にある
                    // → fragmentDepth < sampledDepth（床面の方が遠い＝値が小さい）
                    // キャスターが床面より手前にある場合に影が発生する
                    shadow = fragmentDepth < sampledDepth - bias ? 1.0 : 0.0;
                #else
                    // 標準Z: 近い = 0.0（または-1.0）、遠い = 1.0
                    // NDCのZ値を[0,1]に変換
                    fragmentDepth = fragmentDepth * 0.5 + 0.5;
                    // 床面フラグメントがキャスターより遠い場合は影
                    shadow = fragmentDepth > sampledDepth + bias ? 1.0 : 0.0;
                #endif

                // 射影空間の背面（カメラの後ろ）にあるフラグメントは影なし
                if (positionLS.w <= 0.0)
                {
                    return 0.0;
                }

                return shadow;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                // 仮想光源がない場合は完全に透明
                if (_VirtualLightCount <= 0)
                {
                    return half4(0, 0, 0, 0);
                }

                // 各仮想光源の影を加算合成する
                float3 totalShadowColor = float3(0, 0, 0);
                float totalShadowAlpha = 0.0;

                // 光源数の上限をクランプ
                int lightCount = min(_VirtualLightCount, MAX_VIRTUAL_LIGHTS);

                for (int i = 0; i < lightCount; i++)
                {
                    // 影判定
                    float shadow = ComputeShadow(i, input.positionWS);

                    if (shadow > 0.0)
                    {
                        // 影の色と濃さを適用
                        float4 shadowColor = GetShadowColor(i);
                        float shadowAlpha = GetShadowAlpha(i);

                        // 影の寄与を加算合成
                        totalShadowColor += shadowColor.rgb * shadow;
                        totalShadowAlpha += shadowAlpha * shadow;
                    }
                }

                // 合成倍率パラメータで調整
                totalShadowAlpha *= _BlendMultiplier;

                // アルファを[0,1]にクランプ
                totalShadowAlpha = saturate(totalShadowAlpha);

                // 影がない場合は完全に透明
                if (totalShadowAlpha <= 0.0)
                {
                    return half4(0, 0, 0, 0);
                }

                // 複数光源の影色を正規化（加算合成の結果をクランプ）
                totalShadowColor = saturate(totalShadowColor);

                return half4(totalShadowColor, totalShadowAlpha);
            }
            ENDHLSL
        }
    }

    // フォールバックなし - このシェーダーはShadowOnlyManagerから専用で使用される
}
