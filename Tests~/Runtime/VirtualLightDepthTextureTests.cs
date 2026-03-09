using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// VirtualLightの深度RenderTexture管理とキャスターRenderer収集の検証テスト (Task 2.3)
    /// 深度RenderTextureのライフサイクル管理（作成・再作成・破棄）と
    /// CasterRoot指定による子階層Renderer自動収集を検証する。
    /// Requirements: 3.6, 5.2, 5.5
    /// </summary>
    public class VirtualLightDepthTextureTests
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

        #region 深度RenderTexture作成テスト

        [Test]
        public void DepthRenderTexture_OnEnable後に作成される()
        {
            // OnEnableはAddComponent時に自動的に呼ばれる
            Assert.IsNotNull(_virtualLight.DepthRenderTexture,
                "OnEnable後にDepthRenderTextureが作成されていること");
        }

        [Test]
        public void DepthRenderTexture_デフォルト解像度で作成される()
        {
            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);
            Assert.AreEqual(1024, rt.width, "デフォルト解像度は1024");
            Assert.AreEqual(1024, rt.height, "深度テクスチャは正方形");
        }

        [Test]
        public void DepthRenderTexture_深度フォーマットを持つ()
        {
            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);
            // 深度テクスチャはdepthStencilFormatまたはdepthBufferBitsが設定されている
            Assert.Greater(rt.depth, 0, "深度バッファが設定されていること");
        }

        [Test]
        public void DepthRenderTexture_HideFlagsDontSaveで管理される()
        {
            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);
            Assert.IsTrue(
                (rt.hideFlags & HideFlags.DontSave) != 0,
                "RenderTextureにHideFlags.DontSaveが設定されていること");
        }

        [Test]
        public void DepthRenderTexture_指定解像度で作成される()
        {
            // 新しいVirtualLightを解像度512で作成
            var go = new GameObject("TestVL_512");
            var vl = go.AddComponent<VirtualLight>();
            vl.TextureResolution = 512;
            // 解像度変更を反映するためにEnsureDepthTextureを呼ぶ
            vl.EnsureDepthTexture();

            RenderTexture rt = vl.DepthRenderTexture;
            Assert.IsNotNull(rt);
            Assert.AreEqual(512, rt.width, "指定した解像度512で作成されること");
            Assert.AreEqual(512, rt.height, "正方形テクスチャ");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region 深度RenderTexture解像度変更時の再作成テスト

        [Test]
        public void DepthRenderTexture_解像度変更時に再作成される()
        {
            RenderTexture rtBefore = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rtBefore);
            Assert.AreEqual(1024, rtBefore.width);

            _virtualLight.TextureResolution = 2048;
            _virtualLight.EnsureDepthTexture();

            RenderTexture rtAfter = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rtAfter);
            Assert.AreEqual(2048, rtAfter.width, "新しい解像度で再作成されること");
        }

        [Test]
        public void DepthRenderTexture_同一解像度では再作成されない()
        {
            RenderTexture rtBefore = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rtBefore);

            _virtualLight.EnsureDepthTexture();

            RenderTexture rtAfter = _virtualLight.DepthRenderTexture;
            Assert.AreSame(rtBefore, rtAfter, "同一解像度では同じRenderTextureインスタンスを保持すること");
        }

        [Test]
        public void DepthRenderTexture_再作成後もHideFlagsDontSaveが設定される()
        {
            _virtualLight.TextureResolution = 512;
            _virtualLight.EnsureDepthTexture();

            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);
            Assert.IsTrue(
                (rt.hideFlags & HideFlags.DontSave) != 0,
                "再作成後もHideFlags.DontSaveが設定されていること");
        }

        #endregion

        #region 深度RenderTexture解像度クランプテスト

        [Test]
        public void TextureResolution_64未満は64にクランプされる()
        {
            _virtualLight.TextureResolution = 32;
            Assert.AreEqual(64, _virtualLight.TextureResolution);
        }

        [Test]
        public void TextureResolution_4096超は4096にクランプされる()
        {
            _virtualLight.TextureResolution = 8192;
            Assert.AreEqual(4096, _virtualLight.TextureResolution);
        }

        [Test]
        public void TextureResolution_有効範囲内の値はそのまま設定される()
        {
            _virtualLight.TextureResolution = 256;
            Assert.AreEqual(256, _virtualLight.TextureResolution);
        }

        [Test]
        public void TextureResolution_クランプ後の解像度でRenderTextureが作成される()
        {
            _virtualLight.TextureResolution = 32; // 64にクランプ
            _virtualLight.EnsureDepthTexture();

            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);
            Assert.AreEqual(64, rt.width, "クランプされた解像度64でRenderTextureが作成されること");
        }

        #endregion

        #region 深度RenderTexture破棄テスト

        [Test]
        public void DepthRenderTexture_OnDisableで破棄される()
        {
            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);

            // OnDisableをトリガーする
            _virtualLight.enabled = false;

            Assert.IsNull(_virtualLight.DepthRenderTexture,
                "OnDisable後にDepthRenderTextureがnullになること");
        }

        [Test]
        public void DepthRenderTexture_再度OnEnableで再作成される()
        {
            // OnDisable
            _virtualLight.enabled = false;
            Assert.IsNull(_virtualLight.DepthRenderTexture);

            // OnEnable
            _virtualLight.enabled = true;

            Assert.IsNotNull(_virtualLight.DepthRenderTexture,
                "再度OnEnable後にDepthRenderTextureが再作成されること");
        }

        [Test]
        public void DepthRenderTexture_GameObjectDestroyで破棄される()
        {
            RenderTexture rt = _virtualLight.DepthRenderTexture;
            Assert.IsNotNull(rt);

            // RenderTextureの参照を保持してからGameObjectを破棄
            Object.DestroyImmediate(_gameObject);
            _gameObject = null;

            // Unityの仕様上、DestroyImmediateされたオブジェクトはnullと比較でtrueを返す
            Assert.IsTrue(rt == null, "GameObjectのDestroyでRenderTextureが破棄されること");
        }

        #endregion

        #region CasterRoot Renderer収集テスト

        [Test]
        public void CollectRenderers_CasterRootがnullの場合は空リストを返す()
        {
            _virtualLight.CasterRoot = null;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(0, _virtualLight.CasterRenderers.Count,
                "CasterRootがnullの場合、CasterRenderersは空であること");
        }

        [Test]
        public void CollectRenderers_CasterRoot自体のRendererを収集する()
        {
            var casterRoot = new GameObject("CasterRoot");
            casterRoot.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = casterRoot;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count,
                "CasterRoot自体のRendererが収集されること");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void CollectRenderers_子階層のRendererを全て収集する()
        {
            var casterRoot = new GameObject("CasterRoot");
            var child1 = new GameObject("Child1");
            child1.AddComponent<MeshRenderer>();
            child1.transform.SetParent(casterRoot.transform);

            var child2 = new GameObject("Child2");
            child2.AddComponent<MeshRenderer>();
            child2.transform.SetParent(casterRoot.transform);

            _virtualLight.CasterRoot = casterRoot;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "子階層のRenderer2つが収集されること");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void CollectRenderers_深い階層のRendererも収集する()
        {
            var casterRoot = new GameObject("CasterRoot");
            var child = new GameObject("Child");
            child.transform.SetParent(casterRoot.transform);
            var grandChild = new GameObject("GrandChild");
            grandChild.AddComponent<MeshRenderer>();
            grandChild.transform.SetParent(child.transform);

            _virtualLight.CasterRoot = casterRoot;
            _virtualLight.CollectRenderers();

            Assert.GreaterOrEqual(_virtualLight.CasterRenderers.Count, 1,
                "深い階層のRendererも収集されること");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void CollectRenderers_SkinnedMeshRendererも収集する()
        {
            var casterRoot = new GameObject("CasterRoot");
            var child = new GameObject("SkinnedChild");
            child.AddComponent<SkinnedMeshRenderer>();
            child.transform.SetParent(casterRoot.transform);

            _virtualLight.CasterRoot = casterRoot;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count,
                "SkinnedMeshRendererも収集されること");
            Assert.IsInstanceOf<SkinnedMeshRenderer>(_virtualLight.CasterRenderers[0]);

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void CollectRenderers_複数種類のRendererを混在して収集する()
        {
            var casterRoot = new GameObject("CasterRoot");

            var meshChild = new GameObject("MeshChild");
            meshChild.AddComponent<MeshRenderer>();
            meshChild.transform.SetParent(casterRoot.transform);

            var skinnedChild = new GameObject("SkinnedChild");
            skinnedChild.AddComponent<SkinnedMeshRenderer>();
            skinnedChild.transform.SetParent(casterRoot.transform);

            _virtualLight.CasterRoot = casterRoot;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "MeshRendererとSkinnedMeshRendererの両方が収集されること");

            Object.DestroyImmediate(casterRoot);
        }

        [Test]
        public void CollectRenderers_再収集で前回の結果がクリアされる()
        {
            var casterRoot1 = new GameObject("CasterRoot1");
            casterRoot1.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = casterRoot1;
            _virtualLight.CollectRenderers();
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count);

            // CasterRootを変更して再収集
            var casterRoot2 = new GameObject("CasterRoot2");
            var child1 = new GameObject("Child1");
            child1.AddComponent<MeshRenderer>();
            child1.transform.SetParent(casterRoot2.transform);
            var child2 = new GameObject("Child2");
            child2.AddComponent<MeshRenderer>();
            child2.transform.SetParent(casterRoot2.transform);

            _virtualLight.CasterRoot = casterRoot2;
            _virtualLight.CollectRenderers();

            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "再収集で前回の結果がクリアされ、新しいCasterRoot配下のみが含まれること");

            Object.DestroyImmediate(casterRoot1);
            Object.DestroyImmediate(casterRoot2);
        }

        [Test]
        public void CasterRenderers_読み取り専用リストを返す()
        {
            Assert.IsInstanceOf<System.Collections.Generic.IReadOnlyList<Renderer>>(
                _virtualLight.CasterRenderers,
                "CasterRenderersはIReadOnlyList<Renderer>を返すこと");
        }

        #endregion

        #region OnEnable時のRenderer自動収集テスト

        [Test]
        public void OnEnable_CasterRootが設定済みならRendererを自動収集する()
        {
            // 先にCasterRootを設定した状態のVirtualLightを作成
            var casterRoot = new GameObject("CasterRoot");
            casterRoot.AddComponent<MeshRenderer>();

            // 新しいGameObjectにVirtualLightを追加（OnEnableが自動で走る）
            var go = new GameObject("TestVL");
            var vl = go.AddComponent<VirtualLight>();
            vl.CasterRoot = casterRoot;

            // OnEnableで自動収集されるためには明示的にCollectRenderersを呼ぶか、
            // あるいは再度enableを切り替える
            vl.enabled = false;
            vl.enabled = true;

            Assert.AreEqual(1, vl.CasterRenderers.Count,
                "OnEnableでCasterRoot配下のRendererが自動収集されること");

            Object.DestroyImmediate(casterRoot);
            Object.DestroyImmediate(go);
        }

        #endregion

        #region EnsureDepthTextureメソッドテスト

        [Test]
        public void EnsureDepthTexture_メソッドが公開されている()
        {
            // publicメソッドとして存在することの検証
            var method = typeof(VirtualLight).GetMethod("EnsureDepthTexture");
            Assert.IsNotNull(method, "EnsureDepthTextureメソッドが公開されていること");
        }

        [Test]
        public void EnsureDepthTexture_RenderTextureが未作成なら作成する()
        {
            // OnDisableで破棄してからEnsureDepthTextureで再作成
            _virtualLight.enabled = false;
            Assert.IsNull(_virtualLight.DepthRenderTexture);

            _virtualLight.enabled = true;
            _virtualLight.EnsureDepthTexture();

            Assert.IsNotNull(_virtualLight.DepthRenderTexture,
                "EnsureDepthTextureでRenderTextureが作成されること");
        }

        #endregion

        #region TextureResolution SerializeField検証テスト

        [Test]
        public void TextureResolution_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_textureResolution",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_textureResolutionフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_textureResolutionにSerializeField属性が付与されていること");
        }

        [Test]
        public void CasterRoot_SerializeField属性が付与されている()
        {
            var field = typeof(VirtualLight).GetField("_casterRoot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_casterRootフィールドが存在すること");
            Assert.IsTrue(
                System.Attribute.IsDefined(field, typeof(SerializeField)),
                "_casterRootにSerializeField属性が付与されていること");
        }

        #endregion
    }
}
