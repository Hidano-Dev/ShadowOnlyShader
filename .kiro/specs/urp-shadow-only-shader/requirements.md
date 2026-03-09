# Requirements Document

## Introduction
本ドキュメントは、Unity URP (Universal Render Pipeline) 向けの「影だけを表示するシェーダー」機能の要件を定義します。Unity 6.3 LTS のURPを対象とし、キャラクターコンテンツ向けに透明な床の上に影のみを描画する仕組みを提供します。

本システムは、仮想光源位置に配置したDepthOnlyカメラで深度テクスチャ（RenderTexture）をレンダリングし、そのテクスチャをプロジェクティブテクスチャマッピングで床面シェーダーに投影することで影領域を判定します。UnityのLightコンポーネントやシャドウマップには依存しません。

リアルタイム描画（VTuber配信等）および静止画・動画レンダリングの両方のユースケースに対応します。

## Technical Decisions

以下は、要件定義フェーズで確定した技術的決定事項です。

### 影生成アーキテクチャ
- **手法**: プロジェクティブテクスチャ方式（仮想光源位置のDepthOnlyカメラで深度テクスチャを生成し、床面にプロジェクションマッピング）
- **URP統合**: ScriptableRendererFeature + ScriptableRenderPass（深度パスのみ担当、床面は通常のURP描画）
- **描画方式**: ScriptableRenderPass内でCommandBufferを使用してキャスターを深度RenderTextureに直接描画（Layer変更不要、完全非破壊）
- **シェーダー**: HLSL手書き（.shader）
- **RendererFeature登録**: 手動登録（ドキュメントで案内）

### 仮想光源
- **投影方式**: Orthographic / Perspective 両方サポート（光源ごとに切り替え可能）
- **光源数**: 制限なし（動的リスト）
- **Perspectiveパラメータ**: FOV + Near/Far
- **Orthographicパラメータ**: OrthographicSize + Near/Far
- **テクスチャ解像度**: 仮想光源ごとにInspectorで設定可能

### 影の合成
- **合成方式**: 加算合成 + 合成時倍率パラメータ

### ブラー
- **実装箇所**: 床面フラグメントシェーダーでのサンプリングブラー
- **品質**: Low/Mid/High プリセットで切り替え
- **距離ボケ**: 光源からの距離に応じたボケ変化をサポート

### 色表現
- **Hue Shift**: ベース影色 + Hue Shiftパラメータ（度数）
- **色収差**: RGBチャンネル分離方式（影の中心からの放射方向にオフセット）

### コンポーネント構成
- **構成**: マネージャーコンポーネント（親）+ 仮想光源を子GameObjectとして管理
- **キャスター指定**: ルートGameObject指定 → 子階層のRenderer自動収集
- **アニメーション同期**: SkinnedMeshRendererはコピーせず直接描画参照

### 床面
- **床面メッシュ**: ユーザーが用意（自動生成しない）
- **投影面**: 床面オブジェクトのTransformに応じた任意平面
- **複数床面**: サポート（マネージャーに複数床面Rendererを登録可能）

### パッケージ・API
- **配布形態**: Unity Package (UPM)
- **ランタイムAPI**: 基本的なPublic API（光源の追加・削除、パラメータ変更）
- **エディタ拡張**: デフォルトInspectorで十分（カスタムInspector不要）
- **Gizmo**: あり（仮想光源の方向・範囲をSceneビューに表示）
- **サンプルシーン**: あり

## Requirements

### Requirement 1: 影の描画
**Objective:** コンテンツ制作者として、透明な床の上にキャラクターの影だけを表示したい。それにより、影を任意の背景と合成できるようにするため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall 透明な床面上にキャスターオブジェクトの影のみを描画する
2. The ShadowOnlyShader shall 影以外の床面部分を完全に透明に保つ
3. The ShadowOnlyShader shall 仮想光源位置に配置したDepthOnlyカメラで深度テクスチャをレンダリングし、プロジェクティブテクスチャマッピングで床面に投影して影領域を判定する
4. The ShadowOnlyShader shall ScriptableRendererFeature + ScriptableRenderPass内でCommandBufferを使用してキャスターを深度RenderTextureに描画する
5. The ShadowOnlyShader shall Unity 6.3 LTS バージョンのURPで動作する

### Requirement 2: 影の外観調整
**Objective:** コンテンツ制作者として、影の濃さや色を調整したい。それにより、演出やシーンに合わせた影の表現ができるため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall 影の濃さ（アルファ値）をパラメータとして公開する
2. The ShadowOnlyShader shall 影の色をパラメータとして公開する
3. When 影の濃さパラメータが変更された場合, the ShadowOnlyShader shall 描画される影の透明度をリアルタイムに反映する
4. When 影の色パラメータが変更された場合, the ShadowOnlyShader shall 描画される影の色をリアルタイムに反映する

### Requirement 3: 複数仮想光源対応
**Objective:** コンテンツ制作者として、複数の仮想光源からの影を同時に表示したい。それにより、UnityのLightコンポーネントに依存せず軽量かつ柔軟な影表現ができるため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall UnityのLightコンポーネントやシャドウマップに依存せず、仮想光源位置のDepthOnlyカメラによる深度テクスチャで影を生成する
2. The ShadowOnlyShader shall 仮想光源を制限なしの動的リストで複数定義し、それぞれ独立した影を描画できる
3. The ShadowOnlyShader shall 各仮想光源の位置・方向を子GameObjectのTransformで個別に設定できる
4. The ShadowOnlyShader shall 仮想光源ごとに影の外観パラメータ（濃さ・色・ボケ・パース等）を個別に設定できる
5. The ShadowOnlyShader shall 仮想光源ごとにOrthographic/Perspectiveの投影方式を選択できる
6. The ShadowOnlyShader shall 仮想光源ごとに深度テクスチャの解像度をInspectorから設定できる
7. The ShadowOnlyShader shall 複数仮想光源の影を加算合成し、合成時の倍率パラメータで調整できる

