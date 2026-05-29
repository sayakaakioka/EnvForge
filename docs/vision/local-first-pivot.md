# 設計の概要

このプロジェクトの再実装は、Unity上のナビゲーション実験基盤として設計するのがよ
い。

中核は、ロボットが環境内でスタート地点から目標地点へ移動するタスクである。入力と
してセグメンテーション画像、距離・角度などのベクトル情報、場合によってはLiDAR風
レイ情報を使い、ML-Agentsまたは別の学習基盤で方策を訓練・評価する。

古いプロジェクトでは、ML-Agentsのパッケージ本体と学習設定ファイルが欠けているた
め、学習アルゴリズム、YAML、ハイパーパラメータの完全復元はできない。ただし、`Age
nt`実装、ONNX名、報酬設計、BehaviorParameters、CameraSensor、Timerログから、観測
・報酬・推論運用の大枠は推測できる。

ロボットの行動空間は、要件として連続行動のみを採用する。基本形は「0以上1以下の前
進速度」と「回転量」の2次元連続値であり、停止・前進・左右旋回を連続値で表す。後
退は使わない。

新設計では、以下を分離する。

- 環境生成 - 意味色/セグメンテーション表示 - 観測生成 - ロボット制御 -
  報酬・終了判定 - カリキュラム - 学習設定 - 推論モデル管理 - 評価・ログ

特に重要なのは、ML-Agentsに強く依存する部分を薄いアダプタに閉じ込めることである
。これにより、将来ML-Agentsを使い続ける場合も、別のPython/RL基盤に切り替える場合
も、Unity側の環境・報酬・観測を保ちやすくなる。

## 起動から終了までの流れ

```text
プロジェクト起動
  ↓
実験設定を読み込む
  - シーン種別
  - 観測モード: セグメンテーション画像 / LiDAR / ベクトル
  - 報酬設定
  - カリキュラム設定
  - 学習 or 推論モード
  ↓
環境を初期化
  - 固定マップを読む
  - または Procedural Generator で生成
  - NavMesh / 通行可能領域 / 壁 / 床 / 目標候補を検証
  ↓
セマンティック表示を構築
  - 床 = 通行可能色
  - 壁・物体 = 通行不可色
  - 必要なら目標や特殊領域も別カテゴリ化
  ↓
エージェントとターゲットを配置
  - カリキュラム段階に応じて候補領域を選ぶ
  - 失敗後は同条件リトライ、成功後はランダム化、などを設定可能にする
  ↓
各ステップで観測を作る
  - カメラ画像: セグメンテーション画像
  - ベクトル: 距離、角度、速度、目標相対位置
  - LiDAR: 前方扇状レイ、または360度レイ距離
  ↓
学習器またはONNXモデルが行動を出す
  - 連続値の前進量
  - 連続値の回転量
  - 離散行動は使わない
  ↓
ロボットを動かす
  - Rigidbody / Transform / NavMesh方針のいずれか
  ↓
報酬と終了条件を判定
  - 目標到達
  - 壁衝突
  - 距離改善
  - 経路長改善
  - ステップペナルティ
  - 停滞
  - 通行可能領域外
  ↓
ログを記録
  - 成功率
  - エピソード長
  - 報酬
  - ステージ
  - 使用seed
  - 必要なら観測画像・行動履歴
  ↓
エピソード終了
  ↓
カリキュラム判定
  - 成功数
  - 評価ウィンドウ
  - 合格率
  - 次ステージへ進むか
  ↓
次エピソードへ
  ↓
学習終了 / 推論評価終了
  ↓
ONNX書き出し、評価ログ保存、再現用設定保存
```

## 設計モジュール

### `ExperimentConfig`

- 役割: 実験全体の設定。観測、報酬、学習、カリキュラム、
  モデル名、seedをまとめる。
- 旧対応: 明確な設定ファイルが少なく、Scene/Inspector/YAML相当が分散。

### `SceneEnvironment`

- 役割: Unityシーン、床、壁、障害物、ターゲット候補、
  エージェント候補を管理する。
- 旧対応: `Assets/Scenes/EnvForgeNavigationLab.unity`, `Assets/Masayacity.unity`,
  `Assets/Roads/NewNakano11F/*`

