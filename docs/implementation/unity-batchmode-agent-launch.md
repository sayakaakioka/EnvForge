# Codex から Unity batchmode を起動する手順

この文書は、Codex やサブエージェントから EnvForge の Unity Editor を batchmode
で起動するときのローカル運用手順を記録する。

## 背景

EnvForge の Unity project は以下に置かれている。

    C:\Users\sayak\dev\EnvForge\unity\EnvForge-local-first

Unity Editor は Unity Hub 管理の Unity 6.3 LTS `6000.3.11f1` を使う。

    C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe

Codex から Editor を直接 batchmode 起動したところ、Unity Hub から手動で project
を開く場合は正常に動く一方で、次のような失敗が出た。

- `Unity.Licensing.Client.exe` が Windows dialog で例外を出す。 - Unity log に
  `Connection to channel LicenseClient-sayak refused` が出る。 - Editor 同梱の
  Licensing Client 起動後に assertion failure が出る。 - Unity Package Manager
  local server へ接続できず、batchmode が失敗する。

これは project や UPM package の破損ではなく、Codex から Editor を直叩き
した時点で Unity Hub 側のライセンス状態が十分に初期化されていないことが
主因と見なす。

## 採用する起動方針

当面は standalone の `unity` CLI を導入せず、Unity Hub headless CLI で
ライセンス状態を初期化してから、Unity Editor を直接 batchmode 起動する。

この方針を採用する理由は、現在必要なのが Codex / サブエージェントからの
ローカル検証であり、既にこの手順で `-batchmode -quit -nographics` が return code
`0` で完走することを確認できているためである。

standalone の `unity` CLI は、将来 CI や複数マシンでの自動化を整える段階で
再評価する。現時点では、追加導入による PATH、認証、Hub との関係の不確実性を
増やさないことを優先する。

## 標準手順

Unity batchmode を実行する前に、まず Unity Hub headless CLI を一度起動する。

    & 'C:\Program Files\Unity Hub\Unity Hub.exe' -- --headless help

その後、Unity Editor を batchmode で起動する。

    & 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' `
      -batchmode -quit -nographics `
      -projectPath 'C:\Users\sayak\dev\EnvForge\unity\EnvForge-local-first' `
      -logFile 'C:\Users\sayak\dev\EnvForge\unity-batchmode.log'

成功時は Unity log の末尾付近に以下が出る。

    Batchmode quit successfully invoked - shutting down!
    Exiting batchmode successfully now!
    Application will terminate with return code 0

## 失敗時の確認

同じ例外が再発した場合は、まず失敗した batch 起動が残した Editor 同梱の
Licensing Client が残っていないか確認する。

    Get-Process |
      Where-Object {
        $_.ProcessName -like '*Unity*' -or
        $_.ProcessName -like '*Licens*'
      } |
      Select-Object Id, ProcessName, Path

Hub 側の `Unity.Licensing.Client.exe` は以下の path で動く。

    C:\Program Files\Unity Hub\UnityLicensingClient_V1\Unity.Licensing.Client.exe

Editor 同梱の Licensing Client は以下の path で動く。

    C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\Resources\Licensing\Client\Unity.Licensing.Client.exe

失敗後に残りやすいのは Editor 同梱側である。Hub 側ではなく Editor 同梱側の
残骸だけを止めてから、再度 Hub headless CLI の初期化手順を実行する。

    Stop-Process -Id <Editor同梱LicensingClientのPID> -Force

`-accessToken` や `-hubSessionId` は Unity Hub の log に出る場合があるが、
これらを手作業で再利用しない。認証情報を command line やドキュメントへ
持ち出さず、Hub headless CLI による初期化を使う。

## 保留

- この手順を Makefile や helper script に包むかどうか。 - standalone `unity` CLI
  を導入するかどうか。 - CI 上で同じ方針が通用するかどうか。 - GUI の Unity Hub
  が起動していない状態でも安定するかどうか。
