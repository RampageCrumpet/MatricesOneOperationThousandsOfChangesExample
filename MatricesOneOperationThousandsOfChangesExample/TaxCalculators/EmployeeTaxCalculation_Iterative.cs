using MathNet.Numerics.RootFinding;
using MatricesOneOperationThousandsOfChangesExample.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators
{
    /// <summary>
    /// Computes taxes using straightforward per-employee loops and direct policy evaluation.
    /// This class is used as a baseline to compare the matrix based solution to, you wont find anything interesting in here.
    /// </summary>
    public class EmployeeTaxCalculation_Iterative
    {
        /// <summary>
        /// Calculates federal, state, and payroll taxes for each employee using direct iterative evaluation.
        /// </summary>
        /// <param name="employees">The employees to compute taxes for.</param>
        /// <returns>Tax results aligned to the input employee ordering.</returns>
        public static IEnumerable<TaxResult> CalculateTaxes(IReadOnlyList<Employee> employees)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (employees.Count == 0) return Array.Empty<TaxResult>();

            var results = new List<TaxResult>(employees.Count);

            for (int employeeIndex = 0; employeeIndex < employees.Count; employeeIndex++)
            {
                Employee employee = employees[employeeIndex];
                TaxResult result = CalculateEmployeeTaxes(employee);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Calculates the full tax result for one employee by evaluating federal, state, and payroll policies.
        /// </summary>
        /// <param name="employee">The employee to compute taxes for.</param>
        /// <returns>A populated tax result for the given employee.</returns>
        private static TaxResult CalculateEmployeeTaxes(Employee employee)
        {
            double income = employee.Income;

            double federalTax = CalculateProgressiveTax(income, TaxData.FederalTable);
            double stateTax = CalculateStateTax(income, employee.State);

            PayrollBreakdown payroll = CalculatePayrollBreakdown(income);

            return new TaxResult
            {
                Employee = employee,
                FederalTax = federalTax,
                StateTax = stateTax,

                SocialSecurityEmployee = payroll.SocialSecurityEmployee,
                MedicareEmployee = payroll.MedicareEmployee,
                AdditionalMedicareEmployee = payroll.AdditionalMedicareEmployee,

                SocialSecurityEmployer = payroll.SocialSecurityEmployer,
                MedicareEmployer = payroll.MedicareEmployer,
                FederalUnemploymentTaxes = payroll.FederalUnemploymentTaxes,
                StateUnemploymentTaxes = payroll.StateUnemploymentTaxes
            };
        }

        /// <summary>
        /// Calculates state tax for an income and state by looking up the state table and evaluating it progressively.
        /// </summary>
        /// <param name="income">The income to tax.</param>
        /// <param name="state">The state whose tax table should be applied.</param>
        /// <returns>The computed state tax, or zero if the state has no configured table.</returns>
        private static double CalculateStateTax(double income, TaxData.State state)
        {
            if (!TaxData.StateTables.TryGetValue(state, out TaxData.ProgressiveTaxTable stateTable))
                return 0.0;

            return CalculateProgressiveTax(income, stateTable);
        }

        /// <summary>
        /// Calculates progressive tax for an income given a table of lower bounds and marginal rates.
        /// </summary>
        /// <param name="income">The income to tax.</param>
        /// <param name="table">The progressive tax table defining bracket lower bounds and rates.</param>
        /// <returns>The computed progressive tax.</returns>
        private static double CalculateProgressiveTax(double income, TaxData.ProgressiveTaxTable table)
        {
            double[] lowerBounds = table.LowerBounds;
            double[] rates = table.Rates;

            double tax = 0.0;

            for (int bracketIndex = 0; bracketIndex < rates.Length; bracketIndex++)
            {
                double bracketStart = lowerBounds[bracketIndex];
                double bracketEnd = bracketIndex + 1 < rates.Length ? lowerBounds[bracketIndex + 1] : double.PositiveInfinity;

                if (income <= bracketStart)
                    break;

                double taxableInBracket = Math.Min(income, bracketEnd) - bracketStart;
                tax += taxableInBracket * rates[bracketIndex];
            }

            return tax;
        }

        /// <summary>
        /// Holds the per-employee payroll tax amounts split into employee-side and employer-side fields.
        /// </summary>
        private readonly struct PayrollBreakdown
        {
            /// <summary>
            /// Initializes a payroll breakdown with all expected employee-side and employer-side payroll amounts.
            /// </summary>
            public PayrollBreakdown(
                double socialSecurityEmployee,
                double medicareEmployee,
                double additionalMedicareEmployee,
                double socialSecurityEmployer,
                double medicareEmployer,
                double federalUnemploymentTaxes,
                double stateUnemploymentTaxes)
            {
                SocialSecurityEmployee = socialSecurityEmployee;
                MedicareEmployee = medicareEmployee;
                AdditionalMedicareEmployee = additionalMedicareEmployee;

                SocialSecurityEmployer = socialSecurityEmployer;
                MedicareEmployer = medicareEmployer;
                FederalUnemploymentTaxes = federalUnemploymentTaxes;
                StateUnemploymentTaxes = stateUnemploymentTaxes;
            }

            /// <summary>
            /// Gets the employee-side Social Security amount.
            /// </summary>
            public double SocialSecurityEmployee { get; }

            /// <summary>
            /// Gets the employee-side Medicare amount.
            /// </summary>
            public double MedicareEmployee { get; }

            /// <summary>
            /// Gets the employee-side Additional Medicare amount.
            /// </summary>
            public double AdditionalMedicareEmployee { get; }

            /// <summary>
            /// Gets the employer-side Social Security amount.
            /// </summary>
            public double SocialSecurityEmployer { get; }

            /// <summary>
            /// Gets the employer-side Medicare amount.
            /// </summary>
            public double MedicareEmployer { get; }

            /// <summary>
            /// Gets the employer-side federal unemployment tax amount.
            /// </summary>
            public double FederalUnemploymentTaxes { get; }

            /// <summary>
            /// Gets the employer-side state unemployment tax amount.
            /// </summary>
            public double StateUnemploymentTaxes { get; }
        }

        /// <summary>
        /// Calculates payroll taxes for an income by evaluating each configured payroll policy and assigning typed outputs.
        /// </summary>
        /// <param name="income">The income to apply payroll policies to.</param>
        /// <returns>A payroll breakdown containing all employee-side and employer-side payroll values.</returns>
        private static PayrollBreakdown CalculatePayrollBreakdown(double income)
        {
            double socialSecurityEmployee = 0.0;
            double medicareEmployee = 0.0;
            double additionalMedicareEmployee = 0.0;

            double socialSecurityEmployer = 0.0;
            double medicareEmployer = 0.0;
            double federalUnemploymentTaxes = 0.0;
            double stateUnemploymentTaxes = 0.0;

            for (int policyIndex = 0; policyIndex < TaxData.PayrollPolicies.Count; policyIndex++)
            {
                TaxData.PayrollPolicy policy = TaxData.PayrollPolicies[policyIndex];
                double amount = EvaluatePayrollPolicyAmount(income, policy);

                AssignPayrollAmount(
                    policy,
                    amount,
                    ref socialSecurityEmployee,
                    ref medicareEmployee,
                    ref additionalMedicareEmployee,
                    ref socialSecurityEmployer,
                    ref medicareEmployer,
                    ref federalUnemploymentTaxes,
                    ref stateUnemploymentTaxes);
            }

            return new PayrollBreakdown(
                socialSecurityEmployee,
                medicareEmployee,
                additionalMedicareEmployee,
                socialSecurityEmployer,
                medicareEmployer,
                federalUnemploymentTaxes,
                stateUnemploymentTaxes);
        }

        /// <summary>
        /// Evaluates the dollar amount produced by one payroll policy for a given income.
        /// </summary>
        /// <param name="income">The income to evaluate against the policy.</param>
        /// <param name="policy">The payroll policy to evaluate.</param>
        /// <returns>The computed payroll amount for this policy.</returns>
        private static double EvaluatePayrollPolicyAmount(double income, TaxData.PayrollPolicy policy)
        {
            return policy.Rule switch
            {
                TaxData.PayrollRule.Flat => policy.Rate * income,
                TaxData.PayrollRule.AboveThreshold => policy.Rate * Math.Max(0.0, income - policy.Parameter),
                TaxData.PayrollRule.Capped => policy.Rate * Math.Min(income, policy.Parameter),
                _ => throw new ArgumentOutOfRangeException(nameof(policy.Rule), policy.Rule, "Unhandled payroll rule.")
            };
        }

        /// <summary>
        /// Routes a computed payroll amount into the correct typed output field based on payroll side and tax kind.
        /// </summary>
        private static void AssignPayrollAmount(
            TaxData.PayrollPolicy policy,
            double amount,
            ref double socialSecurityEmployee,
            ref double medicareEmployee,
            ref double additionalMedicareEmployee,
            ref double socialSecurityEmployer,
            ref double medicareEmployer,
            ref double federalUnemploymentTaxes,
            ref double stateUnemploymentTaxes)
        {
            if (policy.Side == TaxData.PayrollSide.Employee)
            {
                switch (policy.Tax)
                {
                    case TaxData.PayrollTax.SocialSecurity: socialSecurityEmployee = amount; return;
                    case TaxData.PayrollTax.Medicare: medicareEmployee = amount; return;
                    case TaxData.PayrollTax.AdditionalMedicare: additionalMedicareEmployee = amount; return;
                    default: throw new InvalidOperationException($"Unexpected employee payroll tax: {policy.Tax}");
                }
            }

            switch (policy.Tax)
            {
                case TaxData.PayrollTax.SocialSecurity: socialSecurityEmployer = amount; return;
                case TaxData.PayrollTax.Medicare: medicareEmployer = amount; return;
                case TaxData.PayrollTax.FederalUnemploymentTax: federalUnemploymentTaxes = amount; return;
                case TaxData.PayrollTax.StateUnemploymentTax: stateUnemploymentTaxes = amount; return;
                default: throw new InvalidOperationException($"Unexpected employer payroll tax: {policy.Tax}");
            }
        }
    }
}
