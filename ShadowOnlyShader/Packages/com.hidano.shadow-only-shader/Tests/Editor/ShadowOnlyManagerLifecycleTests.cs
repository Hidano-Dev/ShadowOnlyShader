using NUnit.Framework;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Editor
{
    /// <summary>
    /// ShadowOnlyManagerのリソース生成・破棄ライフサイクルを検証するユニットテスト。
    /// Requirements: 4.4, 1.5
    /// </summary>
    [TestFixture]
    public class ShadowOnlyManagerLifecycleTests
    {
        #region FloorMaterial ライフサイクル

        [Test]
        public void OnEnable_FloorMaterialが自動生成される()
        {
            // Arrange & Act: AddComponentでOnEnableが呼ばれる
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Assert: FloorMaterialが生成されている
            Assert.IsNotNull(manager.FloorMaterial,
                "OnEnableでFloorMaterialが自動生成されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FloorMaterial_HideFlagsDontSaveが設定される()
        {
            // Arrange & Act
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Assert
            Assert.IsNotNull(manager.FloorMaterial);
            Assert.IsTrue(
                (manager.FloorMaterial.hideFlags & HideFlags.DontSave) != 0,
                "FloorMaterialにはHideFlags.DontSaveが設定されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnDisable_FloorMaterialが破棄される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();
            Assert.IsNotNull(manager.FloorMaterial);

            // FloorMaterialへの参照を保持
            var materialRef = manager.FloorMaterial;

            // Act: OnDisableを発火
            go.SetActive(false);

            // Assert: FloorMaterialがnullになっている
            Assert.IsNull(manager.FloorMaterial,
                "OnDisableでFloorMaterialが破棄されるべき");
            // Unity上でDestroyImmediateされたオブジェクトはnull比較でtrueを返す
            Assert.IsTrue(materialRef == null,
                "破棄されたMaterialはUnityのnullチェックでtrueを返すべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnEnable再呼び出し_FloorMaterialが再生成される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();
            Assert.IsNotNull(manager.FloorMaterial);

            // Act: 無効化→有効化
            go.SetActive(false);
            Assert.IsNull(manager.FloorMaterial);
            go.SetActive(true);

            // Assert: 再生成されている
            Assert.IsNotNull(manager.FloorMaterial,
                "再有効化時にFloorMaterialが再生成されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnDestroy_FloorMaterialが破棄される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();
            var materialRef = manager.FloorMaterial;
            Assert.IsNotNull(materialRef);

            // Act: GameObjectを破棄（OnDestroyが呼ばれる）
            Object.DestroyImmediate(go);

            // Assert: MaterialがDestroyされた
            Assert.IsTrue(materialRef == null,
                "OnDestroyでFloorMaterialが破棄されるべき");
        }

        #endregion

        #region 深度Texture2DArray ライフサイクル

        [Test]
        public void Manager_OnEnable_DepthArrayTextureが作成される()
        {
            // Arrange & Act
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Assert: OnEnableで深度Texture2DArrayが作成される
            Assert.IsNotNull(manager.DepthArrayTexture,
                "OnEnableでDepthArrayTextureが作成されるべき");
            Assert.AreEqual(UnityEngine.Rendering.TextureDimension.Tex2DArray,
                manager.DepthArrayTexture.dimension,
                "DepthArrayTextureはTexture2DArrayであるべき");
            Assert.AreEqual(8,
                manager.DepthArrayTexture.volumeDepth,
                "DepthArrayTextureのスライス数は8であるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Manager_DepthArrayTexture_HideFlagsDontSaveが設定される()
        {
            // Arrange & Act
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Assert
            Assert.IsNotNull(manager.DepthArrayTexture);
            Assert.IsTrue(
                (manager.DepthArrayTexture.hideFlags & HideFlags.DontSave) != 0,
                "DepthArrayTextureにはHideFlags.DontSaveが設定されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Manager_OnDisable_DepthArrayTextureが破棄される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();
            var rtRef = manager.DepthArrayTexture;
            Assert.IsNotNull(rtRef);

            // Act
            go.SetActive(false);

            // Assert
            Assert.IsNull(manager.DepthArrayTexture,
                "OnDisableでDepthArrayTextureが破棄されるべき");
            Assert.IsTrue(rtRef == null,
                "破棄されたRenderTextureはUnityのnullチェックでtrueを返すべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        #endregion

        #region VirtualLight管理

        [Test]
        public void AddVirtualLight_子GameObjectが生成されリストに追加される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Act
            var vl = manager.AddVirtualLight();

            // Assert
            Assert.IsNotNull(vl, "AddVirtualLightはnullでない値を返すべき");
            Assert.AreEqual(1, manager.VirtualLights.Count,
                "VirtualLightsリストに1つ追加されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void AddVirtualLight_複数追加_リストが正しく増加する()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Act
            manager.AddVirtualLight();
            manager.AddVirtualLight();
            manager.AddVirtualLight();

            // Assert
            Assert.AreEqual(3, manager.VirtualLights.Count,
                "3つのVirtualLightが追加されるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RemoveVirtualLight_リストから削除されGameObjectが破棄される()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();
            var vl = manager.AddVirtualLight();
            Assert.AreEqual(1, manager.VirtualLights.Count);

            // Act
            manager.RemoveVirtualLight(vl);

            // Assert
            Assert.AreEqual(0, manager.VirtualLights.Count,
                "RemoveVirtualLight後にリストが空になるべき");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RemoveVirtualLight_nullを渡しても例外が発生しない()
        {
            // Arrange
            var go = new GameObject("TestManager");
            var manager = go.AddComponent<ShadowOnlyManager>();

            // Act & Assert: 例外が発生しないことを検証
            Assert.DoesNotThrow(() => manager.RemoveVirtualLight(null),
                "nullのVirtualLight削除で例外が発生してはいけない");

            // Cleanup
            Object.DestroyImmediate(go);
        }

        #endregion

        #region 床面Renderer管理

        [Test]
        public void AddFloorRenderer_正常に追加される()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();
            var floorGO = new GameObject("Floor");
            floorGO.AddComponent<MeshFilter>();
            var renderer = floorGO.AddComponent<MeshRenderer>();

            // Act
            manager.AddFloorRenderer(renderer);

            // Assert
            Assert.AreEqual(1, manager.FloorRenderers.Count,
                "床面Rendererが1つ追加されるべき");

            // Cleanup
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(floorGO);
        }

        [Test]
        public void AddFloorRenderer_nullを渡すと追加されない()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            // Act
            manager.AddFloorRenderer(null);

            // Assert
            Assert.AreEqual(0, manager.FloorRenderers.Count,
                "nullのRendererは追加されるべきでない");

            // Cleanup
            Object.DestroyImmediate(managerGO);
        }

        [Test]
        public void AddFloorRenderer_重複追加_2つ目は追加されない()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();
            var floorGO = new GameObject("Floor");
            floorGO.AddComponent<MeshFilter>();
            var renderer = floorGO.AddComponent<MeshRenderer>();

            // Act
            manager.AddFloorRenderer(renderer);
            manager.AddFloorRenderer(renderer);

            // Assert
            Assert.AreEqual(1, manager.FloorRenderers.Count,
                "重複するRendererは追加されるべきでない");

            // Cleanup
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(floorGO);
        }

        [Test]
        public void RemoveFloorRenderer_正常に削除される()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();
            var floorGO = new GameObject("Floor");
            floorGO.AddComponent<MeshFilter>();
            var renderer = floorGO.AddComponent<MeshRenderer>();
            manager.AddFloorRenderer(renderer);
            Assert.AreEqual(1, manager.FloorRenderers.Count);

            // Act
            manager.RemoveFloorRenderer(renderer);

            // Assert
            Assert.AreEqual(0, manager.FloorRenderers.Count,
                "床面Rendererが削除されるべき");

            // Cleanup
            Object.DestroyImmediate(managerGO);
            Object.DestroyImmediate(floorGO);
        }

        [Test]
        public void RemoveFloorRenderer_nullを渡しても例外が発生しない()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            // Act & Assert
            Assert.DoesNotThrow(() => manager.RemoveFloorRenderer(null),
                "nullのRenderer削除で例外が発生してはいけない");

            // Cleanup
            Object.DestroyImmediate(managerGO);
        }

        #endregion

        #region RefreshVirtualLights

        [Test]
        public void RefreshVirtualLights_子階層のVirtualLightが自動検出される()
        {
            // Arrange
            var managerGO = new GameObject("TestManager");
            var manager = managerGO.AddComponent<ShadowOnlyManager>();

            // 手動で子VirtualLightを追加（AddVirtualLightを使わずに直接配置）
            var vlGO1 = new GameObject("VL1");
            vlGO1.transform.SetParent(managerGO.transform);
            vlGO1.AddComponent<VirtualLight>();

            var vlGO2 = new GameObject("VL2");
            vlGO2.transform.SetParent(managerGO.transform);
            vlGO2.AddComponent<VirtualLight>();

            // Act
            manager.RefreshVirtualLights();

            // Assert
            Assert.AreEqual(2, manager.VirtualLights.Count,
                "子階層の2つのVirtualLightが検出されるべき");

            // Cleanup
            Object.DestroyImmediate(managerGO);
        }

        #endregion
    }
}