### `MapGenerator`

- 役割: 固定マップまたは自動生成マップを生成する。
- 旧対応: `Assets/Tessera/*`, `RaycastAgent.cs`内の
  `generator.Generate()`

### `SemanticCategoryProvider`

- 役割: オブジェクトを「床」「壁」「障害物」「目標」などに分類する。
- 旧対応: `ColorChanger.cs`, `NakanoBwTrain*.cs`の
  セグメンテーション環境。

### `SegmentationRenderer`

- 役割: 学習用カメラ画像を2色または少数色の意味画像として出す。
- 旧対応: `NakanoBwTrain4.cs`, `NakanoBwTrain5_loop.cs`,
  `ColorChanger.cs`, `SegNakano_*.onnx`

### `RangeSensorProvider`

- 役割: LiDAR風のレイ情報を作る。観測にも報酬にも使える。
- 旧対応: `RaycastAgent1.cs`の`ProbeFrontCone()`

### `ObservationProvider`

- 役割: カメラ、距離、角度、速度、LiDARを統合して学習器へ渡す。
- 旧対応: `RaycastAgent*.cs`, `VObsFPV*.cs`, `NakanoBwTrain*.cs`

### `AgentMotor`

- 役割: 2次元連続行動をロボット移動に変換する。
- 旧対応: `OnActionReceived()`各実装。

### `RewardCalculator`

- 役割: 目標到達、壁衝突、距離改善、角度、停滞、ステップ罰を計算する。
- 旧対応: `RaycastAgent.cs`, `RaycastAgent1.cs`,
  `NakanoBwTrain5_loop.cs`

### `EpisodeManager`

- 役割: エピソード開始・終了、再配置、失敗時リトライを管理する。
- 旧対応: `OnEpisodeBegin()`各実装。

### `CurriculumManager`

- 役割: ステージ制、成功率評価、難易度上昇を管理する。
- 旧対応: `RaycastAgent1.cs`, `RaycastAgent_test.cs`,
  `NakanoBwTrain5_loop.cs`

### `TrainingAdapter`

- 役割: ML-Agentsの`Agent`と独自モジュールを接続する薄い層。
- 旧対応: 旧`Agent`継承クラス全般。

### `TrainingConfig`

- 役割: PPO/SAC、学習率、batch size、buffer size、network設定、
  2次元連続行動設定などを持つ。
- 旧対応: 欠落部分。新設計ではYAMLを必ず同梱。

### `ModelRegistry`

- 役割: ONNXモデル、BehaviorName、観測仕様、対応環境を管理する。
- 旧対応: `Assets/*.onnx`, `BehaviorParameters`

### `Evaluator`

- 役割: 学習済みモデルの成功率・衝突率・経路長などを測る。
- 旧対応: `RaycastAgent_test.cs`, `ControlLogs*`, `CapturedImages`

### `RunLogger`

- 役割: 実験ログ、画像、seed、設定スナップショットを保存する。
- 旧対応: `Assets/ML-Agents/Timers/*.json`, `picturesaved.cs`

## 学習部分の新設計

旧プロジェクトで最も大きく欠けているのは学習部分である。新設計では、Unity側コー
ドだけでなく、学習実行に必要なものをリポジトリに含める。

| 項目 | 内容 |
| --- | --- |
| `mlagents.yaml` | BehaviorNameごとの学習設定 |
| `requirements.txt`または`pyproject.toml` | Python側ML-Agentsのバージョン固定 |
| `Packages/manifest.json` | Unity側ML-AgentsをローカルパスではなくUPM/Git/埋め込みpackageで固定 |
| `train.sh` / `train.ps1` | 学習開始コマンド |
| `evaluate.sh` / `evaluate.ps1` | 推論評価コマンド |
| `configs/experiments/*.json` | 実験条件、seed、観測モード、報酬重み |
| `models/` | ONNXと対応メタ情報 |
| `results/` | 学習ログ、TensorBoard、評価結果 |

旧プロジェクトのONNX名から見ると、少なくとも以下の実験系統があったと考えられる。

