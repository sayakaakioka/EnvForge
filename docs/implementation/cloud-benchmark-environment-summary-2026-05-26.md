# クラウドベンチ環境サマリ 2026-05-26

EnvForge `NavigationFinal`の`100000` trainer
stepsベンチについて、実行環境のCPU/GPU/描画経路を比較用に整理する。

## 比較表

### 比較: Colab CPU

- CPU: Intel Xeon @ 2.20GHz, 2 cores
- メモリ: Unity log上 `12975 MB` / monitor上 `12Gi`
- GPU: なし
- Unity描画: Mesa llvmpipe OpenGL
- 10万step: trainer elapsed `5350.702s`, `18.689 steps/s`

### 比較: Colab T4

- CPU: Intel Xeon @ 2.00GHz, 2 cores
- メモリ: Unity log上 `12975 MB` / monitor上 `12Gi`
- GPU: Tesla T4
- Unity描画: Mesa llvmpipe OpenGL。NVIDIA GPUはtrainer側CUDAのみ
- 10万step: trainer elapsed `3367.665s`, `29.694 steps/s`

### GCE L4 vWS, g2-standard-4

- CPU: `g2-standard-4`, Intel Xeon @ 2.20GHz, 4 cores
- メモリ: Unity log上 `15990 MB`
- GPU: NVIDIA L4 vWS, driver `595.71.05`, VRAM `23034 MiB`
- Unity描画: NVIDIA OpenGL。Unity processが`nvidia-smi`にgraphics
  process `G`として表示
- 10万step: trainer elapsed `917.674s`, `108.971 steps/s`

### GCE L4 vWS, g2-standard-16

- CPU: `g2-standard-16`, Intel Xeon @ 2.20GHz, 16 cores
- メモリ: Unity log上 `64298 MB`
- GPU: NVIDIA L4 vWS, driver `595.71.05`, VRAM `23034 MiB`
- Unity描画: NVIDIA OpenGL。Unity processが`nvidia-smi`にgraphics
  process `G`として表示
- 10万step: trainer elapsed `845.964s`, `118.208 steps/s`

## Colab CPU

保存ログ:

- `G:\マイドライブ\EnvForge\colab-bench\bench-cpu-100k-unity.log` -
  `G:\マイドライブ\EnvForge\colab-bench\bench-cpu-100k-monitor.log` -
  `G:\マイドライブ\EnvForge\colab-bench\bench-cpu-100k-summary.md`

根拠:

```text
Processor: Intel(R) Xeon(R) CPU @ 2.20GHz, 2 core(s) @ 2199 MHz
Available Memory: 12975 MB
Renderer: llvmpipe (LLVM 15.0.7, 256 bits)
Vendor: Mesa
```

CPU runtimeでは`nvidia-smi`は使えず、monitor logには`nvidia-smi: command not
found`が記録されている。

## Colab T4

保存ログ:

- `G:\マイドライブ\EnvForge\colab-bench\bench-t4-100k-unity.log` -
  `G:\マイドライブ\EnvForge\colab-bench\bench-t4-100k-monitor.log` -
  `G:\マイドライブ\EnvForge\colab-bench\bench-t4-100k-resource-usage.md` -
  `G:\マイドライブ\EnvForge\colab-bench\bench-t4-100k-summary.md`

根拠:

```text
Processor: Intel(R) Xeon(R) CPU @ 2.00GHz, 2 core(s) @ 2000 MHz
Available Memory: 12975 MB
Tesla T4, 1, 0, 251, 32.05
Renderer: llvmpipe (LLVM 15.0.7, 256 bits)
Vendor: Mesa
```

T4 runtimeではGPU自体は見えておりtrainer PythonがCUDA compute
appとして観測された。一方でUnity processはNVIDIA GPUを掴まず、Mesa
llvmpipeのsoftware OpenGLで動作した。

## GCE L4 vWS

保存ログ:

