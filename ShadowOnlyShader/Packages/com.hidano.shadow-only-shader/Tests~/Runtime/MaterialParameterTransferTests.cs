using System.Collections.Generic;
using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// ManagerからMaterialへのパラメータ転送の検証テスト (Task 7)
    /// ShadowOnlyManagerが毎フレーム各VirtualLightのパラメータを
    /// 床面MaterialのuniformにMaterial.SetXxxで設定する機能を検証する。
    /// Requirements: 2.3, 2.4, 3.4, 3.7, 4.1, 4.5, 7.6
    /// </summary>
    public class MaterialParameterTransferTests
    {
        private GameObject _managerObject;
        private ShadowOnlyManager _manager;
        private GameObject _floorObject;
        private MeshRenderer _floorRenderer;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("TestShadowOnlyManager");
            _manager = _managerObject.AddComponent<ShadowOnlyManager>();

            _floorObject = new GameObject("TestFloor");
            _floorObject.AddComponent<MeshFilter>();
            _floorRenderer = _floorObject.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_floorObject != null)
                Object.DestroyImmediate(_floorObject);

            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);

            // テストで作成された余分なオブジェクトのクリーンアップ
            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                Object.DestroyImmediate(m.gameObject);
            }
        }

        #region UpdateMaterialProperties メソッドの存在テスト

        [Test]
        public void UpdateMaterialProperties_メソッドが公開されている()
        {
            var method = typeof(ShadowOnlyManager).GetMethod("UpdateMaterialProperties");
            Assert.IsNotNull(method, "UpdateMaterialPropertiesメソッドが公開されていること");
        }

        #endregion

        #region VirtualLightCountの設定テスト

        [Test]
        public void UpdateMaterialProperties_VirtualLightCountが0のときMaterialに0を設定する()
        {
            Material mat = _manager.FloorMaterial;
            Assert.IsNotNull(mat);

            _manager.UpdateMaterialProperties();

            Assert.AreEqual(0, mat.GetInt("_VirtualLightCount"),
                "_VirtualLightCountが0に設定されること");
        }

        [Test]
        public void UpdateMaterialProperties_VirtualLight1つのときMaterialに1を設定する()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.AreEqual(1, mat.GetInt("_VirtualLightCount"),
                "_VirtualLightCountが1に設定されること");
        }

        [Test]
        public void UpdateMaterialProperties_VirtualLight3つのときMaterialに3を設定する()
        {
            _manager.AddVirtualLight();
            _manager.AddVirtualLight();
            _manager.AddVirtualLight();
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.AreEqual(3, mat.GetInt("_VirtualLightCount"),
                "_VirtualLightCountが3に設定されること");
        }

        #endregion

        #region BlendMultiplierの設定テスト

        [Test]
        public void UpdateMaterialProperties_BlendMultiplierがMaterialに設定される()
        {
            _manager.BlendMultiplier = 2.5f;
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.AreEqual(2.5f, mat.GetFloat("_BlendMultiplier"), 0.001f,
                "_BlendMultiplierが正しく設定されること");
        }

        #endregion

        #region VP行列の転送テスト

        [Test]
        public void UpdateMaterialProperties_VP行列がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            // VP行列を更新
            vl.UpdateMatrices();

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Matrix4x4 matVP = mat.GetMatrix("_LightVPMatrix_0");
            Assert.AreEqual(vl.ViewProjectionMatrix, matVP,
                "_LightVPMatrix_0にVP行列が設定されること");
        }

        #endregion

        #region 影色パラメータの転送テスト

        [Test]
        public void UpdateMaterialProperties_影色がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.ShadowColor = Color.red;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Color setColor = mat.GetColor("_ShadowColor_0");
            Assert.AreEqual(Color.red.r, setColor.r, 0.001f,
                "_ShadowColor_0のR値が設定されること");
            Assert.AreEqual(Color.red.g, setColor.g, 0.001f,
                "_ShadowColor_0のG値が設定されること");
            Assert.AreEqual(Color.red.b, setColor.b, 0.001f,
                "_ShadowColor_0のB値が設定されること");
        }

        [Test]
        public void UpdateMaterialProperties_影の濃さがMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.ShadowAlpha = 0.7f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(0.7f, mat.GetFloat("_ShadowAlpha_0"), 0.001f,
                "_ShadowAlpha_0が正しく設定されること");
        }

        #endregion

        #region ブラー関連パラメータの転送テスト

        [Test]
        public void UpdateMaterialProperties_ブラー半径がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.BlurRadius = 3.0f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(3.0f, mat.GetFloat("_BlurRadius_0"), 0.001f,
                "_BlurRadius_0が正しく設定されること");
        }

        [Test]
        public void UpdateMaterialProperties_距離ボケ係数がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.BlurDistanceFactor = 0.5f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(0.5f, mat.GetFloat("_BlurDistanceFactor_0"), 0.001f,
                "_BlurDistanceFactor_0が正しく設定されること");
        }

        #endregion

        #region Hue Shift・色収差パラメータの転送テスト

        [Test]
        public void UpdateMaterialProperties_HueShiftがMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.HueShift = 180f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(180f, mat.GetFloat("_HueShift_0"), 0.001f,
                "_HueShift_0が正しく設定されること");
        }

        [Test]
        public void UpdateMaterialProperties_色収差がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.ChromaticAberration = 0.3f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(0.3f, mat.GetFloat("_ChromaticAberration_0"), 0.001f,
                "_ChromaticAberration_0が正しく設定されること");
        }

        #endregion

        #region コンタクトハードニング強度の転送テスト

        [Test]
        public void UpdateMaterialProperties_コンタクトハードニング強度がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.ContactHardeningStrength = 1.5f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(1.5f, mat.GetFloat("_ContactHardeningStrength_0"), 0.001f,
                "_ContactHardeningStrength_0が正しく設定されること");
        }

        #endregion

        #region 深度バイアスの転送テスト

        [Test]
        public void UpdateMaterialProperties_深度バイアスがMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.DepthBias = 0.01f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Assert.AreEqual(0.01f, mat.GetFloat("_DepthBias_0"), 0.001f,
                "_DepthBias_0が正しく設定されること");
        }

        #endregion

        #region 深度テクスチャの転送テスト

        [Test]
        public void UpdateMaterialProperties_深度テクスチャがMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.EnsureDepthTexture();

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Texture tex = mat.GetTexture("_ShadowDepthTex_0");
            Assert.AreEqual(vl.DepthRenderTexture, tex,
                "_ShadowDepthTex_0に深度テクスチャが設定されること");
        }

        #endregion

        #region 光源ワールド位置の転送テスト

        [Test]
        public void UpdateMaterialProperties_光源ワールド位置がMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            var vlComponent = vl as VirtualLight;
            vlComponent.transform.position = new Vector3(1, 2, 3);

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Vector4 lightPos = mat.GetVector("_LightWorldPos_0");
            Assert.AreEqual(1f, lightPos.x, 0.001f, "X座標が正しく設定されること");
            Assert.AreEqual(2f, lightPos.y, 0.001f, "Y座標が正しく設定されること");
            Assert.AreEqual(3f, lightPos.z, 0.001f, "Z座標が正しく設定されること");
        }

        #endregion

        #region テクスチャサイズの転送テスト

        [Test]
        public void UpdateMaterialProperties_テクスチャサイズがMaterialに設定される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            vl.TextureResolution = 512;
            vl.EnsureDepthTexture();

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            Vector4 texSize = mat.GetVector("_DepthTexSize_0");
            Assert.AreEqual(512f, texSize.x, 0.001f, "テクスチャ幅が正しく設定されること");
            Assert.AreEqual(512f, texSize.y, 0.001f, "テクスチャ高さが正しく設定されること");
            Assert.AreEqual(1f / 512f, texSize.z, 0.0001f, "1/テクスチャ幅が正しく設定されること");
            Assert.AreEqual(1f / 512f, texSize.w, 0.0001f, "1/テクスチャ高さが正しく設定されること");
        }

        #endregion

        #region 複数VirtualLightの転送テスト

        [Test]
        public void UpdateMaterialProperties_複数VirtualLightのパラメータが個別に設定される()
        {
            IVirtualLight vl0 = _manager.AddVirtualLight();
            IVirtualLight vl1 = _manager.AddVirtualLight();

            vl0.ShadowColor = Color.red;
            vl0.ShadowAlpha = 0.3f;
            vl1.ShadowColor = Color.blue;
            vl1.ShadowAlpha = 0.8f;

            Material mat = _manager.FloorMaterial;
            _manager.UpdateMaterialProperties();

            // 1つ目の光源
            Color color0 = mat.GetColor("_ShadowColor_0");
            Assert.AreEqual(Color.red.r, color0.r, 0.001f,
                "1つ目の光源の影色Rが正しく設定されること");
            Assert.AreEqual(0.3f, mat.GetFloat("_ShadowAlpha_0"), 0.001f,
                "1つ目の光源のアルファが正しく設定されること");

            // 2つ目の光源
            Color color1 = mat.GetColor("_ShadowColor_1");
            Assert.AreEqual(Color.blue.b, color1.b, 0.001f,
                "2つ目の光源の影色Bが正しく設定されること");
            Assert.AreEqual(0.8f, mat.GetFloat("_ShadowAlpha_1"), 0.001f,
                "2つ目の光源のアルファが正しく設定されること");
        }

        #endregion

        #region ブラー品質キーワードの切り替えテスト

        [Test]
        public void UpdateMaterialProperties_BlurQualityLowのときBLUR_LOWキーワードが有効になる()
        {
            _manager.BlurQuality = BlurQuality.Low;
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.IsTrue(mat.IsKeywordEnabled("_BLUR_LOW"),
                "_BLUR_LOWキーワードが有効であること");
            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_MID"),
                "_BLUR_MIDキーワードが無効であること");
            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_HIGH"),
                "_BLUR_HIGHキーワードが無効であること");
        }

        [Test]
        public void UpdateMaterialProperties_BlurQualityMidのときBLUR_MIDキーワードが有効になる()
        {
            _manager.BlurQuality = BlurQuality.Mid;
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_LOW"),
                "_BLUR_LOWキーワードが無効であること");
            Assert.IsTrue(mat.IsKeywordEnabled("_BLUR_MID"),
                "_BLUR_MIDキーワードが有効であること");
            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_HIGH"),
                "_BLUR_HIGHキーワードが無効であること");
        }

        [Test]
        public void UpdateMaterialProperties_BlurQualityHighのときBLUR_HIGHキーワードが有効になる()
        {
            _manager.BlurQuality = BlurQuality.High;
            Material mat = _manager.FloorMaterial;

            _manager.UpdateMaterialProperties();

            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_LOW"),
                "_BLUR_LOWキーワードが無効であること");
            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_MID"),
                "_BLUR_MIDキーワードが無効であること");
            Assert.IsTrue(mat.IsKeywordEnabled("_BLUR_HIGH"),
                "_BLUR_HIGHキーワードが有効であること");
        }

        #endregion

        #region 床面RendererへのMaterial自動割り当てテスト

        [Test]
        public void AssignFloorMaterial_床面RendererにFloorMaterialが割り当てられる()
        {
            _manager.AddFloorRenderer(_floorRenderer);

            _manager.AssignFloorMaterial();

            Assert.AreEqual(_manager.FloorMaterial, _floorRenderer.sharedMaterial,
                "床面RendererにFloorMaterialがsharedMaterialとして割り当てられること");
        }

        [Test]
        public void AssignFloorMaterial_複数床面Rendererに同じMaterialが割り当てられる()
        {
            var floor2 = new GameObject("Floor2");
            floor2.AddComponent<MeshFilter>();
            var renderer2 = floor2.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(_floorRenderer);
            _manager.AddFloorRenderer(renderer2);

            _manager.AssignFloorMaterial();

            Assert.AreEqual(_manager.FloorMaterial, _floorRenderer.sharedMaterial,
                "1つ目の床面RendererにFloorMaterialが割り当てられること");
            Assert.AreEqual(_manager.FloorMaterial, renderer2.sharedMaterial,
                "2つ目の床面RendererにFloorMaterialが割り当てられること");

            Object.DestroyImmediate(floor2);
        }

        [Test]
        public void AssignFloorMaterial_nullRendererをスキップしてエラーにならない()
        {
            _manager.AddFloorRenderer(_floorRenderer);
            // _floorRendererを破棄してnullにする
            Object.DestroyImmediate(_floorObject);
            _floorObject = null;

            Assert.DoesNotThrow(() => _manager.AssignFloorMaterial(),
                "nullのRendererがあってもエラーにならないこと");
        }

        [Test]
        public void AssignFloorMaterial_メソッドが公開されている()
        {
            var method = typeof(ShadowOnlyManager).GetMethod("AssignFloorMaterial");
            Assert.IsNotNull(method, "AssignFloorMaterialメソッドが公開されていること");
        }

        #endregion

        #region パラメータ変更のリアルタイム反映テスト

        [Test]
        public void UpdateMaterialProperties_パラメータ変更後に再呼び出しで反映される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            Material mat = _manager.FloorMaterial;

            // 初回設定
            vl.ShadowAlpha = 0.3f;
            _manager.UpdateMaterialProperties();
            Assert.AreEqual(0.3f, mat.GetFloat("_ShadowAlpha_0"), 0.001f);

            // パラメータ変更後に再度呼び出し
            vl.ShadowAlpha = 0.9f;
            _manager.UpdateMaterialProperties();
            Assert.AreEqual(0.9f, mat.GetFloat("_ShadowAlpha_0"), 0.001f,
                "パラメータ変更後にUpdateMaterialPropertiesを呼ぶと新しい値が反映されること");
        }

        [Test]
        public void UpdateMaterialProperties_BlurQuality変更後にキーワードが切り替わる()
        {
            Material mat = _manager.FloorMaterial;

            _manager.BlurQuality = BlurQuality.Low;
            _manager.UpdateMaterialProperties();
            Assert.IsTrue(mat.IsKeywordEnabled("_BLUR_LOW"));

            _manager.BlurQuality = BlurQuality.High;
            _manager.UpdateMaterialProperties();
            Assert.IsTrue(mat.IsKeywordEnabled("_BLUR_HIGH"),
                "BlurQuality変更後に新しいキーワードが有効になること");
            Assert.IsFalse(mat.IsKeywordEnabled("_BLUR_LOW"),
                "前のBlurQualityキーワードが無効になること");
        }

        #endregion

        #region FloorMaterial未生成時の安全性テスト

        [Test]
        public void UpdateMaterialProperties_FloorMaterialがnullでもエラーにならない()
        {
            // Managerを一旦disable→FloorMaterialをnullにする
            _manager.enabled = false;
            // FloorMaterialはOnDisableで破棄される

            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "FloorMaterialがnullのときUpdateMaterialPropertiesでエラーにならないこと");
        }

        #endregion
    }
}
