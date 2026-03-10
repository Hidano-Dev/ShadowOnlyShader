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
        [Tooltip("影の投影方式。Orthographic は平行光（太陽光のような均一な影）、Perspective は点光源（近くが大きく遠くが小さい影）になります")]
        [SerializeField]
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic;

        [Tooltip("Perspective モード時の視野角（度）。値が大きいほど広範囲に影が投影されますが、影が歪みやすくなります")]
        [SerializeField]
        [Range(1f, 179f)]
        private float _fieldOfView = 60f;

        [Tooltip("Orthographic モード時の投影範囲の半径。値が大きいほど広い範囲に影を投影できますが、解像度が粗くなります")]
        [SerializeField]
        [Min(0.001f)]
        private float _orthographicSize = 5f;

        [Tooltip("影が描画される最短距離。光源からこの距離より近いオブジェクトは影を落としません")]
        [SerializeField]
        [Min(0.001f)]
        private float _nearClipPlane = 0.1f;

        [Tooltip("影が描画される最長距離。光源からこの距離より遠いオブジェクトは影を落としません")]
        [SerializeField]
        [Min(0.002f)]
        private float _farClipPlane = 100f;

        #endregion

        #region Serialized Fields - Shadow Appearance

        [Header("Shadow Appearance")]
        [Tooltip("影の色。黒以外にも好きな色の影を落とすことができます")]
        [SerializeField]
        private Color _shadowColor = Color.black;

        [Tooltip("影の濃さ。0 で完全に透明、1 で完全に不透明になります")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _shadowAlpha = 0.5f;

        [Tooltip("影のぼかし量。値が大きいほど影の輪郭がやわらかくなります")]
        [SerializeField]
        [Min(0f)]
        private float _blurRadius = 1f;

        [Tooltip("光源からの距離に応じたぼかしの増加量。値が大きいほど、光源から遠い影ほどぼやけます")]
        [SerializeField]
        [Min(0f)]
        private float _blurDistanceFactor = 0f;

        [Tooltip("影の色相を回転させます（0～360度）。色相環に沿って影の色味を変化させる演出に使います")]
        [SerializeField]
        [Range(0f, 360f)]
        private float _hueShift = 0f;

        [Tooltip("色収差の強さ。値が大きいほど影の輪郭にRGBの色ズレが発生し、レンズを通したような演出になります")]
        [SerializeField]
        [Min(0f)]
        private float _chromaticAberration = 0f;

        [Tooltip("色収差の光源参照（オプション）。設定すると、このLightの色に応じて色収差のフリンジ色が物理的に変化します。暖色光なら赤フリンジが強く、寒色光なら青フリンジが強くなります")]
        [SerializeField]
        private Light _sourceLight;

        [Tooltip("色収差の光源色（SourceLight未設定時のフォールバック）。白=標準的なRGB色収差、単色=色収差なし（物理的に正しい挙動）")]
        [SerializeField]
        private Color _chromaticAberrationColor = Color.white;

        [Header("Depth Bias")]
        [Tooltip("影のちらつき（セルフシャドウ）を抑えるためのオフセット値。影が欠ける場合は値を大きくしてください")]
        [SerializeField]
        private float _depthBias = 0.005f;

        [Tooltip("面の向きに応じた影のオフセット補正。斜めの面で影がちらつく場合に調整します")]
        [SerializeField]
        private float _normalBias = 0f;

        #endregion

        #region Serialized Fields - Caster

        [Header("Caster")]
        [Tooltip("影を落とすオブジェクトの親。この配下にあるすべてのメッシュが影の元になります")]
        [SerializeField]
        private GameObject _casterRoot;

        [Tooltip("影の解像度（ピクセル数）。値が大きいほど影がくっきりしますが、処理負荷が増えます")]
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
        public Light SourceLight
        {
            get => _sourceLight;
            set => _sourceLight = value;
        }

        /// <inheritdoc />
        public Color ChromaticAberrationColor
        {
            get => _chromaticAberrationColor;
            set => _chromaticAberrationColor = value;
        }

        /// <inheritdoc />
        public Color EffectiveChromaticAberrationColor =>
            _sourceLight != null ? _sourceLight.color : _chromaticAberrationColor;

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

        /// <summary>
        /// 深度RenderTextureの存在と解像度を確認し、必要に応じて作成・再作成する。
        /// 解像度が変更された場合は古いRenderTextureを破棄して新しく作成する。
        /// </summary>
        public void EnsureDepthTexture()
        {
            int resolution = _textureResolution;

            // 既存のRenderTextureが存在し、解像度が一致する場合はそのまま
            if (_depthRenderTexture != null && _depthRenderTexture.width == resolution)
            {
                return;
            }

            // 古いRenderTextureが存在する場合は破棄
            ReleaseDepthTexture();

            // 新しいRenderTextureを作成
            _depthRenderTexture = CreateDepthRenderTexture(resolution);
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

        #region Depth RenderTexture Management

        /// <summary>
        /// 指定解像度で深度RenderTextureを作成する。
        /// HideFlags.DontSaveを設定し、シーン保存時に永続化しない。
        /// </summary>
        private RenderTexture CreateDepthRenderTexture(int resolution)
        {
            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Depth);
            rt.hideFlags = HideFlags.DontSave;
            rt.Create();
            return rt;
        }

        /// <summary>
        /// 深度RenderTextureを解放する。
        /// </summary>
        private void ReleaseDepthTexture()
        {
            if (_depthRenderTexture != null)
            {
                _depthRenderTexture.Release();
                DestroyImmediate(_depthRenderTexture);
                _depthRenderTexture = null;
            }
        }

        #endregion

        #region MonoBehaviour Lifecycle

        private void OnEnable()
        {
            // 深度RenderTextureの作成
            EnsureDepthTexture();

            // CasterRoot配下のRendererを自動収集
            CollectRenderers();
        }

        private void OnDisable()
        {
            // 深度RenderTextureの破棄
            ReleaseDepthTexture();
        }

        private void OnDestroy()
        {
            // OnDisableが呼ばれない場合の安全策として、OnDestroyでも破棄を行う
            ReleaseDepthTexture();
        }

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
