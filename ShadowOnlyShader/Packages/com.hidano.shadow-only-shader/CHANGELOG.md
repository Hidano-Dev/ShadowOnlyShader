# 変更履歴

このプロジェクトに対する主な変更はこのファイルに記録されます。

フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づき、
[セマンティック バージョニング](https://semver.org/lang/ja/) に準拠しています。

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
