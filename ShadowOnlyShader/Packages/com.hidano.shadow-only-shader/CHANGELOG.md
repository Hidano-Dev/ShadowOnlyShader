# 変更履歴

このプロジェクトに対する主な変更はこのファイルに記録されます。

フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に基づき、
[セマンティック バージョニング](https://semver.org/lang/ja/) に準拠しています。

## [0.1.1] - 2026-03-09

### 修正

- Runtime asmdefに`Unity.RenderPipelines.Core.Runtime`への参照を追加し、`RenderGraphModule`のコンパイルエラーを修正
- package.jsonのdependenciesに`com.unity.render-pipelines.core`を追加
- Tests/Editor asmdefにテスト用アセンブリ参照(`UnityEngine.TestRunner`、`UnityEditor.TestRunner`等)を追加

## [0.1.0] - 2026-03-09

### 追加

- 初回リリース
