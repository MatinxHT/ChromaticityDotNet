# 07 纺织品同色异谱评价：示例数据

这些是合成相对反射光谱，用于验证功能操作和换光源后的色差变化。所有 CSV 均为两列：波长 / nm、相对反射率，包含表头和 41 个采样点。百分数统一保留两位小数，比例统一保留四位小数；两种单位表示同一组反射率数据。

在 07 页面设置 **380–780 nm、10 nm、10° · CIE 1964、ΔE00 / CIEDE2000、kL:kC:kH = 1:1:1**，然后分别向标准品和样品输入框导入对应 CSV。

| 测试 | 标准品 CSV | 样品 CSV | 输入单位 |
| --- | --- | --- | --- |
| 同色异谱 | [standard.csv](standard.csv) | [metameric-sample.csv](metameric-sample.csv) | 百分数 |
| 相同光谱 | [standard.csv](standard.csv) | 同一个 standard.csv | 百分数 |
| 明显色差 | [standard.csv](standard.csv) | [different-sample.csv](different-sample.csv) | 百分数 |
| 同色异谱（比例） | [standard-fraction.csv](standard-fraction.csv) | [metameric-sample-fraction.csv](metameric-sample-fraction.csv) | 比例 |
| 明显色差（比例） | [standard-fraction.csv](standard-fraction.csv) | [different-sample-fraction.csv](different-sample-fraction.csv) | 比例 |

页面的三个「载入…示例」按钮可以直接填入上述数据，恢复 380–780 nm / 10 nm 和 10° 观察者，并按当前选择的百分数/比例单位填入。点击「评价同色异谱」计算；载入示例保留当前色差公式及系数，测试下述配色目标时请使用默认 ΔE00 和 1:1:1。

同色异谱示例保留微小的 D65 配色残差，设计目标为 **D65 / 10° 下 0 < ΔE00 < 0.1**。可比较 CWF / FL2、A、U3000 / FL12、TL84 / FL11 下的色差变化。相同光谱示例用于检查五种光源下的零色差。

明显色差样品为标准品反射率的 65%，再按百分数两位小数取整。比例与百分数输入应得到相同 Lab 和色差。切换到 2° 后，默认同色异谱示例在 D65 下的色差也会变化，其配色是按 10° 构造的。

可继续验证：修改采样间隔为 5 nm 而保留 41 点，应提示需要 81 点；删除样品末行，应提示点数不足；粘贴三列 Lab，应拒绝光谱输入。每次修改输入或观察条件都会清除旧表和旧曲线，重新计算后恢复。修改色差公式或有效系数则自动更新已有结果。

生成方式：`tools/generate_textile_metamerism_examples.py` 基于仓库内 CIE 数据，近似平衡平滑反射率扰动在 D65 / 10° 下的 XYZ 分量并保留少量残差，再将反射率取到百分数两位小数。反射率按 10 nm 采样、1 nm 线性插值积分，与应用调用的计算库一致。
