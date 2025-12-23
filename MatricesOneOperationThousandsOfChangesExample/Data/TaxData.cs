using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// This class holds hard coded tax bracket data. It's a lot of raw real world data we use to make our demonstration a little more realistic. There's nothing fun hidden in here though and you probably don't need to read it.
    /// </summary>
    public static class TaxData
    {
        public enum State
        {
            California,
            NewYork,
            Texas,
            Florida,
            Pennsylvania,
            NewJersey,
            Illinois,
            Massachusetts,
            Ohio,
            Washington
        }

        public enum PayrollTax
        {
            SocialSecurity,
            Medicare,
            AdditionalMedicare,
            FUTA,
            SUTA
        }

        public enum PayrollSide
        {
            Employee,
            Employer
        }

        public enum PayrollRule
        {
            Flat,
            Capped,
            AboveThreshold
        }

        public readonly struct ProgressiveTaxTable
        {
            public ProgressiveTaxTable(double[] lowerBounds, double[] rates)
            {
                LowerBounds = lowerBounds;
                Rates = rates;
            }

            public double[] LowerBounds { get; }
            public double[] Rates { get; }
        }

        public readonly record struct PayrollPolicy(
           PayrollTax Tax,
           PayrollSide Side,
           PayrollRule Rule,
           double Rate,
           double Parameter = 0.0
       );

        public readonly record struct PayrollYearInputs(
            double SocialSecurityWageBaseCap,
            double AdditionalMedicareThreshold,
            double FutaWageBaseCap,
            double SutaWageBaseCap
        );

        public static readonly PayrollYearInputs PayrollInputs =
            new PayrollYearInputs(
                SocialSecurityWageBaseCap: 170_000,
                AdditionalMedicareThreshold: 200_000,
                FutaWageBaseCap: 7_000,
                SutaWageBaseCap: 9_000
            );

        public static readonly IReadOnlyList<PayrollPolicy> PayrollPolicies =
            new PayrollPolicy[]
            {
                // Employee-side
                new(PayrollTax.SocialSecurity, PayrollSide.Employee, PayrollRule.Capped, Rate: 0.062, Parameter: PayrollInputs.SocialSecurityWageBaseCap),
                new(PayrollTax.Medicare, PayrollSide.Employee, PayrollRule.Flat, Rate: 0.0145),
                new(PayrollTax.AdditionalMedicare, PayrollSide.Employee, PayrollRule.AboveThreshold, Rate: 0.009, Parameter: PayrollInputs.AdditionalMedicareThreshold),

                // Employer-side
                new(PayrollTax.SocialSecurity, PayrollSide.Employer, PayrollRule.Capped, Rate: 0.062, Parameter: PayrollInputs.SocialSecurityWageBaseCap),
                new(PayrollTax.Medicare, PayrollSide.Employer, PayrollRule.Flat, Rate: 0.0145),
                new(PayrollTax.FUTA, PayrollSide.Employer, PayrollRule.Capped, Rate: 0.006, Parameter: PayrollInputs.FutaWageBaseCap),
                new(PayrollTax.SUTA, PayrollSide.Employer, PayrollRule.Capped, Rate: 0.027, Parameter: PayrollInputs.SutaWageBaseCap),
            };

        public static readonly ProgressiveTaxTable FederalTable =
            new ProgressiveTaxTable(
                new double[] { 0, 11_926, 48_476, 103_351, 197_301, 250_526, 626_351 },
                new double[] { 0.10, 0.12, 0.22, 0.24, 0.32, 0.35, 0.37 });


        public static readonly IReadOnlyDictionary<State, ProgressiveTaxTable> StateTables =
            new Dictionary<State, ProgressiveTaxTable>
            {
                [State.California] = new ProgressiveTaxTable(
                    new double[] { 0, 10_757, 25_500, 40_246, 55_867, 70_607, 360_660, 432_788, 721_315 },
                    new double[] { 0.01, 0.02, 0.04, 0.06, 0.08, 0.093, 0.103, 0.113, 0.123 }),

                [State.NewYork] = new ProgressiveTaxTable(
                    new double[] { 0, 8_501, 11_701, 13_901, 80_651, 215_401, 1_077_551, 5_000_001, 25_000_001 },
                    new double[] { 0.04, 0.045, 0.0525, 0.055, 0.06, 0.0685, 0.0965, 0.103, 0.109 }),

                // No income tax states (real; 0% across all income).
                [State.Texas] = new ProgressiveTaxTable(new double[] { 0 }, new double[] { 0.0 }),
                [State.Florida] = new ProgressiveTaxTable(new double[] { 0 }, new double[] { 0.0 }),
                [State.Washington] = new ProgressiveTaxTable(new double[] { 0 }, new double[] { 0.0 }),

                // Flat tax states (single-rate progressive table with one bracket).
                [State.Pennsylvania] = new ProgressiveTaxTable(new double[] { 0 }, new double[] { 0.0307 }),
                [State.Illinois] = new ProgressiveTaxTable(new double[] { 0 }, new double[] { 0.0495 }),

                [State.Massachusetts] = new ProgressiveTaxTable(
                    new double[] { 0, 1_000_000 },
                    new double[] { 0.05, 0.09 }),

                [State.Ohio] = new ProgressiveTaxTable(
                    new double[] { 0, 26_050, 100_000 },
                    new double[] { 0.0, 0.0275, 0.03125 }),

                [State.NewJersey] = new ProgressiveTaxTable(
                    new double[] { 0, 20_000, 35_000, 40_000, 75_000, 500_000, 1_000_000 },
                    new double[] { 0.014, 0.0175, 0.035, 0.05525, 0.0637, 0.0897, 0.1075 }),
            };
    }
}
