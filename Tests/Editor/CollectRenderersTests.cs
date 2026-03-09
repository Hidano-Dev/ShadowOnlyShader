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
