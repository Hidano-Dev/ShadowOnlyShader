using NUnit.Framework;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Editor
{
    /// <summary>
    /// CollectRenderersが子階層のRendererを正しく収集することを検証するユニットテスト。
    /// Requirements: 5.5
    /// </summary>
    [TestFixture]
    public class CollectRenderersTests
    {
        private GameObject _virtualLightGO;
        private VirtualLight _virtualLight;
        private GameObject _casterRoot;
        private GameObject _managerGO;

        [SetUp]
        public void SetUp()
        {
            _virtualLightGO = new GameObject("TestVirtualLight");
            _virtualLight = _virtualLightGO.AddComponent<VirtualLight>();
            _casterRoot = new GameObject("CasterRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_virtualLightGO != null)
                Object.DestroyImmediate(_virtualLightGO);
            if (_casterRoot != null)
                Object.DestroyImmediate(_casterRoot);
            if (_managerGO != null)
                Object.DestroyImmediate(_managerGO);
        }

        /// <summary>
        /// VirtualLightをShadowOnlyManagerの子に配置した状態を作る。
        /// Manager共通CasterRoot（DefaultCasterRoot）のフォールバック検証に使用する。
        /// </summary>
        private ShadowOnlyManager SetUpManagerAsParent()
        {
            _managerGO = new GameObject("TestManager");
            var manager = _managerGO.AddComponent<ShadowOnlyManager>();
            _virtualLightGO.transform.SetParent(_managerGO.transform);
            return manager;
        }

        [Test]
        public void CollectRenderers_CasterRootがnull_Rendererリストが空になる()
        {
            // Arrange
            _virtualLight.CasterRoot = null;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(0, _virtualLight.CasterRenderers.Count,
                "CasterRootがnullの場合、CasterRenderersは空であるべき");
        }

        [Test]
        public void CollectRenderers_CasterRoot直下にRenderer_1つ収集される()
        {
            // Arrange: CasterRootにMeshRendererを追加
            _casterRoot.AddComponent<MeshFilter>();
            _casterRoot.AddComponent<MeshRenderer>();
            _virtualLight.CasterRoot = _casterRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count,
                "CasterRoot自身のRendererが収集されるべき");
        }

        [Test]
        public void CollectRenderers_子階層のRenderer_正しく収集される()
        {
            // Arrange: 子階層にRendererを追加
            var child1 = new GameObject("Child1");
            child1.transform.SetParent(_casterRoot.transform);
            child1.AddComponent<MeshFilter>();
            child1.AddComponent<MeshRenderer>();

            var child2 = new GameObject("Child2");
            child2.transform.SetParent(_casterRoot.transform);
            child2.AddComponent<MeshFilter>();
            child2.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = _casterRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "子階層の2つのRendererが収集されるべき");
        }

        [Test]
        public void CollectRenderers_ネストされた子階層_再帰的に収集される()
        {
            // Arrange: ネストされた構造を作成
            // CasterRoot
            //   └ Child1 (Renderer)
            //       └ GrandChild1 (Renderer)
            var child1 = new GameObject("Child1");
            child1.transform.SetParent(_casterRoot.transform);
            child1.AddComponent<MeshFilter>();
            child1.AddComponent<MeshRenderer>();

            var grandChild = new GameObject("GrandChild1");
            grandChild.transform.SetParent(child1.transform);
            grandChild.AddComponent<MeshFilter>();
            grandChild.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = _casterRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "ネストされた子階層のRendererも再帰的に収集されるべき");
        }

        [Test]
        public void CollectRenderers_Rendererのない子オブジェクト_スキップされる()
        {
            // Arrange: Rendererのない子とRendererのある子を混在
            var childNoRenderer = new GameObject("ChildNoRenderer");
            childNoRenderer.transform.SetParent(_casterRoot.transform);

            var childWithRenderer = new GameObject("ChildWithRenderer");
            childWithRenderer.transform.SetParent(_casterRoot.transform);
            childWithRenderer.AddComponent<MeshFilter>();
            childWithRenderer.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = _casterRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count,
                "Rendererのある子のみ収集されるべき");
        }

        [Test]
        public void CollectRenderers_CasterRoot変更_新しいRootのRendererが収集される()
        {
            // Arrange: 最初のCasterRoot
            var child1 = new GameObject("Child1");
            child1.transform.SetParent(_casterRoot.transform);
            child1.AddComponent<MeshFilter>();
            child1.AddComponent<MeshRenderer>();
            _virtualLight.CasterRoot = _casterRoot;
            _virtualLight.CollectRenderers();
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count);

            // Act: 新しいCasterRootに変更
            var newRoot = new GameObject("NewCasterRoot");
            var newChild1 = new GameObject("NewChild1");
            newChild1.transform.SetParent(newRoot.transform);
            newChild1.AddComponent<MeshFilter>();
            newChild1.AddComponent<MeshRenderer>();
            var newChild2 = new GameObject("NewChild2");
            newChild2.transform.SetParent(newRoot.transform);
            newChild2.AddComponent<MeshFilter>();
            newChild2.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = newRoot;
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "新しいCasterRoot配下のRendererが収集されるべき");

            // Cleanup
            Object.DestroyImmediate(newRoot);
        }

        [Test]
        public void CollectRenderers_再呼び出し_前回のリストがクリアされる()
        {
            // Arrange: 最初に1つ
            var child1 = new GameObject("Child1");
            child1.transform.SetParent(_casterRoot.transform);
            child1.AddComponent<MeshFilter>();
            child1.AddComponent<MeshRenderer>();
            _virtualLight.CasterRoot = _casterRoot;
            _virtualLight.CollectRenderers();
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count);

            // Act: CasterRootをnullにしてから再収集
            _virtualLight.CasterRoot = null;
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(0, _virtualLight.CasterRenderers.Count,
                "再収集時に前回のリストがクリアされるべき");
        }

        [Test]
        public void CollectRenderers_個別未設定_Manager共通のDefaultCasterRootが使用される()
        {
            // Arrange: Manager側にのみキャスタールートを設定
            var manager = SetUpManagerAsParent();
            _casterRoot.AddComponent<MeshFilter>();
            _casterRoot.AddComponent<MeshRenderer>();
            manager.DefaultCasterRoot = _casterRoot;
            _virtualLight.CasterRoot = null;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(_casterRoot, _virtualLight.EffectiveCasterRoot,
                "個別未設定時はManagerのDefaultCasterRootが実効ルートになるべき");
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count,
                "ManagerのDefaultCasterRoot配下のRendererが収集されるべき");
        }

        [Test]
        public void CollectRenderers_個別指定あり_Manager共通より優先される()
        {
            // Arrange: Manager共通とVirtualLight個別の両方を設定
            var manager = SetUpManagerAsParent();
            manager.DefaultCasterRoot = _casterRoot;

            var overrideRoot = new GameObject("OverrideRoot");
            overrideRoot.AddComponent<MeshFilter>();
            overrideRoot.AddComponent<MeshRenderer>();
            var overrideChild = new GameObject("OverrideChild");
            overrideChild.transform.SetParent(overrideRoot.transform);
            overrideChild.AddComponent<MeshFilter>();
            overrideChild.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = overrideRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(overrideRoot, _virtualLight.EffectiveCasterRoot,
                "個別指定がある場合はそちらが実効ルートになるべき");
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "個別指定したルート配下のRendererが収集されるべき");

            // Cleanup
            Object.DestroyImmediate(overrideRoot);
        }

        [Test]
        public void CollectRenderers_Manager共通のDefaultCasterRoot変更_再収集される()
        {
            // Arrange: 最初のDefaultCasterRootで収集
            var manager = SetUpManagerAsParent();
            _casterRoot.AddComponent<MeshFilter>();
            _casterRoot.AddComponent<MeshRenderer>();
            manager.DefaultCasterRoot = _casterRoot;
            _virtualLight.CollectRenderers();
            Assert.AreEqual(1, _virtualLight.CasterRenderers.Count);

            // Act: Manager側のDefaultCasterRootを差し替えて再収集
            //（VirtualLight側にdirty通知は入らないが、実効ルートの変化で検出される）
            var newRoot = new GameObject("NewDefaultRoot");
            var newChild1 = new GameObject("NewChild1");
            newChild1.transform.SetParent(newRoot.transform);
            newChild1.AddComponent<MeshFilter>();
            newChild1.AddComponent<MeshRenderer>();
            var newChild2 = new GameObject("NewChild2");
            newChild2.transform.SetParent(newRoot.transform);
            newChild2.AddComponent<MeshFilter>();
            newChild2.AddComponent<MeshRenderer>();

            manager.DefaultCasterRoot = newRoot;
            _virtualLight.CollectRenderers();

            // Assert
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "DefaultCasterRootの差し替えが自動検出され、新しいルートから再収集されるべき");

            // Cleanup
            Object.DestroyImmediate(newRoot);
        }

        [Test]
        public void EffectiveCasterRoot_Managerなし個別未設定_nullを返す()
        {
            // Arrange
            _virtualLight.CasterRoot = null;

            // Act & Assert
            Assert.IsNull(_virtualLight.EffectiveCasterRoot,
                "Manager配下になく個別指定もない場合、実効ルートはnullであるべき");
        }

        [Test]
        public void CollectRenderers_CasterRoot自身にもRendererがある場合_自身も含まれる()
        {
            // Arrange: CasterRoot自身とその子の両方にRendererを追加
            _casterRoot.AddComponent<MeshFilter>();
            _casterRoot.AddComponent<MeshRenderer>();

            var child = new GameObject("Child");
            child.transform.SetParent(_casterRoot.transform);
            child.AddComponent<MeshFilter>();
            child.AddComponent<MeshRenderer>();

            _virtualLight.CasterRoot = _casterRoot;

            // Act
            _virtualLight.CollectRenderers();

            // Assert: GetComponentsInChildrenはルート自身も含む
            Assert.AreEqual(2, _virtualLight.CasterRenderers.Count,
                "CasterRoot自身のRendererも収集に含まれるべき");
        }
    }
}