| 系統 | 推測 |
| --- | --- |
| `SegNakano_*` | 中野風道路環境 + セグメンテーション画像 |
| `Tessera_Target_Random.onnx` | Tessera生成環境 + ランダムターゲット |
| `Masaya_reward_1.onnx` | 報酬設計を変えた実験 |
| `0109_shapingnashi.onnx` | reward shapingなしの比較実験 |
| `VObs_FPV*.onnx` | 一人称カメラ視点のVisual Observation実験 |
| `11_20.onnx`, `SI12_11.onnx` | 日付または条件違いの学習済みモデル |

旧プロジェクト内には一部、離散行動を使っていた可能性のある実験コードも見える。た
だし新設計では、要件に合わせてそれらは継承しない。比較対象として読むだけに留め、
実装上の標準行動空間は2次元連続行動に統一する。

## 観測設計

旧プロジェクトから見ると、観測は3系統ある。

### セグメンテーション画像

これは、おそらくカメラ画像を画像処理で2値化したものではなく、Unity内で床と壁・物
体を異なる色のマテリアルにして、CameraSensorに見せる方式である。

新設計でもこれは有効である。床を通行可能色、壁・障害物を通行不可色にし、必要なら
目標や特殊領域を追加カテゴリとして表現する。

当面のMVP 2では、ロボット前方のCamera
Sensorで84x84の2色画像を撮影する。通行可能領域は緑、通行不可能領域は青とする。Go
alは画像には含めず、距離と角度のVector
Observationで渡す。障害物、段差、未知領域はすべて通行不可能領域として青に丸める
。

初期実装では画像スタックは使わず、現在フレーム1枚だけを入力する。画像観測は前方
の通行可能性を担当し、既存の4値Vector
ObservationはGoalまでの距離と方向を担当する。

### ベクトル観測

`distance`と`angle`、または目標の相対位置、速度などが使われている。これは学習を
安定させる補助情報として残す価値がある。

### LiDAR風レイ観測

`RaycastAgent1.cs`には、前方扇状に複数Raycastを飛ばす実装がある。ただし、旧コー
ドではそれが直接`VectorSensor`に入っていた形跡は弱く、報酬・停滞検出・デバッグ補
助に使われていた可能性が高い。

新設計では、これは明示的に2モードに分ける。

| モード | 用途 |
| --- | --- |
| `RewardOnly` | 障害物接近、詰まり、危険領域の判定に使う |
| `Observation` | レイ距離をそのまま学習入力に入れる |

## 報酬設計

旧プロジェクトの報酬は、以下の組み合わせと考えられる。

| 報酬 | 旧コード上の形跡 |
| --- | --- |
| 目標到達で大きな正報酬 | `+20`, `+100`, `+5`など |
| 壁衝突で大きな負報酬 | `-20`, `-50`など |
| 目標に近づくと小報酬 | 距離差分 reward |
| 目標から遠ざかると罰 | distance delta |
| 目標が背後にあると角度罰 | angle penalty |
| 毎ステップ小さな罰 | step penalty |
| 停滞・ほぼ無移動に罰 | `NakanoBwTrain5_loop.cs`, `RaycastAgent1.cs` |
| 通行可能領域外に罰 | `Reward_Judge_Agent`系 |
| NavMesh経路長の改善に報酬 | `RaycastAgent1.cs` |

新設計では、報酬をコードに直書きせず、重みを設定ファイル化する。

```yaml
reward:
  goal: 100
  collision: -50
  step: -0.01
  distance_progress: 0.1
  path_progress: 0.45
  angle_penalty: true
  stuck_penalty: true
```

## カリキュラム設計

カリキュラム学習の形跡はある。特に`RaycastAgent1.cs`のステージ制、成功回数、評価
ウィンドウ、合格率によるステージ昇格は明確である。また`NakanoBwTrain5_loop.cs`に
は、失敗時は同条件を再利用し、成功時にランダム化するような設計が見える。

新設計では、カリキュラムを独立モジュールにする。

```text
Stage 0: 近距離・障害物少なめ
Stage 1: 距離を伸ばす
Stage 2: 曲がり角や遮蔽物を増やす
Stage 3: ランダム生成マップ
Stage 4: 評価専用シナリオ
```

各ステージは、以下を持つ。

