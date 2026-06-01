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

`Job details` では、どの Scenario Bundle / training / reward 設定で
ジョブを送信したか、現在取得している学習 status がどの
`submission_id` / `scenario_id` に対応するか、ロード済み Replay Log が
どの `job_id` / `scenario_id` / episode / step 数に対応するかを分けて表示する。
Replay Log 自体は詳細な学習設定を含まないため、同一セッションで送信した
job と一致する場合は送信時の設定要約を併記し、demo や外部由来の replay は
設定の由来を明示する。

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
具体的には `AgentMotor` を disable し、`Rigidbody` を kinematic にして、
Replay Log の位置と Y 回転を authoritative state として扱う。現在の主経路では
Unity側の `NavigationAgent` / `DecisionRequester` は生成しない。

Replay UI は再生、一時停止、停止、1 step 前後移動を提供する。
画面上には現在 step、行動、報酬合計、reward component、終了理由を表示する。

## ユーザー要望ベースの次 TODO

Phase 5 の完了後、または Phase 5 を仕上げながら進める実装 TODO は以下である。

- 学習済みモデルをダウンロード後、そのモデルを用いて EnvForge 上で推論するモードを
  実装し、動作確認する。
- ML-Agents は推論用途に限定する。現行の EnvForge / EmbodiedLab 連携主経路では、
  EnvForge 側で ML-Agents trainer 接続を主目的にしない。
- polling をやめ、Pub/Sub 型の通知で学習 status と成果物準備完了を受け取る。
- Unity を終了しても、学習済みモデル、Replay Log、学習ログを後から回収できるように
  job history と artifact 回収導線を用意する。
- Replay 表示欄に reward、終了理由、学習概要を読みやすく表示する。表示欄が
  切れないよう、Replay overlay と Job details の縦幅も調整する。
- 右上のクラウド操作パネルのデザインを再考する。現状は情報が見えることを優先した
  暫定 HUD であり、見た目、余白、ボタン配置、状態表示の視線誘導を改めて設計する。
- 右上のクラウド操作パネルと左下の Replay パネルのフォントサイズを再設計する。
  少なくとも左上の既存情報パネルより小さくならないようにし、画面全体の情報階層を
  揃える。
- 壁パーツをユーザーが自由に立てられるようにし、その配置を Scenario Bundle に
  反映する。
- コードを現行の主経路に合わせてきれいにシンプルにする。過去の MVP や旧実験の
  遺産は、必要なら履歴として参照し、現行 runtime の複雑さとして残さない。
- テストも同じ方針で整理し、Scenario Bundle、Result Bundle、Replay、artifact 回収、
  推論モードに沿った構成へ寄せる。

優先順は以下を基本とする。

1. Replay / Job details 表示の縦幅と情報表示を調整する。
2. Unity 終了後も成果物を回収できる job history / artifact 回収導線を用意する。
3. 学習済みモデル推論モードを実装し、ML-Agents を推論用途に限定する。
4. polling を Pub/Sub 型通知へ置き換える。
5. 壁パーツ編集を実装する。
6. コードとテストを、現行主経路に合わせて段階的に整理する。

## 保留事項

- Sentis ONNX を runtime load して実推論する導線。
- 認証つき GCS artifact access。
- WebSocket による自動状態更新。
- `submission_id`、送信時刻、preset、主要な学習設定、status、artifact readiness を
  EnvForge 側に保存するローカル job history。
- 長い Replay Log の圧縮、分割、streaming load。
- Replay Log と Scene object id の厳密な対応。
- Cloud run configuration asset の editor tooling。
