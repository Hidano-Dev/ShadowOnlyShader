using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のみ描画システムのマネージャーコンポーネント。
    /// 仮想光源・床面Renderer・グローバルパラメータを一元管理する。
    /// 深度テクスチャはTexture2DArrayとして一元管理し、全VirtualLightで共有する。
    /// IShadowOnlyManagerインターフェースを実装する。
    /// </summary>
    public class ShadowOnlyManager : MonoBehaviour, IShadowOnlyManager
    {
        /// <summary>仮想光源の最大数。</summary>
        internal const int MaxVirtualLights = 8;

        #region Serialized Fields

        [Header("グローバルパラメータ")]
        [Tooltip("影のぼかし品質。Low は軽量だが粗く、High は滑らかだが処理負荷が高くなります")]
        [SerializeField]
        private BlurQuality _blurQuality = BlurQuality.Mid;

        [Tooltip("影全体の濃さの倍率。1 が標準で、値を上げるとすべての影が濃くなり、下げると薄くなります")]
        [SerializeField]
        [Min(0f)]
        private float _blendMultiplier = 1f;

        [Tooltip("ブラー計算の解像度スケール。1.0で通常解像度、0.5で半分、0.25で1/4。低い値ほど軽量になりますが影がぼやけます")]
        [SerializeField]
        [Range(0.1f, 1.0f)]
        private float _blurResolutionScale = 1.0f;

        [Tooltip("影が映り込む床面のRendererを指定します。ここに登録されたオブジェクトの表面に影が描画されます")]
        [SerializeField]
        private List<Renderer> _floorRenderers = new List<Renderer>();

        #endregion

        #region Private Fields

        /// <summary>
        /// 子GameObjectから収集したVirtualLightのリスト。
        /// </summary>
        private readonly List<VirtualLight> _virtualLights = new List<VirtualLight>();

        /// <summary>
        /// IVirtualLightとして公開するためのラッパーリスト。
        /// </summary>
        private readonly List<IVirtualLight> _virtualLightsInterface = new List<IVirtualLight>();

        /// <summary>
        /// 自動生成された床面用Material。
        /// </summary>
        private Material _floorMaterial;

        /// <summary>
        /// 全VirtualLight共有の深度Texture2DArray。
        /// </summary>
        private RenderTexture _depthArrayTexture;

        /// <summary>
        /// 床面Material割り当てが必要かどうかのフラグ。
        /// Material作成時・FloorRenderer追加時にtrueになる。
        /// </summary>
        private bool _floorMaterialDirty;

        /// <summary>
        /// 複数Manager警告の重複表示を防ぐフラグ。
        /// </summary>
        private bool _warnedMultipleManagers;

        #endregion

        #region Pre-allocated Arrays for SetXxxArray

        private readonly Matrix4x4[] _lightVPMatrices = new Matrix4x4[MaxVirtualLights];
        private readonly Vector4[] _shadowColors = new Vector4[MaxVirtualLights];
        private readonly float[] _shadowAlphas = new float[MaxVirtualLights];
        private readonly float[] _depthBiases = new float[MaxVirtualLights];
        private readonly float[] _blurRadii = new float[MaxVirtualLights];
        private readonly float[] _blurDistanceFactors = new float[MaxVirtualLights];
        private readonly float[] _blurCameraDistanceFactors = new float[MaxVirtualLights];
        private readonly float[] _cameraDistancePowers = new float[MaxVirtualLights];
        private readonly float[] _alphaCameraDistanceFactors = new float[MaxVirtualLights];
        private readonly float[] _hueShifts = new float[MaxVirtualLights];
        private readonly float[] _chromaticAberrations = new float[MaxVirtualLights];
        private readonly Vector4[] _chromaticAberrationColors = new Vector4[MaxVirtualLights];
        private readonly float[] _contactHardeningStrengths = new float[MaxVirtualLights];
        private readonly Vector4[] _lightWorldPositions = new Vector4[MaxVirtualLights];
        private readonly Vector4[] _depthTexSizes = new Vector4[MaxVirtualLights];

        #endregion

        #region IShadowOnlyManager - VirtualLights管理

        /// <inheritdoc />
        public IReadOnlyList<IVirtualLight> VirtualLights => _virtualLightsInterface;

        /// <inheritdoc />
        public IVirtualLight AddVirtualLight()
        {
            // 子GameObjectを生成してVirtualLightコンポーネントをアタッチ
            var go = new GameObject("VirtualLight");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var vl = go.AddComponent<VirtualLight>();

            // リストに追加
            _virtualLights.Add(vl);
            _virtualLightsInterface.Add(vl);

            return vl;
        }

        /// <inheritdoc />
        public void RemoveVirtualLight(IVirtualLight light)
        {
            if (light == null) return;

            var vl = light as VirtualLight;
            if (vl == null) return;

            int index = _virtualLights.IndexOf(vl);
            if (index < 0) return;

            _virtualLights.RemoveAt(index);
            _virtualLightsInterface.Remove(light);

            // 対応するGameObjectを破棄
            if (vl != null && vl.gameObject != null)
            {
                DestroyImmediate(vl.gameObject);
            }
        }

        /// <summary>
        /// 子GameObjectのVirtualLightコンポーネントを再収集する。
        /// 動的な子の追加・削除に対応するために使用する。
        /// </summary>
        public void RefreshVirtualLights()
        {
            _virtualLights.Clear();
            _virtualLightsInterface.Clear();

            GetComponentsInChildren<VirtualLight>(_virtualLights);

            // 自分自身にアタッチされたVirtualLightは除外（Managerは管理側）
            // VirtualLightはMonoBehaviourなので、破棄済みのものはnullになる
            for (int i = _virtualLights.Count - 1; i >= 0; i--)
            {
                if (_virtualLights[i] == null)
                {
                    _virtualLights.RemoveAt(i);
                }
            }

            foreach (var vl in _virtualLights)
            {
                _virtualLightsInterface.Add(vl);
            }
        }

        #endregion

        #region IShadowOnlyManager - 床面Renderer管理

        /// <inheritdoc />
        public IReadOnlyList<Renderer> FloorRenderers => _floorRenderers;

        /// <inheritdoc />
        public void AddFloorRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                Debug.LogWarning("[ShadowOnlyShader] nullのRendererは床面として追加できません。", this);
                return;
            }

            if (_floorRenderers.Contains(renderer))
            {
                return; // 重複防止
            }

            _floorRenderers.Add(renderer);
            _floorMaterialDirty = true;
        }

        /// <inheritdoc />
        public void RemoveFloorRenderer(Renderer renderer)
        {
            if (renderer == null) return;

            _floorRenderers.Remove(renderer);
        }

        #endregion

        #region IShadowOnlyManager - グローバルパラメータ

        /// <inheritdoc />
        public BlurQuality BlurQuality
        {
            get => _blurQuality;
            set => _blurQuality = value;
        }

        /// <inheritdoc />
        public float BlendMultiplier
        {
            get => _blendMultiplier;
            set => _blendMultiplier = Mathf.Max(value, 0f);
        }

        /// <inheritdoc />
        public float BlurResolutionScale
        {
            get => _blurResolutionScale;
            set => _blurResolutionScale = Mathf.Clamp(value, 0.1f, 1.0f);
        }

        #endregion

        #region 深度Texture2DArray管理

        /// <summary>
        /// 全VirtualLight共有の深度Texture2DArray。
        /// RenderPassおよびFloorシェーダーから参照される。
        /// </summary>
        public RenderTexture DepthArrayTexture => _depthArrayTexture;

        /// <summary>
        /// 深度Texture2DArrayの存在と解像度を確認し、必要に応じて作成・再作成する。
        /// 全VirtualLightの深度テクスチャは統一解像度のTexture2DArrayとして管理される。
        /// スライス数は実際のアクティブVirtualLight数に合わせ、不要なメモリ消費を防ぐ。
        /// </summary>
        private void EnsureDepthArrayTexture()
        {
            int resolution = ResolveTextureResolution();
            int sliceCount = CountActiveVirtualLights();

            // アクティブなVirtualLightがない場合はテクスチャ不要
            if (sliceCount == 0)
            {
                ReleaseDepthArrayTexture();
                return;
            }

            // 既存のTexture2DArrayが存在し、解像度・スライス数が一致する場合はそのまま
            if (_depthArrayTexture != null
                && _depthArrayTexture.width == resolution
                && _depthArrayTexture.volumeDepth == sliceCount)
            {
                return;
            }

            // 古いTexture2DArrayを破棄
            ReleaseDepthArrayTexture();

            // 新しいTexture2DArrayを作成（スライス数は実際のアクティブライト数）
            _depthArrayTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Depth);
            _depthArrayTexture.dimension = TextureDimension.Tex2DArray;
            _depthArrayTexture.volumeDepth = sliceCount;
            _depthArrayTexture.hideFlags = HideFlags.DontSave;
            _depthArrayTexture.Create();
        }

        /// <summary>
        /// アクティブなVirtualLightの数を返す（最大MaxVirtualLights）。
        /// </summary>
        private int CountActiveVirtualLights()
        {
            int count = 0;
            for (int i = 0; i < _virtualLights.Count && count < MaxVirtualLights; i++)
            {
                var vl = _virtualLights[i];
                if (vl != null && vl.isActiveAndEnabled)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 深度Texture2DArrayを解放する。
        /// </summary>
        private void ReleaseDepthArrayTexture()
        {
            if (_depthArrayTexture != null)
            {
                _depthArrayTexture.Release();
                DestroyImmediate(_depthArrayTexture);
                _depthArrayTexture = null;
            }
        }

        /// <summary>
        /// 深度テクスチャの解像度を決定する。
        /// 最初のアクティブなVirtualLightの解像度設定を使用し、
        /// 取得できない場合はURP Assetの設定にフォールバックする。
        /// </summary>
        private int ResolveTextureResolution()
        {
            // 最初のアクティブなVirtualLightの解像度を使用
            for (int i = 0; i < _virtualLights.Count; i++)
            {
                var vl = _virtualLights[i];
                if (vl != null && vl.isActiveAndEnabled)
                {
                    return vl.ResolveTextureResolution();
                }
            }

            // フォールバック: URP Asset の設定
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                return urpAsset.mainLightShadowmapResolution;
            }

            return 1024;
        }

        #endregion

        #region リソース管理

        /// <summary>
        /// 自動生成された床面用Material。
        /// 影描画に必要なMaterialはOnEnableで自動生成される。
        /// </summary>
        public Material FloorMaterial => _floorMaterial;

        /// <summary>
        /// 床面用Materialを生成する。
        /// HideFlags.DontSaveを設定し、シーン保存時に永続化しない。
        /// </summary>
        private void CreateFloorMaterial()
        {
            if (_floorMaterial != null) return;

            var shader = Shader.Find("Hidden/ShadowOnlyShader/Floor");
            if (shader == null)
            {
                // URP環境でのフォールバック
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                // 最終フォールバック: 全Unity環境に存在する内部シェーダー
                shader = Shader.Find("Hidden/InternalErrorShader");
            }

            if (shader == null)
            {
                Debug.LogWarning("[ShadowOnlyShader] 床面用シェーダーが見つかりません。");
                return;
            }

            _floorMaterial = new Material(shader);
            _floorMaterial.hideFlags = HideFlags.DontSave;
            _floorMaterialDirty = true;
        }

        /// <summary>
        /// 自動生成リソースを破棄する。
        /// </summary>
        private void DestroyResources()
        {
            if (_floorMaterial != null)
            {
                DestroyImmediate(_floorMaterial);
                _floorMaterial = null;
            }

            ReleaseDepthArrayTexture();
        }

        #endregion

        #region パラメータ転送

        /// <summary>
        /// 各VirtualLightのパラメータを床面MaterialのuniformにSetXxxArrayで一括設定する。
        /// 毎フレームLateUpdateから呼び出され、パラメータ変更がリアルタイムに反映される。
        /// 配列化により、SetXxx呼び出し数を ~100回/フレーム → ~15回/フレームに削減。
        /// </summary>
        public void UpdateMaterialProperties()
        {
            if (_floorMaterial == null) return;

            // グローバルパラメータの設定
            _floorMaterial.SetFloat("_BlendMultiplier", _blendMultiplier);

            // 各VirtualLightのパラメータを配列に収集
            // 破棄済みVirtualLightの検出用フラグ
            bool needsRefresh = false;
            int validLightIndex = 0;

            for (int i = 0; i < _virtualLights.Count && validLightIndex < MaxVirtualLights; i++)
            {
                var vl = _virtualLights[i];

                // 破棄済みまたはnullのVirtualLightはスキップ
                if (vl == null)
                {
                    needsRefresh = true;
                    continue;
                }

                // 非アクティブなVirtualLightはスキップ
                if (!vl.isActiveAndEnabled)
                {
                    continue;
                }

                // VP行列（GPU変換済み）
                // GL.GetGPUProjectionMatrixでプラットフォーム固有のProjection行列に変換し、
                // 深度RenderPassと同じ変換を適用することで深度値の一致を保証する
                var gpuProj = GL.GetGPUProjectionMatrix(vl.ProjectionMatrix, true);
                _lightVPMatrices[validLightIndex] = gpuProj * vl.ViewMatrix;

                // 影色
                Color sc = vl.ShadowColor;
                _shadowColors[validLightIndex] = new Vector4(sc.r, sc.g, sc.b, sc.a);

                // 影の濃さ
                _shadowAlphas[validLightIndex] = vl.ShadowAlpha;

                // 深度バイアス
                _depthBiases[validLightIndex] = vl.DepthBias;

                // ブラー関連
                _blurRadii[validLightIndex] = vl.BlurRadius;
                _blurDistanceFactors[validLightIndex] = vl.BlurDistanceFactor;
                _blurCameraDistanceFactors[validLightIndex] = vl.BlurCameraDistanceFactor;
                _cameraDistancePowers[validLightIndex] = vl.CameraDistancePower;
                _alphaCameraDistanceFactors[validLightIndex] = vl.AlphaCameraDistanceFactor;

                // Hue Shift
                _hueShifts[validLightIndex] = vl.HueShift;

                // 色収差
                _chromaticAberrations[validLightIndex] = vl.ChromaticAberration;
                Color caColor = vl.EffectiveChromaticAberrationColor;
                _chromaticAberrationColors[validLightIndex] = new Vector4(caColor.r, caColor.g, caColor.b, caColor.a);

                // コンタクトハードニング（PCSS）
                _contactHardeningStrengths[validLightIndex] = vl.ContactHardeningStrength;

                // 光源ワールド位置（距離ボケ計算用）
                Vector3 pos = vl.transform.position;
                _lightWorldPositions[validLightIndex] = new Vector4(pos.x, pos.y, pos.z, 1f);

                // テクスチャサイズ（全スライス共通解像度）
                if (_depthArrayTexture != null)
                {
                    float w = _depthArrayTexture.width;
                    float h = _depthArrayTexture.height;
                    _depthTexSizes[validLightIndex] = new Vector4(w, h, 1f / w, 1f / h);
                }

                validLightIndex++;
            }

            // 実際の有効な光源数を設定（破棄済みを除外した数）
            _floorMaterial.SetInt("_VirtualLightCount", validLightIndex);

            // 配列パラメータを一括設定（SetXxx × 8回 → SetXxxArray × 1回に集約）
            _floorMaterial.SetMatrixArray("_LightVPMatrices", _lightVPMatrices);
            _floorMaterial.SetVectorArray("_ShadowColors", _shadowColors);
            _floorMaterial.SetFloatArray("_ShadowAlphas", _shadowAlphas);
            _floorMaterial.SetFloatArray("_DepthBiases", _depthBiases);
            _floorMaterial.SetFloatArray("_BlurRadii", _blurRadii);
            _floorMaterial.SetFloatArray("_BlurDistanceFactors", _blurDistanceFactors);
            _floorMaterial.SetFloatArray("_BlurCameraDistanceFactors", _blurCameraDistanceFactors);
            _floorMaterial.SetFloatArray("_CameraDistancePowers", _cameraDistancePowers);
            _floorMaterial.SetFloatArray("_AlphaCameraDistanceFactors", _alphaCameraDistanceFactors);
            _floorMaterial.SetFloatArray("_HueShifts", _hueShifts);
            _floorMaterial.SetFloatArray("_ChromaticAberrations", _chromaticAberrations);
            _floorMaterial.SetVectorArray("_ChromaticAberrationColors", _chromaticAberrationColors);
            _floorMaterial.SetFloatArray("_ContactHardeningStrengths", _contactHardeningStrengths);
            _floorMaterial.SetVectorArray("_LightWorldPositions", _lightWorldPositions);
            _floorMaterial.SetVectorArray("_DepthTexSizes", _depthTexSizes);

            // 深度Texture2DArrayを設定
            if (_depthArrayTexture != null)
            {
                _floorMaterial.SetTexture("_ShadowDepthTexArray", _depthArrayTexture);
            }

            // 破棄済みVirtualLightが検出された場合、リストをリフレッシュ
            if (needsRefresh)
            {
                RefreshVirtualLights();
            }

            // ブラー品質キーワードの切り替え
            UpdateBlurQualityKeywords();

            // Resolve解像度スケールキーワードの切り替え
            UpdateResolveKeyword();
        }

        /// <summary>
        /// ブラー品質プリセットに応じたシェーダーキーワードを切り替える。
        /// Material.EnableKeyword / DisableKeyword で _BLUR_LOW / _BLUR_MID / _BLUR_HIGH を制御する。
        /// </summary>
        private void UpdateBlurQualityKeywords()
        {
            if (_floorMaterial == null) return;

            // まず全てのブラーキーワードを無効にする
            _floorMaterial.DisableKeyword("_BLUR_LOW");
            _floorMaterial.DisableKeyword("_BLUR_MID");
            _floorMaterial.DisableKeyword("_BLUR_HIGH");

            // 選択された品質に対応するキーワードを有効にする
            switch (_blurQuality)
            {
                case BlurQuality.Low:
                    _floorMaterial.EnableKeyword("_BLUR_LOW");
                    break;
                case BlurQuality.Mid:
                    _floorMaterial.EnableKeyword("_BLUR_MID");
                    break;
                case BlurQuality.High:
                    _floorMaterial.EnableKeyword("_BLUR_HIGH");
                    break;
            }
        }

        /// <summary>
        /// Resolve解像度スケールに応じたシェーダーuniform変数を設定する。
        /// _ShadowResolveActive > 0.5 の場合、Display Pass（Pass 0）は
        /// Resolve結果テクスチャをサンプリングするランタイム分岐を取る。
        /// LateUpdateで設定されるため、レンダリング前に確定する。
        /// </summary>
        private void UpdateResolveKeyword()
        {
            if (_floorMaterial == null) return;

            _floorMaterial.SetFloat("_ShadowResolveActive",
                _blurResolutionScale < 0.999f ? 1.0f : 0.0f);
        }

        /// <summary>
        /// 床面RendererにFloorMaterialを自動割り当てする。
        /// dirtyフラグが立っている場合のみ実行される（Material作成時・FloorRenderer追加時）。
        /// nullまたは破棄済みのRendererはスキップする。
        /// </summary>
        public void AssignFloorMaterial()
        {
            if (!_floorMaterialDirty) return;
            if (_floorMaterial == null) return;

            for (int i = _floorRenderers.Count - 1; i >= 0; i--)
            {
                var renderer = _floorRenderers[i];

                // 破棄済みまたはnullのRendererはスキップ
                if (renderer == null)
                {
                    continue;
                }

                // 非アクティブなRendererはMaterial割り当てをスキップ
                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                renderer.sharedMaterial = _floorMaterial;
            }

            _floorMaterialDirty = false;
        }

        #endregion

        #region 複数Manager警告

        /// <summary>
        /// シーン内に複数のShadowOnlyManagerが存在する場合に警告を出力する。
        /// </summary>
        private void CheckMultipleManagers()
        {
            var managers = FindObjectsByType<ShadowOnlyManager>(FindObjectsSortMode.None);
            if (managers.Length > 1 && !_warnedMultipleManagers)
            {
                Debug.LogWarning(
                    "[ShadowOnlyShader] シーン内に複数のShadowOnlyManagerが存在します。" +
                    "最初に見つかったManagerのみが使用されます。",
                    this);
                _warnedMultipleManagers = true;
            }
        }

        #endregion

        #region MonoBehaviour Lifecycle

        private void OnEnable()
        {
            // リソース生成
            CreateFloorMaterial();

            // 子VirtualLightの収集
            RefreshVirtualLights();

            // 深度Texture2DArrayの作成
            EnsureDepthArrayTexture();

            // 複数Manager警告チェック
            CheckMultipleManagers();
        }

        private void OnDisable()
        {
            // リソース破棄
            DestroyResources();
        }

        private void OnDestroy()
        {
            // リソース破棄（OnDisableが呼ばれない場合の安全策）
            DestroyResources();
        }

        private void OnValidate()
        {
            // シリアライズフィールドのバリデーション
            _blendMultiplier = Mathf.Max(_blendMultiplier, 0f);
            _floorMaterialDirty = true;
        }

        private void LateUpdate()
        {
            // 深度Texture2DArrayの確認・再作成（解像度変更対応）
            EnsureDepthArrayTexture();

            // 毎フレーム、各VirtualLightのパラメータをMaterialに転送する
            UpdateMaterialProperties();

            // 床面RendererにMaterialを割り当てる（dirtyフラグ制御）
            AssignFloorMaterial();
        }

        private void OnTransformChildrenChanged()
        {
            // 子GameObjectの追加・削除に応じてVirtualLightリストを更新
            RefreshVirtualLights();
        }

        #endregion
    }
}
