using NUnit.Framework;
using ShadowOnlyShader;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Runtime
{
    /// <summary>
    /// VirtualLightの投影パラメータとVP行列計算の検証テスト (Task 2.1)
    /// </summary>
    public class VirtualLightProjectionTests
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

        #region IVirtualLight Implementation Tests

        [Test]
        public void VirtualLight_ImplementsIVirtualLight()
        {
            Assert.IsInstanceOf<IVirtualLight>(_virtualLight);
        }

        [Test]
        public void VirtualLight_IsMonoBehaviour()
        {
            Assert.IsInstanceOf<MonoBehaviour>(_virtualLight);
        }

        #endregion

        #region Default Values Tests

        [Test]
        public void ProjectionMode_DefaultIsOrthographic()
        {
            Assert.AreEqual(ProjectionMode.Orthographic, _virtualLight.ProjectionMode);
        }

        [Test]
        public void FieldOfView_DefaultIs60()
        {
            Assert.AreEqual(60f, _virtualLight.FieldOfView);
        }

        [Test]
        public void OrthographicSize_DefaultIs5()
        {
            Assert.AreEqual(5f, _virtualLight.OrthographicSize);
        }

        [Test]
        public void NearClipPlane_DefaultIs0001()
        {
            Assert.AreEqual(0.001f, _virtualLight.NearClipPlane, 0.0001f);
        }

        [Test]
        public void FarClipPlane_DefaultIs100()
        {
            Assert.AreEqual(100f, _virtualLight.FarClipPlane);
        }

        #endregion

        #region Parameter Validation (Clamping) Tests

        [Test]
        public void FieldOfView_ClampsToMinimum1()
        {
            _virtualLight.FieldOfView = -10f;
            Assert.AreEqual(1f, _virtualLight.FieldOfView);
        }

        [Test]
        public void FieldOfView_ClampsToMaximum179()
        {
            _virtualLight.FieldOfView = 200f;
            Assert.AreEqual(179f, _virtualLight.FieldOfView);
        }

        [Test]
        public void FieldOfView_AcceptsValidValue()
        {
            _virtualLight.FieldOfView = 90f;
            Assert.AreEqual(90f, _virtualLight.FieldOfView);
        }

        [Test]
        public void OrthographicSize_ClampsToMinimum()
        {
            _virtualLight.OrthographicSize = -5f;
            Assert.Greater(_virtualLight.OrthographicSize, 0f);
        }

        [Test]
        public void OrthographicSize_AcceptsValidValue()
        {
            _virtualLight.OrthographicSize = 10f;
            Assert.AreEqual(10f, _virtualLight.OrthographicSize);
        }

        [Test]
        public void NearClipPlane_ClampsToMinimum()
        {
            _virtualLight.NearClipPlane = -1f;
            Assert.Greater(_virtualLight.NearClipPlane, 0f);
        }

        [Test]
        public void NearClipPlane_AcceptsValidValue()
        {
            _virtualLight.NearClipPlane = 0.5f;
            Assert.AreEqual(0.5f, _virtualLight.NearClipPlane, 0.001f);
        }

        [Test]
        public void FarClipPlane_MustBeGreaterThanNearClipPlane()
        {
            _virtualLight.NearClipPlane = 10f;
            _virtualLight.FarClipPlane = 5f;
            Assert.Greater(_virtualLight.FarClipPlane, _virtualLight.NearClipPlane);
        }

        [Test]
        public void FarClipPlane_AcceptsValidValue()
        {
            _virtualLight.FarClipPlane = 200f;
            Assert.AreEqual(200f, _virtualLight.FarClipPlane);
        }

        #endregion

        #region View Matrix Tests

        [Test]
        public void ViewMatrix_IsNotIdentityByDefault()
        {
            _virtualLight.UpdateMatrices();
            // At origin looking forward, view matrix should still be a valid non-identity matrix
            // (Unity's view matrix includes the z-flip for right-handed coordinate system)
            Assert.AreNotEqual(Matrix4x4.zero, _virtualLight.ViewMatrix);
        }

        [Test]
        public void ViewMatrix_ReflectsTransformPosition()
        {
            _gameObject.transform.position = new Vector3(10f, 20f, 30f);
            _virtualLight.UpdateMatrices();

            Matrix4x4 viewMatrix = _virtualLight.ViewMatrix;

            // The view matrix should transform the light's world position to origin
            // Verify that the inverse of the view matrix gives us back the world position
            Matrix4x4 inverseView = viewMatrix.inverse;
            Vector3 extractedPos = new Vector3(inverseView.m03, inverseView.m13, inverseView.m23);
            Assert.AreEqual(10f, extractedPos.x, 0.01f, "View matrix should encode x position");
            Assert.AreEqual(20f, extractedPos.y, 0.01f, "View matrix should encode y position");
            Assert.AreEqual(30f, extractedPos.z, 0.01f, "View matrix should encode z position");
        }

        [Test]
        public void ViewMatrix_ReflectsTransformRotation()
        {
            _gameObject.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
            _virtualLight.UpdateMatrices();

            Matrix4x4 viewMatrix = _virtualLight.ViewMatrix;
            // View matrix should not be identity when rotated
            Assert.AreNotEqual(Matrix4x4.identity, viewMatrix);
        }

        [Test]
        public void ViewMatrix_ChangesWhenTransformMoves()
        {
            _virtualLight.UpdateMatrices();
            Matrix4x4 viewBefore = _virtualLight.ViewMatrix;

            _gameObject.transform.position = new Vector3(5f, 5f, 5f);
            _virtualLight.UpdateMatrices();
            Matrix4x4 viewAfter = _virtualLight.ViewMatrix;

            Assert.AreNotEqual(viewBefore, viewAfter, "View matrix should change when Transform moves");
        }

        #endregion

        #region Projection Matrix Tests - Orthographic

        [Test]
        public void ProjectionMatrix_Orthographic_IsNotZero()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 5f;
            _virtualLight.UpdateMatrices();

            Assert.AreNotEqual(Matrix4x4.zero, _virtualLight.ProjectionMatrix);
        }

        [Test]
        public void ProjectionMatrix_Orthographic_ChangesWithSize()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;

            _virtualLight.OrthographicSize = 5f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj5 = _virtualLight.ProjectionMatrix;

            _virtualLight.OrthographicSize = 10f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj10 = _virtualLight.ProjectionMatrix;

            Assert.AreNotEqual(proj5, proj10, "Projection matrix should change with OrthographicSize");
        }

        [Test]
        public void ProjectionMatrix_Orthographic_HasCorrectStructure()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 5f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;
            _virtualLight.UpdateMatrices();

            Matrix4x4 proj = _virtualLight.ProjectionMatrix;

            // Orthographic projection: perspective row should be (0,0,0,1)
            Assert.AreEqual(0f, proj.m30, 0.001f);
            Assert.AreEqual(0f, proj.m31, 0.001f);
            Assert.AreEqual(0f, proj.m32, 0.001f);
            Assert.AreEqual(1f, proj.m33, 0.001f);
        }

        #endregion

        #region Projection Matrix Tests - Perspective

        [Test]
        public void ProjectionMatrix_Perspective_IsNotZero()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.UpdateMatrices();

            Assert.AreNotEqual(Matrix4x4.zero, _virtualLight.ProjectionMatrix);
        }

        [Test]
        public void ProjectionMatrix_Perspective_ChangesWithFOV()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;

            _virtualLight.FieldOfView = 30f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj30 = _virtualLight.ProjectionMatrix;

            _virtualLight.FieldOfView = 90f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj90 = _virtualLight.ProjectionMatrix;

            Assert.AreNotEqual(proj30, proj90, "Projection matrix should change with FOV");
        }

        [Test]
        public void ProjectionMatrix_Perspective_HasCorrectStructure()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;
            _virtualLight.UpdateMatrices();

            Matrix4x4 proj = _virtualLight.ProjectionMatrix;

            // Perspective projection: m32 should be -1 (or similar non-zero value depending on API)
            // and m33 should be 0
            Assert.AreNotEqual(0f, proj.m32, "Perspective m32 should be non-zero");
            Assert.AreEqual(0f, proj.m33, 0.001f, "Perspective m33 should be 0");
        }

        #endregion

        #region Projection Mode Switching Tests

        [Test]
        public void ProjectionMatrix_DiffersBetweenOrthographicAndPerspective()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.UpdateMatrices();
            Matrix4x4 orthoProj = _virtualLight.ProjectionMatrix;

            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.UpdateMatrices();
            Matrix4x4 perspProj = _virtualLight.ProjectionMatrix;

            Assert.AreNotEqual(orthoProj, perspProj,
                "Orthographic and Perspective projection matrices should differ");
        }

        [Test]
        public void ProjectionMode_CanSwitchBetweenModes()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            Assert.AreEqual(ProjectionMode.Perspective, _virtualLight.ProjectionMode);

            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            Assert.AreEqual(ProjectionMode.Orthographic, _virtualLight.ProjectionMode);
        }

        #endregion

        #region VP Matrix Tests

        [Test]
        public void ViewProjectionMatrix_IsProductOfViewAndProjection()
        {
            _gameObject.transform.position = new Vector3(5f, 10f, 15f);
            _gameObject.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = _virtualLight.ProjectionMatrix * _virtualLight.ViewMatrix;
            Matrix4x4 actual = _virtualLight.ViewProjectionMatrix;

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(expected[i], actual[i], 0.001f,
                    $"VP matrix element [{i}] should equal P * V");
            }
        }

        [Test]
        public void ViewProjectionMatrix_UpdatesAfterTransformChange()
        {
            _virtualLight.UpdateMatrices();
            Matrix4x4 vpBefore = _virtualLight.ViewProjectionMatrix;

            _gameObject.transform.position = new Vector3(1f, 2f, 3f);
            _virtualLight.UpdateMatrices();
            Matrix4x4 vpAfter = _virtualLight.ViewProjectionMatrix;

            Assert.AreNotEqual(vpBefore, vpAfter, "VP matrix should update after Transform change");
        }

        [Test]
        public void ViewProjectionMatrix_UpdatesAfterProjectionParameterChange()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 vpBefore = _virtualLight.ViewProjectionMatrix;

            _virtualLight.FieldOfView = 90f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 vpAfter = _virtualLight.ViewProjectionMatrix;

            Assert.AreNotEqual(vpBefore, vpAfter, "VP matrix should update after FOV change");
        }

        #endregion

        #region Near/Far Clip Plane Interaction Tests

        [Test]
        public void NearClipPlane_SetBeforeFar_WorksCorrectly()
        {
            _virtualLight.NearClipPlane = 1f;
            _virtualLight.FarClipPlane = 50f;
            Assert.AreEqual(1f, _virtualLight.NearClipPlane, 0.001f);
            Assert.AreEqual(50f, _virtualLight.FarClipPlane, 0.001f);
        }

        [Test]
        public void ProjectionMatrix_ChangesWithNearFar()
        {
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj1 = _virtualLight.ProjectionMatrix;

            _virtualLight.NearClipPlane = 1f;
            _virtualLight.FarClipPlane = 50f;
            _virtualLight.UpdateMatrices();
            Matrix4x4 proj2 = _virtualLight.ProjectionMatrix;

            Assert.AreNotEqual(proj1, proj2, "Projection matrix should change with Near/Far values");
        }

        #endregion
    }
}
