using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// VirtualLightの影の外観パラメータの検証テスト (Task 2.2)
    /// 影の色、濃さ（アルファ）、ブラー半径、距離ボケ係数、Hue Shift、色収差強度、深度バイアスの
    /// 各パラメータが正しく定義・バリデーションされることを検証する。
    /// </summary>
    public class VirtualLightAppearanceTests
    {
        private GameObject _gameObject;
        private VirtualLight _virtualLight;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestVirtualLight");
            _virtualLight = _gameObject.AddComponent<VirtualLight>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        #region デフォルト値テスト

        [Test]
        public void ShadowColor_デフォルト値は黒()
        {
            Assert.AreEqual(Color.black, _virtualLight.ShadowColor);
        }

        [Test]
        public void ShadowAlpha_デフォルト値は05()
        {
            Assert.AreEqual(0.5f, _virtualLight.ShadowAlpha, 0.001f);
        }

        [Test]
        public void BlurRadius_デフォルト値は1()
        {
            Assert.AreEqual(1f, _virtualLight.BlurRadius, 0.001f);
        }

        [Test]
        public void BlurDistanceFactor_デフォルト値は0()
        {
            Assert.AreEqual(0f, _virtualLight.BlurDistanceFactor, 0.001f);
        }

        [Test]
        public void HueShift_デフォルト値は0()
        {
            Assert.AreEqual(0f, _virtualLight.HueShift, 0.001f);
        }

        [Test]
        public void ChromaticAberration_デフォルト値は0()
        {
            Assert.AreEqual(0f, _virtualLight.ChromaticAberration, 0.001f);
        }

        [Test]
        public void DepthBias_デフォルト値は0005()
        {
            Assert.AreEqual(0.005f, _virtualLight.DepthBias, 0.0001f);
        }

        [Test]
        public void NormalBias_デフォルト値は0()
        {
            Assert.AreEqual(0f, _virtualLight.NormalBias, 0.001f);
        }

        #endregion

        #region ShadowColor テスト

        [Test]
        public void ShadowColor_任意の色を設定できる()
        {
            Color expected = new Color(0.5f, 0.3f, 0.8f, 1f);
            _virtualLight.ShadowColor = expected;
            Assert.AreEqual(expected, _virtualLight.ShadowColor);
        }

        [Test]
        public void ShadowColor_赤色を設定できる()
        {
            _virtualLight.ShadowColor = Color.red;
            Assert.AreEqual(Color.red, _virtualLight.ShadowColor);
        }

        [Test]
        public void ShadowColor_透明色を設定できる()
        {
            Color transparent = new Color(0f, 0f, 0f, 0f);
            _virtualLight.ShadowColor = transparent;
            Assert.AreEqual(transparent, _virtualLight.ShadowColor);
        }

        #endregion

        #region ShadowAlpha バリデーションテスト

        [Test]
        public void ShadowAlpha_有効値を設定できる()
        {
            _virtualLight.ShadowAlpha = 0.75f;
            Assert.AreEqual(0.75f, _virtualLight.ShadowAlpha, 0.001f);
        }

        [Test]
        public void ShadowAlpha_0を設定できる()
        {
            _virtualLight.ShadowAlpha = 0f;
            Assert.AreEqual(0f, _virtualLight.ShadowAlpha, 0.001f);
        }

        [Test]
        public void ShadowAlpha_1を設定できる()
        {
            _virtualLight.ShadowAlpha = 1f;
            Assert.AreEqual(1f, _virtualLight.ShadowAlpha, 0.001f);
        }

        [Test]
        public void ShadowAlpha_負の値は0にクランプされる()
        {
            _virtualLight.ShadowAlpha = -0.5f;
            Assert.AreEqual(0f, _virtualLight.ShadowAlpha, 0.001f);
        }

        [Test]
        public void ShadowAlpha_1より大きい値は1にクランプされる()
        {
            _virtualLight.ShadowAlpha = 1.5f;
            Assert.AreEqual(1f, _virtualLight.ShadowAlpha, 0.001f);
        }

        #endregion

        #region BlurRadius バリデーションテスト

        [Test]
        public void BlurRadius_有効値を設定できる()
        {
            _virtualLight.BlurRadius = 5f;
            Assert.AreEqual(5f, _virtualLight.BlurRadius, 0.001f);
        }

        [Test]
        public void BlurRadius_0を設定できる()
        {
            _virtualLight.BlurRadius = 0f;
            Assert.AreEqual(0f, _virtualLight.BlurRadius, 0.001f);
        }

        [Test]
        public void BlurRadius_負の値は0にクランプされる()
        {
            _virtualLight.BlurRadius = -3f;
            Assert.AreEqual(0f, _virtualLight.BlurRadius, 0.001f);
        }

        [Test]
        public void BlurRadius_大きな値を設定できる()
        {
            _virtualLight.BlurRadius = 100f;
            Assert.AreEqual(100f, _virtualLight.BlurRadius, 0.001f);
        }

        #endregion

        #region BlurDistanceFactor バリデーションテスト

        [Test]
        public void BlurDistanceFactor_有効値を設定できる()
        {
            _virtualLight.BlurDistanceFactor = 2.5f;
            Assert.AreEqual(2.5f, _virtualLight.BlurDistanceFactor, 0.001f);
        }

        [Test]
        public void BlurDistanceFactor_0を設定できる()
        {
            _virtualLight.BlurDistanceFactor = 0f;
            Assert.AreEqual(0f, _virtualLight.BlurDistanceFactor, 0.001f);
        }

        [Test]
        public void BlurDistanceFactor_負の値は0にクランプされる()
        {
            _virtualLight.BlurDistanceFactor = -1f;
            Assert.AreEqual(0f, _virtualLight.BlurDistanceFactor, 0.001f);
        }

        #endregion

        #region HueShift バリデーションテスト

        [Test]
        public void HueShift_有効値を設定できる()
        {
            _virtualLight.HueShift = 180f;
            Assert.AreEqual(180f, _virtualLight.HueShift, 0.001f);
        }

        [Test]
        public void HueShift_0を設定できる()
        {
            _virtualLight.HueShift = 0f;
            Assert.AreEqual(0f, _virtualLight.HueShift, 0.001f);
        }

        [Test]
        public void HueShift_360を設定できる()
        {
            _virtualLight.HueShift = 360f;
            Assert.AreEqual(360f, _virtualLight.HueShift, 0.001f);
        }

        [Test]
        public void HueShift_負の値は0にクランプされる()
        {
            _virtualLight.HueShift = -30f;
            Assert.AreEqual(0f, _virtualLight.HueShift, 0.001f);
        }

        [Test]
        public void HueShift_360を超える値は360にクランプされる()
        {
            _virtualLight.HueShift = 400f;
            Assert.AreEqual(360f, _virtualLight.HueShift, 0.001f);
        }

        #endregion

        #region ChromaticAberration バリデーションテスト

        [Test]
        public void ChromaticAberration_有効値を設定できる()
        {
            _virtualLight.ChromaticAberration = 0.5f;
            Assert.AreEqual(0.5f, _virtualLight.ChromaticAberration, 0.001f);
        }

        [Test]
        public void ChromaticAberration_0を設定できる()
        {
            _virtualLight.ChromaticAberration = 0f;
            Assert.AreEqual(0f, _virtualLight.ChromaticAberration, 0.001f);
        }

        [Test]
        public void ChromaticAberration_負の値は0にクランプされる()
        {
            _virtualLight.ChromaticAberration = -0.3f;
            Assert.AreEqual(0f, _virtualLight.ChromaticAberration, 0.001f);
        }

        [Test]
        public void ChromaticAberration_大きな値を設定できる()
        {
            _virtualLight.ChromaticAberration = 10f;
            Assert.AreEqual(10f, _virtualLight.ChromaticAberration, 0.001f);
        }

        #endregion

        #region DepthBias テスト

        [Test]
        public void DepthBias_有効値を設定できる()
        {
            _virtualLight.DepthBias = 0.01f;
            Assert.AreEqual(0.01f, _virtualLight.DepthBias, 0.0001f);
        }

        [Test]
        public void DepthBias_0を設定できる()
        {
            _virtualLight.DepthBias = 0f;
            Assert.AreEqual(0f, _virtualLight.DepthBias, 0.0001f);
        }

        [Test]
        public void DepthBias_負の値も設定できる()
        {
            // 深度バイアスは負の値も許容する（特殊な用途向け）
            _virtualLight.DepthBias = -0.01f;
            Assert.AreEqual(-0.01f, _virtualLight.DepthBias, 0.0001f);
        }

        #endregion

        #region NormalBias テスト

        [Test]
        public void NormalBias_有効値を設定できる()
        {
            _virtualLight.NormalBias = 0.02f;
            Assert.AreEqual(0.02f, _virtualLight.NormalBias, 0.0001f);
        }

        [Test]
        public void NormalBias_0を設定できる()
        {
            _virtualLight.NormalBias = 0f;
            Assert.AreEqual(0f, _virtualLight.NormalBias, 0.0001f);
        }

        [Test]
        public void NormalBias_負の値も設定できる()
        {
            // 法線バイアスは負の値も許容する
            _virtualLight.NormalBias = -0.05f;
            Assert.AreEqual(-0.05f, _virtualLight.NormalBias, 0.0001f);
        }

        #endregion

        #region IVirtualLightインターフェース経由のアクセステスト

        [Test]
        public void IVirtualLight_ShadowColor_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.ShadowColor = Color.blue;
            Assert.AreEqual(Color.blue, ivl.ShadowColor);
        }

        [Test]
        public void IVirtualLight_ShadowAlpha_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.ShadowAlpha = 0.8f;
            Assert.AreEqual(0.8f, ivl.ShadowAlpha, 0.001f);
        }

        [Test]
        public void IVirtualLight_BlurRadius_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.BlurRadius = 3f;
            Assert.AreEqual(3f, ivl.BlurRadius, 0.001f);
        }

        [Test]
        public void IVirtualLight_BlurDistanceFactor_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.BlurDistanceFactor = 1.5f;
            Assert.AreEqual(1.5f, ivl.BlurDistanceFactor, 0.001f);
        }

        [Test]
        public void IVirtualLight_HueShift_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.HueShift = 120f;
            Assert.AreEqual(120f, ivl.HueShift, 0.001f);
        }

        [Test]
        public void IVirtualLight_ChromaticAberration_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.ChromaticAberration = 0.7f;
            Assert.AreEqual(0.7f, ivl.ChromaticAberration, 0.001f);
        }

        [Test]
        public void IVirtualLight_DepthBias_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.DepthBias = 0.003f;
            Assert.AreEqual(0.003f, ivl.DepthBias, 0.0001f);
        }

        [Test]
        public void IVirtualLight_NormalBias_インターフェース経由で取得設定できる()
        {
            IVirtualLight ivl = _virtualLight;
            ivl.NormalBias = 0.01f;
            Assert.AreEqual(0.01f, ivl.NormalBias, 0.0001f);
        }

        #endregion

        #region 複数パラメータ同時設定テスト

        [Test]
        public void 全外観パラメータを同時に設定して正しく保持される()
        {
            Color expectedColor = new Color(0.2f, 0.4f, 0.6f, 1f);
            _virtualLight.ShadowColor = expectedColor;
            _virtualLight.ShadowAlpha = 0.9f;
            _virtualLight.BlurRadius = 3.5f;
            _virtualLight.BlurDistanceFactor = 1.2f;
            _virtualLight.HueShift = 45f;
            _virtualLight.ChromaticAberration = 0.3f;
            _virtualLight.DepthBias = 0.008f;
            _virtualLight.NormalBias = 0.02f;

            Assert.AreEqual(expectedColor, _virtualLight.ShadowColor, "ShadowColor");
            Assert.AreEqual(0.9f, _virtualLight.ShadowAlpha, 0.001f, "ShadowAlpha");
            Assert.AreEqual(3.5f, _virtualLight.BlurRadius, 0.001f, "BlurRadius");
            Assert.AreEqual(1.2f, _virtualLight.BlurDistanceFactor, 0.001f, "BlurDistanceFactor");
            Assert.AreEqual(45f, _virtualLight.HueShift, 0.001f, "HueShift");
            Assert.AreEqual(0.3f, _virtualLight.ChromaticAberration, 0.001f, "ChromaticAberration");
            Assert.AreEqual(0.008f, _virtualLight.DepthBias, 0.0001f, "DepthBias");
            Assert.AreEqual(0.02f, _virtualLight.NormalBias, 0.0001f, "NormalBias");
        }

        #endregion

        #region パラメータ変更が投影行列に影響しないことの検証

        [Test]
        public void 外観パラメータ変更はVP行列に影響しない()
        {
            _virtualLight.UpdateMatrices();
            Matrix4x4 vpBefore = _virtualLight.ViewProjectionMatrix;

            // 全外観パラメータを変更
            _virtualLight.ShadowColor = Color.red;
            _virtualLight.ShadowAlpha = 1f;
            _virtualLight.BlurRadius = 10f;
            _virtualLight.BlurDistanceFactor = 5f;
            _virtualLight.HueShift = 270f;
            _virtualLight.ChromaticAberration = 2f;
            _virtualLight.DepthBias = 0.1f;
            _virtualLight.NormalBias = 0.05f;

            _virtualLight.UpdateMatrices();
            Matrix4x4 vpAfter = _virtualLight.ViewProjectionMatrix;

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(vpBefore[i], vpAfter[i], 0.001f,
                    $"VP行列の要素[{i}]が外観パラメータ変更により変化してはならない");
            }
        }

        #endregion

        #region SerializeField属性の検証（リフレクション経由）

        [Test]
        public void ShadowColor_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_shadowColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_shadowColorフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_shadowColorにSerializeField属性が付与されていること");
        }

        [Test]
        public void ShadowAlpha_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_shadowAlpha",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_shadowAlphaフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_shadowAlphaにSerializeField属性が付与されていること");
        }

        [Test]
        public void BlurRadius_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_blurRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_blurRadiusフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_blurRadiusにSerializeField属性が付与されていること");
        }

        [Test]
        public void BlurDistanceFactor_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_blurDistanceFactor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_blurDistanceFactorフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_blurDistanceFactorにSerializeField属性が付与されていること");
        }

        [Test]
        public void HueShift_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_hueShift",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_hueShiftフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_hueShiftにSerializeField属性が付与されていること");
        }

        [Test]
        public void ChromaticAberration_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_chromaticAberration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_chromaticAberrationフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_chromaticAberrationにSerializeField属性が付与されていること");
        }

        [Test]
        public void DepthBias_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_depthBias",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_depthBiasフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_depthBiasにSerializeField属性が付与されていること");
        }

        [Test]
        public void NormalBias_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_normalBias",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_normalBiasフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_normalBiasにSerializeField属性が付与されていること");
        }

        #endregion

        #region Header属性の検証（Inspectorグループ化）

        [Test]
        public void ShadowColor_はShadowAppearanceヘッダーグループに属する()
        {
            var field = typeof(VirtualLight).GetField("_shadowColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field);
            var headerAttr = (HeaderAttribute)System.Attribute.GetCustomAttribute(field, typeof(HeaderAttribute));
            // ShadowColorはグループの先頭フィールドなのでHeader属性が付与されている
            Assert.IsNotNull(headerAttr, "_shadowColorにHeader属性が付与されていること");
        }

        [Test]
        public void DepthBias_はDepthBiasヘッダーグループに属する()
        {
            var field = typeof(VirtualLight).GetField("_depthBias",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field);
            var headerAttr = (HeaderAttribute)System.Attribute.GetCustomAttribute(field, typeof(HeaderAttribute));
            Assert.IsNotNull(headerAttr, "_depthBiasにHeader属性が付与されていること");
        }

        #endregion
    }
}
