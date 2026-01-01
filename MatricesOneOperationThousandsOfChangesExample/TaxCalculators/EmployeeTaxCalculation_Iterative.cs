using MatricesOneOperationThousandsOfChangesExample.Data;
using static MatricesOneOperationThousandsOfChangesExample.Data.TaxData;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators
{
    /// <summary>
    /// Computes taxes using straightforward per-employee loops and direct policy evaluation.
    /// This class is used as a baseline to compare the matrix based solution to, you wont find anything interesting in here.
    /// </summary>
    public class EmployeeTaxCalculation_Iterative
    {
        /// <summary>
        /// Calculates federal, state, payroll, and general ledger values for each employee into a prewarmed result array.
        /// </summary>
        /// <param name="employees">The employees to compute taxes for.</param>
        /// <param name="results">The prewarmed result array receiving computed values.</param>
        public static void CalculateTaxesInto(IReadOnlyList<Employee> employees, TaxResult[] results)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int employeeCount = employees.Count;

            if (results.Length != employeeCount)
                throw new ArgumentException("results.Length must match employees.Count.", nameof(results));

            IReadOnlyList<TaxData.GeneralLedgerPostingPolicy> generalLedgerPolicies = TaxData.GeneralLedgerPostingPolicies;
            int generalLedgerBucketCount = generalLedgerPolicies.Count;

            for (int employeeIndex = 0; employeeIndex < employeeCount; employeeIndex++)
            {
                Employee employee = employees[employeeIndex];
                TaxResult taxResult = results[employeeIndex]
                    ?? throw new InvalidOperationException("TaxResult array must be prewarmed.");

                double income = employee.Income;

                double federalTax = CalculateProgressiveTax(income, TaxData.FederalTable);
                double stateTax = CalculateStateTax(income, employee.State);

                PayrollBreakdown payroll = CalculatePayrollBreakdown(income);

                taxResult.Employee = employee;
                taxResult.FederalTax = federalTax;
                taxResult.StateTax = stateTax;

                taxResult.SocialSecurityEmployee = payroll.SocialSecurityEmployee;
                taxResult.MedicareEmployee = payroll.MedicareEmployee;
                taxResult.AdditionalMedicareEmployee = payroll.AdditionalMedicareEmployee;

                taxResult.SocialSecurityEmployer = payroll.SocialSecurityEmployer;
                taxResult.MedicareEmployer = payroll.MedicareEmployer;
                taxResult.FederalUnemploymentTaxes = payroll.FederalUnemploymentTaxes;
                taxResult.StateUnemploymentTaxes = payroll.StateUnemploymentTaxes;

                if (generalLedgerBucketCount > 0)
                {
                    double[] generalLedgerPostings = taxResult.GeneralLedgerPostings
                        ?? throw new InvalidOperationException("GeneralLedgerPostings must be prewarmed when GL buckets exist.");

                    if (generalLedgerPostings.Length != generalLedgerBucketCount)
                        throw new InvalidOperationException("GeneralLedgerPostings length does not match GL policy count.");

                    for (int bucketIndex = 0; bucketIndex < generalLedgerBucketCount; bucketIndex++)
                        generalLedgerPostings[bucketIndex] = income * generalLedgerPolicies[bucketIndex].Rate;
                }
                else
                {
                    taxResult.GeneralLedgerPostings = null;
                }
            }
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
            public PayrollBreakdown(double socialSecurityEmployee,  double medicareEmployee, double additionalMedicareEmployee, double socialSecurityEmployer, double medicareEmployer, double federalUnemploymentTaxes, double stateUnemploymentTaxes)
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
        /// Assigns a computed payroll amount to the appropriate output variable based on
        /// the payroll policy’s side and tax type.
        /// </summary>
        /// <param name="policy">
        /// The payroll policy describing the tax type and whether it applies to the employee or employer.
        /// </param>
        /// <param name="amount">
        /// The computed payroll amount to assign.
        /// </param>
        /// <param name="socialSecurityEmployee">
        /// The output location for the employee portion of Social Security tax.
        /// </param>
        /// <param name="medicareEmployee">
        /// The output location for the employee portion of Medicare tax.
        /// </param>
        /// <param name="additionalMedicareEmployee">
        /// The output location for the employee portion of Additional Medicare tax.
        /// </param>
        /// <param name="socialSecurityEmployer">
        /// The output location for the employer portion of Social Security tax.
        /// </param>
        /// <param name="medicareEmployer">
        /// The output location for the employer portion of Medicare tax.
        /// </param>
        /// <param name="federalUnemploymentTaxes">
        /// The output location for the employer portion of Federal Unemployment Tax.
        /// </param>
        /// <param name="stateUnemploymentTaxes">
        /// The output location for the employer portion of State Unemployment Tax.
        /// </param>
        private static void AssignPayrollAmount(TaxData.PayrollPolicy policy, double amount, ref double socialSecurityEmployee, ref double medicareEmployee, ref double additionalMedicareEmployee, ref double socialSecurityEmployer, ref double medicareEmployer, ref double federalUnemploymentTaxes, ref double stateUnemploymentTaxes)
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

        /// <summary>
        /// Computes general ledger postings by iterating employees and applying each posting policy.
        /// </summary>
        /// <param name="income">
        /// Per-employee income values.
        /// </param>
        /// <param name="policies">
        /// General ledger posting policies defining allocation rates.
        /// </param>
        /// <param name="destinationColumnMajor">
        /// Preallocated buffer receiving postings in column-major order.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the destination buffer length does not match the expected output size.
        /// </exception>
        public static void ComputeGeneralLedgerPostingsIterative(
            double[] income,
            IReadOnlyList<GeneralLedgerPostingPolicy> policies,
            double[] destinationColumnMajor)
        {
            if (income == null) throw new ArgumentNullException(nameof(income));
            if (policies == null) throw new ArgumentNullException(nameof(policies));
            if (destinationColumnMajor == null) throw new ArgumentNullException(nameof(destinationColumnMajor));

            int employeeCount = income.Length;
            int bucketCount = policies.Count;

            if (destinationColumnMajor.Length != employeeCount * bucketCount)
                throw new ArgumentException(
                    "Destination buffer length must equal employeeCount * policyCount.",
                    nameof(destinationColumnMajor));

            for (int b = 0; b < bucketCount; b++)
            {
                double rate = policies[b].Rate;
                int columnOffset = b * employeeCount;

                for (int i = 0; i < employeeCount; i++)
                    destinationColumnMajor[columnOffset + i] = income[i] * rate;
            }
        }
    }
}
