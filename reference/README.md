# CIE 参考数据

这里保存从 <https://www.cie.co.at/data-tables> 收录的 39 组官方数据，供 Codex
和维护者读取、核对并转入代码。**不是运行时资源目录**；库不读取这里的 CSV/JSON，
也不在运行时联网。数据收录不代表对应计算功能已经实现。

## 文件组织

每个分类目录里直接保存官方 `.csv` 和同名 `.json`：

- `observers/`：1931/1964 配色函数和基于锥体基本函数的三刺激值。
- `illuminants/`：标准照明体及典型灯光谱，保留官方不同采样版本。
- `daylight/`：日光 S₀、S₁、S₂ 基函数。
- `spectral-loci/`：光谱轨迹、MacLeod–Boynton 坐标。
- `test-samples/`：CRI、保真度指数、CQS 等评价用样本。
- `visual-response/`：光度、LMS 和 α-opic 等响应。
- `metamerism/`：观察者改变的同色异谱评价参考数据。

CSV 按下载字节保存，包括 BOM、换行符和 `NaN`。`.gitattributes` 禁止 Git
转换这些 CSV 的换行，以免校验值在不同平台上变化。JSON 是项目的更新记录，
其中 `officialMetadata` 保存解析后的完整官方元数据；外层另外记录：

| 字段 | 用途 |
| --- | --- |
| `sourcePage` | 每次检查重新读取数据页面，发现 CSV 或元数据链接迁移 |
| `csvUrl` / `metadataUrl` | 本次归档时的官方文件地址 |
| `retrievedAtUtc` | 实际下载时间，不冒充官方修订日期 |
| `csvFile` / `csvSha256` | 本地文件及实际 SHA-256，检测 CSV 内容变化 |
| `metadataSha256` | 官方元数据原始响应的 SHA-256，检测元数据变化 |
| `officialChecksumValidation` | 下载时对官方声明的校验值逐项核对的结果 |
| `officialMetadata` | 标题、DOI、出版物、单位、列定义、质量、处理规则与许可 |

## 联网检查和转录流程

在仓库根目录执行，Python 3 标准库即可：

```sh
# 只核验本地 CSV，完全离线
python3 tools/cie_reference.py verify

# 用 JSON 中的来源页面查找最新文件，比较实际 CSV 和元数据内容；不写文件
python3 tools/cie_reference.py check

# 也可以只检查一个数据集
python3 tools/cie_reference.py check --filter CIE_std_illum_D65

# 经人工/Codex 审阅后，把选定参考表确定性地转入 C# 常量
python3 tools/generate_cie_data.py

# 检查已转录常量是否与参考一致，不写代码
python3 tools/generate_cie_data.py --check
```

联网检查返回 `UNCHANGED`、`UPDATE` 或 `ERROR`，CSV 即使没有更改元数据也会被比较。
更新、错误或本地 CSV 改动时退出码为 1，全部一致时为 0。网络错误不表示没有更新。
检查不自动覆盖任何 CSV、JSON 或代码，也不创建定期任务。

确认更新后，应审阅官方新 CSV、元数据和版本说明，成对替换归档并更新实际校验值；
再按需要转录，运行测试，检查数值行为变化。不要只修改 JSON 校验值来消除报错。
`archive` 子命令供首次收录目录使用，会拒绝覆盖已有条目。

当前生成器转录 1931/1964 配色函数，以及 `illuminants/` 中的全部 50 条独立光源光谱：
A、C、D50、D55、D65、D75、ID50、ID65、L41，FL1–FL12、FL3.1–FL3.15、
HP1–HP5 和九种 LED。光源目录、来源文件、采样间隔及质量标注由同一生成器输出，
查询下拉框读取这个编译后的目录，不再从旧枚举手工维护。

FL/LED 优先采用已归档的官方 1 nm 版本；对应 5 nm CSV 仍保留作为原始版本参考，
不会在目录中重复列出同一光源。C、D55、D75、ID50、ID65、HP 保留源表 5 nm 网格，
查询更细或错位的波长时显式线性插值，不外推。各表原始相对功率尺度不变。
`CieSpectralData.Illuminants` 和 `GetIlluminantSpectrum(string id)` 提供完整目录与独立数据副本。

