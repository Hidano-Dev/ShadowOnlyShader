# Implementation Plan

- [x] 1. UPMパッケージの基盤構造を作成する
  - パッケージマニフェスト（package.json）を作成し、パッケージ名・バージョン・URP依存関係を定義する
  - Runtime用とEditor用のAssembly Definitionファイルを作成し、URPパッケージへの参照を設定する
  - 列挙型（ProjectionMode、BlurQuality）とインターフェース（IShadowOnlyManager、IVirtualLight）を定義する
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ] 2. VirtualLightコンポーネントを実装する
- [x] 2.1 (P) 仮想光源の投影パラメータとVP行列計算を実装する
  - Orthographic/Perspectiveの切り替えに応じたProjection行列の算出を行う
  - TransformからView行列を算出し、VP行列を毎フレーム更新する仕組みを構築する
  - 投影パラメータ（FOV、OrthographicSize、Near/Far）をInspectorに公開し、バリデーション（クランプ処理）を適用する
  - _Requirements: 1.3, 3.3, 3.5, 7.1, 8.2_

- [x] 2.2 (P) 影の外観パラメータを実装する
  - 影の色、濃さ（アルファ）、ブラー半径、距離ボケ係数、Hue Shift、色収差強度、深度バイアスの各パラメータをSerializeFieldとして定義する
  - 各パラメータをInspectorからリアルタイムに調整可能にする
  - _Requirements: 2.1, 2.2, 3.4, 7.2, 7.4, 7.5, 7.6_

- [x] 2.3 深度RenderTextureの管理とキャスターRenderer収集を実装する
  - 仮想光源ごとの深度RenderTextureの作成・解像度変更時の再作成・OnDisableでの破棄を行う
  - CasterRootのGameObject指定により子階層の全Rendererを自動収集する機能を実装する
  - RenderTextureをHideFlags.DontSaveで管理し、テクスチャ解像度のクランプ（64-4096）を適用する
  - 2.1のVP行列と2.2のパラメータが定義済みである前提で、RenderTextureとRenderer収集のライフサイクルを構築する
  - _Requirements: 3.6, 5.2, 5.5_

- [x] 3. ShadowOnlyManagerコンポーネントを実装する
  - 子GameObjectのVirtualLightコンポーネントを自動検出・動的更新する仕組みを構築する
  - 床面Rendererリストの管理（登録・削除）機能を実装する
  - Material/RenderTexture等の自動生成リソースのライフサイクル管理（OnEnable生成、OnDestroy破棄）を実装する
  - ブラー品質プリセット（Low/Mid/High）と加算合成倍率のグローバルパラメータを管理する
  - Public API（AddVirtualLight、RemoveVirtualLight、AddFloorRenderer、RemoveFloorRenderer）を実装する
  - 複数Manager存在時の警告処理を含める
  - _Requirements: 3.2, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 8.1, 8.4_

- [x] 4. DepthOnlyシェーダーを実装する
  - 仮想光源のVP行列で頂点変換を行い、深度値のみを出力するHLSLシェーダーを作成する
  - ZWrite On、ZTest LEqual、ColorMask 0 の設定で、カラー出力なしの深度書き込みのみを行う
  - SkinnedMeshRendererのボーン変換に対応する（Unityの自動スキニングを利用）
  - _Requirements: 1.3, 5.1_

- [ ] 5. ShadowOnlyRendererFeatureとRenderPassを実装する
- [ ] 5.1 ScriptableRendererFeatureを実装する
  - Createメソッドで ShadowOnlyRenderPass を生成する
  - AddRenderPassesメソッドでシーン内のShadowOnlyManagerを検索し、Passにデータを渡してレンダラーに登録する
  - Managerが存在しない場合はPassをスキップし警告ログを出力する
  - _Requirements: 1.4, 6.1, 6.2_

- [ ] 5.2 RenderGraph UnsafePassで深度テクスチャ生成を実装する
  - RecordRenderGraphメソッドでAddUnsafePassを使用し、UnsafeGraphContextからCommandBufferを取得する
  - 各VirtualLightごとに、深度RenderTextureへのSetRenderTarget、Clear、SetViewProjectionMatrices、DrawRendererループを実行する
  - 対象Rendererに一切の変更を加えず、CommandBuffer.DrawRendererで直接描画参照する
  - 非アクティブまたはnullのRendererはスキップする
  - RenderPassEvent.BeforeRenderingOpaquesで挿入し、床面描画前に深度テクスチャを準備する
  - _Requirements: 1.3, 1.4, 3.1, 5.1, 5.3, 5.4_

