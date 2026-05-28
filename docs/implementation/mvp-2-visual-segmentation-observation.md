# MVP 2: Visual Segmentation Observation

## Goal

MVP
1で接続したML-Agents環境に、ロボット前方のセグメンテーション画像観測を追加する。

MVP
2では、前方カメラから見た通行可能領域と通行不可能領域を2色で表現し、その画像を既
存の4値Vector Observationと併用して学習入力にする。

画像は後処理で変換しない。Unityシーン内の学習用表示を直接2色のマテリアルで構成し
、ML-AgentsのCamera Sensorで撮影する。

## Non-goals

- Goalを画像観測に含めること - 画像スタックを使うこと -
  セグメンテーション画像だけで学習すること - LiDAR風レイ観測を追加すること -
  本格的な報酬shaping - カリキュラム学習 - 推論評価基盤

## Observation Design

MVP 2の観測は、視覚観測とベクトル観測を併用する。

### Visual Observation

前方カメラで撮影した84x84の2色セグメンテーション画像を使う。

| 項目 | 内容 |
| --- | --- |
| 解像度 | 84x84 |
| カメラ位置 | ロボット前方 |
| カメラ方向 | ロボット正面 |
| 画像スタック | 1 |
| 通行可能領域 | 緑 |
| 通行不可能領域 | 青 |
| Goal | 画像には含めない |

通行不可能領域には、壁、障害物、段差、未知領域を含める。MVP
2ではこれらを細分化せず、すべて青で表現する。

画像スタックは使わない。まずは現在フレームのみを入力し、時間方向の情報が必要にな
った場合は後続フェーズで検討する。

### Vector Observation

MVP 1と同じ4値Vector Observationを併用する。

| Index | Value | 用途 |
| --- | --- | --- |
| 0 | `normalizedDistanceToGoal` | 正規化されたGoal距離 |
| 1 | `normalizedSignedAngleToGoal` | 正規化されたGoal方向角 |
| 2 | `distanceToGoal` | 生のGoal距離 |
| 3 | `signedAngleToGoalDegrees` | 生のGoal方向角 |

Goalはセグメンテーション画像に含めないため、Goal方向とGoal距離はVector
Observationで渡す。画像観測は前方の通行可能性、ベクトル観測は目標への相対情報を
担当する。

## Scene Strategy

MVP 1の`Mvp1SceneBuilder`は変更せず、MVP 2用に別のBuilderを追加する。

候補名:

- `Mvp2SceneBuilder`

推奨GameObject名:

- `Mvp2Bootstrap`

MVP 2では、MVP 1の基本構成に以下を追加する。

- Segmentation Camera - Camera Sensor - Segmentation materials - Segmentation
  layer / culling mask - Segmentation image capture mode - Segmentation preview
  overlay

## Segmentation Rendering

セグメンテーション画像は画像変換ではなく、学習用表示を直接撮影して作る。

基本方針:

- 床などの通行可能領域には緑のUnlit Materialを割り当てる -
  壁、障害物、段差、未知領域には青のUnlit Materialを割り当てる -
  Goalはセグメンテーションカメラのculling maskから除外する -
  通常デバッグ表示と学習用セグメンテーション表示は、必要に応じてLayerやMaterial
  管理で分離する

Goalを画像に含めないため、Goal markerは通常表示用Layerに置き、Segmentation
Cameraからは見えないようにする。

## Behavior

MVP 1の`Mvp1Navigation`とは別Behaviorにする。

候補名:

- `Mvp2VisualNavigation`

これにより、ベクトルのみのMVP 1と、画像+ベクトルのMVP 2を比較しやすくする。

Action SpaceはMVP 1と同じく2次元連続値にする。

| Action | 内容 |
| --- | --- |
| 0 | 前進速度`v`。Unity側`AgentMotor`で`[0, 1]`へ丸め、2倍にして移動へ入力する。負の出力は後退ではなく停止として扱う |
| 1 | 角速度`omega`。ML-Agents出力は`[-1, 1]`で、Unity側ではこの範囲のまま角速度指令として扱う |

離散行動は使わない。

## Segmentation Capture Mode

学習用セグメンテーション画像を確認するため、画像保存モードを用意する。

保存モードはデバッグ用途に限定し、通常学習では無効にする。

初期設定案:

| 項目 | 値 |
| --- | --- |
| `saveSegmentationFrames` | `false` |
| `saveEverySteps` | `100` |
| output directory | `Application.persistentDataPath/SegmentationCaptures` |

毎ステップ保存はI/O負荷が大きいため避ける。画像が正しく2色化されているか、Goalが
写っていないか、カメラ画角が適切かを確認するために使う。

## Segmentation Preview Overlay

学習中に、Segmentation Cameraが実際に見ている画像をUnity Game
Viewの右下へ小さく表示するデバッグモードを用意する。

初期実装では、追加のUI Canvasや別系統のプレビューCameraは使わず、Camera
Sensorが参照するSegmentation Cameraそのものの`targetTexture`を`OnGUI`でGame
View右下に表示する。これにより、学習入力とプレビュー表示が同じCameraに基づく。

初期設定案:

| 項目 | 値 |
| --- | --- |
| `showSegmentationPreview` | `true` |
| preview rect | `x=0.78, y=0.02, w=0.2, h=0.2` |
| 表示方式 | RenderTexture + `OnGUI` |

