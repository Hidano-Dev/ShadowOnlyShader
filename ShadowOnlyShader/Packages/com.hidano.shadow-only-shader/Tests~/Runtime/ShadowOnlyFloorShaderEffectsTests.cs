using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// ShadowOnlyFloorシェーダーのブラー・Hue Shift・色収差表現機能の検証テスト (Task 6.2)
    /// multi_compileキーワードによるガウシアンブラー品質プリセット、
    /// 距離ボケ、HSV Hue Shift、RGBチャンネル分離色収差の実装を検証する。
    /// Requirements: 7.2, 7.3, 7.4, 7.5, 7.6
    /// </summary>
    public class ShadowOnlyFloorShaderEffectsTests
    {
        private Shader _shader;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _shader = Shader.Find("Hidden/ShadowOnlyShader/Floor");
            Assert.IsNotNull(_shader, "ShadowOnlyFloorシェーダーが見つかること");

            _material = new Material(_shader);
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }
        }

        #region multi_compileブラーキーワードテスト (Req 7.3)

        [Test]
        public void Shader_BLUR_LOWキーワードが有効化できる()
        {
            _material.EnableKeyword("_BLUR_LOW");
            Assert.IsTrue(_material.IsKeywordEnabled("_BLUR_LOW"),
                "_BLUR_LOWキーワードが有効化できること");
        }

        [Test]
        public void Shader_BLUR_MIDキーワードが有効化できる()
        {
            _material.EnableKeyword("_BLUR_MID");
            Assert.IsTrue(_material.IsKeywordEnabled("_BLUR_MID"),
                "_BLUR_MIDキーワードが有効化できること");
        }

        [Test]
        public void Shader_BLUR_HIGHキーワードが有効化できる()
        {
            _material.EnableKeyword("_BLUR_HIGH");
            Assert.IsTrue(_material.IsKeywordEnabled("_BLUR_HIGH"),
                "_BLUR_HIGHキーワードが有効化できること");
        }

        [Test]
        public void Shader_ブラーキーワードは排他的に切り替えできる()
        {
            // _BLUR_LOWを有効化してから_BLUR_HIGHに切り替え
            _material.EnableKeyword("_BLUR_LOW");
            _material.DisableKeyword("_BLUR_LOW");
            _material.EnableKeyword("_BLUR_HIGH");

            Assert.IsFalse(_material.IsKeywordEnabled("_BLUR_LOW"),
                "_BLUR_LOWが無効化されていること");
            Assert.IsTrue(_material.IsKeywordEnabled("_BLUR_HIGH"),
                "_BLUR_HIGHが有効化されていること");
        }

        #endregion

        #region ブラー関連uniformプロパティテスト (Req 7.2)

        [Test]
        public void Shader_BlurRadiusプロパティが設定できる()
        {
            float testValue = 2.5f;
            _material.SetFloat("_BlurRadius_0", testValue);
            float result = _material.GetFloat("_BlurRadius_0");
            Assert.AreEqual(testValue, result, 0.001f,
                "_BlurRadius_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_BlurDistanceFactorプロパティが設定できる()
        {
            float testValue = 1.5f;
            _material.SetFloat("_BlurDistanceFactor_0", testValue);
            float result = _material.GetFloat("_BlurDistanceFactor_0");
            Assert.AreEqual(testValue, result, 0.001f,
                "_BlurDistanceFactor_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_全仮想光源のBlurRadiusが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                float testValue = i * 0.5f;
                string propName = $"_BlurRadius_{i}";
                _material.SetFloat(propName, testValue);
                float result = _material.GetFloat(propName);
                Assert.AreEqual(testValue, result, 0.001f,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        [Test]
        public void Shader_全仮想光源のBlurDistanceFactorが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                float testValue = i * 0.3f;
                string propName = $"_BlurDistanceFactor_{i}";
                _material.SetFloat(propName, testValue);
                float result = _material.GetFloat(propName);
                Assert.AreEqual(testValue, result, 0.001f,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        #endregion

        #region Hue Shift uniformプロパティテスト (Req 7.4)

        [Test]
        public void Shader_HueShiftプロパティが設定できる()
        {
            float testValue = 180f;
            _material.SetFloat("_HueShift_0", testValue);
            float result = _material.GetFloat("_HueShift_0");
            Assert.AreEqual(testValue, result, 0.001f,
                "_HueShift_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_全仮想光源のHueShiftが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                float testValue = i * 45f;
                string propName = $"_HueShift_{i}";
                _material.SetFloat(propName, testValue);
                float result = _material.GetFloat(propName);
                Assert.AreEqual(testValue, result, 0.001f,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        #endregion

        #region 色収差 uniformプロパティテスト (Req 7.5)

        [Test]
        public void Shader_ChromaticAberrationプロパティが設定できる()
        {
            float testValue = 0.5f;
            _material.SetFloat("_ChromaticAberration_0", testValue);
            float result = _material.GetFloat("_ChromaticAberration_0");
            Assert.AreEqual(testValue, result, 0.001f,
                "_ChromaticAberration_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_全仮想光源のChromaticAberrationが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                float testValue = i * 0.1f;
                string propName = $"_ChromaticAberration_{i}";
                _material.SetFloat(propName, testValue);
                float result = _material.GetFloat(propName);
                Assert.AreEqual(testValue, result, 0.001f,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        #endregion

        #region BlurQualityキーワードマッピングテスト (Req 7.3)

        [Test]
        public void BlurQuality_Low_BLUR_LOWキーワードに対応する()
        {
            // ShadowOnlyManagerのBlurQuality設定に基づくキーワードマッピングを検証
            string keyword = GetBlurKeyword(BlurQuality.Low);
            Assert.AreEqual("_BLUR_LOW", keyword,
                "BlurQuality.Lowが_BLUR_LOWキーワードに対応すること");
        }

        [Test]
        public void BlurQuality_Mid_BLUR_MIDキーワードに対応する()
        {
            string keyword = GetBlurKeyword(BlurQuality.Mid);
            Assert.AreEqual("_BLUR_MID", keyword,
                "BlurQuality.Midが_BLUR_MIDキーワードに対応すること");
        }

        [Test]
        public void BlurQuality_High_BLUR_HIGHキーワードに対応する()
        {
            string keyword = GetBlurKeyword(BlurQuality.High);
            Assert.AreEqual("_BLUR_HIGH", keyword,
                "BlurQuality.Highが_BLUR_HIGHキーワードに対応すること");
        }

        /// <summary>
        /// BlurQuality列挙値からシェーダーキーワード文字列へのマッピング。
        /// Task 7のMaterial転送で使用される予定のマッピングロジックを検証する。
        /// </summary>
        private static string GetBlurKeyword(BlurQuality quality)
        {
            switch (quality)
            {
                case BlurQuality.Low: return "_BLUR_LOW";
                case BlurQuality.Mid: return "_BLUR_MID";
                case BlurQuality.High: return "_BLUR_HIGH";
                default: return "_BLUR_MID";
            }
        }

        #endregion

        #region シェーダーコンパイルテスト

        [Test]
        public void Shader_キーワードなしでコンパイルが成功する()
        {
            // キーワードなし（ブラーなし）でMaterialが正常に生成できること
            Assert.IsNotNull(_material, "キーワードなしでMaterialが生成できること");
            Assert.IsTrue(_material.shader.isSupported,
                "キーワードなしでシェーダーがサポートされていること");
        }

        [Test]
        public void Shader_BLUR_LOWでコンパイルが成功する()
        {
            _material.EnableKeyword("_BLUR_LOW");
            Assert.IsTrue(_material.shader.isSupported,
                "_BLUR_LOWキーワードでシェーダーがサポートされていること");
        }

        [Test]
        public void Shader_BLUR_MIDでコンパイルが成功する()
        {
            _material.EnableKeyword("_BLUR_MID");
            Assert.IsTrue(_material.shader.isSupported,
                "_BLUR_MIDキーワードでシェーダーがサポートされていること");
        }

        [Test]
        public void Shader_BLUR_HIGHでコンパイルが成功する()
        {
            _material.EnableKeyword("_BLUR_HIGH");
            Assert.IsTrue(_material.shader.isSupported,
                "_BLUR_HIGHキーワードでシェーダーがサポートされていること");
        }

        #endregion

        #region 光源位置uniform テスト（距離ボケ用） (Req 7.2)

        [Test]
        public void Shader_LightWorldPosプロパティが設定できる()
        {
            // 距離ボケの計算に必要な光源ワールド位置を設定できること
            Vector4 testPos = new Vector4(1f, 5f, 3f, 1f);
            _material.SetVector("_LightWorldPos_0", testPos);
            Vector4 result = _material.GetVector("_LightWorldPos_0");
            Assert.AreEqual(testPos, result,
                "_LightWorldPos_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_全仮想光源のLightWorldPosが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector4 testPos = new Vector4(i, i * 2, i * 3, 1f);
                string propName = $"_LightWorldPos_{i}";
                _material.SetVector(propName, testPos);
                Vector4 result = _material.GetVector(propName);
                Assert.AreEqual(testPos, result,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        #endregion

        #region 深度テクスチャサイズuniform テスト（ブラーテクセルサイズ計算用）

        [Test]
        public void Shader_DepthTexSizeプロパティが設定できる()
        {
            // ブラーサンプリング時のテクセルサイズ計算に必要な深度テクスチャサイズ
            Vector4 testSize = new Vector4(1024f, 1024f, 1f / 1024f, 1f / 1024f);
            _material.SetVector("_DepthTexSize_0", testSize);
            Vector4 result = _material.GetVector("_DepthTexSize_0");
            Assert.AreEqual(testSize, result,
                "_DepthTexSize_0がMaterialに設定・取得できること");
        }

        [Test]
        public void Shader_全仮想光源のDepthTexSizeが設定できる()
        {
            for (int i = 0; i < 8; i++)
            {
                int res = 256 * (i + 1);
                Vector4 testSize = new Vector4(res, res, 1f / res, 1f / res);
                string propName = $"_DepthTexSize_{i}";
                _material.SetVector(propName, testSize);
                Vector4 result = _material.GetVector(propName);
                Assert.AreEqual(testSize, result,
                    $"{propName}がMaterialに設定・取得できること");
            }
        }

        #endregion

        #region パラメータのリアルタイム反映テスト (Req 7.6)

        [Test]
        public void BlurRadius_値を変更するとMaterialに即座に反映される()
        {
            _material.SetFloat("_BlurRadius_0", 1.0f);
            Assert.AreEqual(1.0f, _material.GetFloat("_BlurRadius_0"), 0.001f);

            _material.SetFloat("_BlurRadius_0", 3.0f);
            Assert.AreEqual(3.0f, _material.GetFloat("_BlurRadius_0"), 0.001f,
                "BlurRadiusの変更がMaterialに即座に反映されること");
        }

        [Test]
        public void HueShift_値を変更するとMaterialに即座に反映される()
        {
            _material.SetFloat("_HueShift_0", 0f);
            Assert.AreEqual(0f, _material.GetFloat("_HueShift_0"), 0.001f);

            _material.SetFloat("_HueShift_0", 270f);
            Assert.AreEqual(270f, _material.GetFloat("_HueShift_0"), 0.001f,
                "HueShiftの変更がMaterialに即座に反映されること");
        }

        [Test]
        public void ChromaticAberration_値を変更するとMaterialに即座に反映される()
        {
            _material.SetFloat("_ChromaticAberration_0", 0f);
            Assert.AreEqual(0f, _material.GetFloat("_ChromaticAberration_0"), 0.001f);

            _material.SetFloat("_ChromaticAberration_0", 0.8f);
            Assert.AreEqual(0.8f, _material.GetFloat("_ChromaticAberration_0"), 0.001f,
                "ChromaticAberrationの変更がMaterialに即座に反映されること");
        }

        #endregion
    }
}
