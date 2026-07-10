using NUnit.Framework;
using UnityEngine;

namespace ShadowOnlyShader.Tests.Editor
{
    /// <summary>
    /// Fit To Casters（投影範囲のキャスター自動追従）の検証。
    /// オフセンター正射影の生成、マージン、テクセルスナップ、
    /// フォールバック、非Orthographicモードでの無視を確認する。
    /// </summary>
    [TestFixture]
    public class VirtualLightFitToCastersTests
    {
        private const int Resolution = 1024;

        private GameObject _lightObject;
        private VirtualLight _virtualLight;
        private GameObject _casterRoot;

        [SetUp]
        public void SetUp()
        {
            _lightObject = new GameObject("TestVirtualLight");
            _virtualLight = _lightObject.AddComponent<VirtualLight>();
            _virtualLight.ProjectionMode = ProjectionMode.Orthographic;
            _virtualLight.OrthographicSize = 5f;
            _virtualLight.NearClipPlane = 0.1f;
            _virtualLight.FarClipPlane = 100f;
            _virtualLight.TextureResolution = Resolution;

            // 真上から見下ろすライト（ビュー空間XY = ワールドXZ）
            _lightObject.transform.position = new Vector3(0f, 10f, 0f);
            _lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_lightObject != null) Object.DestroyImmediate(_lightObject);
            if (_casterRoot != null) Object.DestroyImmediate(_casterRoot);
        }

        /// <summary>
        /// キャスタールート配下にキューブを作成する。初回呼び出しでルートを生成し
        /// VirtualLightのCasterRootに設定する。
        /// </summary>
        private GameObject CreateCasterCube(Vector3 position, Vector3 scale)
        {
            if (_casterRoot == null)
            {
                _casterRoot = new GameObject("CasterRoot");
                _virtualLight.CasterRoot = _casterRoot;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_casterRoot.transform);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            return cube;
        }

        /// <summary>Orthographic行列から投影ウィンドウの半幅・半高・中心を復元する。</summary>
        private static void DecodeOrthoWindow(
            Matrix4x4 proj, out float halfWidth, out float halfHeight, out Vector2 center)
        {
            // Matrix4x4.Ortho: m00 = 2/(r-l), m03 = -(r+l)/(r-l)
            halfWidth = 1f / proj.m00;
            halfHeight = 1f / proj.m11;
            center = new Vector2(-proj.m03 * halfWidth, -proj.m13 * halfHeight);
        }

