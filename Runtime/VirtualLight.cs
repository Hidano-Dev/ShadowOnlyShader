using System.Collections.Generic;
using UnityEngine;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 仮想光源コンポーネント。
    /// 投影パラメータの管理、View/Projection/VP行列の計算を行う。
    /// IVirtualLightインターフェースを実装する。
    /// </summary>
    public class VirtualLight : MonoBehaviour, IVirtualLight
    {
        #region Serialized Fields - Projection Parameters

        [Header("Projection")]
        [SerializeField]
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic;

        [SerializeField]
        [Range(1f, 179f)]
        private float _fieldOfView = 60f;

        [SerializeField]
        [Min(0.001f)]
        private float _orthographicSize = 5f;

        [SerializeField]
        [Min(0.001f)]
        private float _nearClipPlane = 0.1f;

        [SerializeField]
        [Min(0.002f)]
        private float _farClipPlane = 100f;

        #endregion

        #region Serialized Fields - Shadow Appearance

        [Header("Shadow Appearance")]
        [SerializeField]
        private Color _shadowColor = Color.black;

        [SerializeField]
        [Range(0f, 1f)]
        private float _shadowAlpha = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float _blurRadius = 1f;

        [SerializeField]
        [Min(0f)]
        private float _blurDistanceFactor = 0f;

        [SerializeField]
        [Range(0f, 360f)]
        private float _hueShift = 0f;

        [SerializeField]
        [Min(0f)]
        private float _chromaticAberration = 0f;

        [Header("Depth Bias")]
        [SerializeField]
        private float _depthBias = 0.005f;

        [SerializeField]
        private float _normalBias = 0f;

        #endregion

        #region Serialized Fields - Caster

        [Header("Caster")]
        [SerializeField]
        private GameObject _casterRoot;

        [SerializeField]
        private int _textureResolution = 1024;

        #endregion

        #region Private Fields

        private Matrix4x4 _viewMatrix = Matrix4x4.identity;
        private Matrix4x4 _projectionMatrix = Matrix4x4.identity;
        private Matrix4x4 _viewProjectionMatrix = Matrix4x4.identity;
        private RenderTexture _depthRenderTexture;
        private readonly List<Renderer> _casterRenderers = new List<Renderer>();

        #endregion

        #region IVirtualLight - Projection Parameters

        /// <inheritdoc />
        public ProjectionMode ProjectionMode
        {
            get => _projectionMode;
            set => _projectionMode = value;
        }

        /// <inheritdoc />
        public float FieldOfView
        {
            get => _fieldOfView;
            set => _fieldOfView = Mathf.Clamp(value, 1f, 179f);
        }

        /// <inheritdoc />
        public float OrthographicSize
        {
            get => _orthographicSize;
            set => _orthographicSize = Mathf.Max(value, 0.001f);
        }

        /// <inheritdoc />
        public float NearClipPlane
        {
            get => _nearClipPlane;
            set => _nearClipPlane = Mathf.Max(value, 0.001f);
        }

        /// <inheritdoc />
        public float FarClipPlane
        {
            get => _farClipPlane;
            set
            {
                // Far must be greater than Near
                _farClipPlane = Mathf.Max(value, _nearClipPlane + 0.001f);
            }
        }

        /// <inheritdoc />
        public int TextureResolution
        {
            get => _textureResolution;
            set => _textureResolution = Mathf.Clamp(value, 64, 4096);
        }

        #endregion

        #region IVirtualLight - Shadow Appearance

        /// <inheritdoc />
        public Color ShadowColor
        {
            get => _shadowColor;
            set => _shadowColor = value;
        }

        /// <inheritdoc />
        public float ShadowAlpha
        {
            get => _shadowAlpha;
            set => _shadowAlpha = Mathf.Clamp01(value);
        }

        /// <inheritdoc />
        public float BlurRadius
        {
            get => _blurRadius;
            set => _blurRadius = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float BlurDistanceFactor
        {
            get => _blurDistanceFactor;
            set => _blurDistanceFactor = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float HueShift
        {
            get => _hueShift;
            set => _hueShift = Mathf.Clamp(value, 0f, 360f);
        }

        /// <inheritdoc />
        public float ChromaticAberration
        {
            get => _chromaticAberration;
            set => _chromaticAberration = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float DepthBias
        {
            get => _depthBias;
            set => _depthBias = value;
        }

        /// <inheritdoc />
        public float NormalBias
        {
            get => _normalBias;
            set => _normalBias = value;
        }

        #endregion

        #region IVirtualLight - Caster

        /// <inheritdoc />
        public GameObject CasterRoot
        {
            get => _casterRoot;
            set => _casterRoot = value;
        }

        /// <inheritdoc />
        public IReadOnlyList<Renderer> CasterRenderers => _casterRenderers;

        #endregion

        #region IVirtualLight - Computed Matrices

        /// <inheritdoc />
        public Matrix4x4 ViewMatrix => _viewMatrix;

        /// <inheritdoc />
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        /// <inheritdoc />
        public Matrix4x4 ViewProjectionMatrix => _viewProjectionMatrix;

        /// <inheritdoc />
        public RenderTexture DepthRenderTexture => _depthRenderTexture;

        #endregion

        #region IVirtualLight - Methods

        /// <inheritdoc />
        public void CollectRenderers()
        {
            _casterRenderers.Clear();
            if (_casterRoot != null)
            {
                _casterRoot.GetComponentsInChildren<Renderer>(_casterRenderers);
            }
        }

        /// <inheritdoc />
        public void UpdateMatrices()
        {
            _viewMatrix = CalculateViewMatrix();
            _projectionMatrix = CalculateProjectionMatrix();
            _viewProjectionMatrix = _projectionMatrix * _viewMatrix;
        }

        #endregion

        #region Matrix Calculation

        /// <summary>
        /// TransformからView行列を算出する。
        /// カメラのView行列と同様に、ワールド空間からビュー空間への変換を行う。
        /// </summary>
        private Matrix4x4 CalculateViewMatrix()
        {
            // Unity's camera convention: camera looks down -Z in local space
            // Matrix4x4.TRS gives us the local-to-world transform.
            // View matrix is the inverse of the camera's world transform,
            // with Z axis flipped (right-hand to left-hand conversion for rendering).
            Transform t = transform;
            Matrix4x4 worldToLocal = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

            // Flip Z axis for Unity's rendering convention (camera looks down +Z in view space becomes -Z)
            Matrix4x4 zFlip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            return zFlip * worldToLocal;
        }

        /// <summary>
        /// ProjectionModeに応じたProjection行列を算出する。
        /// </summary>
        private Matrix4x4 CalculateProjectionMatrix()
        {
            float near = _nearClipPlane;
            float far = _farClipPlane;

            if (_projectionMode == ProjectionMode.Perspective)
            {
                // Perspective projection with 1:1 aspect ratio (square depth texture)
                return Matrix4x4.Perspective(_fieldOfView, 1f, near, far);
            }
            else
            {
                // Orthographic projection with 1:1 aspect ratio
                float size = _orthographicSize;
                return Matrix4x4.Ortho(-size, size, -size, size, near, far);
            }
        }

        #endregion

        #region MonoBehaviour Lifecycle

        private void OnValidate()
        {
            // Apply clamping to serialized fields when modified in Inspector
            _fieldOfView = Mathf.Clamp(_fieldOfView, 1f, 179f);
            _orthographicSize = Mathf.Max(_orthographicSize, 0.001f);
            _nearClipPlane = Mathf.Max(_nearClipPlane, 0.001f);
            _farClipPlane = Mathf.Max(_farClipPlane, _nearClipPlane + 0.001f);
            _textureResolution = Mathf.Clamp(_textureResolution, 64, 4096);
            _shadowAlpha = Mathf.Clamp01(_shadowAlpha);
            _blurRadius = Mathf.Max(_blurRadius, 0f);
            _blurDistanceFactor = Mathf.Max(_blurDistanceFactor, 0f);
            _hueShift = Mathf.Clamp(_hueShift, 0f, 360f);
            _chromaticAberration = Mathf.Max(_chromaticAberration, 0f);
        }

        private void LateUpdate()
        {
            UpdateMatrices();
        }

        #endregion
    }
}
