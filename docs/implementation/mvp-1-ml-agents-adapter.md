# MVP 1: Minimal ML-Agents Adapter

## Goal

MVP 0.1で用意した手動ナビゲーション環境、距離・角度メトリクス、Vector Observationを使って、ML-Agentsに接続する最小構成を作る。

対象Unity EditorはUnity 6.3 LTS (`6000.3.11f1`) とし、Unity packageは`com.unity.ml-agents` `4.0.0`を`Packages/manifest.json`で明示管理する。

Python側は`uv`で管理し、`mlagents==1.1.0`を`pyproject.toml`で固定する。`mlagents==1.1.0`はPython `>=3.10.1,<=3.10.12` を要求するため、`requires-python`もその範囲に合わせる。PyTorchは`torch~=2.2.1`に固定する。新しいPyTorchはONNX export時に`onnxscript`を要求するが、`onnxscript`は`mlagents==1.1.0`が使う`onnx==1.15.0`と依存衝突するため使わない。学習実行はWSL2から`./scripts/train-mvp1.sh`を使う。

WSL2でのセットアップ手順:

```bash
uv python install 3.10.12
uv sync --python 3.10.12
uv run mlagents-learn --help
bash scripts/train-mvp1.sh
```

既に別Pythonで`.venv`が作られている場合は、`.venv`を削除してから`uv sync --python 3.10.12`を実行する。

MVP 1では、MVP 0用の`Mvp0SceneBuilder`にmodeを追加しない。手動デバッグ用のMVP 0環境はそのまま残し、ML-Agents用には別の`Mvp1SceneBuilder`を作る。

これにより、手動検証環境と学習接続環境を分離し、片方の変更がもう片方を壊しにくい形にする。

## Non-goals

- 本格的な報酬shaping
- カリキュラム学習
- セグメンテーション画像観測
- LiDAR風レイ観測
- ONNXモデル管理
- 複数シーン対応
- 学習設定YAMLの詳細調整
- 推論評価基盤

## Scene Strategy

MVP 1用に、`Mvp1SceneBuilder`を追加する。

Unity Editor上で手作業で置くものは、MVP 0と同様に空のBootstrap GameObjectだけにする。このGameObjectに`Mvp1SceneBuilder`を付け、Play開始時にML-Agents用の最小環境を生成する。

推奨GameObject名:

- `Mvp1Bootstrap`

`Mvp1SceneBuilder`は、MVP 0と同じ基本環境を生成する。

- Floor
- Walls
- Agent
- Goal
- Main Camera
- Directional Light
- Navigation metrics
- Navigation observation provider
- Goal reach checker
- Wall collision reporter
- ML-Agents adapter

## Scripts

### Mvp1SceneBuilder

ML-Agents接続用の最小環境を生成する。

`Mvp0SceneBuilder`のmode切り替えではなく、別Builderとして実装する。

`BehaviorParameters`のBehaviorNameは`Mvp1Navigation`に固定し、`configs/ml-agents/mvp1-navigation.yaml`のbehavior entryと一致させる。

### NavigationAgent

ML-Agentsの`Agent`を継承する薄いアダプタ。

責務:

- `CollectObservations`で`NavigationObservationProvider`の値を渡す
- `OnActionReceived`で2次元連続行動を`AgentMotor`へ渡す
- `Heuristic`でWASD相当の手動入力を使えるようにする
- Goal到達・Wall衝突・手動リセットなどのイベントをML-Agents episode終了へつなぐ

### AgentMotor

MVP 0のものを再利用する。

2次元連続行動を受け取り、Agentを移動・旋回させる。

### NavigationObservation / NavigationObservationProvider

MVP 0.1のものを再利用する。

MVP 1では、まず固定順序の4値を観測として使う。

| Index | Value |
| --- | --- |
| 0 | `normalizedDistanceToGoal` |
| 1 | `normalizedSignedAngleToGoal` |
| 2 | `distanceToGoal` |
| 3 | `signedAngleToGoalDegrees` |

将来、学習が不安定な場合は、ML-Agentsへ渡す値を正規化2値のみに絞ることも検討する。

### Episode Bridge

MVP 0の`EpisodeManager`は手動環境用に残す。

Goal到達やWall衝突の通知先は、`INavigationEpisodeEvents` interfaceで抽象化する。

- `EpisodeManager`は手動環境用の通知先として`INavigationEpisodeEvents`を実装する
- `NavigationAgent`はML-Agents環境用の通知先として`INavigationEpisodeEvents`を実装する
- `GoalReachChecker`と`WallCollisionReporter`は具体的な通知先を知らず、`INavigationEpisodeEvents`だけに通知する

MVP 1では、ML-Agents episode終了と報酬付与を扱う薄い接続層を用意する。`NavigationAgent`自身がその役割を持つか、必要なら別コンポーネントへ切り出す。

候補名:

- `MlAgentsEpisodeBridge`
- `TrainingEpisodeManager`
- `NavigationTrainingAdapter`

## Minimal Reward

MVP 1では、まず最小報酬だけを使う。

- Goal到達: positive reward
- Wall衝突: negative reward
- Step: small negative reward

距離改善報酬、角度報酬、停滞ペナルティなどはMVP 1では入れない。

## Action Space

行動空間は2次元連続値にする。

- `forward`: 前進・後退
- `turn`: 左右旋回

離散行動は使わない。

## Done

- ML-Agents packageをUnity projectに追加できる
- Python側を`uv`で同期できる
- `configs/ml-agents/mvp1-navigation.yaml`で学習設定を管理できる
- `scripts/train-mvp1.sh`から`mlagents-learn`を起動できる
- `Mvp1Bootstrap`からML-Agents用の環境を生成できる
- `NavigationAgent`が4値のVector Observationを出せる
- `NavigationAgent`が2次元連続行動でAgentを動かせる
- Goal到達で正報酬を与えてepisode終了できる
- Wall衝突で負報酬を与えてepisode終了できる
- 毎stepの小さな負報酬を与えられる
- PythonからUnityへ接続し、`Mvp1Navigation` behaviorが認識される
- 学習stepが進み、AgentがML-Agents経由の行動で動く
- `Heuristic`で手動操作できる

確認済みの接続ログ例:

```text
Connected to Unity environment with package version 4.0.0
Connected new brain: Mvp1Navigation?team=0
```

初回学習では、Agentがランダムに動く、壁にぶつかる、Goalへ到達しないことがある、という状態でよい。MVP 1の接続確認としては、Python側でstepが進み、Unity側のAgentがML-Agents経由で動くことを成功条件とする。

## Next

MVP 1が動いた後に、学習設定と実験管理を整理する。

- `configs/ml-agents/mvp1-navigation.yaml`を実測結果に合わせて調整する
- BehaviorNameと学習設定名の不一致を起動時に検出しやすくする
- 学習コマンドを追加する
- 評価用シーンまたは評価モードを検討する
- 距離改善報酬や停滞ペナルティを検討する
