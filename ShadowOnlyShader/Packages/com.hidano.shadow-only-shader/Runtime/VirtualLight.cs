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
    /// 深度テクスチャはShadowOnlyManagerがTexture2DArrayとして一元管理する。
    /// </summary>
    public class VirtualLight : MonoBehaviour, IVirtualLight
    {
        /// <summary>
        /// URP Assetの影解像度設定を使用することを示す定数。
        /// _textureResolutionがこの値の場合、実行時にURPの設定から解像度を取得する。
        /// </summary>
        internal const int UseURPResolution = 0;

        /// <summary>
        /// Pointモードで使用するキューブ面数（±X/±Y/±Z）。
        /// </summary>
        public const int PointFaceCount = 6;

        /// <summary>
        /// Pointモードの各面の視野角（度）。
        /// 90°×6面で全方向を隙間・重なりなくカバーする。
        /// </summary>
        internal const float PointFaceFieldOfView = 90f;

        /// <summary>
        /// Pointモードの6面の視線方向（ワールド軸基準）。
        /// ポイントライトは全方向に均等なため、Transformの回転には追従させず
        /// ワールド軸固定にする（回転しても影が変化しない物理的に正しい挙動）。
        /// </summary>
        private static readonly Quaternion[] PointFaceRotations =
        {
            Quaternion.LookRotation(Vector3.right, Vector3.up),
            Quaternion.LookRotation(Vector3.left, Vector3.up),
            Quaternion.LookRotation(Vector3.up, Vector3.back),
            Quaternion.LookRotation(Vector3.down, Vector3.forward),
            Quaternion.LookRotation(Vector3.forward, Vector3.up),
            Quaternion.LookRotation(Vector3.back, Vector3.up),
        };

        #region Serialized Fields - Caster

        [Header("Caster")]
        [Tooltip("この光源だけ影の元を個別に変えたい場合に指定します。未設定の場合はShadowOnlyManagerのDefault Caster Rootが使用されます。指定した場合、その配下にあるすべてのメッシュが影の元になります")]
        [SerializeField]
        private GameObject _casterRoot;

        #endregion

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
        [Tooltip("影の投影方式。Orthographic は平行光（太陽光のような均一な影）、Perspective はスポットライト（1方向の円錐範囲）、Point はポイントライト（全方向、深度テクスチャを6スライス使用）になります")]
        [SerializeField]
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic;

        [Tooltip("Perspective モード時の視野角（度）。値が大きいほど広範囲に影が投影されますが、影が歪みやすくなります")]
        [SerializeField]
        [Range(1f, 179f)]
        private float _fieldOfView = 60f;

        [Tooltip("Orthographic モード時に、投影範囲をキャスター全体へ毎フレーム自動フィットさせます。ライトの角度によらずキャスターが常に投影範囲に収まり、テクセル密度も高く保たれます。有効時は Orthographic Size は使用されません")]
        [SerializeField]
        private bool _fitToCasters;

        [Tooltip("Orthographic モード時の投影範囲の半径。値が大きいほど広い範囲に影を投影できますが、解像度が粗くなります")]
        [SerializeField]
        [Min(0.001f)]
        private float _orthographicSize = 5f;

        [Tooltip("影が描画される最短距離。光源からこの距離より近いオブジェクトは影を落としません")]
        [SerializeField]
        [Min(0.001f)]
        private float _nearClipPlane = 0.001f;

        [Tooltip("影が描画される最長距離。光源からこの距離より遠いオブジェクトは影を落としません")]
        [SerializeField]
        [Min(0.002f)]
        private float _farClipPlane = 100f;

        [Tooltip("影のちらつき（セルフシャドウ）を抑えるためのオフセット値。影が欠ける場合は値を大きくしてください")]
        [SerializeField]
        private float _depthBias = 0f;

        [Tooltip("面の向きに応じた影のオフセット補正。斜めの面で影がちらつく場合に調整します")]
        [SerializeField]
        private float _normalBias = 0f;

        #endregion

        #region Serialized Fields - Shadow Appearance

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

        [Tooltip("接地ダークニングの強度。キャスターに近い影（足元など）を濃くして設置感を強調します。ベースのShadow Alphaに上乗せする倍率で、0 で無効になります")]
        [SerializeField]
        [Min(0f)]
        private float _contactDarkeningStrength = 0f;

        [Tooltip("接地ダークニングの効果範囲。光源のNear〜Far間を0〜1とした深度差がこの値に達すると効果がゼロになります。小さいほど接地部分だけが濃くなります")]
        [SerializeField]
        [Min(0.0001f)]
        private float _contactDarkeningRange = 0.05f;

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

        /// <summary>
        /// Pointモード時の各面のView行列（UpdateMatricesで更新される）。
        /// </summary>
        private readonly Matrix4x4[] _pointFaceViewMatrices = new Matrix4x4[PointFaceCount];
        private readonly List<Renderer> _casterRenderers = new List<Renderer>();

        /// <summary>
        /// Rendererリストの再収集が必要かどうかのフラグ。
        /// CasterRoot変更、階層変更時にtrueになる。
        /// </summary>
        private bool _renderersDirty = true;

        /// <summary>
        /// 前回CollectRenderersを実行したときの実効キャスタールート。
        /// ManagerのDefaultCasterRoot変更を毎フレームの比較で検出するために保持する。
        /// </summary>
        private GameObject _lastCollectedRoot;

        /// <summary>
        /// 親階層のShadowOnlyManagerのキャッシュ（Playモード時のみ使用）。
        /// EffectiveCasterRootの解決で毎フレームGetComponentInParentが走るのを防ぐ。
        /// </summary>
        private ShadowOnlyManager _cachedManager;

        /// <summary>
        /// 前フレームの同期状態。ランタイムでのSourceLight着脱検出に使用。
        /// </summary>
        private bool _wasSyncing;

        /// <summary>
        /// Fit To Castersで直近に算出した投影ウィンドウ（ビュー空間XY = ローカル空間XY）。
        /// Gizmo表示用にキャッシュする。
        /// </summary>
        private Rect _fittedOrthoWindow;

        /// <summary>
        /// _fittedOrthoWindowが有効かどうか。
        /// Fitが無効、Orthographic以外、キャスター不在でのフォールバック時はfalse。
        /// </summary>
        private bool _hasFittedOrthoWindow;

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
        public bool FitToCasters
        {
            get => _fitToCasters;
            set => _fitToCasters = value;
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
            set => _textureResolution = Mathf.Clamp(value, 64, 8192);
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
        public float ContactDarkeningStrength
        {
            get => _contactDarkeningStrength;
            set => _contactDarkeningStrength = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float ContactDarkeningRange
        {
            get => _contactDarkeningRange;
            set => _contactDarkeningRange = Mathf.Max(value, 0.0001f);
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
            set
            {
                _casterRoot = value;
                _renderersDirty = true;
            }
        }

        /// <inheritdoc />
        public GameObject EffectiveCasterRoot
        {
            get
            {
                if (_casterRoot != null) return _casterRoot;
                var manager = ResolveManager();
                return manager != null ? manager.DefaultCasterRoot : null;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<Renderer> CasterRenderers => _casterRenderers;

        /// <summary>
        /// 親階層のShadowOnlyManagerを取得する。
        /// Editモードでは再親子付けを検出できないためキャッシュせず毎回検索し、
        /// Playモードではキャッシュを使用する（OnTransformParentChangedで無効化）。
        /// </summary>
        private ShadowOnlyManager ResolveManager()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return GetComponentInParent<ShadowOnlyManager>(true);
            }
#endif
            if (_cachedManager == null)
            {
                _cachedManager = GetComponentInParent<ShadowOnlyManager>(true);
            }
            return _cachedManager;
        }

        #endregion

        #region IVirtualLight - Computed Matrices

        /// <inheritdoc />
        public Matrix4x4 ViewMatrix => _viewMatrix;

        /// <inheritdoc />
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        /// <inheritdoc />
        public Matrix4x4 ViewProjectionMatrix => _viewProjectionMatrix;

        /// <inheritdoc />
        public int SliceCount => _projectionMode == ProjectionMode.Point ? PointFaceCount : 1;

        /// <inheritdoc />
        public Matrix4x4 GetSliceViewMatrix(int sliceIndex)
        {
            if (_projectionMode == ProjectionMode.Point)
            {
                sliceIndex = Mathf.Clamp(sliceIndex, 0, PointFaceCount - 1);
                return _pointFaceViewMatrices[sliceIndex];
            }
            return _viewMatrix;
        }

        #endregion

        #region IVirtualLight - Methods

        /// <inheritdoc />
        public void CollectRenderers()
        {
            // ManagerのDefaultCasterRoot変更はイベントで通知されないため、
            // 実効ルートの変化を毎回の呼び出しで検出して再収集する
            GameObject root = EffectiveCasterRoot;
            if (!_renderersDirty && root == _lastCollectedRoot) return;

            _casterRenderers.Clear();
            if (root != null)
            {
                root.GetComponentsInChildren<Renderer>(_casterRenderers);
            }
            _lastCollectedRoot = root;
            _renderersDirty = false;
        }

        /// <inheritdoc />
        public void UpdateMatrices()
        {
            _viewMatrix = CalculateViewMatrix();
            _projectionMatrix = CalculateProjectionMatrix();
            _viewProjectionMatrix = _projectionMatrix * _viewMatrix;

            if (_projectionMode == ProjectionMode.Point)
            {
                Vector3 position = transform.position;
                for (int face = 0; face < PointFaceCount; face++)
                {
                    _pointFaceViewMatrices[face] = CalculateViewMatrix(position, PointFaceRotations[face]);
                }
            }
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
            Transform t = transform;
            return CalculateViewMatrix(t.position, t.rotation);
        }

        /// <summary>
        /// 指定した位置・回転からView行列を算出する。
        /// Pointモードの各面View行列の計算にも使用される。
        /// </summary>
        private static Matrix4x4 CalculateViewMatrix(Vector3 position, Quaternion rotation)
        {
            // Unity's camera convention: camera looks down -Z in local space
            // Matrix4x4.TRS gives us the local-to-world transform.
            // View matrix is the inverse of the camera's world transform,
            // with Z axis flipped (right-hand to left-hand conversion for rendering).
            Matrix4x4 worldToLocal = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;

            // Flip Z axis for Unity's rendering convention (camera looks down +Z in view space becomes -Z)
            Matrix4x4 zFlip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            return zFlip * worldToLocal;
        }

        /// <summary>
        /// ProjectionModeに応じたProjection行列を算出する。
        /// Fit To Casters有効時は_viewMatrixを参照するため、
        /// UpdateMatrices内でView行列の更新後に呼び出すこと。
        /// </summary>
        private Matrix4x4 CalculateProjectionMatrix()
        {
            float near = _nearClipPlane;
            float far = _farClipPlane;

            _hasFittedOrthoWindow = false;

            if (_projectionMode == ProjectionMode.Point)
            {
                // Cube face projection: FOV 90° fixed, 1:1 aspect ratio.
                // 全6面で同一のProjection行列を共有する
                return Matrix4x4.Perspective(PointFaceFieldOfView, 1f, near, far);
            }

            if (_projectionMode == ProjectionMode.Perspective)
            {
                // Perspective projection with 1:1 aspect ratio (square depth texture)
                return Matrix4x4.Perspective(_fieldOfView, 1f, near, far);
            }
            else
            {
                if (_fitToCasters && TryCalculateFittedOrthoProjection(near, far, out Matrix4x4 fitted))
                {
                    return fitted;
                }

                // Orthographic projection with 1:1 aspect ratio
                float size = _orthographicSize;
                return Matrix4x4.Ortho(-size, size, -size, size, near, far);
            }
        }

        #endregion

        #region Fit To Casters

        /// <summary>
        /// Fit To Casters時に投影範囲へ加える相対マージンの比率（片側10%）。
        /// キャスターのアニメーションによるBoundsの微小変動を吸収する。
        /// </summary>
        private const float FitMarginRatio = 0.1f;

        /// <summary>
        /// ブラーカーネルの最大テクセル半径。
        /// ShadowOnlyFloorCommon.hlslのBlurQuality High（13x13カーネル = 半径6テクセル）に対応し、
        /// Managerを解決できない場合のフォールバックにも使用する。
        /// </summary>
        private const float MaxBlurKernelRadius = 6f;

        /// <summary>
        /// Fit To Castersで直近に算出した投影ウィンドウ（ビュー空間XY）を返す。
        /// ビュー空間のXYはライトのローカル空間XYと一致するため、Gizmo描画にそのまま使用できる。
        /// Fitが無効、またはキャスター不在でOrthographicSizeにフォールバックした場合はfalseを返す。
        /// </summary>
        internal bool TryGetFittedOrthoWindow(out Rect window)
        {
            window = _fittedOrthoWindow;
            return _hasFittedOrthoWindow;
        }

        /// <summary>
        /// キャスターRendererの合成Boundsをビュー空間へ変換し、
        /// XY範囲を覆うオフセンター正射影行列を算出する。
        /// 有効なキャスターが1つもない場合はfalseを返す（OrthographicSizeにフォールバック）。
        /// </summary>
        private bool TryCalculateFittedOrthoProjection(float near, float far, out Matrix4x4 projection)
        {
            projection = Matrix4x4.identity;
            CollectRenderers();

            bool hasBounds = false;
            float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;

            for (int i = 0; i < _casterRenderers.Count; i++)
            {
                var renderer = _casterRenderers[i];
                // 深度パス（ShadowOnlyRenderPass.CollectPassData）と同じ条件で描画対象を絞る
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                {
                    continue;
                }

                // ワールドAABBの8頂点をビュー空間へ変換してXY範囲を蓄積する
                // （AABBごと変換すると回転で範囲が過大になるため頂点単位で変換する）
                Bounds bounds = renderer.bounds;
                Vector3 bMin = bounds.min;
                Vector3 bMax = bounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = new Vector3(
                        (corner & 1) == 0 ? bMin.x : bMax.x,
                        (corner & 2) == 0 ? bMin.y : bMax.y,
                        (corner & 4) == 0 ? bMin.z : bMax.z);
                    Vector3 v = _viewMatrix.MultiplyPoint3x4(p);

                    if (!hasBounds)
                    {
                        minX = maxX = v.x;
                        minY = maxY = v.y;
                        hasBounds = true;
                    }
                    else
                    {
                        if (v.x < minX) minX = v.x;
                        if (v.x > maxX) maxX = v.x;
                        if (v.y < minY) minY = v.y;
                        if (v.y > maxY) maxY = v.y;
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;

            // 正方形ウィンドウにする（テクセルを正方形に保ち、ブラーの等方性を維持する）
            float halfBase = Mathf.Max(maxX - minX, maxY - minY) * 0.5f;

            // 相対マージンに加え、ブラーカーネルがUV外を参照しないようカーネル半径分の
            // テクセルを余白として確保する。テクセル実寸は最終サイズに依存するため閉形式で解く:
            //   half = halfBase*(1+margin) + kernelTexels * (2*half/resolution)
            int resolution = ResolveTextureResolution();
            float kernelTexels = ResolveBlurKernelRadius() * _blurRadius + 1f; // +1はテクセルスナップのずれ分
            float texelRatio = Mathf.Min(2f * kernelTexels / resolution, 0.5f);
            float half = Mathf.Max(halfBase * (1f + FitMarginRatio) / (1f - texelRatio), 0.001f);

            // テクセルスナップ: 投影ウィンドウ中心を1テクセル単位に量子化してshadow swimmingを防ぐ
            float texelSize = 2f * half / resolution;
            centerX = Mathf.Floor(centerX / texelSize) * texelSize;
            centerY = Mathf.Floor(centerY / texelSize) * texelSize;

            _fittedOrthoWindow = new Rect(centerX - half, centerY - half, half * 2f, half * 2f);
            _hasFittedOrthoWindow = true;
            projection = Matrix4x4.Ortho(
                centerX - half, centerX + half,
                centerY - half, centerY + half,
                near, far);
            return true;
        }

        /// <summary>
        /// ManagerのBlurQualityに応じたブラーカーネルのテクセル半径を返す。
        /// ShadowOnlyFloorCommon.hlslのカーネルサイズ（Low=5x5/Mid=9x9/High=13x13）に対応する。
        /// </summary>
        private float ResolveBlurKernelRadius()
        {
            var manager = ResolveManager();
            if (manager == null)
            {
                return MaxBlurKernelRadius;
            }

            switch (manager.BlurQuality)
            {
                case BlurQuality.Low: return 2f;
                case BlurQuality.Mid: return 4f;
                default: return MaxBlurKernelRadius;
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
                    // Point lightは全方向: 90°×6面のキューブ投影で全方向をカバー
                    _projectionMode = ProjectionMode.Point;
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
            // CasterRoot配下のRendererを自動収集
            _renderersDirty = true;
            CollectRenderers();

            // 親ManagerにVirtualLightリストの再収集を通知
            NotifyManagerRefresh();
        }

        private void OnDisable()
        {
            // 親ManagerにVirtualLightリストの再収集を通知
            NotifyManagerRefresh();
        }

        /// <summary>
        /// 親階層のShadowOnlyManagerにVirtualLightリストの再収集を通知する。
        /// OnEnable/OnDisable時に呼び出され、ランタイムでのアクティブ切り替えに対応する。
        /// </summary>
        private void NotifyManagerRefresh()
        {
            var manager = GetComponentInParent<ShadowOnlyManager>();
            if (manager != null)
            {
                manager.RefreshVirtualLights();
            }
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
                _textureResolution = Mathf.Clamp(_textureResolution, 64, 8192);
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
            _contactDarkeningStrength = Mathf.Max(_contactDarkeningStrength, 0f);
            _contactDarkeningRange = Mathf.Max(_contactDarkeningRange, 0.0001f);

            // Inspector変更時にRendererリストを再収集対象にする
            _renderersDirty = true;
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

        private void OnTransformChildrenChanged()
        {
            // CasterRoot配下の子階層が変更された場合にRendererリストを再収集
            _renderersDirty = true;
        }

        private void OnTransformParentChanged()
        {
            // 親が変わると所属するManagerも変わり得るため、キャッシュを無効化する
            _cachedManager = null;
            _renderersDirty = true;
        }

        #endregion
    }
}
