using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample
{
    /// <summary>
    /// This class holds hard coded tax bracket data. 
    /// </summary>
    public static class TaxData
    {
        /// <summary>
        /// The lower bounds for each federal tax bracket.
        /// </summary>
        public static double[] FederalLowerBounds = { 0, 11_926, 48_476, 103_351, 197_301, 250_526, 626_351 };

        /// <summary>
        /// The tax rates for each federal tax bracket.
        /// </summary>
        public static double[] FederalRates = { 0.10, 0.12, 0.22, 0.24, 0.32, 0.35, 0.37 };

        /// <summary>
        /// The lower bounds for each California state tax bracket.
        /// </summary>
        public static double[] CaliforniaLowerBounds = { 0, 10_757, 25_500, 40_246, 55_867, 70_607, 360_660, 432_788, 721_315 };

        /// <summary>
        /// The tax rates for each California state tax bracket.
        /// </summary>
        public static double[] CaliforniaRates = { 0.01, 0.02, 0.04, 0.06, 0.08, 0.093, 0.103, 0.113, 0.123 };

        /// <summary>
        /// The lower bounds for each New York state tax bracket.
        /// </summary>
        public static double[] NewYorkLowerBounds = { 0, 8_501, 11_701, 13_901, 80_651, 215_401, 1_077_551, 5_000_001, 25_000_001 };

        /// <summary>
        /// The tax rates for each New York state tax bracket.
        /// </summary>
        public static double[] NewYorkRates = { 0.04, 0.045, 0.0525, 0.055, 0.06, 0.0685, 0.0965, 0.103, 0.109 };
    }
}
