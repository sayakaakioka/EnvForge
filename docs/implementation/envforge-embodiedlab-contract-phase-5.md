# EnvForge / EmbodiedLab 契約実装 Phase 5

## 目的

Phase 5 では、EmbodiedLab が生成した学習結果を EnvForge 側で取得し、
人間が Unity 上で挙動を確認できる最小導線を実装する。

人間が最初に確認したいのは、学習済みモデルを完全に製品利用することではなく、
自分が設定した環境と報酬体系に対して、ロボットがどのように動き、どこで失敗し、
どの reward component が効いたかである。そのため、初期版では Sentis 推論よりも
Replay Log の取得とローカル再生を優先する。

## 今回の範囲

今回の最小範囲は以下である。

- Result Document の top-level `artifacts` と `result_bundle.artifacts` を読める
  DTO を用意する。
- `gcs` artifact location から public GCS URL を作る。
- Replay Log artifact を download して JSON Lines として parse する。
- Replay Log を既存の Navigation scene 上で再生する。
- 通常 ONNX と Unity Sentis 向け ONNX を local file として保存する。
- backend がない状態でも bundled demo replay を読み込めるようにする。

Unity 側の最小 UI は IMGUI overlay とし、以下の操作を提供する。

- `Submit + Train`
- `Poll Result`
- `Download Replay`
- `Download Model`
- `Load Demo Replay`

API settings asset が未割り当ての場合は、`http://localhost:8000` を fallback の
API base URL として使う。これにより、ローカルで EmbodiedLab API を起動した状態なら
追加 asset を作らなくても疎通確認できる。

## Result artifact の扱い

EmbodiedLab は trainer 完了時に以下を返す。

    results/<submission_id>/model/policy.zip
    results/<submission_id>/model/policy.onnx
    results/<submission_id>/model/policy.sentis.onnx
    results/<submission_id>/replay/replay.jsonl

EnvForge は replay を直接再生に使う。ONNX artifact は
`Application.persistentDataPath/EnvForge/<submission_id>/` に保存する。
Sentis runtime load / inference の安定検証は次フェーズの作業とする。

## Replay 再生の扱い

Replay Log を読み込んだ時点で、対象 agent の live control を止める。
具体的には `NavigationAgent`、`DecisionRequester`、`AgentMotor` を disable し、
`Rigidbody` を kinematic にして、Replay Log の位置と Y 回転を authoritative
state として扱う。

Replay UI は再生、一時停止、停止、1 step 前後移動を提供する。
画面上には現在 step、行動、報酬合計、reward component、終了理由を表示する。

## 保留事項

- Sentis ONNX を runtime load して実推論する導線。
- 認証つき GCS artifact access。
- WebSocket による自動状態更新。
- 長い Replay Log の圧縮、分割、streaming load。
- Replay Log と Scene object id の厳密な対応。
- Cloud run configuration asset の editor tooling。