旧 `Standardilluminant` 白点/31 点计算枚举保持兼容；CWF→FL2、F7→FL7、
TL84→FL11、U30→FL12 是库内别名约定，不保证任意同名实物灯具等同于该光谱。
完整目录的光谱可以传入 `REFtoXYZ(Spectrum, Spectrum, StandardObserver)`。
其他类别的参考文件仍只归档，不代表对应评价算法已实现。

## 已知数据处理事项

- 1964 10° z-bar 的 560–830 nm 原始条目为 `NaN`。只在转入计算常量时置零；
  原 CSV 保留原样。CIE 目录说明这类原出版物空白通常可计算为零，其他数据集
  仍需逐项确认，不能全局替换所有 `NaN`。
- FL 和 LED 的官方 1 nm 数据元数据标注 `approximated`，保留原有 5 nm 表作为
  独立数据集。不要把 1 nm 间隔等同于新增实测精度。
- 日光基函数元数据的文字描述与列定义不一致：当前实际 CSV 为 300–830 nm、
  5 nm、107 行。保留官方元数据的原始陈述，并在这里记录核验结论。
- 部分文件有 BOM 或空白尾行，解析时可处理这些格式；不要改写原 CSV。
- `CIE_max_sle_mesopic` 的第一列是适应系数 m，不是波长。以下清单中的自变量
  范围来自实际文件，不能统一按 nm 解读。
- 旧 31 点常量与新常量分开。更新参考数据及重新生成代码，不会改写旧计算路径。

数据署名和许可见 [CIE-DATA-NOTICE.md](../CIE-DATA-NOTICE.md)；逐项官方引文见 JSON。

## 当前文件清单

范围、间隔与行数从实际 CSV 检查取得；常规波长单位为 nm。

