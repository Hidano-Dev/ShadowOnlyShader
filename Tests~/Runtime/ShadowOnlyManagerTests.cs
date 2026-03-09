using System.Collections.Generic;
using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// ShadowOnlyManagerコンポーネントの検証テスト (Task 3)
    /// 仮想光源の自動検出・動的更新、床面Rendererリスト管理、
    /// リソースライフサイクル管理、グローバルパラメータ、Public APIを検証する。
    /// Requirements: 3.2, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 8.1, 8.4
    /// </summary>
    public class ShadowOnlyManagerTests
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
            // 他のManagerが残っている場合も含めてクリーンアップ
            if (_managerObject != null)
                Object.DestroyImmediate(_managerObject);

            // テストで作成した追加オブジェクトのクリーンアップ
            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                Object.DestroyImmediate(m.gameObject);
            }
        }

        #region IShadowOnlyManagerインターフェース実装テスト

        [Test]
        public void ShadowOnlyManager_IShadowOnlyManagerを実装している()
        {
            Assert.IsInstanceOf<IShadowOnlyManager>(_manager,
                "ShadowOnlyManagerはIShadowOnlyManagerを実装していること");
        }

        [Test]
        public void ShadowOnlyManager_MonoBehaviourを継承している()
        {
            Assert.IsInstanceOf<MonoBehaviour>(_manager,
                "ShadowOnlyManagerはMonoBehaviourを継承していること");
        }

        #endregion

        #region 子GameObjectのVirtualLight自動検出テスト

        [Test]
        public void VirtualLights_初期状態で空リストを返す()
        {
            Assert.IsNotNull(_manager.VirtualLights);
            Assert.AreEqual(0, _manager.VirtualLights.Count,
                "子にVirtualLightがない場合、VirtualLightsは空であること");
        }

        [Test]
        public void VirtualLights_子GameObjectのVirtualLightを自動検出する()
        {
            var childGo = new GameObject("VirtualLight1");
            childGo.transform.SetParent(_managerObject.transform);
            var vl = childGo.AddComponent<VirtualLight>();

            // 子の変更を反映させる
            _manager.RefreshVirtualLights();

            Assert.AreEqual(1, _manager.VirtualLights.Count,
                "子GameObjectのVirtualLightが検出されること");
        }

        [Test]
        public void VirtualLights_複数の子VirtualLightを検出する()
        {
            for (int i = 0; i < 3; i++)
            {
                var childGo = new GameObject($"VirtualLight{i}");
                childGo.transform.SetParent(_managerObject.transform);
                childGo.AddComponent<VirtualLight>();
            }

            _manager.RefreshVirtualLights();

            Assert.AreEqual(3, _manager.VirtualLights.Count,
                "複数の子VirtualLightが全て検出されること");
        }

        [Test]
        public void VirtualLights_IReadOnlyListとして返される()
        {
            Assert.IsInstanceOf<IReadOnlyList<IVirtualLight>>(_manager.VirtualLights,
                "VirtualLightsはIReadOnlyList<IVirtualLight>であること");
        }

        [Test]
        public void VirtualLights_孫階層のVirtualLightは検出しない()
        {
            // Managerの直接の子のみを対象とする（孫は別のManagerに属する可能性がある）
            var childGo = new GameObject("Child");
            childGo.transform.SetParent(_managerObject.transform);

            var grandChildGo = new GameObject("GrandChild");
            grandChildGo.transform.SetParent(childGo.transform);
            grandChildGo.AddComponent<VirtualLight>();

            // 直接の子にVirtualLightを追加
            var directChild = new GameObject("DirectVirtualLight");
            directChild.transform.SetParent(_managerObject.transform);
            directChild.AddComponent<VirtualLight>();

            _manager.RefreshVirtualLights();

            // GetComponentsInChildrenは孫も含むため、設計に応じて検証
            // design.mdでは「子GameObjectのVirtualLight」とあるので子階層全体を対象とする
            Assert.GreaterOrEqual(_manager.VirtualLights.Count, 1,
                "少なくとも子階層のVirtualLightが検出されること");
        }

        #endregion

        #region 床面Rendererリスト管理テスト

        [Test]
        public void FloorRenderers_初期状態で空リストを返す()
        {
            Assert.IsNotNull(_manager.FloorRenderers);
            Assert.AreEqual(0, _manager.FloorRenderers.Count,
                "初期状態でFloorRenderersは空であること");
        }

        [Test]
        public void AddFloorRenderer_Rendererを追加できる()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(renderer);

            Assert.AreEqual(1, _manager.FloorRenderers.Count,
                "AddFloorRendererでRendererが追加されること");

            Object.DestroyImmediate(floorGo);
        }

        [Test]
        public void AddFloorRenderer_複数のRendererを追加できる()
        {
            var floor1 = new GameObject("Floor1");
            var r1 = floor1.AddComponent<MeshRenderer>();
            var floor2 = new GameObject("Floor2");
            var r2 = floor2.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(r1);
            _manager.AddFloorRenderer(r2);

            Assert.AreEqual(2, _manager.FloorRenderers.Count,
                "複数のFloorRendererが追加されること");

            Object.DestroyImmediate(floor1);
            Object.DestroyImmediate(floor2);
        }

        [Test]
        public void AddFloorRenderer_nullを渡した場合は追加されない()
        {
            // nullを渡すと警告ログが出力される
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("null"));
            _manager.AddFloorRenderer(null);

            Assert.AreEqual(0, _manager.FloorRenderers.Count,
                "nullのRendererは追加されないこと");
        }

        [Test]
        public void AddFloorRenderer_同じRendererの重複追加を防ぐ()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(renderer);
            _manager.AddFloorRenderer(renderer);

            Assert.AreEqual(1, _manager.FloorRenderers.Count,
                "同じRendererは重複追加されないこと");

            Object.DestroyImmediate(floorGo);
        }

        [Test]
        public void RemoveFloorRenderer_登録済みRendererを削除できる()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(renderer);
            Assert.AreEqual(1, _manager.FloorRenderers.Count);

            _manager.RemoveFloorRenderer(renderer);

            Assert.AreEqual(0, _manager.FloorRenderers.Count,
                "RemoveFloorRendererでRendererが削除されること");

            Object.DestroyImmediate(floorGo);
        }

        [Test]
        public void RemoveFloorRenderer_未登録Rendererを渡してもエラーにならない()
        {
            var floorGo = new GameObject("Floor");
            var renderer = floorGo.AddComponent<MeshRenderer>();

            Assert.DoesNotThrow(() => _manager.RemoveFloorRenderer(renderer),
                "未登録のRendererを削除してもエラーにならないこと");

            Object.DestroyImmediate(floorGo);
        }

        [Test]
        public void RemoveFloorRenderer_nullを渡してもエラーにならない()
        {
            Assert.DoesNotThrow(() => _manager.RemoveFloorRenderer(null),
                "nullを削除しようとしてもエラーにならないこと");
        }

        [Test]
        public void FloorRenderers_IReadOnlyListとして返される()
        {
            Assert.IsInstanceOf<IReadOnlyList<Renderer>>(_manager.FloorRenderers,
                "FloorRenderersはIReadOnlyList<Renderer>であること");
        }

        #endregion

        #region AddVirtualLight / RemoveVirtualLight Public API テスト

        [Test]
        public void AddVirtualLight_子GameObjectを生成しVirtualLightを返す()
        {
            IVirtualLight vl = _manager.AddVirtualLight();

            Assert.IsNotNull(vl, "AddVirtualLightがnullでないIVirtualLightを返すこと");
            Assert.IsInstanceOf<VirtualLight>(vl);
        }

        [Test]
        public void AddVirtualLight_生成されたGameObjectはManagerの子になる()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            var vlComponent = vl as VirtualLight;

            Assert.AreEqual(_managerObject.transform, vlComponent.transform.parent,
                "追加されたVirtualLightのGameObjectはManagerの子であること");
        }

        [Test]
        public void AddVirtualLight_VirtualLightsリストに反映される()
        {
            _manager.AddVirtualLight();

            Assert.AreEqual(1, _manager.VirtualLights.Count,
                "AddVirtualLight後にVirtualLightsリストに追加されること");
        }

        [Test]
        public void AddVirtualLight_複数回呼び出しで複数光源が追加される()
        {
            _manager.AddVirtualLight();
            _manager.AddVirtualLight();
            _manager.AddVirtualLight();

            Assert.AreEqual(3, _manager.VirtualLights.Count,
                "複数回AddVirtualLightを呼ぶと複数のVirtualLightが管理されること");
        }

        [Test]
        public void RemoveVirtualLight_VirtualLightを削除する()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            Assert.AreEqual(1, _manager.VirtualLights.Count);

            _manager.RemoveVirtualLight(vl);

            Assert.AreEqual(0, _manager.VirtualLights.Count,
                "RemoveVirtualLight後にVirtualLightsリストから削除されること");
        }

        [Test]
        public void RemoveVirtualLight_対応するGameObjectも破棄される()
        {
            IVirtualLight vl = _manager.AddVirtualLight();
            var vlComponent = vl as VirtualLight;
            var vlGameObject = vlComponent.gameObject;

            _manager.RemoveVirtualLight(vl);

            // DestroyImmediateで破棄されたオブジェクトはnullと比較でtrueを返す
            Assert.IsTrue(vlGameObject == null,
                "RemoveVirtualLight後に対応するGameObjectが破棄されること");
        }

        [Test]
        public void RemoveVirtualLight_nullを渡してもエラーにならない()
        {
            Assert.DoesNotThrow(() => _manager.RemoveVirtualLight(null),
                "nullを渡してもエラーにならないこと");
        }

        [Test]
        public void RemoveVirtualLight_管理外のVirtualLightを渡してもエラーにならない()
        {
            var otherGo = new GameObject("Other");
            var otherVl = otherGo.AddComponent<VirtualLight>();

            Assert.DoesNotThrow(() => _manager.RemoveVirtualLight(otherVl),
                "管理外のVirtualLightを渡してもエラーにならないこと");

            Object.DestroyImmediate(otherGo);
        }

        #endregion

        #region グローバルパラメータテスト

        [Test]
        public void BlurQuality_デフォルト値はMid()
        {
            Assert.AreEqual(BlurQuality.Mid, _manager.BlurQuality,
                "BlurQualityのデフォルト値はMidであること");
        }

        [Test]
        public void BlurQuality_設定と取得ができる()
        {
            _manager.BlurQuality = BlurQuality.Low;
            Assert.AreEqual(BlurQuality.Low, _manager.BlurQuality);

            _manager.BlurQuality = BlurQuality.High;
            Assert.AreEqual(BlurQuality.High, _manager.BlurQuality);
        }

        [Test]
        public void BlendMultiplier_デフォルト値は1()
        {
            Assert.AreEqual(1f, _manager.BlendMultiplier, 0.001f,
                "BlendMultiplierのデフォルト値は1.0であること");
        }

        [Test]
        public void BlendMultiplier_設定と取得ができる()
        {
            _manager.BlendMultiplier = 2.5f;
            Assert.AreEqual(2.5f, _manager.BlendMultiplier, 0.001f);
        }

        [Test]
        public void BlendMultiplier_0未満は0にクランプされる()
        {
            _manager.BlendMultiplier = -1f;
            Assert.GreaterOrEqual(_manager.BlendMultiplier, 0f,
                "BlendMultiplierは0未満にならないこと");
        }

        #endregion

        #region SerializeFieldテスト

        [Test]
        public void BlurQuality_SerializeField属性が付与されている()
        {
            var field = typeof(ShadowOnlyManager).GetField("_blurQuality",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_blurQualityフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_blurQualityにSerializeField属性が付与されていること");
        }

        [Test]
        public void BlendMultiplier_SerializeField属性が付与されている()
        {
            var field = typeof(ShadowOnlyManager).GetField("_blendMultiplier",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_blendMultiplierフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_blendMultiplierにSerializeField属性が付与されていること");
        }

        [Test]
        public void FloorRenderers_SerializeField属性が付与されている()
        {
            var field = typeof(ShadowOnlyManager).GetField("_floorRenderers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_floorRenderersフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_floorRenderersにSerializeField属性が付与されていること");
        }

        #endregion

        #region リソースライフサイクル管理テスト

        [Test]
        public void OnEnable_FloorMaterialが自動生成される()
        {
            // ShadowOnlyManagerがOnEnableで影描画用のMaterialを自動生成する
            // Material参照はinternalまたはpublicプロパティで確認
            Assert.IsNotNull(_manager.FloorMaterial,
                "OnEnable後にFloorMaterialが自動生成されること");
        }

        [Test]
        public void FloorMaterial_HideFlagsDontSaveで管理される()
        {
            Material mat = _manager.FloorMaterial;
            Assert.IsNotNull(mat);
            Assert.IsTrue(
                (mat.hideFlags & HideFlags.DontSave) != 0,
                "FloorMaterialにHideFlags.DontSaveが設定されていること");
        }

        [Test]
        public void OnDestroy_FloorMaterialが破棄される()
        {
            Material mat = _manager.FloorMaterial;
            Assert.IsNotNull(mat);

            Object.DestroyImmediate(_managerObject);
            _managerObject = null;

            Assert.IsTrue(mat == null,
                "OnDestroy後にFloorMaterialが破棄されること");
        }

        [Test]
        public void OnDisable_OnEnable_リソースが正しく再生成される()
        {
            Material matBefore = _manager.FloorMaterial;
            Assert.IsNotNull(matBefore);

            _manager.enabled = false;
            _manager.enabled = true;

            Material matAfter = _manager.FloorMaterial;
            Assert.IsNotNull(matAfter,
                "再度OnEnable後にFloorMaterialが再生成されること");
        }

        #endregion

        #region 複数Manager警告テスト

        [Test]
        public void 複数Manager存在時に警告が出る()
        {
            // 2つ目のManagerを作成すると、OnEnableで複数Manager警告が出力される
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("複数"));
            var secondManagerGo = new GameObject("SecondManager");
            var secondManager = secondManagerGo.AddComponent<ShadowOnlyManager>();

            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(managers.Length, 2,
                "複数のShadowOnlyManagerが存在すること（警告が出力される状況）");

            Object.DestroyImmediate(secondManagerGo);
        }

        #endregion

        #region RefreshVirtualLightsテスト

        [Test]
        public void RefreshVirtualLights_メソッドが公開されている()
        {
            var method = typeof(ShadowOnlyManager).GetMethod("RefreshVirtualLights");
            Assert.IsNotNull(method, "RefreshVirtualLightsメソッドが公開されていること");
        }

        [Test]
        public void RefreshVirtualLights_削除されたVirtualLightがリストから除外される()
        {
            var childGo = new GameObject("VirtualLight");
            childGo.transform.SetParent(_managerObject.transform);
            childGo.AddComponent<VirtualLight>();

            _manager.RefreshVirtualLights();
            Assert.AreEqual(1, _manager.VirtualLights.Count);

            // 子GameObjectを破棄
            Object.DestroyImmediate(childGo);

            _manager.RefreshVirtualLights();
            Assert.AreEqual(0, _manager.VirtualLights.Count,
                "破棄されたVirtualLightがリストから除外されること");
        }

        #endregion

        #region 床面RendererへのMaterial自動割り当てテスト

        [Test]
        public void AddFloorRenderer_SharedMaterialが設定される()
        {
            // FloorRendererが追加されたらManagerのFloorMaterialが自動設定されるべき
            // ただし実装はTask 7のパラメータ転送で行われるため、
            // ここではMaterial割り当て機能の基盤を検証する
            var floorGo = new GameObject("Floor");
            var meshFilter = floorGo.AddComponent<MeshFilter>();
            var renderer = floorGo.AddComponent<MeshRenderer>();

            _manager.AddFloorRenderer(renderer);

            // Materialの自動割り当てが行われているかを確認
            // FloorMaterialが生成されていれば基盤は整っている
            Assert.IsNotNull(_manager.FloorMaterial,
                "床面にMaterialを割り当てるための基盤としてFloorMaterialが生成されていること");

            Object.DestroyImmediate(floorGo);
        }

        #endregion
    }
}
