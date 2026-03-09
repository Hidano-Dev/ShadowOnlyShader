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

            // ブラー品質プリセット切り替え用キーワード
            // _BLUR_LOW: 5x5カーネル（約9サンプル）
            // _BLUR_MID: 9x9カーネル（約25サンプル）
            // _BLUR_HIGH: 13x13カーネル（約49サンプル）
            // キーワードなし: ブラーなし（単一サンプル）
            #pragma multi_compile _ _BLUR_LOW _BLUR_MID _BLUR_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 仮想光源の最大数
            #define MAX_VIRTUAL_LIGHTS 8

            // ブラー品質プリセットのサンプル半径（カーネル半径）
            #if defined(_BLUR_HIGH)
                #define BLUR_KERNEL_RADIUS 6
            #elif defined(_BLUR_MID)
                #define BLUR_KERNEL_RADIUS 4
            #elif defined(_BLUR_LOW)
                #define BLUR_KERNEL_RADIUS 2
            #else
                #define BLUR_KERNEL_RADIUS 0
            #endif

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

            // ブラー関連パラメータ
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

            // Hue Shift（度数 0-360）
            float _HueShift_0;
            float _HueShift_1;
            float _HueShift_2;
            float _HueShift_3;
            float _HueShift_4;
            float _HueShift_5;
            float _HueShift_6;
            float _HueShift_7;

            // 色収差強度
            float _ChromaticAberration_0;
            float _ChromaticAberration_1;
            float _ChromaticAberration_2;
            float _ChromaticAberration_3;
            float _ChromaticAberration_4;
            float _ChromaticAberration_5;
            float _ChromaticAberration_6;
            float _ChromaticAberration_7;

            // 各仮想光源のワールド位置（距離ボケ計算用）
            float4 _LightWorldPos_0;
            float4 _LightWorldPos_1;
            float4 _LightWorldPos_2;
            float4 _LightWorldPos_3;
            float4 _LightWorldPos_4;
            float4 _LightWorldPos_5;
            float4 _LightWorldPos_6;
            float4 _LightWorldPos_7;

            // 各仮想光源の深度テクスチャサイズ (width, height, 1/width, 1/height)
            float4 _DepthTexSize_0;
            float4 _DepthTexSize_1;
            float4 _DepthTexSize_2;
            float4 _DepthTexSize_3;
            float4 _DepthTexSize_4;
            float4 _DepthTexSize_5;
            float4 _DepthTexSize_6;
            float4 _DepthTexSize_7;

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

            // ===================================================================
            // ヘルパー関数: インデックスによるパラメータアクセス
            // HLSLではuniform配列のインデックスアクセスに制限があるため、
            // if分岐で各パラメータを個別にアクセスする
            // ===================================================================

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

            float GetBlurRadius(int index)
            {
                if (index == 0) return _BlurRadius_0;
                if (index == 1) return _BlurRadius_1;
                if (index == 2) return _BlurRadius_2;
                if (index == 3) return _BlurRadius_3;
                if (index == 4) return _BlurRadius_4;
                if (index == 5) return _BlurRadius_5;
                if (index == 6) return _BlurRadius_6;
                return _BlurRadius_7;
            }

            float GetBlurDistanceFactor(int index)
            {
                if (index == 0) return _BlurDistanceFactor_0;
                if (index == 1) return _BlurDistanceFactor_1;
                if (index == 2) return _BlurDistanceFactor_2;
                if (index == 3) return _BlurDistanceFactor_3;
                if (index == 4) return _BlurDistanceFactor_4;
                if (index == 5) return _BlurDistanceFactor_5;
                if (index == 6) return _BlurDistanceFactor_6;
                return _BlurDistanceFactor_7;
            }

            float GetHueShift(int index)
            {
                if (index == 0) return _HueShift_0;
                if (index == 1) return _HueShift_1;
                if (index == 2) return _HueShift_2;
                if (index == 3) return _HueShift_3;
                if (index == 4) return _HueShift_4;
                if (index == 5) return _HueShift_5;
                if (index == 6) return _HueShift_6;
                return _HueShift_7;
            }

            float GetChromaticAberration(int index)
            {
                if (index == 0) return _ChromaticAberration_0;
                if (index == 1) return _ChromaticAberration_1;
                if (index == 2) return _ChromaticAberration_2;
                if (index == 3) return _ChromaticAberration_3;
                if (index == 4) return _ChromaticAberration_4;
                if (index == 5) return _ChromaticAberration_5;
                if (index == 6) return _ChromaticAberration_6;
                return _ChromaticAberration_7;
            }

            float4 GetLightWorldPos(int index)
            {
                if (index == 0) return _LightWorldPos_0;
                if (index == 1) return _LightWorldPos_1;
                if (index == 2) return _LightWorldPos_2;
                if (index == 3) return _LightWorldPos_3;
                if (index == 4) return _LightWorldPos_4;
                if (index == 5) return _LightWorldPos_5;
                if (index == 6) return _LightWorldPos_6;
                return _LightWorldPos_7;
            }

            float4 GetDepthTexSize(int index)
            {
                if (index == 0) return _DepthTexSize_0;
                if (index == 1) return _DepthTexSize_1;
                if (index == 2) return _DepthTexSize_2;
                if (index == 3) return _DepthTexSize_3;
                if (index == 4) return _DepthTexSize_4;
                if (index == 5) return _DepthTexSize_5;
                if (index == 6) return _DepthTexSize_6;
                return _DepthTexSize_7;
            }

            // 深度テクスチャのサンプリング（インデックスアクセス）
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

            // ===================================================================
            // RGB <-> HSV 変換ユーティリティ
            // HSV色空間でのHue Shift（色相回転）処理に使用
            // ===================================================================

            // RGBからHSVへの変換
            float3 RGBtoHSV(float3 rgb)
            {
                float cMax = max(rgb.r, max(rgb.g, rgb.b));
                float cMin = min(rgb.r, min(rgb.g, rgb.b));
                float delta = cMax - cMin;

                float h = 0.0;
                float s = 0.0;
                float v = cMax;

                if (delta > 0.00001)
                {
                    s = delta / cMax;

                    if (cMax == rgb.r)
                    {
                        h = (rgb.g - rgb.b) / delta;
                        if (h < 0.0) h += 6.0;
                    }
                    else if (cMax == rgb.g)
                    {
                        h = 2.0 + (rgb.b - rgb.r) / delta;
                    }
                    else
                    {
                        h = 4.0 + (rgb.r - rgb.g) / delta;
                    }
                    h /= 6.0; // [0, 1]に正規化
                }

                return float3(h, s, v);
            }

            // HSVからRGBへの変換
            float3 HSVtoRGB(float3 hsv)
            {
                float h = hsv.x * 6.0;
                float s = hsv.y;
                float v = hsv.z;

                float c = v * s;
                float x = c * (1.0 - abs(fmod(h, 2.0) - 1.0));
                float m = v - c;

                float3 rgb;
                if (h < 1.0)      rgb = float3(c, x, 0);
                else if (h < 2.0) rgb = float3(x, c, 0);
                else if (h < 3.0) rgb = float3(0, c, x);
                else if (h < 4.0) rgb = float3(0, x, c);
                else if (h < 5.0) rgb = float3(x, 0, c);
                else              rgb = float3(c, 0, x);

                return rgb + m;
            }

            // Hue Shift適用: 影色の色相をシフトする
            // hueShiftDegrees: 色相回転量（度数 0-360）
            float3 ApplyHueShift(float3 rgb, float hueShiftDegrees)
            {
                if (hueShiftDegrees < 0.001)
                    return rgb;

                float3 hsv = RGBtoHSV(rgb);
                hsv.x = frac(hsv.x + hueShiftDegrees / 360.0);
                return HSVtoRGB(hsv);
            }

            // ===================================================================
            // ガウシアンブラー重み計算
            // ===================================================================

            // 1Dガウシアン関数
            float GaussianWeight(float offset, float sigma)
            {
                return exp(-(offset * offset) / (2.0 * sigma * sigma));
            }

            // ===================================================================
            // 深度比較: 指定UVでの影判定（単一サンプル）
            // ===================================================================

            float ComputeShadowAtUV(int lightIndex, float2 shadowUV, float fragmentDepth, float bias)
            {
                float sampledDepth = SampleShadowDepth(lightIndex, shadowUV);
                float shadow = 0.0;

                #if UNITY_REVERSED_Z
                    shadow = fragmentDepth < sampledDepth - bias ? 1.0 : 0.0;
                #else
                    shadow = fragmentDepth > sampledDepth + bias ? 1.0 : 0.0;
                #endif

                return shadow;
            }

            // ===================================================================
            // 影判定: プロジェクティブテクスチャマッピングによる深度比較
            // ブラーキーワードが有効な場合はガウシアンサンプリングブラーを適用
            // 距離ボケにも対応（光源からの距離に応じたブラー半径の動的変化）
            // ===================================================================

            float ComputeShadow(int lightIndex, float3 positionWS)
            {
                // ワールド位置をライト射影空間に変換
                float4x4 lightVP = GetLightVPMatrix(lightIndex);
                float4 positionLS = mul(lightVP, float4(positionWS, 1.0));

                // 射影空間の背面（カメラの後ろ）にあるフラグメントは影なし
                if (positionLS.w <= 0.0)
                {
                    return 0.0;
                }

                // 透視除算（クリップ空間からNDCへ）
                float3 ndc = positionLS.xyz / positionLS.w;

                // 射影範囲外の判定（クランプ処理）
                if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0)
                {
                    return 0.0;
                }

                // NDCをUV座標に変換 [-1,1] -> [0,1]
                float2 shadowUV = ndc.xy * 0.5 + 0.5;

                // プラットフォームに応じたY座標の反転
                #if UNITY_UV_STARTS_AT_TOP
                    shadowUV.y = 1.0 - shadowUV.y;
                #endif

                // フラグメントの深度値
                float fragmentDepth = ndc.z;
                #if !UNITY_REVERSED_Z
                    fragmentDepth = fragmentDepth * 0.5 + 0.5;
                #endif

                float bias = GetDepthBias(lightIndex);

                // Perspective投影では深度が非線形（depth ≈ near/d）のため、
                // NDC空間での深度差がw²に反比例して縮小する。
                // バイアスを1/w²でスケーリングすることで、投影モードに依存しない
                // 一貫した深度比較を実現する。Orthographic（w=1）では影響なし。
                bias /= (positionLS.w * positionLS.w);

                // ブラーなし（キーワード未設定時）: 単一サンプル
                #if BLUR_KERNEL_RADIUS == 0
                    return ComputeShadowAtUV(lightIndex, shadowUV, fragmentDepth, bias);
                #else
                    // ガウシアンサンプリングブラー
                    float blurRadius = GetBlurRadius(lightIndex);

                    // 距離ボケ: 光源からの距離に応じたブラー半径の動的変化
                    float blurDistanceFactor = GetBlurDistanceFactor(lightIndex);
                    if (blurDistanceFactor > 0.0)
                    {
                        float3 lightPos = GetLightWorldPos(lightIndex).xyz;
                        float dist = length(positionWS - lightPos);
                        // 距離に比例してブラー半径を増加させる
                        blurRadius *= (1.0 + dist * blurDistanceFactor);
                    }

                    // ブラー半径が0の場合は単一サンプルにフォールバック
                    if (blurRadius < 0.001)
                    {
                        return ComputeShadowAtUV(lightIndex, shadowUV, fragmentDepth, bias);
                    }

                    // テクセルサイズの取得
                    float4 texSize = GetDepthTexSize(lightIndex);
                    float2 texelSize = texSize.zw; // (1/width, 1/height)

                    // テクセルサイズが0の場合のフォールバック（安全策）
                    if (texelSize.x < 0.000001)
                    {
                        texelSize = float2(1.0 / 1024.0, 1.0 / 1024.0);
                    }

                    // ガウシアンブラー: カーネル半径に応じたサンプリング
                    float totalWeight = 0.0;
                    float totalShadow = 0.0;
                    float sigma = max((float)BLUR_KERNEL_RADIUS / 3.0, 0.5);

                    for (int y = -BLUR_KERNEL_RADIUS; y <= BLUR_KERNEL_RADIUS; y++)
                    {
                        for (int x = -BLUR_KERNEL_RADIUS; x <= BLUR_KERNEL_RADIUS; x++)
                        {
                            float2 offset = float2((float)x, (float)y) * texelSize * blurRadius;
                            float2 sampleUV = shadowUV + offset;

                            // UV範囲外チェック
                            if (sampleUV.x < 0.0 || sampleUV.x > 1.0 ||
                                sampleUV.y < 0.0 || sampleUV.y > 1.0)
                            {
                                continue;
                            }

                            float dist = length(float2((float)x, (float)y));
                            float weight = GaussianWeight(dist, sigma);

                            totalShadow += ComputeShadowAtUV(lightIndex, sampleUV, fragmentDepth, bias) * weight;
                            totalWeight += weight;
                        }
                    }

                    return totalWeight > 0.0 ? totalShadow / totalWeight : 0.0;
                #endif
            }

            // ===================================================================
            // 色収差: RGBチャンネル分離
            // 影の中心からの放射方向にRGBチャンネルをオフセットする
            // ===================================================================

            // 色収差を適用した影色を計算する
            // shadowUVCenter: 影のUV中心（0.5, 0.5 = テクスチャ中心）
            // fragmentUV: フラグメントのUV座標
            // baseColor: ベース影色
            // aberrationStrength: 色収差強度
            float3 ApplyChromaticAberration(float3 baseColor, float2 fragmentUV, float aberrationStrength)
            {
                if (aberrationStrength < 0.001)
                    return baseColor;

                // 影の中心からの放射方向（UV空間の中心 0.5, 0.5 からの方向）
                float2 centerUV = float2(0.5, 0.5);
                float2 direction = fragmentUV - centerUV;
                float dirLength = length(direction);

                if (dirLength < 0.001)
                    return baseColor;

                // 放射方向に沿ってRGBチャンネルをオフセット
                // R: 外側にオフセット（正方向）
                // G: 変化なし
                // B: 内側にオフセット（負方向）
                float offsetAmount = aberrationStrength * dirLength;

                float3 result;
                result.r = baseColor.r * (1.0 + offsetAmount);
                result.g = baseColor.g;
                result.b = baseColor.b * (1.0 - offsetAmount);

                return saturate(result);
            }

            // ===================================================================
            // フラグメントシェーダー
            // ===================================================================

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
                    // 影判定（ブラー適用済み）
                    float shadow = ComputeShadow(i, input.positionWS);

                    if (shadow > 0.0)
                    {
                        // 影の色と濃さを取得
                        float4 shadowColor = GetShadowColor(i);
                        float shadowAlpha = GetShadowAlpha(i);

                        float3 finalColor = shadowColor.rgb;

                        // Hue Shift適用（HSV色空間での色相回転）
                        float hueShift = GetHueShift(i);
                        finalColor = ApplyHueShift(finalColor, hueShift);

                        // 色収差適用（RGBチャンネル分離）
                        float chromaticAberration = GetChromaticAberration(i);
                        if (chromaticAberration > 0.001)
                        {
                            // フラグメントのUV座標を取得（ライト射影空間）
                            float4x4 lightVP = GetLightVPMatrix(i);
                            float4 posLS = mul(lightVP, float4(input.positionWS, 1.0));
                            float2 fragUV = (posLS.xy / posLS.w) * 0.5 + 0.5;

                            finalColor = ApplyChromaticAberration(finalColor, fragUV, chromaticAberration);
                        }

                        // 影の寄与を加算合成
                        totalShadowColor += finalColor * shadow;
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
