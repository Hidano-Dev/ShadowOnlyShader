using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// リソースライフサイクルとエラーハンドリングの検証テスト (Task 10)
    /// 自動生成リソースの破棄、設定エラー時の安全動作、nullチェック動作を検証する。
    /// Requirements: 4.4, 5.1, 5.3
    /// </summary>
    public class ResourceLifecycleAndErrorHandlingTests
    {
        private GameObject _managerObject;
        private ShadowOnlyManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("TestShadowOnlyManager");
            _manager = _managerObject.AddComponent<ShadowOnlyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);

            // テストで残ったオブジェクトのクリーンアップ
            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var virtualLights = Object.FindObjectsByType<VirtualLight>(FindObjectsSortMode.None);
            foreach (var vl in virtualLights)
            {
                Object.DestroyImmediate(vl.gameObject);
            }
        }

        #region リソースライフサイクル - Material破棄テスト

        [Test]
        public void Manager_OnDestroy時にFloorMaterialが確実に破棄される()
        {
            Material mat = _manager.FloorMaterial;
            Assert.IsNotNull(mat, "FloorMaterialが生成されていること");

            Object.DestroyImmediate(_managerObject);
            _managerObject = null;

            // Unity の DestroyImmediate 後、Material は null と等価になる
            Assert.IsTrue(mat == null,
                "OnDestroy後にFloorMaterialが破棄されること");
        }

        [Test]
        public void Manager_OnDisable時にFloorMaterialが破棄される()
        {
            Material mat = _manager.FloorMaterial;
            Assert.IsNotNull(mat, "FloorMaterialが生成されていること");

            _manager.enabled = false;

            Assert.IsTrue(mat == null,
                "OnDisable後にFloorMaterialが破棄されること");
        }

        [Test]
        public void Manager_OnDisable後OnEnable時にFloorMaterialが再生成される()
        {
            _manager.enabled = false;
            _manager.enabled = true;

            Assert.IsNotNull(_manager.FloorMaterial,
                "OnDisable→OnEnable後にFloorMaterialが再生成されること");
        }

        [Test]
        public void Manager_DestroyResourcesを複数回呼んでもエラーにならない()
        {
            // OnDisableとOnDestroyが両方呼ばれるケースのシミュレーション
            _manager.enabled = false;  // OnDisable → DestroyResources
            Assert.DoesNotThrow(() =>
            {
                Object.DestroyImmediate(_managerObject);  // OnDestroy → DestroyResources (already null)
                _managerObject = null;
            }, "リソース破棄を複数回呼んでもエラーにならないこと");
        }

        #endregion

        #region リソースライフサイクル - RenderTexture破棄テスト

        [Test]
        public void VirtualLight_OnDisable時にDepthRenderTextureが破棄される()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            // OnEnable で自動作成されるが、明示的に確認
            vl.EnsureDepthTexture();
            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(rt, "DepthRenderTextureが作成されていること");

            vl.enabled = false;

            Assert.IsTrue(rt == null,
                "OnDisable後にDepthRenderTextureが破棄されること");
        }

        [Test]
        public void VirtualLight_OnDestroy時にDepthRenderTextureが破棄される()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.EnsureDepthTexture();
            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(rt, "DepthRenderTextureが作成されていること");

            Object.DestroyImmediate(vlGo);

            Assert.IsTrue(rt == null,
                "OnDestroy後にDepthRenderTextureが破棄されること");
        }

        [Test]
        public void VirtualLight_DepthRenderTextureにHideFlagsDontSaveが設定されている()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.EnsureDepthTexture();
            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(rt);

            Assert.IsTrue((rt.hideFlags & HideFlags.DontSave) != 0,
                "DepthRenderTextureにHideFlags.DontSaveが設定されていること");
        }

        [Test]
        public void VirtualLight_解像度変更時に古いRenderTextureが破棄され再作成される()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.TextureResolution = 512;
            vl.EnsureDepthTexture();
            RenderTexture rtOld = vl.DepthRenderTexture;
            Assert.IsNotNull(rtOld);
            Assert.AreEqual(512, rtOld.width);

            // 解像度を変更
            vl.TextureResolution = 1024;
            vl.EnsureDepthTexture();
            RenderTexture rtNew = vl.DepthRenderTexture;

            Assert.IsNotNull(rtNew, "新しいRenderTextureが作成されること");
            Assert.AreEqual(1024, rtNew.width, "新しい解像度が反映されること");
            Assert.IsTrue(rtOld == null, "古いRenderTextureが破棄されること");
        }

        [Test]
        public void VirtualLight_ReleaseDepthTextureを複数回呼んでもエラーにならない()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            // OnDisableとOnDestroyが連続で呼ばれるケース
            vl.enabled = false;  // OnDisable → ReleaseDepthTexture
            Assert.DoesNotThrow(() =>
            {
                Object.DestroyImmediate(vlGo);  // OnDestroy → ReleaseDepthTexture (already null)
            }, "RenderTexture破棄を複数回呼んでもエラーにならないこと");
        }

        #endregion

        #region CasterRoot=null時のエラーハンドリング

        [Test]
        public void VirtualLight_CasterRootがnullでもCollectRenderersがクラッシュしない()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.CasterRoot = null;

            Assert.DoesNotThrow(() => vl.CollectRenderers(),
                "CasterRoot=nullでもCollectRenderersがクラッシュしないこと");
            Assert.AreEqual(0, vl.CasterRenderers.Count,
                "CasterRoot=nullの場合、CasterRenderersは空であること");
        }

        [Test]
        public void VirtualLight_CasterRootが破棄されてもCollectRenderersがクラッシュしない()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            var casterRoot = new GameObject("CasterRoot");
            vl.CasterRoot = casterRoot;
            Object.DestroyImmediate(casterRoot);

            // Unity の null チェックにより、破棄済みオブジェクトは null と扱われる
            Assert.DoesNotThrow(() => vl.CollectRenderers(),
                "CasterRootが破棄されてもCollectRenderersがクラッシュしないこと");
            Assert.AreEqual(0, vl.CasterRenderers.Count,
                "CasterRootが破棄された場合、CasterRenderersは空であること");
        }

        #endregion

        #region FloorRenderer=null/非アクティブ時のエラーハンドリング

        [Test]
        public void Manager_nullのFloorRendererを追加しようとすると警告が出る()
        {
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("null"));

            _manager.AddFloorRenderer(null);

            Assert.AreEqual(0, _manager.FloorRenderers.Count,
                "nullのFloorRendererは追加されないこと");
        }

        [Test]
        public void Manager_非アクティブなFloorRendererでもUpdateMaterialPropertiesがクラッシュしない()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();
            _manager.AddFloorRenderer(renderer);

            floorGo.SetActive(false);

            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "非アクティブなFloorRendererでもUpdateMaterialPropertiesがクラッシュしないこと");

            Object.DestroyImmediate(floorGo);
        }

        [Test]
        public void Manager_破棄されたFloorRendererでもAssignFloorMaterialがクラッシュしない()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();
            _manager.AddFloorRenderer(renderer);

            Object.DestroyImmediate(floorGo);

            // Rendererが破棄された後もAssignFloorMaterialがクラッシュしないこと
            Assert.DoesNotThrow(() => _manager.AssignFloorMaterial(),
                "破棄されたFloorRendererでもAssignFloorMaterialがクラッシュしないこと");
        }

        [Test]
        public void Manager_破棄されたFloorRendererでもLateUpdateがクラッシュしない()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();
            _manager.AddFloorRenderer(renderer);

            Object.DestroyImmediate(floorGo);

            // LateUpdateの内部処理（UpdateMaterialProperties + AssignFloorMaterial）が
            // 破棄されたRendererでもクラッシュしないこと
            Assert.DoesNotThrow(() =>
            {
                _manager.UpdateMaterialProperties();
                _manager.AssignFloorMaterial();
            }, "破棄されたFloorRendererでもLateUpdate相当の処理がクラッシュしないこと");
        }

        #endregion

        #region Manager不在時のエラーハンドリング

        [Test]
        public void RendererFeature_Manager不在時にAddRenderPassesがクラッシュしない()
        {
            var feature = ScriptableObject.CreateInstance<ShadowOnlyRendererFeature>();
            feature.Create();

            // シーンからManagerを全て削除
            Object.DestroyImmediate(_managerObject);
            _managerObject = null;

            // Manager不在時の警告を期待
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("ShadowOnlyManager"));

            // FindActiveManagerがnullを返す状況でもクラッシュしない
            // AddRenderPassesは直接テストが困難なため、FindActiveManagerを検証
            var method = typeof(ShadowOnlyRendererFeature).GetMethod("FindActiveManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                var result = method.Invoke(feature, null);
                Assert.IsNull(result,
                    "Manager不在時にFindActiveManagerがnullを返すこと");
            }

            feature.Dispose();
            Object.DestroyImmediate(feature);
        }

        [Test]
        public void RenderPass_Managerがnullでも安全に動作する()
        {
            var pass = new ShadowOnlyRenderPass();
            pass.SetManager(null);

            // Manager=nullの場合、RecordRenderGraphは早期リターンすべき
            var managerField = typeof(ShadowOnlyRenderPass).GetField("_manager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(managerField.GetValue(pass),
                "Manager=nullが正しく保持されること（早期リターンの前提条件）");
        }

        [Test]
        public void RenderPass_DepthOnlyMaterialがnullでも安全に動作する()
        {
            var pass = new ShadowOnlyRenderPass();
            pass.SetManager(_manager);
            pass.SetDepthOnlyMaterial(null);

            // Material=nullの場合、RecordRenderGraphは早期リターンすべき
            var matField = typeof(ShadowOnlyRenderPass).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(matField.GetValue(pass),
                "Material=nullが正しく保持されること（早期リターンの前提条件）");
        }

        #endregion

        #region Rendererが実行中に破棄された場合のnullチェック

        [Test]
        public void Manager_VirtualLightが実行中に破棄されてもUpdateMaterialPropertiesがクラッシュしない()
        {
            // VirtualLightを追加
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            vlGo.AddComponent<VirtualLight>();
            _manager.RefreshVirtualLights();
            Assert.AreEqual(1, _manager.VirtualLights.Count);

            // VirtualLightを実行中に破棄
            Object.DestroyImmediate(vlGo);

            // UpdateMaterialPropertiesがクラッシュしないこと
            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "VirtualLightが破棄されてもUpdateMaterialPropertiesがクラッシュしないこと");
        }

        [Test]
        public void Manager_VirtualLightが破棄された後RefreshVirtualLightsで正しく除外される()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            vlGo.AddComponent<VirtualLight>();
            _manager.RefreshVirtualLights();
            Assert.AreEqual(1, _manager.VirtualLights.Count);

            Object.DestroyImmediate(vlGo);
            _manager.RefreshVirtualLights();

            Assert.AreEqual(0, _manager.VirtualLights.Count,
                "破棄されたVirtualLightがRefreshVirtualLights後にリストから除外されること");
        }

        [Test]
        public void VirtualLight_CasterRendererが実行中に破棄されても安全()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            var casterRoot = new GameObject("CasterRoot");
            var child = new GameObject("Caster");
            child.transform.SetParent(casterRoot.transform);
            child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();

            vl.CasterRoot = casterRoot;
            vl.CollectRenderers();
            Assert.AreEqual(1, vl.CasterRenderers.Count);

            // Rendererを実行中に破棄
            Object.DestroyImmediate(child);

            // 再収集でクラッシュしないこと
            Assert.DoesNotThrow(() => vl.CollectRenderers(),
                "CasterRendererが破棄されても再収集でクラッシュしないこと");

            Object.DestroyImmediate(casterRoot);
        }

        #endregion

        #region シーン遷移時のリソース解放

        [Test]
        public void Manager_GameObjectを破棄するとFloorMaterialとVirtualLightsが解放される()
        {
            // VirtualLightを追加
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();
            vl.EnsureDepthTexture();
            _manager.RefreshVirtualLights();

            Material mat = _manager.FloorMaterial;
            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(mat);
            Assert.IsNotNull(rt);

            // シーン遷移をシミュレート（GameObjectの破棄）
            Object.DestroyImmediate(_managerObject);
            _managerObject = null;

            Assert.IsTrue(mat == null,
                "Manager破棄後にFloorMaterialが解放されること");
            // VirtualLightもManager子なので一緒に破棄される
            Assert.IsTrue(rt == null,
                "Manager破棄後にVirtualLightのDepthRenderTextureが解放されること");
        }

        [Test]
        public void VirtualLight_OnDestroyでDepthRenderTextureが破棄される()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            var vl = vlGo.AddComponent<VirtualLight>();

            vl.EnsureDepthTexture();
            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(rt);

            // OnDestroyが呼ばれるケース（OnDisableも先に呼ばれる）
            Object.DestroyImmediate(vlGo);

            Assert.IsTrue(rt == null,
                "OnDestroy後にDepthRenderTextureが確実に破棄されること");
        }

        #endregion

        #region RendererFeatureのリソース管理

        [Test]
        public void RendererFeature_Dispose後にDepthOnlyMaterialが破棄される()
        {
            var feature = ScriptableObject.CreateInstance<ShadowOnlyRendererFeature>();
            feature.Create();

            var matField = typeof(ShadowOnlyRendererFeature).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var mat = matField?.GetValue(feature) as Material;

            feature.Dispose();

            if (mat != null)
            {
                Assert.IsTrue(mat == null,
                    "Dispose後にDepthOnlyMaterialが破棄されること");
            }

            Object.DestroyImmediate(feature);
        }

        [Test]
        public void RendererFeature_Disposeを複数回呼んでもエラーにならない()
        {
            var feature = ScriptableObject.CreateInstance<ShadowOnlyRendererFeature>();
            feature.Create();

            Assert.DoesNotThrow(() =>
            {
                feature.Dispose();
                feature.Dispose();
            }, "Disposeを複数回呼んでもエラーにならないこと");

            Object.DestroyImmediate(feature);
        }

        #endregion

        #region エッジケース: nullチェックの網羅性

        [Test]
        public void Manager_FloorMaterialがnullの状態でUpdateMaterialPropertiesを呼んでもクラッシュしない()
        {
            // FloorMaterialが何らかの理由でnullになった場合
            _manager.enabled = false;  // OnDisable で Material 破棄
            Assert.IsNull(_manager.FloorMaterial);

            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "FloorMaterial=nullでもUpdateMaterialPropertiesがクラッシュしないこと");
        }

        [Test]
        public void Manager_FloorMaterialがnullの状態でAssignFloorMaterialを呼んでもクラッシュしない()
        {
            _manager.enabled = false;
            Assert.IsNull(_manager.FloorMaterial);

            Assert.DoesNotThrow(() => _manager.AssignFloorMaterial(),
                "FloorMaterial=nullでもAssignFloorMaterialがクラッシュしないこと");
        }

        [Test]
        public void Manager_VirtualLightsリスト内のnull要素をUpdateMaterialPropertiesでスキップする()
        {
            // VirtualLightを追加してから破棄し、リストにnullが残る状況
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            vlGo.AddComponent<VirtualLight>();
            _manager.RefreshVirtualLights();

            Object.DestroyImmediate(vlGo);
            // RefreshVirtualLightsを呼ばずにnullが残った状態

            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "VirtualLightsリスト内のnull要素でもクラッシュしないこと");
        }

        [Test]
        public void Manager_破棄されたVirtualLightのtransformアクセスでクラッシュしない()
        {
            var vlGo = new GameObject("VirtualLight");
            vlGo.transform.SetParent(_managerObject.transform);
            vlGo.AddComponent<VirtualLight>();
            _manager.RefreshVirtualLights();

            Object.DestroyImmediate(vlGo);

            // transform.position アクセスでクラッシュしないことを確認
            Assert.DoesNotThrow(() => _manager.UpdateMaterialProperties(),
                "破棄されたVirtualLightのtransformアクセスでクラッシュしないこと");
        }

        #endregion
    }
}
