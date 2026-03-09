using NUnit.Framework;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Editor
{
    /// <summary>
    /// VirtualLightのVP行列計算（Orthographic/Perspective）が正しい値を返すことを検証するユニットテスト。
    /// Requirements: 1.3, 3.5
    /// </summary>
    [TestFixture]
    public class VirtualLightMatrixTests
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

        #region View行列テスト

        [Test]
        public void UpdateMatrices_原点_Identity回転_View行列がZフリップされた単位行列に近い()
        {
            // Arrange: 原点、回転なし
            _gameObject.transform.position = Vector3.zero;
            _gameObject.transform.rotation = Quaternion.identity;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: View行列はZ軸が反転された単位行列
            Matrix4x4 view = _virtualLight.ViewMatrix;

            // Zフリップ行列 * Identity = Zフリップ行列
            // 対角成分は (1, 1, -1, 1)
            Assert.AreEqual(1f, view.m00, 0.001f, "m00は1であるべき");
            Assert.AreEqual(1f, view.m11, 0.001f, "m11は1であるべき");
            Assert.AreEqual(-1f, view.m22, 0.001f, "m22は-1であるべき（Z軸反転）");
            Assert.AreEqual(1f, view.m33, 0.001f, "m33は1であるべき");
        }

        [Test]
        public void UpdateMatrices_位置オフセット_View行列に平行移動が反映される()
        {
            // Arrange: (3, 5, 7)に配置
            _gameObject.transform.position = new Vector3(3f, 5f, 7f);
            _gameObject.transform.rotation = Quaternion.identity;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: View行列の平行移動成分を検証
            // worldToLocal * zFlip なので、平行移動成分は (-3, -5, 7)
            Matrix4x4 view = _virtualLight.ViewMatrix;
            Assert.AreEqual(-3f, view.m03, 0.001f, "X平行移動が反映されるべき");
            Assert.AreEqual(-5f, view.m13, 0.001f, "Y平行移動が反映されるべき");
            Assert.AreEqual(7f, view.m23, 0.001f, "Z平行移動はフリップされて反映されるべき");
        }

        [Test]
        public void UpdateMatrices_Y軸90度回転_View行列に回転が反映される()
        {
            // Arrange: Y軸90度回転
            _gameObject.transform.position = Vector3.zero;
            _gameObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: View行列がゼロでない非対角成分を持つことを検証
            Matrix4x4 view = _virtualLight.ViewMatrix;

            // Y軸90度回転の逆 * Zフリップ = 非自明な行列
            // m00とm22がゼロに近くなるはず（回転により軸が入れ替わる）
            Assert.AreEqual(0f, view.m00, 0.01f, "Y軸90度回転でm00は0に近いはず");
            Assert.AreEqual(1f, view.m11, 0.01f, "Y軸回転ではm11は不変");
        }

        #endregion

        #region Orthographic Projection行列テスト

        [Test]
        public void UpdateMatrices_Orthographic_正射影行列が生成される()
        {
            // Arrange
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 5f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: Orthographic行列の特性を検証
            Matrix4x4 proj = _virtualLight.ProjectionMatrix;

            // Matrix4x4.Orthoで生成される行列の参照値と比較
            Matrix4x4 expected = Matrix4x4.Ortho(-5f, 5f, -5f, 5f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, proj, 0.001f, "Orthographic Projection行列");
        }

        [Test]
        public void UpdateMatrices_Orthographic_サイズ変更_行列が更新される()
        {
            // Arrange
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 10f;
            _virtualLight.NearClipPlane = 1f;
            _virtualLight.FarClipPlane = 50f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert
            Matrix4x4 proj = _virtualLight.ProjectionMatrix;
            Matrix4x4 expected = Matrix4x4.Ortho(-10f, 10f, -10f, 10f, 1f, 50f);
            AssertMatricesAreEqual(expected, proj, 0.001f, "サイズ変更後のOrthographic Projection行列");
        }

        #endregion

        #region Perspective Projection行列テスト

        [Test]
        public void UpdateMatrices_Perspective_透視投影行列が生成される()
        {
            // Arrange
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: Perspective行列の参照値と比較（アスペクト比1:1）
            Matrix4x4 proj = _virtualLight.ProjectionMatrix;
            Matrix4x4 expected = Matrix4x4.Perspective(60f, 1f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, proj, 0.001f, "Perspective Projection行列");
        }

        [Test]
        public void UpdateMatrices_Perspective_FOV変更_行列が更新される()
        {
            // Arrange
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 90f;
            _virtualLight.NearClipPlane = 0.3f;
            _virtualLight.FarClipPlane = 200f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert
            Matrix4x4 proj = _virtualLight.ProjectionMatrix;
            Matrix4x4 expected = Matrix4x4.Perspective(90f, 1f, 0.3f, 200f);
            AssertMatricesAreEqual(expected, proj, 0.001f, "FOV変更後のPerspective Projection行列");
        }

        #endregion

        #region VP行列テスト

        [Test]
        public void UpdateMatrices_VP行列はProjection乗算Viewと一致する()
        {
            // Arrange
            _gameObject.transform.position = new Vector3(1f, 2f, 3f);
            _gameObject.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert: VP = P * V
            Matrix4x4 vp = _virtualLight.ViewProjectionMatrix;
            Matrix4x4 expected = _virtualLight.ProjectionMatrix * _virtualLight.ViewMatrix;
            AssertMatricesAreEqual(expected, vp, 0.001f, "VP行列はProjection * Viewに一致するべき");
        }

        [Test]
        public void UpdateMatrices_Orthographic_VP行列はProjection乗算Viewと一致する()
        {
            // Arrange
            _gameObject.transform.position = new Vector3(-2f, 4f, -6f);
            _gameObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 8f;
            _virtualLight.NearClipPlane = 0.5f;
            _virtualLight.FarClipPlane = 50f;

            // Act
            _virtualLight.UpdateMatrices();

            // Assert
            Matrix4x4 vp = _virtualLight.ViewProjectionMatrix;
            Matrix4x4 expected = _virtualLight.ProjectionMatrix * _virtualLight.ViewMatrix;
            AssertMatricesAreEqual(expected, vp, 0.001f, "Orthographic VP行列もProjection * Viewに一致するべき");
        }

        [Test]
        public void UpdateMatrices_ProjectionMode切り替え_行列が正しく更新される()
        {
            // Arrange & Act: Orthographic
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 5f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 orthoProj = _virtualLight.ProjectionMatrix;

            // Act: Perspectiveに切り替え
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 perspProj = _virtualLight.ProjectionMatrix;

            // Assert: 異なる行列が生成される
            Assert.AreNotEqual(orthoProj, perspProj, "ProjectionMode切り替えで異なるProjection行列が生成されるべき");
        }

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// 2つのMatrix4x4が各要素で指定精度以内であることを検証する。
        /// </summary>
        private static void AssertMatricesAreEqual(Matrix4x4 expected, Matrix4x4 actual, float tolerance, string message)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Assert.AreEqual(
                        expected[row, col],
                        actual[row, col],
                        tolerance,
                        $"{message}: [{row},{col}]の値が一致しない (期待値: {expected[row, col]}, 実際値: {actual[row, col]})"
                    );
                }
            }
        }

        #endregion
    }
}
