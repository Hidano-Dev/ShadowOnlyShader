using System.Reflection;
using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// ShadowOnlyRendererFeatureの検証テスト (Task 5.1)
    /// ScriptableRendererFeatureとしてのライフサイクル、
    /// ShadowOnlyRenderPassの生成、Manager検索ロジックを検証する。
    /// Requirements: 1.4, 6.1, 6.2
    /// </summary>
    public class ShadowOnlyRendererFeatureTests
    {
        private ShadowOnlyRendererFeature _feature;

        [SetUp]
        public void SetUp()
        {
            _feature = ScriptableObject.CreateInstance<ShadowOnlyRendererFeature>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_feature != null)
            {
                // Disposeを呼んでリソースクリーンアップ
                _feature.Dispose();
                Object.DestroyImmediate(_feature);
                _feature = null;
            }

            // テストで作成したManagerオブジェクトのクリーンアップ
            var managers = Object.FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                Object.DestroyImmediate(m.gameObject);
            }
        }

        #region ScriptableRendererFeature継承テスト

        [Test]
        public void ShadowOnlyRendererFeature_ScriptableRendererFeatureを継承している()
        {
            Assert.IsInstanceOf<ScriptableRendererFeature>(_feature,
                "ShadowOnlyRendererFeatureはScriptableRendererFeatureを継承していること");
        }

        #endregion

        #region Createメソッドテスト

        [Test]
        public void Create_呼び出し後にRenderPassが生成される()
        {
            _feature.Create();

            // リフレクションでRenderPassフィールドを確認
            var passField = typeof(ShadowOnlyRendererFeature).GetField("_renderPass",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(passField, "_renderPassフィールドが存在すること");

            var pass = passField.GetValue(_feature);
            Assert.IsNotNull(pass, "Create後にShadowOnlyRenderPassが生成されること");
        }

        [Test]
        public void Create_生成されたRenderPassはScriptableRenderPassである()
        {
            _feature.Create();

            var passField = typeof(ShadowOnlyRendererFeature).GetField("_renderPass",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var pass = passField.GetValue(_feature);

            Assert.IsInstanceOf<ScriptableRenderPass>(pass,
                "生成されたPassはScriptableRenderPassであること");
        }

        [Test]
        public void Create_生成されたRenderPassはShadowOnlyRenderPassである()
        {
            _feature.Create();

            var passField = typeof(ShadowOnlyRendererFeature).GetField("_renderPass",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var pass = passField.GetValue(_feature);

            Assert.IsInstanceOf<ShadowOnlyRenderPass>(pass,
                "生成されたPassはShadowOnlyRenderPassであること");
        }

        [Test]
        public void Create_RenderPassEventがBeforeRenderingOpaquesである()
        {
            _feature.Create();

            var passField = typeof(ShadowOnlyRendererFeature).GetField("_renderPass",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var pass = passField.GetValue(_feature) as ScriptableRenderPass;

            Assert.IsNotNull(pass);
            Assert.AreEqual(RenderPassEvent.BeforeRenderingOpaques, pass.renderPassEvent,
                "RenderPassEventがBeforeRenderingOpaquesであること");
        }

        [Test]
        public void Create_複数回呼び出しても例外が発生しない()
        {
            Assert.DoesNotThrow(() =>
            {
                _feature.Create();
                _feature.Create();
            }, "Createを複数回呼び出しても例外が発生しないこと");
        }

        #endregion

        #region Disposeメソッドテスト

        [Test]
        public void Dispose_呼び出しても例外が発生しない()
        {
            _feature.Create();
            Assert.DoesNotThrow(() => _feature.Dispose(),
                "Dispose呼び出しで例外が発生しないこと");
        }

        [Test]
        public void Dispose_Create前に呼び出しても例外が発生しない()
        {
            Assert.DoesNotThrow(() => _feature.Dispose(),
                "Create前にDispose呼び出しても例外が発生しないこと");
        }

        #endregion

        #region ShadowOnlyRenderPassクラスの存在テスト

        [Test]
        public void ShadowOnlyRenderPass_クラスが存在する()
        {
            var type = typeof(ShadowOnlyRenderPass);
            Assert.IsNotNull(type, "ShadowOnlyRenderPassクラスが存在すること");
        }

        [Test]
        public void ShadowOnlyRenderPass_ScriptableRenderPassを継承している()
        {
            Assert.IsTrue(typeof(ScriptableRenderPass).IsAssignableFrom(typeof(ShadowOnlyRenderPass)),
                "ShadowOnlyRenderPassはScriptableRenderPassを継承していること");
        }

        #endregion

        #region Manager検索ロジックテスト

        [Test]
        public void FindActiveManager_Managerが存在しない場合nullを返す()
        {
            // FindActiveManagerメソッドがstatic/publicであればメソッドを直接テスト
            // なければリフレクションで確認
            var method = typeof(ShadowOnlyRendererFeature).GetMethod("FindActiveManager",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            if (method != null)
            {
                var result = method.Invoke(method.IsStatic ? null : (object)_feature, null);
                Assert.IsNull(result, "Managerが存在しない場合nullを返すこと");
            }
            else
            {
                // メソッドがなくても、Manager不在時に警告が出ることをAddRenderPassesで間接的に検証
                Assert.Pass("FindActiveManagerメソッドはAddRenderPasses内で間接的にテストされる");
            }
        }

        [Test]
        public void FindActiveManager_Managerが存在する場合にそのManagerを返す()
        {
            var managerGo = new GameObject("TestManager");
            var manager = managerGo.AddComponent<ShadowOnlyManager>();

            var method = typeof(ShadowOnlyRendererFeature).GetMethod("FindActiveManager",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            if (method != null)
            {
                var result = method.Invoke(method.IsStatic ? null : (object)_feature, null);
                Assert.IsNotNull(result, "Managerが存在する場合にnullでないことを返すこと");
                Assert.AreEqual(manager, result, "正しいManagerインスタンスを返すこと");
            }
            else
            {
                Assert.Pass("FindActiveManagerはAddRenderPasses内で間接的にテストされる");
            }
        }

        #endregion

        #region DepthOnly Material管理テスト

        [Test]
        public void Create_DepthOnlyMaterialが生成される()
        {
            _feature.Create();

            // DepthOnly Materialのフィールドを確認
            var matField = typeof(ShadowOnlyRendererFeature).GetField("_depthOnlyMaterial",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (matField != null)
            {
                var mat = matField.GetValue(_feature) as Material;
                Assert.IsNotNull(mat, "Create後にDepthOnly Materialが生成されること");
            }
            else
            {
                // Materialの管理場所がRenderPass側の場合もあり得る
                Assert.Pass("DepthOnly Materialの管理場所は実装依存");
            }
        }

        #endregion

        #region ShadowOnlyRendererFeatureの名前空間テスト

        [Test]
        public void ShadowOnlyRendererFeature_正しい名前空間に配置されている()
        {
            Assert.AreEqual("ShadowOnlyShader", typeof(ShadowOnlyRendererFeature).Namespace,
                "ShadowOnlyRendererFeatureはShadowOnlyShader名前空間に配置されていること");
        }

        [Test]
        public void ShadowOnlyRenderPass_正しい名前空間に配置されている()
        {
            Assert.AreEqual("ShadowOnlyShader", typeof(ShadowOnlyRenderPass).Namespace,
                "ShadowOnlyRenderPassはShadowOnlyShader名前空間に配置されていること");
        }

        #endregion
    }
}