        [Test]
        public void Fit無効_OrthographicSizeによる中心正射影が生成される()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            _virtualLight.FitToCasters = false;

            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = Matrix4x4.Ortho(-5f, 5f, -5f, 5f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, _virtualLight.ProjectionMatrix, 0.001f,
                "Fit無効時は従来のOrthographicSize行列であるべき");
        }

        [Test]
        public void Fit有効_キャスター不在_OrthographicSizeにフォールバックする()
        {
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = Matrix4x4.Ortho(-5f, 5f, -5f, 5f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, _virtualLight.ProjectionMatrix, 0.001f,
                "キャスター不在時はOrthographicSizeにフォールバックするべき");
        }

        [Test]
        public void Fit有効_無効なRendererのみ_OrthographicSizeにフォールバックする()
        {
            var cube = CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            cube.GetComponent<MeshRenderer>().enabled = false;
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = Matrix4x4.Ortho(-5f, 5f, -5f, 5f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, _virtualLight.ProjectionMatrix, 0.001f,
                "無効なRendererしかない場合はフォールバックするべき");
        }

        [Test]
        public void Fit有効_全キャスターのBoundsがNDCのXY範囲に収まる()
        {
            var cube1 = CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            var cube2 = CreateCasterCube(new Vector3(-4f, 0.5f, -1f), new Vector3(2f, 1f, 0.5f));
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            Matrix4x4 vp = _virtualLight.ViewProjectionMatrix;
            foreach (var cube in new[] { cube1, cube2 })
            {
                Bounds b = cube.GetComponent<Renderer>().bounds;
                Vector3 bMin = b.min;
                Vector3 bMax = b.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = new Vector3(
                        (corner & 1) == 0 ? bMin.x : bMax.x,
                        (corner & 2) == 0 ? bMin.y : bMax.y,
                        (corner & 4) == 0 ? bMin.z : bMax.z);
                    Vector3 ndc = vp.MultiplyPoint3x4(p);
                    Assert.LessOrEqual(Mathf.Abs(ndc.x), 1f,
                        $"{cube.name}の頂点{p}のNDC.xが投影範囲に収まるべき");
                    Assert.LessOrEqual(Mathf.Abs(ndc.y), 1f,
                        $"{cube.name}の頂点{p}のNDC.yが投影範囲に収まるべき");
                }
            }
        }

        [Test]
        public void Fit有効_投影ウィンドウは正方形になる()
        {
            // 横長のキャスター配置（X方向に広い）
            CreateCasterCube(new Vector3(6f, 0f, 0f), Vector3.one);
            CreateCasterCube(new Vector3(-6f, 0f, 1f), Vector3.one);
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            DecodeOrthoWindow(_virtualLight.ProjectionMatrix,
                out float halfWidth, out float halfHeight, out _);
            Assert.AreEqual(halfWidth, halfHeight, 0.001f,
                "投影ウィンドウは正方形（テクセルが正方形）であるべき");
        }

        [Test]
        public void Fit有効_マージンが確保される()
        {
            // 1x1x1キューブが原点: ビュー空間XYの半径は0.5
            CreateCasterCube(Vector3.zero, Vector3.one);
            _virtualLight.FitToCasters = true;
            _virtualLight.BlurRadius = 1f;

            _virtualLight.UpdateMatrices();

            DecodeOrthoWindow(_virtualLight.ProjectionMatrix,
                out float halfWidth, out _, out _);

            // 片側10%マージン + ブラーカーネル余白の分だけキャスター半径より大きい
            Assert.GreaterOrEqual(halfWidth, 0.5f * 1.1f - 0.001f,
                "投影半径はキャスター半径 + 10%マージン以上であるべき");
            // 過大でないこと（マージンとカーネル余白を考慮しても2倍以内）
            Assert.LessOrEqual(halfWidth, 1.0f,
                "投影半径が過大にならないべき");
        }

        [Test]
        public void Fit有効_ウィンドウ中心がテクセル単位にスナップされる()
        {
            CreateCasterCube(new Vector3(3.123f, 0f, 2.456f), Vector3.one);
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            DecodeOrthoWindow(_virtualLight.ProjectionMatrix,
                out float halfWidth, out _, out Vector2 center);
            float texelSize = 2f * halfWidth / Resolution;

            float xInTexels = center.x / texelSize;
            float yInTexels = center.y / texelSize;
            Assert.AreEqual(Mathf.Round(xInTexels), xInTexels, 0.01f,
                "ウィンドウ中心Xは1テクセル単位に量子化されるべき");
            Assert.AreEqual(Mathf.Round(yInTexels), yInTexels, 0.01f,
                "ウィンドウ中心Yは1テクセル単位に量子化されるべき");
        }

        [Test]
        public void Fit有効_オフセンターのキャスターにウィンドウ中心が追従する()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            _virtualLight.FitToCasters = true;

            _virtualLight.UpdateMatrices();

            DecodeOrthoWindow(_virtualLight.ProjectionMatrix,
                out float halfWidth, out _, out Vector2 center);

            // 真上から見下ろすライトではビュー空間XYがワールドXZに対応するため、
            // ウィンドウ中心はキャスターのワールド(x, z)の近傍
            // （テクセルスナップ分の誤差以内）にあるべき
            float texelSize = 2f * halfWidth / Resolution;
            Assert.AreEqual(3f, center.x, texelSize + 0.001f,
                "ウィンドウ中心Xはキャスター中心に追従するべき");
            Assert.AreEqual(2f, center.y, texelSize + 0.001f,
                "ウィンドウ中心Yはキャスター中心に追従するべき");
        }

        [Test]
        public void Fit有効_Perspectiveモードでは無視される()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            _virtualLight.FitToCasters = true;
            _virtualLight.ProjectionMode = ProjectionMode.Perspective;
            _virtualLight.FieldOfView = 60f;

            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = Matrix4x4.Perspective(60f, 1f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, _virtualLight.ProjectionMatrix, 0.001f,
                "PerspectiveモードではFitが無視されるべき");
        }

        [Test]
        public void Fit有効_Pointモードでは無視される()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);
            _virtualLight.FitToCasters = true;
            _virtualLight.ProjectionMode = ProjectionMode.Point;

            _virtualLight.UpdateMatrices();

            Matrix4x4 expected = Matrix4x4.Perspective(90f, 1f, 0.1f, 100f);
            AssertMatricesAreEqual(expected, _virtualLight.ProjectionMatrix, 0.001f,
                "PointモードではFitが無視されるべき");
        }

        [Test]
        public void Fit有効_手動サイズと異なるオフセンター行列が生成される()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);

            _virtualLight.FitToCasters = false;
            _virtualLight.UpdateMatrices();
            Matrix4x4 manual = _virtualLight.ProjectionMatrix;

            _virtualLight.FitToCasters = true;
            _virtualLight.UpdateMatrices();
            Matrix4x4 fitted = _virtualLight.ProjectionMatrix;

            Assert.AreNotEqual(manual, fitted,
                "Fit有効時はオフセンターの異なる行列が生成されるべき");
            // オフセンター: 平行移動成分（m03/m13）がゼロでない
            Assert.AreNotEqual(0f, fitted.m03,
                "オフセンター行列はX平行移動成分を持つべき");
        }

        [Test]
        public void TryGetFittedOrthoWindow_Fit有効時のみウィンドウを返す()
        {
            CreateCasterCube(new Vector3(3f, 0f, 2f), Vector3.one);

            _virtualLight.FitToCasters = false;
            _virtualLight.UpdateMatrices();
            Assert.IsFalse(_virtualLight.TryGetFittedOrthoWindow(out _),
                "Fit無効時はウィンドウを返さないべき");

            _virtualLight.FitToCasters = true;
            _virtualLight.UpdateMatrices();
            Assert.IsTrue(_virtualLight.TryGetFittedOrthoWindow(out Rect window),
                "Fit有効時はウィンドウを返すべき");

            DecodeOrthoWindow(_virtualLight.ProjectionMatrix,
                out float halfWidth, out _, out Vector2 center);
            Assert.AreEqual(halfWidth * 2f, window.width, 0.001f,
                "ウィンドウ幅がProjection行列と一致するべき");
            Assert.AreEqual(center.x, window.center.x, 0.001f,
                "ウィンドウ中心がProjection行列と一致するべき");
        }

        #region ヘルパーメソッド

        private static void AssertMatricesAreEqual(
            Matrix4x4 expected, Matrix4x4 actual, float tolerance, string message)
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
