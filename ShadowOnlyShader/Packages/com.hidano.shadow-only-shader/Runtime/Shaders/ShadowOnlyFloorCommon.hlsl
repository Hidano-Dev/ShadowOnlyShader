#ifndef SHADOW_ONLY_FLOOR_COMMON_INCLUDED
#define SHADOW_ONLY_FLOOR_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 仮想光源の最大数（ShadowOnlyManager.MaxVirtualLights と一致させること）
#define MAX_VIRTUAL_LIGHTS 32

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

// --- Uniform変数（配列化） ---
// ShadowOnlyManagerからSetXxxArrayで一括設定される

// アクティブな仮想光源数
int _VirtualLightCount;

// 合成倍率パラメータ
float _BlendMultiplier;

// 各仮想光源のVP行列（ワールド空間からライト射影空間への変換）
float4x4 _LightVPMatrices[MAX_VIRTUAL_LIGHTS];

// 深度Texture2DArray（全仮想光源共有、スライスインデックスでアクセス）
TEXTURE2D_ARRAY(_ShadowDepthTexArray);
SAMPLER(sampler_ShadowDepthTexArray);

// 各仮想光源の影の色（RGBAのうちRGBを使用）
float4 _ShadowColors[MAX_VIRTUAL_LIGHTS];

// 各仮想光源の影の濃さ（アルファ値）
float _ShadowAlphas[MAX_VIRTUAL_LIGHTS];

// 各仮想光源の深度バイアス
float _DepthBiases[MAX_VIRTUAL_LIGHTS];

// ブラー関連パラメータ
float _BlurRadii[MAX_VIRTUAL_LIGHTS];
float _BlurDistanceFactors[MAX_VIRTUAL_LIGHTS];
float _BlurCameraDistanceFactors[MAX_VIRTUAL_LIGHTS];
float _CameraDistancePowers[MAX_VIRTUAL_LIGHTS];
float _AlphaCameraDistanceFactors[MAX_VIRTUAL_LIGHTS];

// Hue Shift（度数 0-360）
float _HueShifts[MAX_VIRTUAL_LIGHTS];

// 色収差強度
float _ChromaticAberrations[MAX_VIRTUAL_LIGHTS];

// 色収差の光源色（スペクトル重み変調用）
float4 _ChromaticAberrationColors[MAX_VIRTUAL_LIGHTS];

// コンタクトハードニング（PCSS）強度
float _ContactHardeningStrengths[MAX_VIRTUAL_LIGHTS];

// 各仮想光源のワールド位置（距離ボケ計算用）
float4 _LightWorldPositions[MAX_VIRTUAL_LIGHTS];

// 各仮想光源の深度テクスチャサイズ (width, height, 1/width, 1/height)
float4 _DepthTexSizes[MAX_VIRTUAL_LIGHTS];

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
// PCSS: ブロッカーサーチ
// 周辺の深度テクスチャをサンプリングし、遮蔽物の平均深度を求める
// 戻り値: x = 平均ブロッカー深度, y = ブロッカー数（0 = 影なし）
// ===================================================================

#if BLUR_KERNEL_RADIUS > 0
// ブロッカーサーチ用のサンプリング半径（カーネル半径の半分で粗くサンプリング）
#define BLOCKER_SEARCH_RADIUS ((BLUR_KERNEL_RADIUS + 1) / 2)