### Requirement 4: セットアップ
**Objective:** コンテンツ制作者として、最小限の手順でセットアップを完了したい。それにより、設定の手間や人為的ミスを最小限にするため。

#### Acceptance Criteria
1. When マネージャーコンポーネントがGameObjectにアタッチされた場合, the ShadowOnlyShader shall 影描画に必要なMaterialを自動生成する
2. The ShadowOnlyShader shall 床面メッシュはユーザーが用意したRendererを使用し、自動生成しない
3. The ShadowOnlyShader shall マネージャーコンポーネントに複数の床面Rendererを登録できる
4. The ShadowOnlyShader shall 自動生成されたリソース（Material、RenderTexture等）をマネージャーのライフサイクルに従って破棄する
5. The ShadowOnlyShader shall マネージャーコンポーネントのInspectorから影の外観パラメータを設定できるようにする
6. The ShadowOnlyShader shall ScriptableRendererFeatureのURP Rendererへの手動登録手順をドキュメントで案内する

### Requirement 5: 対象Rendererの非破壊的取り扱い
**Objective:** コンテンツ制作者として、影を落とす対象のRendererコンポーネントを変更せずに影を生成したい。それにより、既存のオブジェクト設定に影響を与えないため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall 影を落とす対象のRendererコンポーネントに変更を加えないこと（Layer変更、Material変更、コンポーネント追加等を行わない）
2. The ShadowOnlyShader shall 任意のRendererクラス（MeshRenderer、SkinnedMeshRenderer等）を影の投影元として指定できること
3. The ShadowOnlyShader shall CommandBufferで対象Rendererを直接描画参照し、Rendererのコピーやレイヤー変更を行わないこと
4. The ShadowOnlyShader shall SkinnedMeshRendererのアニメーション状態をコピーせずに直接参照し、影にリアルタイムに反映すること
5. The ShadowOnlyShader shall キャスター指定にルートGameObjectを指定すると、その子階層の全Rendererを自動収集すること

### Requirement 6: スコープとプラットフォーム制約
**Objective:** 開発者として、対象レンダーパイプラインとUnityバージョンの範囲を明確にしたい。それにより、開発・テスト対象を適切に限定するため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall URP (Universal Render Pipeline) のみを対象とする
2. The ShadowOnlyShader shall Unity 6.3 LTS のURPバージョンで動作を保証する
3. The ShadowOnlyShader shall Built-in Render Pipeline および HDRP では動作対象外とする
4. The ShadowOnlyShader shall Unity Package (UPM) 形式で配布する

### Requirement 7: 影の表現力
**Objective:** コンテンツ制作者として、影のパース・ボケ具合・色相のズレ・色収差を制御したい。それにより、演出意図に合わせたリッチな影表現を実現するため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall 仮想光源からの距離に応じた影のパース（拡大・縮小・歪み）を表現できる
2. The ShadowOnlyShader shall 影のボケ具合（ブラー強度）をパラメータとして公開し、距離に応じたソフトシャドウを表現できる
3. The ShadowOnlyShader shall ブラー品質をLow/Mid/Highのプリセットで切り替えられる
4. The ShadowOnlyShader shall 影の色相ズレ（Hue Shift）をパラメータ（度数）として公開し、ベース影色に対して色相を回転できる
5. The ShadowOnlyShader shall 影の色収差（Chromatic Aberration）をパラメータとして公開し、RGBチャンネルを影の中心からの放射方向に分離して表現できる
6. The ShadowOnlyShader shall パース・ボケ・色相ズレ・色収差の各パラメータをInspectorからリアルタイムに調整できる

### Requirement 8: コンポーネント構成
**Objective:** コンテンツ制作者として、仮想光源を直感的に追加・削除・配置したい。それにより、Sceneビュー上で視覚的に影の設定を行えるため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall マネージャーコンポーネントを親GameObjectに配置し、仮想光源を子GameObjectとして管理する構成を採用する
2. The ShadowOnlyShader shall 仮想光源の子GameObjectのTransformで光源の位置・方向を制御できる
3. The ShadowOnlyShader shall 仮想光源の方向・描画範囲をSceneビューでGizmo表示する
4. The ShadowOnlyShader shall 基本的なPublic API（光源の追加・削除、パラメータ変更）をスクリプトから利用可能にする

### Requirement 9: サンプルシーン
**Objective:** コンテンツ制作者として、すぐに動作確認できるサンプルシーンがほしい。それにより、セットアップ手順を理解しやすくするため。

#### Acceptance Criteria
1. The ShadowOnlyShader shall UPMパッケージにサンプルシーンを含める
2. The ShadowOnlyShader shall サンプルシーンにセットアップ済みのマネージャー・仮想光源・キャスター・床面を含める
3. The ShadowOnlyShader shall サンプルシーンでRendererFeatureが登録済みのURP Renderer設定を含める
