# EnvForge / EmbodiedLab 契約実装 Phase 3

## 目的

Phase 3 では、EnvForge 側で生成した Scenario Bundle を EmbodiedLab に送る
導線を作り始める。

最初の目標は、クラウド送信 UI を作ることではなく、EnvForge で生成する JSON が
EmbodiedLab の Pydantic model と `/submissions` endpoint で検証できることを
確認することである。

## 今回の範囲

今回の最小範囲は以下である。

- EnvForge 側に canonical Scenario Bundle fixture を置く。
- EnvForge の固定ナビゲーションシナリオの default source を明示する。
- EmbodiedLab 側に同じ JSON fixture を置き、`ScenarioBundle` で検証する。
- EmbodiedLab 側で同じ JSON fixture を `/submissions` に POST する smoke test を
  追加する。
- EnvForge 側に UI なしの API settings と API client 境界を追加する。

fixture は以下に置く。

    fixtures/scenario-bundles/navigation_default.json

この fixture は、現在の `NavigationSceneBuilder` が生成する固定ナビゲーション
シナリオを表す。座標系は `envforge_xz_meters`、床は中心原点であり、
`world.bounds` は `x = -8..8`、`z = -6..6` である。

## この順序を採用する理由

EnvForge から EmbodiedLab へ HTTP 送信する前に、契約の JSON 形状が一致している
ことを固定 fixture で確認する。これにより、Unity UI、認証、ネットワーク、
ジョブ起動の問題と、データ契約の問題を切り分けやすくする。

EmbodiedLab 側の Pydantic model は当面の source of truth である。
EnvForge 側の C# DTO はこの model に追従する境界であり、fixture はその追従確認の
ための最小サンプルである。

Phase 3B では、UI や認証をまだ入れず、送信境界だけを細く通す。これにより、
Scenario Bundle の生成、HTTP の request/response、training start、result polling
をそれぞれ独立して検証できる。

## 保留事項

- Unity Editor から fixture を自動生成する menu item または batchmode method。
- EnvForge の fixture と EmbodiedLab の copied fixture の同期方法。
- 送信 UI とジョブ状態表示。
- API URL や認証情報をどの asset と環境で切り替えるか。
- 実 backend に対する手動または自動 smoke test。