float2 BlockerSearch(int lightIndex, float2 shadowUV, float fragmentDepth,
                     float2 texelSize, float searchRadius, float bias)
{
    float blockerSum = 0.0;
    float blockerCount = 0.0;

    [loop] for (int y = -BLOCKER_SEARCH_RADIUS; y <= BLOCKER_SEARCH_RADIUS; y++)
    {
        [loop] for (int x = -BLOCKER_SEARCH_RADIUS; x <= BLOCKER_SEARCH_RADIUS; x++)
        {
            float2 sampleUV = shadowUV + float2((float)x, (float)y) * texelSize * searchRadius;

            if (sampleUV.x < 0.0 || sampleUV.x > 1.0 ||
                sampleUV.y < 0.0 || sampleUV.y > 1.0)
            {
                continue;
            }

            float sampledDepth = SAMPLE_TEXTURE2D_ARRAY(_ShadowDepthTexArray, sampler_ShadowDepthTexArray, sampleUV, lightIndex).r;

            // ブロッカー判定: フラグメントより手前にある深度値を収集
            #if UNITY_REVERSED_Z
            if (sampledDepth > fragmentDepth + bias)
            #else
            if (sampledDepth < fragmentDepth - bias)
            #endif
            {
                blockerSum += sampledDepth;
                blockerCount += 1.0;
            }
        }
    }

    return float2(blockerSum, blockerCount);
}
#endif

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
    float sampledDepth = SAMPLE_TEXTURE2D_ARRAY(_ShadowDepthTexArray, sampler_ShadowDepthTexArray, shadowUV, lightIndex).r;
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

