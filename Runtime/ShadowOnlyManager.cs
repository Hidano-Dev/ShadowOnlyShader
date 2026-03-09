using System.Collections.Generic;
using UnityEngine;

namespace ShadowOnlyShader
{
    /// <summary>
    /// 影のみ描画システムのマネージャーコンポーネント。
    /// 仮想光源・床面Renderer・グローバルパラメータを一元管理する。
    /// IShadowOnlyManagerインターフェースを実装する。
    /// </summary>
    public class ShadowOnlyManager : MonoBehaviour, IShadowOnlyManager
    {
        #region Serialized Fields

        [Header("グローバルパラメータ")]
        [SerializeField]
        private BlurQuality _blurQuality = BlurQuality.Mid;

        [SerializeField]
        [Min(0f)]
        private float _blendMultiplier = 1f;

        [Header("床面Renderer")]
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
        /// 複数Manager警告の重複表示を防ぐフラグ。
        /// </summary>
        private bool _warnedMultipleManagers;

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

            // ShadowOnlyFloorシェーダーがまだ存在しない場合はStandardシェーダーで代替
            // Task 6でShadowOnlyFloorシェーダーが実装された際に切り替える
            var shader = Shader.Find("Hidden/ShadowOnlyShader/Floor");
            if (shader == null)
            {
                // フォールバック: 基本的なUnlitシェーダーで仮のMaterialを生成
                shader = Shader.Find("Unlit/Transparent");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
            }

            _floorMaterial = new Material(shader);
            _floorMaterial.hideFlags = HideFlags.DontSave;
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
        }

        #endregion

        #region パラメータ転送

        /// <summary>
        /// シェーダーuniformプロパティ名のキャッシュ。
        /// インデックス付きのプロパティ名を毎フレーム生成しないためのキャッシュ。
        /// </summary>
        private static readonly string[] LightVPMatrixNames = GenerateIndexedNames("_LightVPMatrix_", 8);
        private static readonly string[] ShadowDepthTexNames = GenerateIndexedNames("_ShadowDepthTex_", 8);
        private static readonly string[] ShadowColorNames = GenerateIndexedNames("_ShadowColor_", 8);
        private static readonly string[] ShadowAlphaNames = GenerateIndexedNames("_ShadowAlpha_", 8);
        private static readonly string[] BlurRadiusNames = GenerateIndexedNames("_BlurRadius_", 8);
        private static readonly string[] BlurDistanceFactorNames = GenerateIndexedNames("_BlurDistanceFactor_", 8);
        private static readonly string[] HueShiftNames = GenerateIndexedNames("_HueShift_", 8);
        private static readonly string[] ChromaticAberrationNames = GenerateIndexedNames("_ChromaticAberration_", 8);
        private static readonly string[] DepthBiasNames = GenerateIndexedNames("_DepthBias_", 8);
        private static readonly string[] LightWorldPosNames = GenerateIndexedNames("_LightWorldPos_", 8);
        private static readonly string[] DepthTexSizeNames = GenerateIndexedNames("_DepthTexSize_", 8);

        private static string[] GenerateIndexedNames(string prefix, int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = prefix + i;
            }
            return names;
        }

        /// <summary>
        /// 各VirtualLightのパラメータを床面MaterialのuniformにMaterial.SetXxxで設定する。
        /// 毎フレームLateUpdateから呼び出され、パラメータ変更がリアルタイムに反映される。
        /// </summary>
        public void UpdateMaterialProperties()
        {
            if (_floorMaterial == null) return;

            // グローバルパラメータの設定
            _floorMaterial.SetFloat("_BlendMultiplier", _blendMultiplier);

            // 各VirtualLightのパラメータを転送
            // 破棄済みVirtualLightの検出用フラグ
            bool needsRefresh = false;
            int validLightIndex = 0;

            for (int i = 0; i < _virtualLights.Count && validLightIndex < 8; i++)
            {
                var vl = _virtualLights[i];

                // 破棄済みまたはnullのVirtualLightはスキップ
                if (vl == null)
                {
                    needsRefresh = true;
                    continue;
                }

                // VP行列
                _floorMaterial.SetMatrix(LightVPMatrixNames[validLightIndex], vl.ViewProjectionMatrix);

                // 深度テクスチャ
                var depthRT = vl.DepthRenderTexture;
                if (depthRT != null)
                {
                    _floorMaterial.SetTexture(ShadowDepthTexNames[validLightIndex], depthRT);

                    // テクスチャサイズ (width, height, 1/width, 1/height)
                    float w = depthRT.width;
                    float h = depthRT.height;
                    _floorMaterial.SetVector(DepthTexSizeNames[validLightIndex], new Vector4(w, h, 1f / w, 1f / h));
                }

                // 影色
                _floorMaterial.SetColor(ShadowColorNames[validLightIndex], vl.ShadowColor);

                // 影の濃さ
                _floorMaterial.SetFloat(ShadowAlphaNames[validLightIndex], vl.ShadowAlpha);

                // ブラー関連
                _floorMaterial.SetFloat(BlurRadiusNames[validLightIndex], vl.BlurRadius);
                _floorMaterial.SetFloat(BlurDistanceFactorNames[validLightIndex], vl.BlurDistanceFactor);

                // Hue Shift
                _floorMaterial.SetFloat(HueShiftNames[validLightIndex], vl.HueShift);

                // 色収差
                _floorMaterial.SetFloat(ChromaticAberrationNames[validLightIndex], vl.ChromaticAberration);

                // 深度バイアス
                _floorMaterial.SetFloat(DepthBiasNames[validLightIndex], vl.DepthBias);

                // 光源ワールド位置（距離ボケ計算用）
                Vector3 pos = vl.transform.position;
                _floorMaterial.SetVector(LightWorldPosNames[validLightIndex], new Vector4(pos.x, pos.y, pos.z, 1f));

                validLightIndex++;
            }

            // 実際の有効な光源数を設定（破棄済みを除外した数）
            _floorMaterial.SetInt("_VirtualLightCount", validLightIndex);

            // 破棄済みVirtualLightが検出された場合、リストをリフレッシュ
            if (needsRefresh)
            {
                RefreshVirtualLights();
            }

            // ブラー品質キーワードの切り替え
            UpdateBlurQualityKeywords();
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
        /// 床面RendererにFloorMaterialを自動割り当てする。
        /// 登録済みの各FloorRendererのsharedMaterialをFloorMaterialに設定する。
        /// nullまたは破棄済みのRendererはスキップする。
        /// </summary>
        public void AssignFloorMaterial()
        {
            if (_floorMaterial == null) return;

            for (int i = _floorRenderers.Count - 1; i >= 0; i--)
            {
                var renderer = _floorRenderers[i];

                // 破棄済みまたはnullのRendererはスキップ（警告は初回のみ出力）
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
        }

        private void LateUpdate()
        {
            // 毎フレーム、各VirtualLightのパラメータをMaterialに転送する
            UpdateMaterialProperties();

            // 床面RendererにMaterialを割り当てる
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
