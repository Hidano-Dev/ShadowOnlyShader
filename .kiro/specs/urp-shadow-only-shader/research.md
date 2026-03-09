# Research & Design Decisions

---
**Purpose**: 技術設計の根拠となるディスカバリー調査結果、アーキテクチャ検討、および設計判断の記録。

**Usage**:
- ディスカバリーフェーズでの調査活動とその成果を記録する。
- `design.md` には詳細すぎるトレードオフの議論を文書化する。
- 将来の監査や再利用のための参考資料とエビデンスを提供する。
---

## Summary
- **Feature**: `urp-shadow-only-shader`
- **Discovery Scope**: New Feature（グリーンフィールド）
- **Key Findings**:
  - Unity 6.3 LTS ではRenderGraph APIが必須であり、Compatibility Modeは非推奨・非表示化済み。UnsafePassを使用することでCommandBuffer.DrawRendererによる直接描画が可能。
  - プロジェクティブテクスチャマッピングによる影生成は、仮想光源のVP行列を使用してワールド空間からライトカメラの同次座標系へ変換し、深度比較で影判定を行う標準的な手法である。
  - フラグメントシェーダーでのサンプリングブラーは、ガウシアンカーネルのサンプル数をLow/Mid/Highで切り替えることで品質プリセットを実現可能。

## Research Log

### Unity 6.3 LTS における RenderGraph API の状況