// 影判定の内部実装: uvOffset を加算して影をサンプリングする
// uvOffset = (0,0) のとき通常の影判定と同じ
float ComputeShadowInternal(int lightIndex, float3 positionWS, float2 uvOffset)
{
    // ワールド位置をライト射影空間に変換
    float4x4 lightVP = _LightVPMatrices[lightIndex];
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

    // プラットフォームに応じたY座標の反転。
    // _LightVPMatrices は GL.GetGPUProjectionMatrix(proj, true) 済みでサンプリング側には
    // テクスチャY反転が入るが、深度パスの cmd.SetViewProjectionMatrices による実描画では
    // そのY反転が適用されず、深度RTはスクリーン向き（非反転）で格納される。
    // この食い違いを UNITY_UV_STARTS_AT_TOP プラットフォーム（D3D等）で補正する。
    // これを省くと影が前後（V方向）に反転する。
    #if UNITY_UV_STARTS_AT_TOP
        shadowUV.y = 1.0 - shadowUV.y;
    #endif

    // 色収差用UVオフセットを適用
    shadowUV += uvOffset;

    // オフセット後のUV範囲外チェック
    if (shadowUV.x < 0.0 || shadowUV.x > 1.0 ||
        shadowUV.y < 0.0 || shadowUV.y > 1.0)
    {
        return 0.0;
    }

    // フラグメントの深度値
    float fragmentDepth = ndc.z;
    #if !UNITY_REVERSED_Z
        fragmentDepth = fragmentDepth * 0.5 + 0.5;
    #endif

    float bias = _DepthBiases[lightIndex];

    // Perspective投影では深度が非線形（depth ≈ near/d）のため、
    // NDC空間での深度差がw²に反比例して縮小する。
    // バイアスを1/w²でスケーリングすることで、投影モードに依存しない
    // 一貫した深度比較を実現する。Orthographic（w=1）では影響なし。
    bias /= (positionLS.w * positionLS.w);

    // ブラーなし（キーワード未設定時）: 単一サンプル
    #if BLUR_KERNEL_RADIUS == 0
        return ComputeShadowAtUV(lightIndex, shadowUV, fragmentDepth, bias);
    #else
        // テクセルサイズの取得
        float4 texSize = _DepthTexSizes[lightIndex];
        float2 texelSize = texSize.zw; // (1/width, 1/height)

        // テクセルサイズが0の場合のフォールバック（安全策）
        if (texelSize.x < 0.000001)
        {
            texelSize = float2(1.0 / 1024.0, 1.0 / 1024.0);
        }

        // ガウシアンサンプリングブラー
        float blurRadius = _BlurRadii[lightIndex];

        // 距離ボケ: 光源からの距離に応じたブラー半径の動的変化
        float blurDistanceFactor = _BlurDistanceFactors[lightIndex];
        if (blurDistanceFactor > 0.0)
        {
            float3 lightPos = _LightWorldPositions[lightIndex].xyz;
            float dist = length(positionWS - lightPos);
            // 距離に比例してブラー半径を増加させる
            blurRadius *= (1.0 + dist * blurDistanceFactor);
        }

        // カメラ距離ボケ: カメラからの距離に応じたブラー半径の動的変化
        float blurCameraDistanceFactor = _BlurCameraDistanceFactors[lightIndex];
        if (blurCameraDistanceFactor > 0.0)
        {
            float3 cameraPos = GetCameraPositionWS();
            float cameraDist = length(positionWS - cameraPos);
            // べき乗で距離カーブを調整（power=1:線形, >1:遠方で急激, <1:近くから効く）
            float cameraDistPower = _CameraDistancePowers[lightIndex];
            float scaledDist = pow(max(cameraDist, 0.001), cameraDistPower);
            blurRadius *= (1.0 + scaledDist * blurCameraDistanceFactor);
        }

        // PCSS: コンタクトハードニング
        // キャスターとレシーバーの深度差に基づくブラー半径の動的変化
        float contactHardening = _ContactHardeningStrengths[lightIndex];
        if (contactHardening > 0.0)
        {
            // ブロッカーサーチ: 検索半径はベースブラー半径を使用
            float searchRadius = max(blurRadius, 1.0);
            float2 blockerResult = BlockerSearch(lightIndex, shadowUV, fragmentDepth,
                                                 texelSize, searchRadius, bias);

            if (blockerResult.y < 0.5)
            {
                // ブロッカーなし = 影なし
                return 0.0;
            }

            // 平均ブロッカー深度
            float avgBlockerDepth = blockerResult.x / blockerResult.y;

            // ペナンブラ幅の推定
            // PCSS公式: penumbra = lightSize * |d_receiver - d_blocker| / d_blocker
            float depthDiff = abs(fragmentDepth - avgBlockerDepth);
            float penumbraWidth = contactHardening * depthDiff / max(abs(avgBlockerDepth), 0.001);

            // ペナンブラ幅をブラー半径に加算
            // ベースのblurRadiusが最小値として機能し、ペナンブラで増幅される
            blurRadius = max(blurRadius, penumbraWidth);
        }

        // ブラー半径が0の場合は単一サンプルにフォールバック
        if (blurRadius < 0.001)
        {
            return ComputeShadowAtUV(lightIndex, shadowUV, fragmentDepth, bias);
        }

        // ガウシアンブラー: カーネル半径に応じたサンプリング
        float totalWeight = 0.0;
        float totalShadow = 0.0;
        float sigma = max((float)BLUR_KERNEL_RADIUS / 3.0, 0.5);

        [loop] for (int y = -BLUR_KERNEL_RADIUS; y <= BLUR_KERNEL_RADIUS; y++)
        {
            [loop] for (int x = -BLUR_KERNEL_RADIUS; x <= BLUR_KERNEL_RADIUS; x++)
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

float ComputeShadow(int lightIndex, float3 positionWS)
{
    return ComputeShadowInternal(lightIndex, positionWS, float2(0, 0));
}

// ===================================================================
// 色収差: RGBチャンネルごとに異なるUV位置で影をサンプリング
// 影のエッジにRGBフリンジを生成する
// ===================================================================

// ブラー有無でCAサンプル数を切り替え
#if BLUR_KERNEL_RADIUS == 0
    #define CA_SAMPLES 11
#else
    #define CA_SAMPLES 5
#endif

float3 ComputeShadowWithChromaticAberration(int lightIndex, float3 positionWS, float aberrationStrength)
{
    // ライト射影空間でのUV方向を計算
    float4x4 lightVP = _LightVPMatrices[lightIndex];
    float4 posLS = mul(lightVP, float4(positionWS, 1.0));

    if (posLS.w <= 0.0)
        return float3(0, 0, 0);

    float2 fragUV = (posLS.xy / posLS.w) * 0.5 + 0.5;

    // ComputeShadowInternal と同じY反転補正（深度RTとサンプリング行列のY食い違い対策）
    #if UNITY_UV_STARTS_AT_TOP
        fragUV.y = 1.0 - fragUV.y;
    #endif

    // 投影中心からの放射方向
    float2 centerUV = float2(0.5, 0.5);
    float2 dir = fragUV - centerUV;
    float dirLen = length(dir);

    if (dirLen < 0.001)
    {
        // 中心付近では色収差なし（通常の影を返す）
        float s = ComputeShadow(lightIndex, positionWS);
        return float3(s, s, s);
    }

    // UVスケーリング係数
    float scaleFactor = aberrationStrength * 0.04;

    // スペクトル多点サンプリング
    float3 totalColor = float3(0, 0, 0);
    float3 totalWeight = float3(0, 0, 0);

    float invLastIndex = 1.0 / (float)(CA_SAMPLES - 1);

    [loop] for (int s = 0; s < CA_SAMPLES; s++)
    {
        float t = (float)s * invLastIndex; // 0.0 → 1.0
        float offsetScale = t * 2.0 - 1.0; // -1.0 → +1.0
        float2 uvOffset = dir * scaleFactor * offsetScale;

        float shadowVal = ComputeShadowInternal(lightIndex, positionWS, uvOffset);

        // スペクトル重み
        float3 w;
        w.r = saturate(1.0 - 2.0 * t);
        w.g = 1.0 - abs(2.0 * t - 1.0);
        w.b = saturate(2.0 * t - 1.0);

        totalColor += shadowVal * w;
        totalWeight += w;
    }

    return totalColor / max(totalWeight, float3(0.001, 0.001, 0.001));
}

// CA_SAMPLESマクロはこの関数内でのみ使用するため解除
#undef CA_SAMPLES

// ===================================================================
// 単一ライトの影寄与を計算する関数
// Resolve Passのライトごと分割描画や、ComputeFloorShadowのループ内部から使用
// 戻り値: half4(影色寄与, アルファ寄与 × _BlendMultiplier)
// ===================================================================

half4 ComputeSingleLightContribution(Varyings input, int i)
{
    // 色収差の有無で影サンプリング方法を切り替え
    float chromaticAberration = _ChromaticAberrations[i];
    float3 shadowRGB;

    if (chromaticAberration > 0.001)
    {
        // 色収差あり: RGBチャンネルごとに異なるUV位置でサンプリング
        shadowRGB = ComputeShadowWithChromaticAberration(i, input.positionWS, chromaticAberration);

        // 光源色による色収差変調
        float4 caColor = _ChromaticAberrationColors[i];
        float caColorSum = caColor.r + caColor.g + caColor.b;
        if (caColorSum > 0.001)
        {
            float shadowWeighted = dot(shadowRGB, caColor.rgb) / caColorSum;
            shadowRGB.r = lerp(shadowWeighted, shadowRGB.r, caColor.r);
            shadowRGB.g = lerp(shadowWeighted, shadowRGB.g, caColor.g);
            shadowRGB.b = lerp(shadowWeighted, shadowRGB.b, caColor.b);
        }
    }
    else
    {
        // 色収差なし: 通常の影判定
        float s = ComputeShadow(i, input.positionWS);
        shadowRGB = float3(s, s, s);
    }

    // いずれかのチャンネルに影がある場合
    float shadow = max(shadowRGB.r, max(shadowRGB.g, shadowRGB.b));

    if (shadow <= 0.0)
    {
        return half4(0, 0, 0, 0);
    }

    // 影の色と濃さを取得
    float4 shadowColor = _ShadowColors[i];
    float shadowAlpha = _ShadowAlphas[i];

    // カメラ距離アルファ減衰: カメラから遠いほど影が薄くなる
    float alphaCameraDistanceFactor = _AlphaCameraDistanceFactors[i];
    if (alphaCameraDistanceFactor > 0.0)
    {
        float3 cameraPos = GetCameraPositionWS();
        float cameraDist = length(input.positionWS - cameraPos);
        // べき乗で距離カーブを調整
        float cameraDistPower = _CameraDistancePowers[i];
        float scaledDist = pow(max(cameraDist, 0.001), cameraDistPower);
        shadowAlpha *= saturate(1.0 / (1.0 + scaledDist * alphaCameraDistanceFactor));
    }

    float3 finalColor = shadowColor.rgb;

    // Hue Shift適用（HSV色空間での色相回転）
    float hueShift = _HueShifts[i];
    finalColor = ApplyHueShift(finalColor, hueShift);

    // 色収差フリンジ処理
    float3 contribution = finalColor * shadowRGB;

    float3 fringeDiff = shadowRGB - min(shadowRGB.r, min(shadowRGB.g, shadowRGB.b));
    float fringeStrength = max(fringeDiff.r, max(fringeDiff.g, fringeDiff.b));

    if (fringeStrength > 0.001)
    {
        float3 fringeTint = fringeDiff / fringeStrength;
        contribution += fringeTint * fringeStrength * 0.5;
    }

    // アルファ寄与（_BlendMultiplierを含む）。
    // Screen合成（finalAlpha = 1 - Π(1 - a_i)）の前提として各ライトの寄与を[0,1]に収める。
    // ライトごとResolveパスの GPU ブレンド（Blend One OneMinusSrcAlpha）も
    // src.a が [0,1] であることを前提とするため、ここで saturate しておく。
    float avgShadow = (shadowRGB.r + shadowRGB.g + shadowRGB.b) / 3.0;
    float alphaContribution = saturate(shadowAlpha * avgShadow * _BlendMultiplier);

    return half4(contribution, alphaContribution);
}

// ===================================================================
// フロアシャドウ計算メイン関数
// 全ライトの影を合成して最終色を返す
// ===================================================================

half4 ComputeFloorShadow(Varyings input)
{
    // 仮想光源がない場合は完全に透明
    if (_VirtualLightCount <= 0)
    {
        return half4(0, 0, 0, 0);
    }

    // 各仮想光源の影を合成する。
    // アルファ（影の濃さ）は Screen合成: finalAlpha = 1 - Π(1 - a_i)。
    // 加算（Σa_i）だと複数光源の影が重なった部分で濃さが足し合わさり、
    // 個々を薄くしても重なりだけ濃くなってしまう。Screen合成では重なっても
    // 最大で 1（不透明）に漸近するだけで、単一の影より極端に濃くならない。
    // 色（tint）は従来どおり加算し、最後に saturate でクランプする。
    float3 totalShadowColor = float3(0, 0, 0);
    float transmittance = 1.0; // 各ライトの透過率(1 - a_i)の積

    // 光源数の上限をクランプ
    int lightCount = min(_VirtualLightCount, MAX_VIRTUAL_LIGHTS);

    [loop] for (int i = 0; i < lightCount; i++)
    {
        half4 lightContrib = ComputeSingleLightContribution(input, i);
        totalShadowColor += lightContrib.rgb;
        transmittance *= (1.0 - lightContrib.a);
    }

    // Screen合成の最終アルファ（透過率の補数）。各 a_i は[0,1]なので自動的に[0,1]。
    float totalShadowAlpha = 1.0 - transmittance;

    // 影がない場合は完全に透明
    if (totalShadowAlpha <= 0.0)
    {
        return half4(0, 0, 0, 0);
    }

    // 複数光源の影色を正規化（加算合成の結果をクランプ）
    totalShadowColor = saturate(totalShadowColor);

    return half4(totalShadowColor, totalShadowAlpha);
}

#endif // SHADOW_ONLY_FLOOR_COMMON_INCLUDED
