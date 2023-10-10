using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ChromaticityDotNet.Model.ConfigEnum;

namespace ChromaticityDotNet.Model
{
    /// <summary>
    /// Standard color chips data
    /// </summary>
    public class ColorChips
    {
        /// <summary>
        /// ColorFastnessChinaStandard
        /// </summary>
        public enum ColorFastnessChinaStandard
        {
            /// <summary>
            /// 纺织品色牢度试验评定变色用灰色样卡标准，采用欧氏距离色差法
            /// </summary>
            GBT250,
            /// <summary>
            /// 纺织品色牢度试验评定沾色用灰色样卡标准，采用欧氏距离色差法
            /// </summary>
            GBT251,
        }

        /// <summary>
        /// 色卡数据的接口
        /// </summary>
        public interface IColorStandard
        {
            string StandardName { get; }
            string[] FastnessRank { get; }
            double[] DeltaE { get; }
            double[] DeltaEOffset { get; }
            DeltaEType DeltaEFormula { get; }
            int GBVersion { get; }
        }

        public class GBT250Info : IColorStandard
        {
            public string StandardName => "纺织品色牢度试验评定变色用灰色样卡标准";
            public string[] FastnessRank { get; } = { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" };
            public double[] DeltaE { get; } = { 0, 0.8, 1.7, 2.5, 3.4, 4.8, 6.8, 9.6, 13.6 };
            public double[] DeltaEOffset { get; } = { 0.2, 0.2, 0.3, 0.35, 0.4, 0.5, 0.6, 0.7, 1.0 };
            public DeltaEType DeltaEFormula => DeltaEType.DeltaE1976;
            public int GBVersion => 2008;
        }

        public class GBT251Info : IColorStandard
        {
            public string StandardName => "纺织品色牢度试验评定沾色用灰色样卡标准";
            public string[] FastnessRank { get; } = { "5", "4-5", "4", "3-4", "3", "2-3", "2", "1-2", "1" };
            public double[] DeltaE { get; } = { 0, 2.2, 4.3, 6.0, 8.5, 12.0, 16.9, 24.0, 34.1 };
            public double[] DeltaEOffset { get; } = { 0.2, 0.3, 0.3, 0.4, 0.5, 0.7, 1.0, 1.5, 2.0 };
            public DeltaEType DeltaEFormula => DeltaEType.DeltaE1976;
            public int GBVersion => 2008;
        }
    }
}