プレビューは学習入力の確認用であり、通常の学習結果には影響させない。描画負荷やGa
me
View上の邪魔が気になる場合は、`showSegmentationPreview`を`false`にして無効化でき
るようにする。

ML-Agentsの`CameraSensorComponent.RuntimeCameraEnable`は参照先Cameraの`enabled`
状態を上書きする場合があるため、プレビュー有効時は`RuntimeCameraEnable`も有効に
する。さらに`SegmentationPreviewOverlay`が毎フレーム`targetTexture`と`enabled`状
態を復元し、表示用RenderTextureを保つ。

将来、枠線、ラベル、複数センサー表示などが必要になった場合は、RenderTextureとRaw
Imageを使うUI表示へ移行する。

## Training Config

MVP 2用に、MVP 1とは別の学習設定ファイルを追加する。

候補:

- `configs/ml-agents/mvp2-visual-navigation.yaml`

初期方針:

- trainerはPPOを使う - visual encoderはまず軽量な設定にする -
  84x84画像と4値Vector Observationを併用する - time-scaleはMVP
  1より慎重に調整する

初回は接続確認とstep進行を優先し、報酬やネットワーク設定の最適化は後続課題にする
。

現在の学習上限は`max_steps:
1500000`とする。Unity側の1エピソード上限は`NavigationAgent.MaxStep =
1000`とする。

学習scriptは冒頭で、BehaviorName、観測、行動、Unity側の`v`の`[0, 1]`
clampと速度2倍スケール、episode limit、training
limit、ML-Agents拡張の有無を出力する。

## Episode Initialization

各エピソード開始時に、ロボットの開始位置と開始向きをランダム化する。

- 開始位置はfloor内側の範囲からサンプリングする -
  開始向きはY軸まわり`0`から`360`度でサンプリングする -
  壁などのColliderと重なる候補位置は避ける -
  一定回数サンプリングして有効位置が見つからない場合は既定開始位置へ戻す

## Reward Design

MVP 2はMVP 1と同じ`NavigationAgent`を使うため、報酬体系もMVP 1/2で共通にする。

現在の報酬は以下である。

| 条件 | 報酬 |
| --- | --- |
| Goal到達 | `+100` |
| 壁衝突 | `-50` |
| 行動入力がある | `+0.01` |
| Goalまでの距離が前回判定より近づく | `+0.1` |
| Goal方向角が`<-90`または`>90` | `-0.1` |
| Goal方向角が`<-150`または`>150` | `-5` |
| 前進入力がほぼ0、または旋回入力が`-0.3`から`0.3`の範囲 | `-0.1` |

`AgentMotor`は`MovePosition`と`MoveRotation`で移動するため、報酬判定では物理速度
そのものではなく、`OnActionReceived`で受け取った連続行動入力を使う。ここでは前進
入力を速度指令、旋回入力を角度変化率の指令として扱う。

## Done

- `Mvp2Bootstrap`からMVP 2環境を生成できる - ロボット前方に84x84のSegmentation
  Cameraを配置できる - 通行可能領域が緑、通行不可能領域が青で撮影される -
  Goalがセグメンテーション画像に含まれない - Camera
  Sensorが`Mvp2VisualNavigation`のVisual Observationとして接続される -
  既存の4値Vector Observationを併用できる - 画像スタックが1である - Segmentation
  Capture Modeで画像を保存して確認できる - Segmentation Preview
  Overlayで学習入力カメラの映像をGame View右下に確認できる -
  Python側から`Mvp2VisualNavigation` behaviorが認識される -
  学習stepが進み、Agentが画像+ベクトル観測で行動できる - MVP
  1/2共通Agentに指定された報酬体系を実装した - `v`をUnity側で`[0,
  1]`へ丸め、2倍にしてロボット移動へ入力するようにした - `omega`は`[-1,
  1]`の標準化角速度指令として維持した - 1エピソード上限を1000 stepにした -
  学習上限を150万stepにした -
  エピソード開始時のロボット位置と向きをランダム化した -
  学習script冒頭でモデルと設定を出力するようにした -
  ロボット頭上の矢印を、ロボット正面方向に伸びる非対称な矢印形状へ変更した -
  動作確認用マップに短い内壁を2つ追加し、難度を大きく上げずに曲がり方の変化を増
  やした

## Open Questions

- 通常表示用Materialと学習表示用Materialを同一にするか、Layerや置換用Materialで
  分離するか - 画像保存をUnity Editor専用にするか、build後の実行でも使うか -
  速度情報をVector Observationに追加する必要があるか -
  画像入力により学習が不安定な場合、報酬設計やネットワーク設定をどう調整するか -
  プレビュー表示を常時有効にするか、Editorデバッグ時だけ有効にするか -
  速度・角度変化率の報酬判定を入力指令ではなくRigidbody実測値に切り替える必要が
  あるか - MVP
  2標準trainerでは、Python側の実入力画像テンソル統計はまだ出していない。必要にな
  った場合はML-Agents標準networkにも同様のdebug hookを検討する

## Next

- Unity Editor上でSegmentation Preview Overlayが右下に表示されることを確認する -
  プレビュー有効時の描画負荷と学習step進行への影響を確認する -
  WSL2側ML-Agentsミラーへの同期対象を更新する - MVP
  3として、資料に近い`84x112`画像入力、2値Vector Observation、カスタムCNN
  policyを検討する
