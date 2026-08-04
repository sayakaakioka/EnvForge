# EmbodiedLab Unity SDK 移行

## 現在の状態

EnvForge は `com.embodiedlab.unity` を利用しているが、Unity project は旧 SDK commit
`b96a46779bef8ed24af77d9aecf49f94150d8afa` を固定している。

`EmbodiedLab.Unity` では、契約同期と tutorial 整理に続き、server-owned job lifecycle、
artifact 検証、Replay 読み込み、ONNX / camera 境界を小さな公開 API として再設計する。
この公開 API が main へ入った後、EnvForge を新 API 利用へ書き直す。

移行前の旧 API や重複実装を compatibility layer として残さない。過去の移行経緯は
Git 履歴と pull request を参照する。

## 責務境界

`EmbodiedLab.Unity` が持つ責務:

- Scenario / Result / Replay Bundle の生成 DTO と serialization
- job submit、状態監視、明示的な再同期、cancel
- artifact metadata、size、SHA-256 の検証と download
- Replay Bundle manifest / chunk の逐次読み込み
- ONNX metadata と observation / action contract の検証
- Unity 2022.3.19f1 以降で利用できる package 境界

EnvForge が持つ責務:

- world editor と Scenario Bundle 構築
- Cloud / World / Replay / Library UI
- ユーザー向け job と map の履歴
- Replay の scene 表示と操作
- navigation 固有の observation、action、local inference
- EnvForge 固有の保存先と画面遷移

ONNX Runtime の native binary は特定 OS をプロダクト全体の対応範囲とみなさず、
Windows x64、Ubuntu、macOS の各 target を package と build で個別に検証する。

## 実装順序

1. EmbodiedLab で server-owned lifecycle と最終データ契約を確定する。
2. `EmbodiedLab.Unity` の公開 API、検証上限、tutorial を確定する。
3. EnvForge の package pin を確定済み SDK main commit へ更新する。
4. `Assets/Scripts/Navigation/Cloud` の旧 lifecycle 呼び出しを新 API へ置き換える。
5. EnvForge 内の重複 DTO、transport、artifact、Replay parse code を削除する。
6. EnvForge 固有 UI、履歴、Replay 表示、local inference を SDK の結果へ接続する。
7. Windows、Ubuntu、macOS の対象 build と Unity Editor で動作確認する。

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
