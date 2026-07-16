# 変更履歴

このプロジェクトに対する主な変更はこのファイルに記録されます。

フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づき、
[セマンティック バージョニング](https://semver.org/lang/ja/) に準拠しています。

## [0.9.0] - 2026-07-16

### 追加

- Edit モードでの影プレビューに対応（`ShadowOnlyManager` / `VirtualLight` を `[ExecuteAlways]` 化）
  - Play しなくても Scene / Game ビューで影が表示され、床面の白いデフォルトマテリアルも Edit モード中から透明な影マテリアルに置き換わる
  - シーンファイルの汚染防止: 床 Renderer へ割り当てる自動生成マテリアル（`HideFlags.DontSave`）は、シーン保存・Prefab 編集モードでの保存の直前に退避しておいた元のマテリアルへ自動復元され、保存後に再割り当てされる。Manager の無効化・破棄時や `RemoveFloorRenderer` 時も同様に復元されるため、プレビューによって `.unity` / `.prefab` ファイルに差分が入らない
  - `SourceLight` を設定した VirtualLight も Edit モードでプレビュー表示される。Edit モード中は Light の Transform・タイプ・range・shadowStrength を非破壊で参照して影を描画し（`EffectiveXxx` アクセサ）、シリアライズ値への同期書き込みは従来どおり Play モード専用のためシーンに差分は入らない。Scene ビューの Gizmo も同じ実効値で描画される
  - RendererFeature が Preview カメラ（マテリアル / Inspector プレビュー）と Reflection Probe カメラで影パスをスキップするようになった。特に Reflection Probe は従来 Resolve RT をプローブ解像度で上書きする問題があったため修正を兼ねる

### 既知の制限

- 床 Renderer が Prefab インスタンスの場合、Edit モード中に「Apply to Prefab」を実行すると自動生成マテリアルの Missing 参照が Prefab アセットに書き込まれる可能性があります。床のマテリアルを Prefab に Apply する際は一度 Manager を無効化してください

## [0.8.0] - 2026-07-10

### 追加

- Fit To Casters（投影範囲のキャスター自動追従）を追加
  - VirtualLight に `Fit To Casters`（デフォルト無効）を追加。有効にすると Orthographic モードの投影範囲が、キャスター全体の合成 Bounds を覆うオフセンター正射影として毎フレーム自動算出される
  - ライトの角度を変えてもキャスターが投影範囲から外れず、投影範囲が必要最小限に保たれるためテクセル密度が高くなる。広い `Orthographic Size`（テクセル実寸の粗さ）に起因するブラーの縞状ゴーストの実用的な対策になる
  - 投影範囲には片側 10% の相対マージンに加え、ブラーカーネルが UV 外を参照しないよう `Blur Radius` × カーネル半径（Manager の Blur Quality に連動）分のテクセル余白を確保する
  - 投影ウィンドウ中心を 1 テクセル単位に量子化（テクセルスナップ）し、光源・キャスター移動時の影の揺らぎ（shadow swimming）を抑制する
  - 投影ウィンドウは正方形（テクセルが正方形）を維持し、ブラーの等方性を保つ
  - キャスターが 1 つも存在しない場合は従来どおり `Orthographic Size` にフォールバックする。Perspective / Point モードでは無視される
  - `IVirtualLight` に `FitToCasters` プロパティを追加
  - Inspector: `Fit To Casters` は Orthographic 時のみ表示され、有効時は `Orthographic Size` をグレーアウトする。Scene ビューの Gizmo はフィット後の実際の投影範囲を表示する

## [0.7.1] - 2026-07-10

### 修正

- `.meta` ファイルの不正な GUID を修正（14 個をランダム値で再生成）
  - 初期作成分の GUID が連番ローテーションパターン（`a1b2c3d4e5f6...` の 1 文字シフト系列）で生成されており、`Editor.meta` は桁余りの 33 文字となって「GUID を抽出できない / アセットが無視される」警告が発生していた。連番パターンは他プロジェクトとの GUID 衝突リスクもあるため全廃
  - シーン（SampleScene / Samples~ のサンプルシーン）と URP Renderer アセットのスクリプト参照も同じマッピングで一括更新済み
  - **注意**: 0.7.0 を利用中のプロジェクトが 0.7.1 に更新すると、シーンに配置済みの本パッケージのコンポーネント（`ShadowOnlyManager` / `VirtualLight` / `ShadowOnlyRendererFeature` など）の参照が GUID 変更により Missing になります。該当コンポーネントの再アタッチと Renderer Feature の再追加が必要です

## [0.7.0] - 2026-07-10

### 追加

- ポイントライト（全方向）対応: `ProjectionMode.Point` を追加
  - FOV 90° の透視投影 6 面（±X/±Y/±Z、ワールド軸基準）で全方向に影を投影する。従来は Point ライトが単一の Perspective 投影（デフォルト FOV 60°、正面方向のみ）で近似されており、視錐台に入ったキャスターしか影が出なかった
  - 面の向きはワールド軸固定のため、光源の Transform を回転させても影は変化しない（ポイントライトとして物理的に正しい挙動）
  - `SourceLight` に Point タイプの Light を設定すると `ProjectionMode.Point` に自動同期し、`range` が Far Clip Plane に反映される
  - Point モードの光源は深度 Texture2DArray を 6 スライス消費する（同時描画上限 32 スライスのため、Point ライトは最大 5 個 + 通常光源 2 個など）。残りスライスに収まらない光源は丸ごとスキップされ、後続の小さい光源は描画される
  - `IVirtualLight` に `SliceCount` プロパティと `GetSliceViewMatrix(int)` メソッドを追加
  - Inspector: Point モード時は FOV / Orthographic Size を非表示にし、6 スライス消費の注記を表示。Scene ビューの Gizmo は Near/Far 範囲をワイヤー球で表示
  - 診断: キャスター・床面の投影範囲判定を 6 面フラスタムに対応し、上限超過警告をスライス数ベースに変更
- 接地ダークニング（コンタクト・ダークニング）を追加
  - キャスターと受影面の距離が近い部分（足元など）の影を濃くし、AO のような設置感を影パイプライン内で表現する
  - VirtualLight に `Contact Darkening Strength`（ベースの Shadow Alpha に上乗せする倍率、0 で無効）と `Contact Darkening Range`（Near〜Far 間を 0〜1 とした深度差での効果範囲）を追加。Inspector では Effects フォールドアウト内に表示され、Range は Strength > 0 のときのみ表示される
  - ベースの Shadow Alpha を薄めに設定すると「全体は薄い影、接地部分だけ濃い」表現が 1 光源で完結する
  - 実装は PCSS（Contact Hardening）と同じブロッカーサーチを再利用しており、Contact Hardening 有効時の追加コストはごく小さい。ブラーキーワード無効時（BlurQuality キーワードなし）は適用されない
  - シェーダー内部のライト射影空間変換を `ComputeShadowCoords` ヘルパーに抽出（`ComputeShadowInternal` と接地ダークニングで共有）

### 変更

- キャスタールートの指定を ShadowOnlyManager での一括指定に変更
  - Manager に `Default Caster Root` を追加。未設定の VirtualLight はこの共通設定を影の投影元として使用する
  - VirtualLight 側の `Caster Root` は個別上書き用に変更（設定した光源のみ、その配下が影の元になる）
  - `IVirtualLight` に実効ルートを返す読み取り専用プロパティ `EffectiveCasterRoot` を追加、`IShadowOnlyManager` に `DefaultCasterRoot` を追加
  - Manager 側の `Default Caster Root` を実行中に差し替えた場合も、次フレームの収集で自動的に反映される（VirtualLight 側への明示的な通知は不要）
  - Inspector: VirtualLight の Caster Root が未設定のとき、実際に使用される Manager の Default Caster Root を情報表示。診断のキャスター未設定エラーは共通・個別の両方を案内するメッセージに変更

## [0.6.0] - 2026-07-09

### 変更

- 仮想光源の同時描画上限を 8 個から 32 個に拡大
  - 上限は Unity の Light 数制限とは無関係で、本パッケージのシェーダー uniform 配列サイズに由来する（`ShadowOnlyManager.MaxVirtualLights` と `ShadowOnlyFloorCommon.hlsl` の `MAX_VIRTUAL_LIGHTS` を連動して変更可能）
  - 上限を超えた場合は先頭の 32 個のみが描画される（従来どおり超過分は無視、他の光源の描画には影響しない）
- 診断表示の分離
  - VirtualLight 個別の診断（CasterRoot 未設定 / 投影範囲外 / ShadowAlpha=0 / DepthBias 過大 / Texture Resolution 過大 / Manager 配下にない）を各 VirtualLight の Inspector 上部に表示するよう変更
  - ShadowOnlyManager 側には「どの光源にエラー・警告が何件あるか」の集約 1 件のみを表示（灯体数が多い場合の診断リスト肥大を解消）
- 深度テクスチャ解像度の診断を Manager 側に集約し、設定の出どころを明示
  - 実際に使われる解像度は「最初のアクティブな VirtualLight」の設定で決まるため、光源ごとの警告をやめ、由来（VirtualLight の Texture Resolution 明示指定か、URP アセットの Main Light Shadow Resolution か）と修正手順、全スライス合計の GPU メモリ量を 1 件で報告

### 修正

- 深度 Texture2DArray の作成失敗（高解像度 × 多スライスによる GPU メモリ不足等）時に影が一切表示されなくなる問題を修正
  - 作成に失敗した場合は解像度を半減しながらリトライし、警告ログを出して描画を継続する（下限 256）

## [0.5.1] - 2026-07-09

### 追加

- 診断機能（`ShadowOnlyDiagnostics`）
  - 「影が表示されない」原因になり得る設定・状態を自動検出し、ShadowOnlyManager の Inspector 上部に HelpBox で一覧表示
  - パイプライン系: URP 未使用 / ShadowOnlyRendererFeature の未登録・無効化 / Quality 設定側 URP アセットの差し替え漏れ（Graphics 設定のみ変更の落とし穴）を検出
  - 環境系: 必須シェーダー（DepthOnly / Floor / FloorDisplay）の欠落・コンパイル不可、Texture2DArray・深度フォーマット非対応を検出
  - Manager 系: 非 Play モードの注意喚起 / Manager 無効 / 複数 Manager / `BlendMultiplier = 0` を検出
  - VirtualLight 系: 光源なし・全て非アクティブ・上限（8 個）超過 / Manager 配下にない光源 / CasterRoot 未設定・Renderer なし・全て非表示 / キャスターが投影フラスタム外 / `ShadowAlpha = 0`（SourceLight の Shadow Strength 由来も明示） / DepthBias 過大 / 深度テクスチャ解像度過大（4096 以上）を検出
  - 床面系: 未登録 / null 要素 / 全て非表示 / 床がキャスターに含まれる（自己投影） / 床がどの光源の投影範囲にも入っていない / Play 中のマテリアル上書きを検出
  - 診断は約 2 秒間隔で自動更新され、「再診断」ボタンで即時更新も可能

## [0.5.0] - 2026-06-19

### 変更

- 複数仮想光源の影合成を加算から Screen 合成（`finalAlpha = 1 - Π(1 - aᵢ)`）に変更
  - 影が重なった部分が加算（`Σ aᵢ`）で過剰に濃くなる問題を解消。個々の影を薄くしても重なりだけ濃くなる現象がなくなり、重なっても不透明度は最大 1 に漸近するのみ
  - 単一描画パス（`ComputeFloorShadow`）とライトごと Resolve パス（GPU ブレンド `Blend One One, One OneMinusSrcAlpha`）の両経路を整合
  - 各ライトのアルファ寄与を `[0, 1]` に `saturate` してから合成

### 修正

- 影の位置ずれ・前後（V 方向）反転を修正（複数の独立原因）
  - RenderGraph UnsafePass で深度パス・Resolve パスにビューポート（`SetViewport`）が未設定で、縦画面等のアスペクト比依存ずれが発生していた問題
  - Resolve パスがカメラの View/Projection 行列を継承前提で未設定だったため、低解像度 RT 内で床が縮小・隅寄りに描画されていた問題（URP 実描画行列 `GetViewMatrix`/`GetProjectionMatrix` を明示設定）
  - RenderScale ≠ 1 で Display シェーダーの `screenUV` がずれて影が縮小・隅寄りになる問題（`GetNormalizedScreenSpaceUV` に置換）
  - 深度 RT のサンプリング行列（`GL.GetGPUProjectionMatrix`）と実描画（`SetViewProjectionMatrices`）の Y 反転の食い違いを補正し、影の前後反転を解消

## [0.4.0] - 2026-03-22

### 追加

- 低解像度 Resolve パス（`ShadowOnlyShadowResolvePass`）
  - `BlurResolutionScale` パラメータで影計算の解像度を 0.1〜1.0 の範囲で制御
  - 1.0 未満の場合、重いブラー・PCSS 計算を低解像度 RT で事前実行し、結果をサンプリングする 2 パス構成に自動切替
  - Per-light 分割描画と Single-draw 一括描画の自動切替（RT 面積に応じた GPU キャッシュ最適化）
- FloorDisplay シェーダー（`Hidden/ShadowOnlyShader/FloorDisplay`）
  - Resolve 結果テクスチャをサンプリングするだけの軽量シェーダー
  - Resolve 有効時にフロア Renderer のマテリアルを自動切替
- シェーダーコードの共通化（`ShadowOnlyFloorCommon.hlsl`）
  - Floor シェーダーの影計算ロジックを共有 hlsl に分離し、3 パス（Display / Per-light Resolve / Single-draw Resolve）で再利用
- SourceLight 同期の状態保存・復元機能
  - `SourceLight` 設定前の Transform・投影パラメータを自動保存し、解除時に復元
  - ランタイムでの SourceLight 着脱に対応
- 深度テクスチャの Texture2DArray 化
  - 全 VirtualLight の深度テクスチャを統一解像度の Texture2DArray として一元管理
  - アクティブライト数に応じたスライス数の動的調整でメモリ消費を最適化

### 改善

- ShadowOnlyManager Editor の大幅改善
  - BlurResolutionScale スライダーの追加
  - VirtualLight 一覧の表示改善
- VirtualLight Editor の大幅改善
  - SourceLight 同期状態に応じた表示切替の改善
  - ProjectionMode に応じたパラメータの表示/非表示制御
- 配列化による Material パラメータ転送の最適化（SetXxx 呼び出し数 ~100 回/フレーム → ~15 回/フレーム）
- Renderer リストのオブジェクトプール化による GC アロケーション回避
- Perspective 投影時の深度バイアスを 1/w² でスケーリングし、投影モードに依存しない一貫した深度比較を実現

## [0.3.0] - 2026-03-13

### 追加

- コンタクトハードニング（PCSS）機能 — キャスターとの距離に応じた自然な半影表現
  - `ContactHardeningStrength` パラメータで仮想光源サイズを制御
  - ブロッカーサーチによる平均遮蔽深度の推定とペナンブラ幅の動的計算
  - 0 で無効（従来の均一ブラー）、値が大きいほど遠方の影がより柔らかくなる
- カメラ距離ベースの影効果
  - `BlurCameraDistanceFactor` — カメラからの距離に応じたブラー半径の増加
  - `AlphaCameraDistanceFactor` — カメラからの距離に応じた影の透明度減衰
  - `CameraDistancePower` — 距離カーブのべき乗指数（1=線形、>1で遠方急変化、<1で近くから効く）

### 修正

- RenderGraph UnsafePass でカメラのカラー/デプスターゲットが復元されない問題を修正
  - Unity Recorder 等の非デフォルトレンダーターゲット環境で後続パスの描画が壊れる問題を解消
- キャスター Renderer が 0 件の場合に深度テクスチャがクリアされない問題を修正

### 改善

- VirtualLight Editor で Orthographic モード時に FieldOfView を自動非表示
- VirtualLight 新規作成時のデフォルト位置を (0, 5, 0) に変更（地面から離れた自然な初期位置）

## [0.2.1] - 2026-03-10

### 改善

- SkinnedMeshRenderer 対応をドキュメントに明記（セットアップ手順・特徴欄）
- package.json に `skinned-mesh`・`character` キーワードを追加

## [0.2.0] - 2026-03-10

### 追加

- 光源色による色収差フリンジの物理ベース変調（SourceLight プロパティ）
  - 暖色光なら赤フリンジが強く、寒色光なら青フリンジが強くなる物理的に正しい挙動
  - SourceLight 未設定時は ChromaticAberrationColor をフォールバックとして使用
- VirtualLight の全 Inspector パラメータに Tooltip 属性を追加
- VirtualLight カスタム Inspector — SourceLight 設定時に ChromaticAberrationColor を自動非表示

### 修正

- RenderPass の不具合を修正
- Orthographic 投影で影が Caster 形状にならない不具合を修正
- Perspective 投影で影が表示されない不具合を修正

## [0.1.1] - 2026-03-09

### 修正

- Runtime asmdefに`Unity.RenderPipelines.Core.Runtime`への参照を追加し、`RenderGraphModule`のコンパイルエラーを修正
- package.jsonのdependenciesに`com.unity.render-pipelines.core`を追加
- Tests/Editor asmdefにテスト用アセンブリ参照(`UnityEngine.TestRunner`、`UnityEditor.TestRunner`等)を追加

## [0.1.0] - 2026-03-09

### 追加

- 初回リリース
