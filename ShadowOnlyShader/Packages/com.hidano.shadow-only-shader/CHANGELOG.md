# 変更履歴

このプロジェクトに対する主な変更はこのファイルに記録されます。

フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づき、
[セマンティック バージョニング](https://semver.org/lang/ja/) に準拠しています。

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
