# MVP 0.1: Vector Observation Prep

## Goal

MVP 0の手動ナビゲーション環境を維持したまま、将来ML-Agentsの`CollectObservations`へ渡すためのベクトル観測値を整理する。

この段階では、学習器にはまだ接続しない。目的は、AgentとGoalの相対関係を安定したデータ構造として取り出し、Consoleログで確認できるようにすることである。
また、Gameビュー上の簡易Debug Overlayで現在値を確認できるようにする。Consoleログは成功・失敗・リセットなどのイベント時に限定し、毎フレームのログ出力は行わない。

## Non-goals

- ML-Agents packageの追加
- `Agent`継承クラスの作成
- `CollectObservations`への接続
- 報酬設計
- YAML学習設定
- ONNX推論
- 通常のUI表示

## Observation Values

まずは、`NavigationMetrics`から得られる距離と角度だけを観測値にする。

- `distanceToGoal`
- `signedAngleToGoalDegrees`
- `normalizedDistanceToGoal`
- `normalizedSignedAngleToGoal`

観測値の出力順序は、モデル仕様に近い扱いになるため固定する。

| Index | Value |
| --- | --- |
| 0 | `normalizedDistanceToGoal` |
| 1 | `normalizedSignedAngleToGoal` |
| 2 | `distanceToGoal` |
| 3 | `signedAngleToGoalDegrees` |

正規化値は、将来の学習入力に渡しやすい範囲へ収める。

- `normalizedDistanceToGoal`: `0..1`
- `normalizedSignedAngleToGoal`: `-1..1`

`normalizedDistanceToGoal`は、MVP 0.1では床サイズの対角長を最大想定距離として計算する。床サイズを変えた場合も、正規化基準が自動で追従するようにする。

速度や前回距離との差分は、MVP 0.1ではまだ含めない。必要になった時点で追加する。

## Scripts

### NavigationObservation

観測値をまとめる小さいデータ構造。

ML-Agentsには依存しない。

`ValueCount`と`TryWriteTo(float[] values)`を持ち、固定順序で値を書き出せるようにする。

### NavigationObservationProvider

`NavigationMetrics`から`NavigationObservation`を作る。

MVP 0.1では、`EpisodeManager`のログに観測値のsummaryを含めるために使う。

### NavigationDebugOverlay

Gameビュー左上に現在の観測値を表示する。

Consoleを毎フレーム汚さず、手動操作中に距離・角度・正規化値が更新されていることを確認するために使う。

## Done

- `NavigationObservation`で距離・角度・正規化距離・正規化角度を表現できる
- `NavigationObservation`の出力順序が固定されている
- `NavigationObservationProvider`が`NavigationMetrics`から観測値を作れる
- 成功・失敗・手動リセット・リセット時のConsoleログで観測値を確認できる
- Gameビュー左上のDebug Overlayで現在の観測値を確認できる
- ML-Agentsに依存していない

## Next

MVP 0.1が動いた後に、MVP 1としてML-Agents接続の最小実装へ進む。

- ML-Agents packageを追加する
- `NavigationAgent : Agent`を作る
- `NavigationObservationProvider`の値を`CollectObservations`へ渡す
- 2次元連続行動を`AgentMotor`へ接続する