- `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T10554
  3Z\unity.log` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T10554
  3Z\resource-usage.log` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T10554
  3Z\gce-benchmark-summary.md` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T10554
  3Z.tar.gz`

根拠:

```text
Processor: Intel(R) Xeon(R) CPU @ 2.20GHz, 4 core(s) @ 2200 MHz
Available Memory: 15990 MB
Display 0 'NVIDIA VGX  32"': 1024x768 (primary device).
Renderer: NVIDIA L4/PCIe/SSE2
Vendor: NVIDIA Corporation
Version: 4.5.0 NVIDIA 595.71.05
```

`nvidia-smi`では、Unityがgraphics process `G`、trainer Pythonがcompute process
`C`として表示された。

## GCE L4 vWS g2-standard-16

保存ログ:

- `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T11552
  8Z\unity.log` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T11552
  8Z\resource-usage.log` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T11552
  8Z\gce-benchmark-summary.md` -
  `G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T11552
  8Z.tar.gz`

根拠:

```text
Processor: Intel(R) Xeon(R) CPU @ 2.20GHz, 16 core(s) @ 2200 MHz
Available Memory: 64298 MB
Display 0 'NVIDIA VGX  32"': 1024x768 (primary device).
Renderer: NVIDIA L4/PCIe/SSE2
Vendor: NVIDIA Corporation
Version: 4.5.0 NVIDIA 595.71.05
```

`g2-standard-4`との違いは、GPUを同じL4 vWS
1枚に固定したまま、CPUとメモリを増やした点である。条件を揃えるため、Unity
build、`navigation-final-bench-100k.yaml`、`time-scale 10`、NVIDIA
Xorg/OpenGL、`--mlagents-port 5004`は前回と同じにした。

結果は、trainer elapsed `845.964s`、wall elapsed `851s`、trainer steps/sec
`118.208`だった。`g2-standard-4`の`108.971 steps/s`に対して約`1.08x`で、CPU
coreを4から16へ増やしても改善幅は小さかった。

参考リソース利用率は以下。

| 指標 | 値 |
| --- | --- |
| GPU utilization | 平均`5.690%`、中央値`5.000%`、最小`0%`、最大`18%`、`n=29` |
| GPU memory used | 平均`418.586 MiB`、中央値`432.000 MiB` |
| power draw | 平均`31.317 W` |
| trainer main CPU active | 平均`99.750%` |
| trainer child CPU active | 平均`24.971%` |
| Unity CPU | 平均`66.571%` |

`nvidia-smi`では、trainer Pythonがcompute process `C`、Unity playerがgraphics
process `G`として表示された。Unity logでもNVIDIA rendererを確認している。

## RTX PRO 6000 quotaメモ

L4より高性能なGPUとして`g4-standard-12` / NVIDIA RTX PRO
6000系も候補にしたが、`us-central1`の`GPUS_PER_GPU_FAMILY`、dimension
`gpu_family:
NVIDIA_RTX_PRO_6000`のquotaが`0`で、増枠申請は一度拒否された。拒否理由は、新規pr
ojectまたはbilling accountの利用履歴不足のため、48時間待って再申請するかbilling
historyが増えるまで待つよう案内されたものだった。

そのため、RTX PRO 6000のquota待ちの間に、同じL4
vWSでCPU/メモリだけを増やす`g2-standard-16`を追加ベンチとして実行した。

## 解釈

今回の差は、単純なGPU種類だけではなく、VMのCPU
core数、ColabとGCEの実行基盤差、Unity描画がNVIDIA
OpenGLに載ったかどうかが混ざっている。

ただしGCE L4 vWSでは、Colab T4で確認できなかったUnity
GPU描画が成立しており、10万stepの速度もColab T4比で約`3.67x`から`3.98x`、Colab
CPU比で約`5.83x`から`6.32x`だった。今後クラウドでheadless実行を進める場合、Colab
GPUよりも、OpenGL/Vulkan graphics workloadを正式に扱えるGPU
VMを優先して評価する価値が高い。

一方、`g2-standard-4`から`g2-standard-16`への差は約`1.08x`に留まった。今回の単発
ベンチだけで断定はしないが、少なくともこの条件では、CPU
core数だけを大きく増やすよりも、Unity
GPU描画が成立する環境を選ぶことの方が大きな効果を持つ可能性が高い。
