using MatricesOneOperationThousandsOfChangesExample.Data;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers.MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers;


namespace MatricesOneOperationThousandsOfChangesExample
{
    /// <summary>
    /// Orchestrates the matrix-based tax calculation by building the plan and inputs, executing the blocked multiply, and packing results.
    /// </summary>
    public sealed class EmployeeTaxCalculation_MatrixBased
    {
        /// <summary>
        /// Caches the plan so repeated benchmark runs do not pay plan-build cost each time.
        /// </summary>
        private static readonly MatrixPolicyPlan CachedPlan = MatrixPolicyPlan.Build();

        /// <summary>
        /// Calculates federal, state, and payroll taxes for a set of employees using a shared feature basis and matrix multiplication.
        /// </summary>
        /// <param name="employees">The employees to compute taxes for.</param>
        /// <returns>A materialized sequence of tax results aligned to the input employee ordering.</returns>
        public static IEnumerable<TaxResult> CalculateTaxes(IReadOnlyList<Employee> employees)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (employees.Count == 0) return Array.Empty<TaxResult>();

            MatrixPolicyPlan plan = CachedPlan;

            int featureCount = 1 + plan.SharedThresholds.Length;

            EmployeeInputs.EmployeePreprocessResult preprocess =
                EmployeeInputs.BuildForMatrix(employees, featureCount);

            /// <summary>
            /// Fill columns 1..N only (threshold columns) because column 0 (income) is already populated by BuildForMatrix.
            /// </summary>
            FeatureMatrixBuilder.FillThresholdColumns(
                preprocess.Inputs.Incomes,
                plan.SharedThresholds,
                preprocess.EmployeeFeatureMatrix);

            double[] federalTaxes = new double[employees.Count];
            double[] stateTaxes = new double[employees.Count];

            double[][] payrollTaxesByPolicy = AllocatePayrollPolicyArrays(
                payrollPolicyCount: TaxData.PayrollPolicies.Count,
                employeeCount: employees.Count);

            MatrixExecutionEngine.Execute(
                preprocess.EmployeeFeatureMatrix,
                plan,
                preprocess.Inputs,
                federalTaxes,
                stateTaxes,
                payrollTaxesByPolicy);

            return TaxResultPacker.Pack(
                employees,
                federalTaxes,
                stateTaxes,
                payrollTaxesByPolicy,
                plan.PayrollIndices);
        }

        /// <summary>
        /// Allocates a per-policy output array for payroll taxes where each policy buffer is sized to the employee count.
        /// </summary>
        /// <param name="payrollPolicyCount">The number of payroll policies whose outputs must be captured.</param>
        /// <param name="employeeCount">The number of employees whose outputs must be captured.</param>
        /// <returns>A jagged array indexed by payroll policy index and then employee index.</returns>
        private static double[][] AllocatePayrollPolicyArrays(int payrollPolicyCount, int employeeCount)
        {
            double[][] payrollTaxesByPolicy = new double[payrollPolicyCount][];
            for (int policyIndex = 0; policyIndex < payrollPolicyCount; policyIndex++)
                payrollTaxesByPolicy[policyIndex] = new double[employeeCount];
            return payrollTaxesByPolicy;
        }
    }
}
