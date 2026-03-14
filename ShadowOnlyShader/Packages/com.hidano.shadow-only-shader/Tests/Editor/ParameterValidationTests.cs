using NUnit.Framework;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Editor
{
    /// <summary>
    /// パラメータバリデーション（クランプ、範囲チェック）を検証するユニットテスト。
    /// Requirements: 3.5, 6.1, 6.2
    /// </summary>
    [TestFixture]
    public class ParameterValidationTests
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
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        #region TextureResolution バリデーション

        [Test]
        public void TextureResolution_64未満の値_64にクランプされる()
        {
            _virtualLight.TextureResolution = 10;
            Assert.AreEqual(64, _virtualLight.TextureResolution,
                "TextureResolutionは64未満の場合64にクランプされるべき");
        }

        [Test]
        public void TextureResolution_8192超の値_8192にクランプされる()
        {
            _virtualLight.TextureResolution = 16384;
            Assert.AreEqual(8192, _virtualLight.TextureResolution,
                "TextureResolutionは8192を超える場合8192にクランプされるべき");
        }

        [Test]
        public void TextureResolution_範囲内の値_そのまま設定される()
        {
            _virtualLight.TextureResolution = 512;
            Assert.AreEqual(512, _virtualLight.TextureResolution,
                "範囲内のTextureResolutionはそのまま設定されるべき");
        }

        [Test]
        public void TextureResolution_下限境界値64_正しく設定される()
        {
            _virtualLight.TextureResolution = 64;
            Assert.AreEqual(64, _virtualLight.TextureResolution);
        }

        [Test]
        public void TextureResolution_上限境界値8192_正しく設定される()
        {
            _virtualLight.TextureResolution = 8192;
            Assert.AreEqual(8192, _virtualLight.TextureResolution);
        }

        #endregion

        #region FieldOfView バリデーション

        [Test]
        public void FieldOfView_1未満の値_1にクランプされる()
        {
            _virtualLight.FieldOfView = 0f;
            Assert.AreEqual(1f, _virtualLight.FieldOfView, 0.001f,
                "FOVは1度未満の場合1度にクランプされるべき");
        }

        [Test]
        public void FieldOfView_179超の値_179にクランプされる()
        {
            _virtualLight.FieldOfView = 200f;
            Assert.AreEqual(179f, _virtualLight.FieldOfView, 0.001f,
                "FOVは179度を超える場合179度にクランプされるべき");
        }

        [Test]
        public void FieldOfView_負の値_1にクランプされる()
        {
            _virtualLight.FieldOfView = -10f;
            Assert.AreEqual(1f, _virtualLight.FieldOfView, 0.001f,
                "負のFOVは1度にクランプされるべき");
        }

        [Test]
        public void FieldOfView_範囲内の値_そのまま設定される()
        {
            _virtualLight.FieldOfView = 60f;
            Assert.AreEqual(60f, _virtualLight.FieldOfView, 0.001f);
        }

        #endregion

        #region OrthographicSize バリデーション

        [Test]
        public void OrthographicSize_ゼロ_最小値にクランプされる()
        {
            _virtualLight.OrthographicSize = 0f;
            Assert.GreaterOrEqual(_virtualLight.OrthographicSize, 0.001f,
                "OrthographicSizeは0.001以上にクランプされるべき");
        }

        [Test]
        public void OrthographicSize_負の値_最小値にクランプされる()
        {
            _virtualLight.OrthographicSize = -5f;
            Assert.GreaterOrEqual(_virtualLight.OrthographicSize, 0.001f,
                "負のOrthographicSizeは0.001以上にクランプされるべき");
        }

        [Test]
        public void OrthographicSize_正の値_そのまま設定される()
        {
            _virtualLight.OrthographicSize = 10f;
            Assert.AreEqual(10f, _virtualLight.OrthographicSize, 0.001f);
        }

        #endregion

        #region NearClipPlane バリデーション

        [Test]
        public void NearClipPlane_ゼロ_最小値にクランプされる()
        {
            _virtualLight.NearClipPlane = 0f;
            Assert.GreaterOrEqual(_virtualLight.NearClipPlane, 0.001f,
                "NearClipPlaneは0.001以上にクランプされるべき");
        }

        [Test]
        public void NearClipPlane_負の値_最小値にクランプされる()
        {
            _virtualLight.NearClipPlane = -1f;
            Assert.GreaterOrEqual(_virtualLight.NearClipPlane, 0.001f,
                "負のNearClipPlaneは0.001以上にクランプされるべき");
        }

        [Test]
        public void NearClipPlane_正の値_そのまま設定される()
        {
            _virtualLight.NearClipPlane = 0.5f;
            Assert.AreEqual(0.5f, _virtualLight.NearClipPlane, 0.001f);
        }

        #endregion

        #region FarClipPlane バリデーション

        [Test]
        public void FarClipPlane_Near以下の値_Near超に強制される()
        {
            _virtualLight.NearClipPlane = 1f;
            _virtualLight.FarClipPlane = 0.5f;
            Assert.Greater(_virtualLight.FarClipPlane, _virtualLight.NearClipPlane,
                "FarClipPlaneはNearClipPlaneより大きくなるべき");
        }

        [Test]
        public void FarClipPlane_Nearと同値_Near超に強制される()
        {
            _virtualLight.NearClipPlane = 1f;
            _virtualLight.FarClipPlane = 1f;
            Assert.Greater(_virtualLight.FarClipPlane, _virtualLight.NearClipPlane,
                "FarClipPlaneはNearClipPlaneと同値にはならないべき");
        }

        #endregion

        #region ShadowAlpha バリデーション

        [Test]
        public void ShadowAlpha_0未満_0にクランプされる()
        {
            _virtualLight.ShadowAlpha = -0.5f;
            Assert.AreEqual(0f, _virtualLight.ShadowAlpha, 0.001f,
                "ShadowAlphaは0にクランプされるべき");
        }

        [Test]
        public void ShadowAlpha_1超_1にクランプされる()
        {
            _virtualLight.ShadowAlpha = 1.5f;
            Assert.AreEqual(1f, _virtualLight.ShadowAlpha, 0.001f,
                "ShadowAlphaは1にクランプされるべき");
        }

        [Test]
        public void ShadowAlpha_範囲内の値_そのまま設定される()
        {
            _virtualLight.ShadowAlpha = 0.7f;
            Assert.AreEqual(0.7f, _virtualLight.ShadowAlpha, 0.001f);
        }

        #endregion

        #region BlurRadius バリデーション

        [Test]
        public void BlurRadius_負の値_0にクランプされる()
        {
            _virtualLight.BlurRadius = -1f;
            Assert.AreEqual(0f, _virtualLight.BlurRadius, 0.001f,
                "BlurRadiusは0にクランプされるべき");
        }

        [Test]
        public void BlurRadius_正の値_そのまま設定される()
        {
            _virtualLight.BlurRadius = 2.5f;
            Assert.AreEqual(2.5f, _virtualLight.BlurRadius, 0.001f);
        }

        #endregion

        #region BlurDistanceFactor バリデーション

        [Test]
        public void BlurDistanceFactor_負の値_0にクランプされる()
        {
            _virtualLight.BlurDistanceFactor = -1f;
            Assert.AreEqual(0f, _virtualLight.BlurDistanceFactor, 0.001f,
                "BlurDistanceFactorは0にクランプされるべき");
        }

        [Test]
        public void BlurDistanceFactor_正の値_そのまま設定される()
        {
            _virtualLight.BlurDistanceFactor = 1.5f;
            Assert.AreEqual(1.5f, _virtualLight.BlurDistanceFactor, 0.001f);
        }

        #endregion

        #region HueShift バリデーション

        [Test]
        public void HueShift_0未満_0にクランプされる()
        {
            _virtualLight.HueShift = -10f;
            Assert.AreEqual(0f, _virtualLight.HueShift, 0.001f,
                "HueShiftは0にクランプされるべき");
        }

        [Test]
        public void HueShift_360超_360にクランプされる()
        {
            _virtualLight.HueShift = 400f;
            Assert.AreEqual(360f, _virtualLight.HueShift, 0.001f,
                "HueShiftは360にクランプされるべき");
        }

        [Test]
        public void HueShift_範囲内の値_そのまま設定される()
        {
            _virtualLight.HueShift = 180f;
            Assert.AreEqual(180f, _virtualLight.HueShift, 0.001f);
        }

        #endregion

        #region ChromaticAberration バリデーション

        [Test]
        public void ChromaticAberration_負の値_0にクランプされる()
        {
            _virtualLight.ChromaticAberration = -0.5f;
            Assert.AreEqual(0f, _virtualLight.ChromaticAberration, 0.001f,
                "ChromaticAberrationは0にクランプされるべき");
        }

        [Test]
        public void ChromaticAberration_正の値_そのまま設定される()
        {
            _virtualLight.ChromaticAberration = 0.3f;
            Assert.AreEqual(0.3f, _virtualLight.ChromaticAberration, 0.001f);
        }

        #endregion

        #region ShadowOnlyManager バリデーション

        [Test]
        public void BlendMultiplier_負の値_0にクランプされる()
        {
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            manager.BlendMultiplier = -1f;
            Assert.AreEqual(0f, manager.BlendMultiplier, 0.001f,
                "BlendMultiplierは0にクランプされるべき");

            Object.DestroyImmediate(managerGO);
        }

        [Test]
        public void BlendMultiplier_正の値_そのまま設定される()
        {
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            manager.BlendMultiplier = 2.5f;
            Assert.AreEqual(2.5f, manager.BlendMultiplier, 0.001f);

            Object.DestroyImmediate(managerGO);
        }

        [Test]
        public void BlurQuality_各プリセット値が設定可能()
        {
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            manager.BlurQuality = BlurQuality.Low;
            Assert.AreEqual(BlurQuality.Low, manager.BlurQuality);

            manager.BlurQuality = BlurQuality.Mid;
            Assert.AreEqual(BlurQuality.Mid, manager.BlurQuality);

            manager.BlurQuality = BlurQuality.High;
            Assert.AreEqual(BlurQuality.High, manager.BlurQuality);

            Object.DestroyImmediate(managerGO);
        }

        #endregion

        #region DepthBias/NormalBias（範囲制限なし）

        [Test]
        public void DepthBias_任意の値が設定可能()
        {
            _virtualLight.DepthBias = -0.01f;
            Assert.AreEqual(-0.01f, _virtualLight.DepthBias, 0.001f);

            _virtualLight.DepthBias = 0.1f;
            Assert.AreEqual(0.1f, _virtualLight.DepthBias, 0.001f);
        }

        [Test]
        public void NormalBias_任意の値が設定可能()
        {
            _virtualLight.NormalBias = -1f;
            Assert.AreEqual(-1f, _virtualLight.NormalBias, 0.001f);

            _virtualLight.NormalBias = 2f;
            Assert.AreEqual(2f, _virtualLight.NormalBias, 0.001f);
        }

        #endregion
    }
}
