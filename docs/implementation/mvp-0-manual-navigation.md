# MVP 0: Manual Navigation

## Goal

Unity上で、AgentがGoalへ向かって移動し、到達・衝突・リセットの最小ループを確認する。

この段階の目的は、学習基盤を作ることではなく、ナビゲーション実験の最小単位がUnityシーン内で成立することを確認することである。

## Non-goals

- ML-Agentsによる学習
- ONNXモデルによる推論
- セグメンテーション画像観測
- LiDAR風レイ観測
- カリキュラム学習
- 報酬重みの設定ファイル化
- 自動マップ生成
- 評価ログ基盤

ただし、MVP 0内のデバッグ用途として、AgentからGoalまでの距離と角度はConsoleに出してよい。これは後のベクトル観測や報酬設計につなげるための最小確認に留める。

## Scene

最初のシーンは、固定配置の小さな3D環境にする。

環境本体は手作業で配置せず、スクリプトで生成する。これにより、他の環境でも同じ初期シーンを再現しやすくする。

Unity上で手作業で置くものは、原則として空のBootstrap GameObjectだけにする。このGameObjectに`Mvp0SceneBuilder`を付け、Play開始時に床・壁・Agent・Goal・必要なColliderなどを生成する。

- Floor
- Walls
- Agent
- Goal
- Main Camera
- Directional Light

Main CameraとDirectional Lightは、テンプレートの既存オブジェクトを使ってよい。必要になった時点で、これらも生成対象に含めるか判断する。

Goalは床に置いた球ではなく、床から少し浮かせた黄色のsphereにする。将来、通行可能領域や非通行領域を緑・青で表示する可能性があるため、Goalの色はそれらと被らない色にする。

Goal到達判定は、GoalのColliderに触れたかどうかではなく、AgentとGoalの水平距離で判定する。これは、後の報酬設計、評価、ML-Agents観測とつなげやすくするためである。

AgentはCapsuleで表現し、正面方向が分かるように白い三角形のdirection arrowを上に浮かせる。矢印は見た目だけの子オブジェクトとし、Colliderは付けない。

## Controls

Agentは、将来の連続行動に対応しやすいように、次の2値を受け取って動く。

- `forward`: 前進・後退の入力
- `turn`: 左右旋回の入力

MVP 0では、これらの値をキーボード入力から与える。

## Scripts

### Mvp0SceneBuilder

MVP 0用の最小環境を生成する。

- Floorを生成する
- Wallsを生成する
- Agentを生成する
- Agentの上にdirection arrowを生成する
- Goalを生成する
- 必要なColliderとRigidbodyを設定する
- `AgentMotor`、`ManualAgentController`、`EpisodeManager`などを接続する

配置やサイズは、まずコード内の定数で持つ。MVP 0では、ScriptableObjectやJSON設定にはまだ分離しない。

### AgentMotor

`forward`と`turn`を受け取り、Agentを移動・旋回させる。

この段階では、ML-Agentsの`ActionBuffers`には依存しない。

### ManualAgentController

キーボード入力を読み取り、`AgentMotor`へ渡す。

想定する入力:

- `W` / `S`: 前進 / 後退
- `A` / `D`: 左旋回 / 右旋回
- `R`: 手動リセット

### EpisodeManager

エピソードの開始・終了を管理する。

- AgentとGoalの初期配置
- Goal到達時の成功判定
- Wall衝突時の失敗判定
- 成功・失敗・手動リセット後の再配置
- エピソード番号、成功数、失敗数、手動リセット数の記録
- リセット時の簡易メトリクス出力

### NavigationMetrics

AgentとGoalの相対関係を計算する。

- AgentからGoalまでの距離
- Agent正面から見たGoalの符号付き角度

MVP 0では、これらをConsoleログ用のデバッグ情報として使う。ML-Agentsの観測値にはまだ接続しない。

### GoalReachChecker

AgentとGoalの水平距離を毎フレーム確認し、到達半径内に入ったら`EpisodeManager`へ通知する。

MVP 0では、Goalのsphereは見た目専用にする。到達判定は`NavigationMetrics.DistanceToGoal`を使う。

### WallCollisionReporter

Wall衝突を検出し、`EpisodeManager`へ通知する。

## Done

- 空のBootstrap GameObjectからMVP 0環境を生成できる
- キーボードでAgentを前進・後退・旋回できる
- Agentの正面方向をdirection arrowで確認できる
- Goal到達を検出できる
- Wall衝突を検出できる
- 成功後にAgentとGoalを再配置できる
- 失敗後にAgentとGoalを再配置できる
- 手動リセットできる
- ConsoleにAgentからGoalまでの距離と角度が出る
- Consoleに成功・失敗・リセットのログが出る

## Next

MVP 0が動いた後に、MVP 0.1としてVector Observation用のデータ構造を準備する。

- `NavigationMetrics`をもとにVector Observation用のデータ構造を準備する

その後、MVP 1として次の候補へ進む。

- ML-Agentsの薄いアダプタを追加する
- セグメンテーション表示を試す
- LiDAR風レイを追加する
- 固定マップを少し複雑にする
