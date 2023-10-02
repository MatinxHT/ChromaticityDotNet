using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChromaticityDotNet
{
    public class DataClass
    {
        public class CIEXYZ
        {
            public double CIEX { get; set; }
            public double CIEY { get; set; }
            public double CIEZ { get; set; }
        }

        public class CIELABCH
        {
            public double CIEL { get; set; }
            public double CIEA { get; set; }
            public double CIEB { get; set; }
            public double CIEC { get; set;}
            public double CIEH { get; set;}
        }
    }
}