- [ ] 6. ShadowOnlyFloor床面シェーダーを実装する
- [ ] 6.1 プロジェクティブテクスチャマッピングによる影判定の基本機能を実装する
  - フラグメントのワールド位置を各仮想光源のVP行列でライト射影空間に変換する
  - 射影空間のUV座標で深度テクスチャをサンプリングし、深度比較で影領域を判定する
  - 影以外の部分をalpha=0で透明に保ち、Blend SrcAlpha OneMinusSrcAlpha、ZWrite Offで出力する
  - 射影範囲外のフラグメントではクランプ処理で影を描画しない
  - 複数仮想光源の影を加算合成し、合成倍率パラメータで調整する
  - 影の色・濃さパラメータをuniform配列として受け取り、リアルタイムに反映する
  - _Requirements: 1.1, 1.2, 1.3, 2.3, 2.4, 3.7, 7.1_

- [ ] 6.2 ブラー・Hue Shift・色収差の表現機能を実装する
  - multi_compileキーワード（_BLUR_LOW/_BLUR_MID/_BLUR_HIGH）でガウシアンサンプリングブラーの品質プリセットを切り替える
  - 光源からの距離に応じたブラー半径の動的変化（距離ボケ）を実装する
  - HSV色空間でのHue Shift（色相回転）処理を実装する
  - RGBチャンネル分離による色収差（影の中心からの放射方向オフセット）を実装する
  - _Requirements: 7.2, 7.3, 7.4, 7.5, 7.6_

- [ ] 7. ManagerからMaterialへのパラメータ転送を実装する
  - ShadowOnlyManagerが毎フレーム各VirtualLightのパラメータ（VP行列、深度テクスチャ、影色、濃さ、ブラー関連、Hue Shift、色収差、深度バイアス）を床面MaterialのuniformにMaterial.SetXxxで設定する
  - アクティブな仮想光源数を_VirtualLightCountとして設定する
  - ブラー品質キーワードをMaterial.EnableKeywordで切り替える
  - 床面Rendererへの自動Material割り当てを行う
  - パラメータ変更がリアルタイムに影の描画に反映されることを確認する
  - _Requirements: 2.3, 2.4, 3.4, 3.7, 4.1, 4.5, 7.6_

- [ ] 8. Gizmo描画を実装する
  - VirtualLightの方向・描画範囲をSceneビューでGizmoとして表示する
  - Orthographicの場合は矩形（Gizmos.DrawWireCube）、Perspectiveの場合は錐台（Gizmos.DrawFrustum）を描画する
  - Near/Farクリップ面の視覚化を含める
  - Editor asmdefに配置する
  - _Requirements: 8.3_

- [ ] 9. サンプルシーンを作成する
  - セットアップ済みのマネージャー・仮想光源・キャスター・床面オブジェクトを含むサンプルシーンを作成する
  - RendererFeatureが登録済みのURP Renderer設定アセットを含める
  - Samples~フォルダに配置し、UPMのサンプルインポート機能で利用可能にする
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 10. リソースライフサイクルとエラーハンドリングの検証を行う
  - 自動生成リソース（Material、RenderTexture）がOnDestroy/OnDisableで確実に破棄されることを検証する
  - CasterRoot=null、FloorRenderer=null/非アクティブ、Manager不在などの設定エラー時にクラッシュせず適切な警告が出ることを検証する
  - Rendererが実行中に破棄された場合のnullチェック動作を検証する
  - シーン遷移時のリソース解放が正しく行われることを検証する
  - _Requirements: 4.4, 5.1, 5.3_

- [ ]*  11. 受け入れ基準に基づくテストカバレッジを追加する
  - VirtualLightのVP行列計算（Orthographic/Perspective）が正しい値を返すユニットテストを作成する
  - CollectRenderersが子階層のRendererを正しく収集するテストを作成する
  - パラメータバリデーション（クランプ、範囲チェック）のテストを作成する
  - ShadowOnlyManagerのリソース生成・破棄ライフサイクルのテストを作成する
  - _Requirements: 1.3, 1.5, 3.5, 4.4, 5.5, 6.1, 6.2_
