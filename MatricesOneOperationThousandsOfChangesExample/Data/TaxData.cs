using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// This class holds tax data such as payroll policies and progressive tax tables used for tax calculations.
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
            FederalUnemploymentTax,
            StateUnemploymentTax,
            EmployerBurden
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
            double FederalUnemploymentWageBaseCap,
            double StateUnemploymentWageBaseCap
        );

        public readonly record struct GeneralLedgerPostingPolicy(
            int BucketId,
            double Rate
        );

        /// <summary>
        /// Year-specific payroll inputs used by payroll policies (caps / thresholds).
        /// </summary>
        public static readonly PayrollYearInputs PayrollInputs;

        /// <summary>
        /// Payroll tax policies evaluated for every employee (employee-side and employer-side).
        /// </summary>
        public static readonly IReadOnlyList<PayrollPolicy> PayrollPolicies;

        /// <summary>
        /// Federal progressive withholding table.
        /// </summary>
        public static readonly ProgressiveTaxTable FederalTable;

        /// <summary>
        /// State progressive withholding tables keyed by state.
        /// </summary>
        public static readonly IReadOnlyDictionary<State, ProgressiveTaxTable> StateTables;

        /// <summary>
        /// Various general ledger posting policies used for accounting entries.
        /// </summary>
        public static readonly IReadOnlyList<GeneralLedgerPostingPolicy> GeneralLedgerPostingPolicies;

        static TaxData()
        {
            PayrollInputs = TaxDataGenerator.BuildPayrollInputs();
            PayrollPolicies = TaxDataGenerator.BuildPayrollPolicies(PayrollInputs);
            FederalTable = TaxDataGenerator.BuildFederalTable();
            StateTables = TaxDataGenerator.BuildStateTables();
            GeneralLedgerPostingPolicies = TaxDataGenerator.BuildGeneralLedgerPostingPolicies(PayrollInputs);
        }
    }
}