| 項目 | 内容 |
| --- | --- |
| start areas | 開始候補領域 |
| target areas | 目標候補領域 |
| map generator profile | マップ生成条件 |
| observation profile | 画像のみ、画像+LiDAR、LiDARのみなど |
| reward profile | shapingあり/なし |
| pass condition | 成功率、平均報酬、衝突率など |

## Tessera / 生成環境

TesseraはUnity向けのWave Function
Collapse系3Dタイル生成アセットとして使われていた可能性が高い。ただし、新設計の中
核をTesseraに依存させるのは避ける。

推奨は、`IMapGenerator`を作って差し替え可能にすることである。

| 実装 | 向いている用途 |
| --- | --- |
| `FixedSceneGenerator` | まず同じ実験を再現する |
| `TesseraMapGenerator` | Unity Editor上でタイル生成を扱いたい |
| `DeBroglieMapGenerator` | C#ライブラリとしてポータブルにWFCを使いたい |
| `CustomRoadGenerator` | 道路・迷路に特化して軽量に作りたい |

ポータビリティ重視なら、生成器を抽象化して、Tesseraは選択肢の1つに留めるのがよい
。

参考:

- [ML-Agents
  Agents/Sensors](https://unity-technologies.github.io/ml-agents/Learning-Enviro
  nment-Design-Agents/) - [Tessera
  docs](https://www.boristhebrave.com/docs/tessera/6/articles/intro.html) -
  [DeBroglie
  docs](https://boristhebrave.github.io/DeBroglie/articles/index.html)

## ML-Agentsとの接続

ML-Agentsを使う場合、Unity側は以下のような薄いアダプタにする。

```text
NavigationAgent : Agent
  Initialize()
    各モジュール取得

  OnEpisodeBegin()
    EpisodeManager.ResetEpisode()

  CollectObservations(sensor)
    ObservationProvider.WriteVectorObservations(sensor)

  OnActionReceived(actions)
    forward = actions.ContinuousActions[0]
    turn = actions.ContinuousActions[1]
    AgentMotor.Apply(forward, turn)
    RewardCalculator.Calculate()
    EpisodeManager.CheckDone()

  Heuristic(actionsOut)
    手動操作・デバッグ用
```

CameraSensorやRayPerceptionSensorはML-Agents標準に寄せる。独自Raycastを使う場合
も、`RangeSensorProvider`に閉じ込めれば交換可能にできる。

BehaviorParametersも連続行動のみを前提にする。Action
Specは、原則としてContinuous Actionsを2、Discrete Branchesを0にする。

## 最終的な推奨構成

```text
Assets/
  Scripts/
    Core/
      ExperimentConfig.cs
      EpisodeManager.cs
      CurriculumManager.cs

    Environment/
      SceneEnvironment.cs
      IMapGenerator.cs
      FixedSceneGenerator.cs
      TesseraMapGenerator.cs
      SemanticCategoryProvider.cs

    Sensors/
      ObservationProvider.cs
      SegmentationRenderer.cs
      RangeSensorProvider.cs
      CameraObservationProfile.cs
      LidarObservationProfile.cs

    Agent/
      NavigationAgent.cs
      AgentMotor.cs
      RewardCalculator.cs

    Training/
      MlAgentsTrainingAdapter.cs
      BehaviorProfile.cs
      ModelRegistry.cs

    Evaluation/
      Evaluator.cs
      RunLogger.cs
      ImageCaptureLogger.cs

  Configs/
    experiments/
    rewards/
    curriculum/
    observations/

  Models/
    onnx/

  Scenes/
    TrainingScene.unity
    EvaluationScene.unity

  Generated/
    logs/
    captures/
```

## 結論

この再実装では、古いUnity +
ML-Agentsプロジェクトをそのまま復元するのではなく、同じ実験効果を再現できる、観
測・報酬・学習・評価が分離された実験基盤として作るのがよい。

再現の核は以下の4つである。

1. セグメンテーション画像による視覚観測 2.
   目標距離・角度・経路進捗に基づく報酬設計 3.
   ステージ制または成功率ベースのカリキュラム 4.
   学習設定、seed、ONNX、評価ログを必ず同梱するポータブルな実験管理

この形にすれば、旧プロジェクトで失われているML-Agents学習部分を補いながら、将来
のUnity/ML-Agents更新にも耐えやすい構成になる。
