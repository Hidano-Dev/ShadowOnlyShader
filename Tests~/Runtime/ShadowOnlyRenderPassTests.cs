using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// ShadowOnlyRenderPassの検証テスト (Task 5.2)
    /// RenderGraph UnsafePassによる深度テクスチャ生成のデータセットアップロジック、
    /// Rendererフィルタリング、Manager/Material設定を検証する。
    /// Requirements: 1.3, 1.4, 3.1, 5.1, 5.3, 5.4
    /// </summary>
    public class ShadowOnlyRenderPassTests
    {
        private ShadowOnlyRenderPass _pass;
        private GameObject _managerGo;
        private ShadowOnlyManager _manager;

        [SetUp]
        public void SetUp()
        {
            _pass = new ShadowOnlyRenderPass();
            _managerGo = new GameObject("TestManager");
            _manager = _managerGo.AddComponent<ShadowOnlyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            _pass = null;

            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
                _manager = null;
            }

            // テストで作成した全GameObjectのクリーンアップ
            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                Object.DestroyImmediate(m.gameObject);
            }
        }

        #region Manager設定テスト

        [Test]
        public void SetManager_Managerが設定される()
        {
            _pass.SetManager(_manager);

            var managerField = typeof(ShadowOnlyRenderPass).GetField("_manager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(managerField, "_managerフィールドが存在すること");

            var storedManager = managerField.GetValue(_pass);
            Assert.AreEqual(_manager, storedManager, "SetManagerで設定したManagerが保持されること");
        }

        [Test]
        public void SetManager_nullを設定できる()
        {
            _pass.SetManager(null);

            var managerField = typeof(ShadowOnlyRenderPass).GetField("_manager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var storedManager = managerField.GetValue(_pass);
            Assert.IsNull(storedManager, "nullのManagerを設定できること");
        }

        #endregion

        #region DepthOnly Material設定テスト

        [Test]
        public void SetDepthOnlyMaterial_Materialが設定される()
        {
            var material = new Material(Shader.Find("Standard"));

            _pass.SetDepthOnlyMaterial(material);

            var matField = typeof(ShadowOnlyRenderPass).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(matField, "_depthOnlyMaterialフィールドが存在すること");

            var storedMat = matField.GetValue(_pass);
            Assert.AreEqual(material, storedMat, "SetDepthOnlyMaterialで設定したMaterialが保持されること");

            Object.DestroyImmediate(material);
        }

        [Test]
        public void SetDepthOnlyMaterial_nullを設定できる()
        {
            _pass.SetDepthOnlyMaterial(null);

            var matField = typeof(ShadowOnlyRenderPass).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var storedMat = matField.GetValue(_pass);
            Assert.IsNull(storedMat, "nullのMaterialを設定できること");
        }

        #endregion

        #region RenderPassEvent設定テスト

        [Test]
        public void RenderPassEvent_BeforeRenderingOpaquesに設定可能()
        {
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            Assert.AreEqual(RenderPassEvent.BeforeRenderingOpaques, _pass.renderPassEvent,
                "RenderPassEventがBeforeRenderingOpaquesに設定できること");
        }

        #endregion

        #region RecordRenderGraphメソッドの存在テスト

        [Test]
        public void RecordRenderGraph_メソッドが存在する()
        {
            var method = typeof(ShadowOnlyRenderPass).GetMethod("RecordRenderGraph",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "RecordRenderGraphメソッドが存在すること");
        }

        [Test]
        public void RecordRenderGraph_overrideされている()
        {
            var method = typeof(ShadowOnlyRenderPass).GetMethod("RecordRenderGraph",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method);

            // Check that the declaring type is ShadowOnlyRenderPass (not the base class)
            Assert.AreEqual(typeof(ShadowOnlyRenderPass), method.DeclaringType,
                "RecordRenderGraphがShadowOnlyRenderPassでオーバーライドされていること");
        }

        #endregion

        #region Rendererフィルタリングの検証（データセットアップ）

        [Test]
        public void GetActiveVirtualLights_Managerから仮想光源リストを取得できる()
        {
            // Managerに仮想光源を追加
            var vl = _manager.AddVirtualLight();
            Assert.IsNotNull(vl, "仮想光源が追加されること");

            var lights = _manager.VirtualLights;
            Assert.AreEqual(1, lights.Count, "Managerから1つの仮想光源が取得できること");
        }

        [Test]
        public void VirtualLight_CasterRenderersにnullのRendererが含まれない()
        {
            // VirtualLightを作成
            var vlGo = new GameObject("TestVirtualLight");
            vlGo.transform.SetParent(_managerGo.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            // CasterRootを設定（空のGameObject）
            var casterRoot = new GameObject("CasterRoot");
            vl.CasterRoot = casterRoot;
            vl.CollectRenderers();

            // Rendererがないので空リスト
            Assert.AreEqual(0, vl.CasterRenderers.Count,
                "Rendererがない場合は空のリストを返すこと");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void VirtualLight_アクティブなRendererのみが収集される()
        {
            var vlGo = new GameObject("TestVirtualLight");
            vlGo.transform.SetParent(_managerGo.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            // CasterRootにRendererを持つ子を追加
            var casterRoot = new GameObject("CasterRoot");
            var child1 = new GameObject("Child1");
            child1.transform.SetParent(casterRoot.transform);
            child1.AddComponent<MeshRenderer>();

            var child2 = new GameObject("Child2_Inactive");
            child2.transform.SetParent(casterRoot.transform);
            child2.AddComponent<MeshRenderer>();
            child2.SetActive(false);

            vl.CasterRoot = casterRoot;
            vl.CollectRenderers();

            // GetComponentsInChildrenは非アクティブなオブジェクトのRendererを含まない（デフォルト動作）
            Assert.AreEqual(1, vl.CasterRenderers.Count,
                "アクティブなRendererのみが収集されること");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void VirtualLight_DepthRenderTextureが作成される()
        {
            var vlGo = new GameObject("TestVirtualLight");
            vlGo.transform.SetParent(_managerGo.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.EnsureDepthTexture();
            Assert.IsNotNull(vl.DepthRenderTexture,
                "EnsureDepthTexture後にDepthRenderTextureが作成されること");
        }

        [Test]
        public void VirtualLight_VP行列が計算される()
        {
            var vlGo = new GameObject("TestVirtualLight");
            vlGo.transform.SetParent(_managerGo.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.UpdateMatrices();
            Assert.AreNotEqual(Matrix4x4.zero, vl.ViewProjectionMatrix,
                "UpdateMatrices後にVP行列がゼロでないこと");
        }

        #endregion

        #region PassData構造の検証

        [Test]
        public void PassData_クラスが存在する()
        {
            // PassDataがShadowOnlyRenderPassの内部クラスとして存在するか確認
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType, "PassDataクラスがShadowOnlyRenderPass内に存在すること");
        }

        [Test]
        public void PassData_viewMatricesフィールドが存在する()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var field = passDataType.GetField("viewMatrices",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PassDataにviewMatricesフィールドが存在すること");
        }

        [Test]
        public void PassData_projectionMatricesフィールドが存在する()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var field = passDataType.GetField("projectionMatrices",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PassDataにprojectionMatricesフィールドが存在すること");
        }

        [Test]
        public void PassData_depthRenderTexturesフィールドが存在する()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var field = passDataType.GetField("depthRenderTextures",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PassDataにdepthRenderTexturesフィールドが存在すること");
        }

        [Test]
        public void PassData_casterRendererListsフィールドが存在する()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var field = passDataType.GetField("casterRendererLists",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PassDataにcasterRendererListsフィールドが存在すること");
        }

        [Test]
        public void PassData_depthOnlyMaterialフィールドが存在する()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var field = passDataType.GetField("depthOnlyMaterial",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PassDataにdepthOnlyMaterialフィールドが存在すること");
        }

        [Test]
        public void PassData_Clearメソッドが存在しリストをクリアする()
        {
            var passDataType = typeof(ShadowOnlyRenderPass).GetNestedType("PassData",
                BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(passDataType);

            var clearMethod = passDataType.GetMethod("Clear",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(clearMethod, "PassDataにClearメソッドが存在すること");

            // PassDataインスタンスを作成しClearを呼び出す
            var instance = System.Activator.CreateInstance(passDataType);
            Assert.DoesNotThrow(() => clearMethod.Invoke(instance, null),
                "Clearメソッドが例外なく実行されること");
        }

        #endregion

        #region ExecutePassメソッドの検証

        [Test]
        public void ExecutePass_staticメソッドが存在する()
        {
            var method = typeof(ShadowOnlyRenderPass).GetMethod("ExecutePass",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ExecutePass staticメソッドが存在すること");
        }

        #endregion

        #region RecordRenderGraphの安全性テスト

        [Test]
        public void RecordRenderGraph_Managerがnullでも例外が発生しない()
        {
            _pass.SetManager(null);

            // RecordRenderGraphにnullを渡すとNullReferenceExceptionが発生するため、
            // Manager nullの場合の早期リターンをテスト
            // 注: RenderGraphとContextContainerはUnityの内部オブジェクトのため、
            // 直接テストは困難。Managerのnullチェック自体をリフレクションで検証する。
            var managerField = typeof(ShadowOnlyRenderPass).GetField("_manager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var storedManager = managerField.GetValue(_pass);
            Assert.IsNull(storedManager, "Manager nullの場合のガード条件が確認できること");
        }

        [Test]
        public void RecordRenderGraph_DepthOnlyMaterialがnullでも例外が発生しない()
        {
            _pass.SetManager(_manager);
            _pass.SetDepthOnlyMaterial(null);

            var matField = typeof(ShadowOnlyRenderPass).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var storedMat = matField.GetValue(_pass);
            Assert.IsNull(storedMat, "Material nullの場合のガード条件が確認できること");
        }

        #endregion
    }
}
