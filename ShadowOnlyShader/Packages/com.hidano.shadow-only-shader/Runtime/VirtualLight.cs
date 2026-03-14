using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 仮想光源コンポーネント。
    /// 投影パラメータの管理、View/Projection/VP行列の計算を行う。
    /// IVirtualLightインターフェースを実装する。
    /// </summary>
    public class VirtualLight : MonoBehaviour, IVirtualLight
    {
        /// <summary>
        /// URP Assetの影解像度設定を使用することを示す定数。
        /// _textureResolutionがこの値の場合、実行時にURPの設定から解像度を取得する。
        /// </summary>
        internal const int UseURPResolution = 0;

        #region Serialized Fields - Source Light

        [Header("Source Light")]
        [Tooltip("参照する Unity Light コンポーネント。設定すると Transform・投影パラメータ・Bias・ShadowAlpha を自動同期し、色収差のフリンジ色も Light の色に連動します")]
        [SerializeField]
        private Light _sourceLight;

        [Tooltip("色収差の光源色（SourceLight未設定時のフォールバック）。白=標準的なRGB色収差、単色=色収差なし（物理的に正しい挙動）")]
        [SerializeField]
        private Color _chromaticAberrationColor = Color.white;

        #endregion

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

        [Header("Depth Bias")]
        [Tooltip("影のちらつき（セルフシャドウ）を抑えるためのオフセット値。影が欠ける場合は値を大きくしてください")]
        [SerializeField]
        private float _depthBias = 0f;

        [Tooltip("面の向きに応じた影のオフセット補正。斜めの面で影がちらつく場合に調整します")]
        [SerializeField]
        private float _normalBias = 0f;

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

        [Tooltip("カメラからの距離に応じたぼかしの増加量。値が大きいほど、カメラから遠い影ほどぼやけます")]
        [SerializeField]
        [Min(0f)]
        private float _blurCameraDistanceFactor = 0f;

        [Tooltip("カメラからの距離に応じた影の減衰量。値が大きいほど、カメラから遠い影ほど薄くなります（近い影は濃いまま保たれます）")]
        [SerializeField]
        [Min(0f)]
        private float _alphaCameraDistanceFactor = 0f;

        [Tooltip("カメラ距離効果のカーブ指数。1=線形、2=二乗（遠方で急変化）、0.5=平方根（近くから速く効く）。ぼかし・アルファ両方に適用されます")]
        [SerializeField]
        [Min(0.01f)]
        private float _cameraDistancePower = 1f;

        [Tooltip("影の色相を回転させます（0～360度）。色相環に沿って影の色味を変化させる演出に使います")]
        [SerializeField]
        [Range(0f, 360f)]
        private float _hueShift = 0f;

        [Tooltip("色収差の強さ。値が大きいほど影の輪郭にRGBの色ズレが発生し、レンズを通したような演出になります")]
        [SerializeField]
        [Min(0f)]
        private float _chromaticAberration = 0f;

        [Tooltip("コンタクトハードニング（PCSS）の強度。値が大きいほど、影元から離れた部分のぼかしが強くなります。0 で無効（従来の均一ブラー）になります")]
        [SerializeField]
        [Min(0f)]
        private float _contactHardeningStrength = 0f;

        #endregion

        #region Serialized Fields - Caster

        [Header("Caster")]
        [Tooltip("影を落とすオブジェクトの親。この配下にあるすべてのメッシュが影の元になります")]
        [SerializeField]
        private GameObject _casterRoot;

        [Header("Quality")]
        [Tooltip("影の解像度。URP Default は URP Asset の Main Light Shadow Resolution を使用します")]
        [SerializeField]
        private int _textureResolution = UseURPResolution;

        #endregion

        #region Serialized Fields - Pre-Sync Saved State

        [HideInInspector] [SerializeField] private bool _hasSavedPreSyncState;
        [HideInInspector] [SerializeField] private Vector3 _savedPosition;
        [HideInInspector] [SerializeField] private Quaternion _savedRotation = Quaternion.identity;
        [HideInInspector] [SerializeField] private ProjectionMode _savedProjectionMode;
        [HideInInspector] [SerializeField] private float _savedFieldOfView = 60f;
        [HideInInspector] [SerializeField] private float _savedOrthographicSize = 5f;
        [HideInInspector] [SerializeField] private float _savedFarClipPlane = 100f;
        [HideInInspector] [SerializeField] private float _savedDepthBias;
        [HideInInspector] [SerializeField] private float _savedNormalBias;
        [HideInInspector] [SerializeField] private float _savedShadowAlpha = 0.5f;

        #endregion

        #region Private Fields

        private Matrix4x4 _viewMatrix = Matrix4x4.identity;
        private Matrix4x4 _projectionMatrix = Matrix4x4.identity;
        private Matrix4x4 _viewProjectionMatrix = Matrix4x4.identity;
        private RenderTexture _depthRenderTexture;
        private readonly List<Renderer> _casterRenderers = new List<Renderer>();

        /// <summary>
        /// 前フレームの同期状態。ランタイムでのSourceLight着脱検出に使用。
        /// </summary>
        private bool _wasSyncing;

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
        public float BlurCameraDistanceFactor
        {
            get => _blurCameraDistanceFactor;
            set => _blurCameraDistanceFactor = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float AlphaCameraDistanceFactor
        {
            get => _alphaCameraDistanceFactor;
            set => _alphaCameraDistanceFactor = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float CameraDistancePower
        {
            get => _cameraDistancePower;
            set => _cameraDistancePower = Mathf.Max(value, 0.01f);
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
        public float ContactHardeningStrength
        {
            get => _contactHardeningStrength;
            set => _contactHardeningStrength = Mathf.Max(value, 0f);
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

        /// <summary>
        /// 深度RenderTextureの存在と解像度を確認し、必要に応じて作成・再作成する。
        /// 解像度が変更された場合は古いRenderTextureを破棄して新しく作成する。
        /// </summary>
        public void EnsureDepthTexture()
        {
            int resolution = ResolveTextureResolution();

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

        #region Texture Resolution

        /// <summary>
        /// _textureResolution の実効値を返す。
        /// UseURPResolution (0) の場合は URP Asset の mainLightShadowmapResolution を使用し、
        /// それも取得できない場合は 1024 にフォールバックする。
        /// </summary>
        internal int ResolveTextureResolution()
        {
            if (_textureResolution != UseURPResolution)
            {
                return _textureResolution;
            }

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                return urpAsset.mainLightShadowmapResolution;
            }

            return 1024;
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

        #region Source Light Sync

        /// <summary>
        /// 現在のTransformと投影パラメータを保存する。
        /// SourceLightが設定される直前に呼び出す。
        /// </summary>
        internal void SavePreSyncState()
        {
            Transform t = transform;
            _savedPosition = t.position;
            _savedRotation = t.rotation;
            _savedProjectionMode = _projectionMode;
            _savedFieldOfView = _fieldOfView;
            _savedOrthographicSize = _orthographicSize;
            _savedFarClipPlane = _farClipPlane;
            _savedShadowAlpha = _shadowAlpha;
            _hasSavedPreSyncState = true;
        }

        /// <summary>
        /// 保存済みのTransformと投影パラメータを復元する。
        /// SourceLightがクリアされた直後に呼び出す。
        /// </summary>
        internal void RestorePreSyncState()
        {
            if (!_hasSavedPreSyncState) return;

            Transform t = transform;
            t.position = _savedPosition;
            t.rotation = _savedRotation;
            _projectionMode = _savedProjectionMode;
            _fieldOfView = _savedFieldOfView;
            _orthographicSize = _savedOrthographicSize;
            _farClipPlane = _savedFarClipPlane;
            _shadowAlpha = _savedShadowAlpha;
            _hasSavedPreSyncState = false;
        }

        /// <summary>
        /// 保存済みの同期前状態があるかどうか。
        /// </summary>
        internal bool HasSavedPreSyncState => _hasSavedPreSyncState;

        /// <summary>
        /// SourceLightが設定されている場合、
        /// Lightコンポーネントから位置・回転・投影パラメータ・バイアス・影の濃さを同期する。
        /// </summary>
        private void SyncFromSourceLight()
        {
            if (_sourceLight == null) return;

            // Transform同期
            Transform lightTransform = _sourceLight.transform;
            Transform t = transform;
            t.position = lightTransform.position;
            t.rotation = lightTransform.rotation;

            // 投影パラメータ同期
            switch (_sourceLight.type)
            {
                case LightType.Spot:
                    _projectionMode = ProjectionMode.Perspective;
                    _fieldOfView = _sourceLight.spotAngle;
                    _farClipPlane = _sourceLight.range;
                    break;
                case LightType.Directional:
                    _projectionMode = ProjectionMode.Orthographic;
                    // Directional lightにはrangeの概念がないため、farClipPlaneは既存値を維持
                    break;
                case LightType.Point:
                    // Point lightは全方向なのでPerspectiveで近似
                    _projectionMode = ProjectionMode.Perspective;
                    _farClipPlane = _sourceLight.range;
                    break;
            }

            // 影の濃さ同期
            _shadowAlpha = _sourceLight.shadowStrength;
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
            if (_textureResolution != UseURPResolution)
            {
                _textureResolution = Mathf.Clamp(_textureResolution, 64, 4096);
            }
            _shadowAlpha = Mathf.Clamp01(_shadowAlpha);
            _blurRadius = Mathf.Max(_blurRadius, 0f);
            _blurDistanceFactor = Mathf.Max(_blurDistanceFactor, 0f);
            _blurCameraDistanceFactor = Mathf.Max(_blurCameraDistanceFactor, 0f);
            _alphaCameraDistanceFactor = Mathf.Max(_alphaCameraDistanceFactor, 0f);
            _cameraDistancePower = Mathf.Max(_cameraDistancePower, 0.01f);
            _hueShift = Mathf.Clamp(_hueShift, 0f, 360f);
            _chromaticAberration = Mathf.Max(_chromaticAberration, 0f);
            _contactHardeningStrength = Mathf.Max(_contactHardeningStrength, 0f);
        }

        private void LateUpdate()
        {
            bool isSyncing = _sourceLight != null;

            // OFF → ON 遷移: 現在の状態を保存
            if (isSyncing && !_wasSyncing)
            {
                SavePreSyncState();
            }
            // ON → OFF 遷移: 保存した状態を復元
            else if (!isSyncing && _wasSyncing)
            {
                RestorePreSyncState();
            }

            _wasSyncing = isSyncing;

            SyncFromSourceLight();
            UpdateMatrices();
        }

        #endregion
    }
}