- **Context**: 要件で Unity 6.3 LTS を対象としており、ScriptableRenderPass の実装方法を確認する必要があった。
- **Sources Consulted**:
  - [Unity Manual: Compatibility Mode in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/compatibility-mode.html)
  - [Render Graph Updates in Unity 6.3](https://discussions.unity.com/t/render-graph-updates-in-unity-6-3/1668122)
  - [Unity Manual: Upgrade to Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html)
  - [Unity Manual: Use the CommandBuffer interface in a render graph](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-unsafe-pass.html)
- **Findings**:
  - Unity 6.3 では Compatibility Mode が非推奨・非表示化されており、RenderGraph API の使用が実質的に必須。
  - `RecordRenderGraph` メソッドが ScriptableRenderPass の主要エントリポイント。
  - `AddUnsafePass` を使用すると従来の `CommandBuffer` API（`DrawRenderer` 含む）にフルアクセスが可能。
  - `AddRasterRenderPass` は `RasterCommandBuffer` を使用し、`DrawRenderer` は `DrawRendererList` 経由となる。
  - UnsafePass では RenderGraph の自動最適化が効かないため、パフォーマンス面での考慮が必要。
- **Implications**:
  - 本プロジェクトでは `AddUnsafePass` を採用し、`CommandBuffer.DrawRenderer` で各キャスターRendererを深度RenderTextureに直接描画する方式が最も適切。
  - RenderGraph対応の `RecordRenderGraph` オーバーライドを使用し、レガシーの `Execute` メソッドは使用しない。

### CommandBuffer.DrawRenderer による非破壊的描画

- **Context**: 要件5で対象Rendererへの非破壊的取り扱いが求められており、CommandBuffer.DrawRendererの動作を確認する必要があった。
- **Sources Consulted**:
  - [Unity Scripting API: CommandBuffer.DrawRenderer](https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.DrawRenderer.html)
  - [Custom Renderer Features - Cyanilux](https://www.cyanilux.com/tutorials/custom-renderer-features/)
  - [Custom Shadow Mapping in Unity - Shahriar Shahrabi](https://shahriyarshahrabi.medium.com/custom-shadow-mapping-in-unity-c42a81e1bbf8)
- **Findings**:
  - `CommandBuffer.DrawRenderer(Renderer, Material, submeshIndex, shaderPass)` は対象Rendererのメッシュを指定Materialで描画する。
  - Rendererコンポーネント自体には一切変更を加えない（Layer変更不要、Material変更不要）。
  - SkinnedMeshRendererの場合、現在のアニメーション状態のメッシュがそのまま描画される。
  - ライティング関連のシェーダーデータ（ライト色、方向、シャドウ等）はセットされない。深度のみの描画には適切。
- **Implications**:
  - 非破壊的要件を完全に満たす手段として `CommandBuffer.DrawRenderer` が確認された。
  - 深度専用のMaterial（DepthOnlyシェーダー）を用意し、そのMaterialを指定して各Rendererを描画する。

### プロジェクティブテクスチャマッピングによる影判定

- **Context**: 仮想光源のDepthOnlyカメラで生成した深度テクスチャを床面に投影する手法の実装パターンを調査。
- **Sources Consulted**:
  - [Custom Shadow Mapping in Unity - Shahriar Shahrabi](https://shahriyarshahrabi.medium.com/custom-shadow-mapping-in-unity-c42a81e1bbf8)
  - [URP Shadow Architecture Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@12.0/manual/Shadows-in-URP.html)
- **Findings**:
  - 光源カメラのVP行列を使用して、ワールド空間の頂点をライトの射影空間に変換する。
  - 射影空間のxy座標を深度テクスチャのUV座標として使用し、z値と深度テクスチャの値を比較して影を判定する。
  - Orthographic投影では平行光源、Perspective投影では点光源/スポットライトに相当する影が生成される。
  - バイアス値（深度バイアス + 法線バイアス）によるシャドウアクネの防止が必要。
  - 射影範囲外のフラグメントではクランプ処理で影を描画しないようにする。
- **Implications**:
  - 各仮想光源のView行列とProjection行列をC#側で計算し、シェーダーにuniformとして渡す。
  - Orthographic/Perspective切り替えは `Matrix4x4.Ortho` / `Matrix4x4.Perspective` で射影行列を生成する。
  - 深度比較のバイアスパラメータを公開する必要がある。

### フラグメントシェーダーでのサンプリングブラー実装

- **Context**: 要件7でブラー品質プリセット（Low/Mid/High）が求められており、フラグメントシェーダーでの実装パターンを調査。
- **Sources Consulted**:
  - [Gaussian Blur Post Process in Unity URP - Daniel Ilett](https://danielilett.com/2023-06-01-tut6-6-gaussian-blur/)
  - [Blur Postprocessing Effect - Ronja's tutorials](https://www.ronja-tutorials.com/post/023-postprocessing-blur/)
- **Findings**:
  - ガウシアンブラーは `[KeywordEnum(Low, Medium, High)]` でシェーダーキーワードとして品質を切り替え可能。
  - Low: 5x5カーネル（約9サンプル）、Mid: 9x9カーネル（約25サンプル）、High: 13x13カーネル（約49サンプル）が現実的なプリセット。
  - 2パス（水平+垂直）分離フィルタは本ケースでは不適切（床面フラグメントシェーダーで1パスで完結させる必要がある）。
  - テクセル間の線形補間を利用した最適化により、サンプル数を半減可能。
  - 距離ボケは、フラグメントのワールド位置から光源までの距離に基づいてブラー半径を動的に変更することで実現。
- **Implications**:
  - `#pragma multi_compile` でブラー品質キーワードを定義し、コンパイル時にサンプル数を切り替える。
  - 距離ボケはブラー半径のスケーリングパラメータとして公開する。

### UPMパッケージレイアウト

- **Context**: 要件6.4でUPM形式の配布が求められており、パッケージ構成を確認。
- **Sources Consulted**:
  - [Unity Manual: Package layout for UPM packages](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-layout.html)
  - [Unity Manual: Create or edit the assembly definitions](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-asmdef.html)
- **Findings**:
  - 標準レイアウト: `package.json`, `Runtime/`, `Editor/`, `Samples~/`, `Documentation~/`
  - Assembly Definition (.asmdef) ファイルが各コードフォルダに必要。
  - `Samples~` フォルダはチルダ付きで通常のインポートからは除外され、Package Managerの「Samples」セクションから利用可能。
  - Runtime asmdef は URP パッケージへの参照が必要（`com.unity.render-pipelines.universal`）。
- **Implications**:
  - Runtime/ 配下にコア機能（C#スクリプト、シェーダー）を配置。
  - Editor/ 配下にGizmo描画等のエディタ専用機能を配置。
  - Samples~/ 配下にサンプルシーンと関連アセットを配置。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| MonoBehaviour Manager + RenderGraph UnsafePass | マネージャーMonoBehaviourが仮想光源を管理し、ScriptableRendererFeature/RenderPassがRenderGraph UnsafePassで深度レンダリングを実行 | CommandBuffer.DrawRendererへのフルアクセス、非破壊的描画、Unity標準パターン | UnsafePassはRenderGraph最適化の恩恵を受けられない | Unity 6.3 LTS対応として最適 |
| RasterRenderPass + DrawRendererList | RenderGraphのRasterRenderPassでRendererListを使用して描画 | RenderGraph最適化の恩恵、将来的な標準化 | DrawRendererListはフィルタリングにLayerMaskを使用するため、非破壊要件と矛盾する可能性 | Layer変更が必要になるリスクが高い |
| カメラベース深度レンダリング | 実際のCameraコンポーネントで深度テクスチャをレンダリング | シンプルな実装 | パフォーマンス問題、カメラ管理の複雑さ、URPカメラスタッキングの制約 | 要件の「仮想光源」概念と合致しない |

**選定**: MonoBehaviour Manager + RenderGraph UnsafePass 方式を採用。非破壊要件（5.1-5.4）を確実に満たしつつ、Unity 6.3 LTS のRenderGraph APIに対応できる。

## Design Decisions

### Decision: RenderGraph UnsafePass の採用

- **Context**: Unity 6.3 LTS で Compatibility Mode が非推奨化され、RenderGraph API の使用が必須。しかし要件5で非破壊的なRenderer描画が求められており、`CommandBuffer.DrawRenderer` へのアクセスが必要。
- **Alternatives Considered**:
  1. RasterRenderPass + DrawRendererList -- LayerMask フィルタリングが必要となり、非破壊要件に抵触する可能性。
  2. Compatibility Mode の Execute メソッド -- Unity 6.3 LTS で非推奨、将来の削除予定。
- **Selected Approach**: `AddUnsafePass` で `UnsafeGraphContext` を取得し、`CommandBufferHelpers.GetNativeCommandBuffer` で従来の CommandBuffer にアクセスして `DrawRenderer` を使用する。
- **Rationale**: 非破壊要件を確実に満たしつつ、Unity 6.3 LTS の RenderGraph API に準拠できる唯一の実用的な方法。
- **Trade-offs**: RenderGraph の自動最適化が効かないが、深度パスのみの限定的な使用であり影響は小さい。
- **Follow-up**: UnsafePass のパフォーマンス影響をプロファイリングで検証する。

### Decision: フラグメントシェーダーでの1パスブラー

- **Context**: 要件7.2-7.3で床面シェーダーでのサンプリングブラーが求められている。
- **Alternatives Considered**:
  1. 2パス分離フィルタ（水平+垂直） -- ポストプロセスとしては効率的だが、床面フラグメントシェーダーで完結させる要件に合わない。
  2. RenderTexture上での事前ブラー -- 追加のRenderPassが必要となり複雑化する。
- **Selected Approach**: 床面フラグメントシェーダー内でN×Nガウシアンサンプリングを1パスで実行。`multi_compile` キーワードで品質プリセットを切り替える。
- **Rationale**: 要件のTechnical Decisionsで「床面フラグメントシェーダーでのサンプリングブラー」が明示されており、これに準拠する。
- **Trade-offs**: High品質時のサンプル数が多い（最大49サンプル/フラグメント）が、深度テクスチャのサンプリングは軽量。
- **Follow-up**: High品質でのGPU負荷を測定し、必要に応じてサンプル数を調整。

### Decision: 仮想光源ごとの独立パラメータ構成

- **Context**: 要件3.4で仮想光源ごとの独立パラメータ設定が求められている。
- **Alternatives Considered**:
  1. グローバルパラメータ + 仮想光源ごとのオーバーライド -- 複雑だが柔軟。
  2. 仮想光源ごとに完全独立したパラメータ -- シンプルだが冗長。
- **Selected Approach**: 仮想光源ごとに完全独立したパラメータセットを持つ。共通デフォルト値はマネージャー側で提供しない（光源追加時の初期値としてのみ使用）。
- **Rationale**: 要件では各仮想光源が「独立した影を描画」とされており、パラメータも独立している方が直感的。
- **Trade-offs**: 複数光源で同一パラメータを使いたい場合に個別設定が必要だが、アニメーション/スクリプトでの制御で対応可能。

### Decision: 複数光源の影合成方式

- **Context**: 要件3.7で「加算合成 + 合成時倍率パラメータ」が求められている。
- **Selected Approach**: 各仮想光源の影をマルチパスで加算合成する。床面シェーダーは仮想光源の数だけ影判定を実行し、最終的にα値として加算合成する。倍率パラメータで全体的な合成強度を調整。
- **Rationale**: Technical Decisionsで「加算合成 + 合成時倍率パラメータ」が明示されている。
- **Trade-offs**: 光源数が多いほどフラグメントシェーダーの負荷が増加するが、深度テクスチャサンプリングは軽量であり実用的。

## Risks & Mitigations

- **RenderGraph UnsafePass のパフォーマンス** -- 深度パスのみの限定使用であり影響は小さい。プロファイリングで検証し、問題があれば DrawRendererList への移行を検討。
- **Unity 6.3 LTS の API 変更** -- Unity 6.3 LTS は長期サポート版であり、LTSサイクル中のAPI破壊的変更リスクは低い。ただしRenderGraph APIは比較的新しいため、マイナーアップデートでの変更に注意。
- **フラグメントシェーダーでの複数光源ブラー処理の負荷** -- 光源数 x ブラーサンプル数がフラグメントコストに直結。上限光源数のガイドラインをドキュメントで提示し、パフォーマンスプロファイリングを推奨。
- **SkinnedMeshRenderer の DrawRenderer 互換性** -- CommandBuffer.DrawRenderer は SkinnedMeshRenderer に対応しているが、BoneWeight の更新タイミングに依存する可能性がある。Unity のデフォルト動作では描画時点の最新ボーン状態が使用される。

## References
- [Unity Manual: Compatibility Mode in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/compatibility-mode.html) -- Unity 6.3 での Compatibility Mode 非推奨化の公式情報
- [Unity Manual: Use the CommandBuffer interface in a render graph (UnsafePass)](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-unsafe-pass.html) -- UnsafePass API の公式ドキュメント
- [Unity Scripting API: CommandBuffer.DrawRenderer](https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.DrawRenderer.html) -- DrawRenderer API リファレンス
- [Custom Shadow Mapping in Unity - Shahriar Shahrabi](https://shahriyarshahrabi.medium.com/custom-shadow-mapping-in-unity-c42a81e1bbf8) -- プロジェクティブテクスチャマッピングによるカスタムシャドウ実装例
- [Custom Renderer Features - Cyanilux](https://www.cyanilux.com/tutorials/custom-renderer-features/) -- ScriptableRendererFeature/RenderPass の実装チュートリアル
- [Gaussian Blur Post Process in Unity URP - Daniel Ilett](https://danielilett.com/2023-06-01-tut6-6-gaussian-blur/) -- URP でのガウシアンブラー実装例
- [Unity Manual: Package layout for UPM packages](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-layout.html) -- UPM パッケージレイアウトの公式ガイド
- [Unity Manual: Write a render pass using the render graph system](https://docs.unity3d.com/Manual//urp/render-graph-write-render-pass.html) -- RenderGraph でのカスタムレンダーパスの書き方
