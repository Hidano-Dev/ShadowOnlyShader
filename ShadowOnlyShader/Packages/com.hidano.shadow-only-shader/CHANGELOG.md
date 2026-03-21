# 変更履歴

このプロジェクトに対する主な変更はこのファイルに記録されます。

フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づき、
[セマンティック バージョニング](https://semver.org/lang/ja/) に準拠しています。

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
