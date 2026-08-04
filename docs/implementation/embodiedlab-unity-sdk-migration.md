# EmbodiedLab Unity SDK 移行

## 現在の状態

EnvForge は `com.embodiedlab.unity` の main commit
`abb976ea97b1010fb3a6dbfb177cefdde5aa90b6` を固定している。

server-owned job lifecycle、size / SHA-256 を含む artifact 検証、identity と chunk
metadata を照合する Replay 読み込みへ移行済みである。Scenario builder は camera、
action、reward、PPO、resource 値をすべて JSON に明記し、local inference は Scenario の
observation / action 値と固定された policy mapping を使う。

移行前の旧 API や重複実装を compatibility layer として残さない。過去の移行経緯は
Git 履歴と pull request を参照する。

## 責務境界

`EmbodiedLab.Unity` が持つ責務:

- Scenario / Result / Replay Bundle の生成 DTO と serialization
- job submit、状態監視、明示的な再同期、cancel
- artifact metadata、size、SHA-256 の検証と download
- Replay Bundle manifest / chunk の逐次読み込み
- Result 内の ONNX artifact metadata と size / SHA-256 の検証
- Unity 2022.3.19f1 以降で利用できる package 境界

EnvForge が持つ責務:

- world editor と Scenario Bundle 構築
- Cloud / World / Replay / Library UI
- ユーザー向け job と map の履歴
- Replay の scene 表示と操作
- navigation 固有の ONNX session 入出力、observation / action mapping、local inference
- EnvForge 固有の保存先と画面遷移

Cloud、Result、Replay の SDK workflow は Windows x64、Ubuntu x64、macOS を対象とする。
同梱 native ONNX Runtime による local inference は現時点で Windows x64 のみを対象とし、
Ubuntu と macOS は native integration を追加してから個別に検証する。

## 到達済み

1. EmbodiedLab で server-owned lifecycle と最終データ契約を確定した。
2. `EmbodiedLab.Unity` の artifact / Replay 検証と tutorial を確定した。
3. EnvForge の package pin と canonical fixture を確定済み SDK main へ同期した。
4. `Assets/Scripts/Navigation/Cloud` の restore、artifact、Replay 呼び出しを新 API へ置き換えた。
5. 旧 artifact field、旧 Replay property、意味の違う action 解釈を削除した。
6. Scenario の camera、action、reward、training 値を local runtime と inference へ接続した。

## 次の検証

1. Unity 6.3 LTS Editor と Windows x64 standalone の local inference を検証する。
2. Ubuntu x64 と macOS は cloud / result / replay の package / application build を
   対象環境で個別に検証する。
3. Cloud の実ジョブで submit、monitor、verified download、Replay、local inference を目視確認する。

## 完了条件

- EnvForge は一つの submit 操作で job handle を受け取り、server 内部の dispatch 操作を扱わない。
- queued / running / terminal Result を一つの monitor から受け取る。
- model と Replay artifact は size と SHA-256 を確認してから利用する。
- Replay chunk を全件結合せず、必要な chunk を逐次読み込みできる。
- Scenario JSON の寸法、camera、reward、training 値を runtime が正本として使う。
- `policy.onnx` の metadata と Scenario の observation / action contract が一致しない場合は
  明確に失敗する。
- 旧 SDK 呼び出し、重複 DTO、旧 transport、不要な compatibility adapter が残らない。
- package / EditMode / PlayMode test と対象 platform build が成功する。

## 移行後に別途扱う項目

- 認証、private artifact、signed URL
- shared device 向けの cancel capability 保護 storage
- generated environment mode
- 複数 robot、動的障害物、高忠実度 simulation