| 分类 / CSV | 自变量范围 | 间隔 | 数据行数 |
| --- | --- | --- | --- |
| [daylight/CIE_illum_Dxx_comp.csv](daylight/CIE_illum_Dxx_comp.csv) | 300–830 | 5 | 107 |
| [illuminants/CIE_RefSpectrum_L41.csv](illuminants/CIE_RefSpectrum_L41.csv) | 360–830 | 1 | 471 |
| [illuminants/CIE_illum_C.csv](illuminants/CIE_illum_C.csv) | 300–780 | 5 | 97 |
| [illuminants/CIE_illum_D55.csv](illuminants/CIE_illum_D55.csv) | 300–780 | 5 | 97 |
| [illuminants/CIE_illum_D75.csv](illuminants/CIE_illum_D75.csv) | 300–780 | 5 | 97 |
| [illuminants/CIE_illum_FLs.csv](illuminants/CIE_illum_FLs.csv) | 380–780 | 5 | 81 |
| [illuminants/CIE_illum_FLs_1nm.csv](illuminants/CIE_illum_FLs_1nm.csv) | 380–780 | 1 | 401 |
| [illuminants/CIE_illum_HPs.csv](illuminants/CIE_illum_HPs.csv) | 380–780 | 5 | 81 |
| [illuminants/CIE_illum_ID50.csv](illuminants/CIE_illum_ID50.csv) | 300–780 | 5 | 97 |
| [illuminants/CIE_illum_ID65.csv](illuminants/CIE_illum_ID65.csv) | 300–780 | 5 | 97 |
| [illuminants/CIE_illum_LEDs.csv](illuminants/CIE_illum_LEDs.csv) | 380–780 | 5 | 81 |
| [illuminants/CIE_illum_LEDs_1nm.csv](illuminants/CIE_illum_LEDs_1nm.csv) | 380–780 | 1 | 401 |
| [illuminants/CIE_std_illum_A_1nm.csv](illuminants/CIE_std_illum_A_1nm.csv) | 300–830 | 1 | 531 |
| [illuminants/CIE_std_illum_D50.csv](illuminants/CIE_std_illum_D50.csv) | 300–830 | 1 | 531 |
| [illuminants/CIE_std_illum_D65.csv](illuminants/CIE_std_illum_D65.csv) | 300–830 | 1 | 531 |
| [metamerism/CIE_1st_deriv_meta_ind.csv](metamerism/CIE_1st_deriv_meta_ind.csv) | 380–780 | 5 | 81 |
| [observers/CIE_cfb_stv_10deg.csv](observers/CIE_cfb_stv_10deg.csv) | 390–830 | 1 | 441 |
| [observers/CIE_cfb_stv_2deg.csv](observers/CIE_cfb_stv_2deg.csv) | 390–830 | 1 | 441 |
| [observers/CIE_xyz_1931_2deg.csv](observers/CIE_xyz_1931_2deg.csv) | 360–830 | 1 | 471 |
| [observers/CIE_xyz_1964_10deg.csv](observers/CIE_xyz_1964_10deg.csv) | 360–830 | 1 | 471 |
| [spectral-loci/CIE_cc_1931_2deg.csv](spectral-loci/CIE_cc_1931_2deg.csv) | 360–830 | 1 | 471 |
| [spectral-loci/CIE_cc_1964_10deg.csv](spectral-loci/CIE_cc_1964_10deg.csv) | 360–830 | 1 | 471 |
| [spectral-loci/CIE_smb_cc_2deg.csv](spectral-loci/CIE_smb_cc_2deg.csv) | 390–830 | 5 | 89 |
| [test-samples/CIE_srf_CQS_5nm.csv](test-samples/CIE_srf_CQS_5nm.csv) | 380–780 | 5 | 81 |
| [test-samples/CIE_srf_FCI_5nm.csv](test-samples/CIE_srf_FCI_5nm.csv) | 380–780 | 5 | 81 |
| [test-samples/CIE_srf_PS_5nm.csv](test-samples/CIE_srf_PS_5nm.csv) | 380–780 | 5 | 81 |
| [test-samples/CIE_srf_cfi.csv](test-samples/CIE_srf_cfi.csv) | 380–780 | 5 | 81 |
| [test-samples/CIE_srf_cfi_1nm.csv](test-samples/CIE_srf_cfi_1nm.csv) | 380–780 | 1 | 401 |
| [test-samples/CIE_srf_cri.csv](test-samples/CIE_srf_cri.csv) | 360–830 | 5 | 95 |
| [visual-response/CIE_a-opic_action_spectra.csv](visual-response/CIE_a-opic_action_spectra.csv) | 380–780 | 1 | 401 |
| [visual-response/CIE_cfb_sle_10deg.csv](visual-response/CIE_cfb_sle_10deg.csv) | 390–830 | 1 | 441 |
| [visual-response/CIE_cfb_sle_2deg.csv](visual-response/CIE_cfb_sle_2deg.csv) | 390–830 | 1 | 441 |
| [visual-response/CIE_lms_cf_10deg.csv](visual-response/CIE_lms_cf_10deg.csv) | 390–830 | 5 | 89 |
| [visual-response/CIE_lms_cf_2deg.csv](visual-response/CIE_lms_cf_2deg.csv) | 390–830 | 5 | 89 |
| [visual-response/CIE_max_sle_mesopic.csv](visual-response/CIE_max_sle_mesopic.csv) | 0–1 | 0.1 | 11 |
| [visual-response/CIE_sle_10deg.csv](visual-response/CIE_sle_10deg.csv) | 360–830 | 1 | 471 |
| [visual-response/CIE_sle_mesopic_m_0.8.csv](visual-response/CIE_sle_mesopic_m_0.8.csv) | 360–830 | 1 | 471 |
| [visual-response/CIE_sle_photopic.csv](visual-response/CIE_sle_photopic.csv) | 360–830 | 1 | 471 |
| [visual-response/CIE_sle_scotopic.csv](visual-response/CIE_sle_scotopic.csv) | 380–780 | 1 | 401 |
