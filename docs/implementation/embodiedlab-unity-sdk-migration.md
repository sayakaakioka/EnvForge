# EmbodiedLab Unity SDK 移行計画

## 目的

EnvForge 内の汎用的な EmbodiedLab client 機能を、独立した
`EmbodiedLab.Unity` UPM package へ移す。EnvForge は SDK の最初の利用者となり、
EnvForge 固有の編集、表示、Replay、推論機能に集中する。

## SDK へ移す責務

- Scenario / Result / Replay Bundle DTO と serializer
- submission 作成、training 開始、result fetch
- WebSocket result stream と HTTP 再同期
- artifact location の解決と download
- Replay Bundle manifest / chunk の取得、展開、parse
- API error、retry、timeout、cancellation、compatibility check
- API endpoint と WebSocket endpoint の SDK 設定

現在の主な移行元は `Assets/Scripts/Navigation/Cloud` と
`Assets/Scripts/Navigation/Contracts` である。ただし、ディレクトリ単位で機械的に
移動せず、次の EnvForge 固有責務を切り分けて残す。

## EnvForge に残す責務

- Unity scene と world editor
- scene から Scenario Bundle を構築する処理
- Cloud panel、Job details、Library などの UI
- ユーザ向け job history と EnvForge 固有のローカル状態
- Replay player と scene 上の可視化
- ONNX Runtime 推論と操作 UI
- EnvForge 固有の保存先、表示文言、画面遷移

Replay Bundle の取得と parse は SDK に移すが、robot、wall、goal を scene 上に表示し、
再生位置やカメラを操作する処理は EnvForge に残す。

## 設計・移行原則

- 現在必要な最小限の公開 API だけを設計する。
- 将来の可能性だけを理由に抽象化、extension point、汎用 layer を追加しない。
- 重複実装、不要な互換 layer、到達不能な旧コードを残さない。
- 既存動作を contract fixture と test で固定してから、責務を段階的に切り出す。
- 各段階で対象リポジトリの test と lint を実行し、成功を次段階の開始条件とする。
- 公開 API、contract、責務境界などの設計判断が必要な場合は、2 から 3 案の
  メリットとデメリット、推奨案と推奨理由を提示する。
- 設計判断はユーザとの合意前に実装しない。

## 移行方針

1. 既存 EnvForge fixture と EmbodiedLab test で現在の契約を固定する。
2. SDK の最小 API を導入し、EnvForge の呼び出し元を一経路ずつ置き換える。
3. 同じ責務の新旧実装を長期間併存させない。
4. 移行済みの DTO、client、downloader は EnvForge から削除し、未移行参照を明確な
   compile error または test failure にする。
5. EnvForge の UI と end-to-end 動作を確認してから次の責務へ進む。

## 学習環境モードとの関係

EnvForge は Scenario Bundle で `fixed` または `generated` mode を選択できるようにする。
既存の固定マップは `fixed` の既定動作として維持する。`generated` は宣言的な生成規則を
設定する別の選択肢であり、4 分割した領域へのランダムな壁パーツ配置はその一例である。

SDK は mode と生成規則の DTO、serialization、compatibility check を担当する。
生成規則の検証と episode ごとの実行は EmbodiedLab が担当し、EnvForge や SDK が
任意コードをクラウドへ送って実行する機能は当面扱わない。

## 検証

- UPM package を Git URL から追加できる。
- EnvForge の既存 Scenario Bundle fixture が SDK 経由でも同じ意味を保つ。
- submit、train、progress、completed result、artifact download が SDK 経由で動く。
- Unity を再起動しても EnvForge の job history と取得済み artifact を利用できる。
- Replay の取得処理を SDK へ移しても、既存 Replay UI と再生結果が変わらない。

## 保留事項

- SDK の公開 API が確定するまで、EnvForge の UI 型を SDK へ持ち込まない。
- job history の汎用部分を将来 SDK へ移すかは、複数フロントエンドの要求が確認できるまで
  EnvForge 側に保留する。
- 認証、private artifact、signed URL は SDK 設計に拡張点を残すが、初期移行では
  現在の backend contract を変更しない。
