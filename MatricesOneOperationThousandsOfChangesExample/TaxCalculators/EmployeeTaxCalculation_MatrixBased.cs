using MathNet.Numerics.LinearAlgebra.Double;
using MatricesOneOperationThousandsOfChangesExample.Data;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers;

namespace MatricesOneOperationThousandsOfChangesExample
{
    /// <summary>
    /// Orchestrates the matrix-based tax calculation by building the plan and inputs, executing the blocked multiply, and packing results.
    /// </summary>
    public class EmployeeTaxCalculation_MatrixBased
    {
        /// <summary>
        /// Caches the plan so repeated benchmark runs do not pay plan-build cost each time.
        /// </summary>
        private static readonly MatrixPolicyPlan matrixPolicyPlan = MatrixPolicyPlan.Build();

        private readonly double[] incomes;
        private readonly DenseMatrix employeeFeatureMatrix;

        private readonly Dictionary<TaxData.State, int[]> indicesByState;

        private readonly double[] federalTaxes;
        private readonly double[] stateTaxes;
        private readonly double[][] payrollTaxesByPolicy;

        public EmployeeTaxCalculation_MatrixBased(IReadOnlyList<Employee> employees)
        {
            incomes = new double[employees.Count];
            var temp = new Dictionary<TaxData.State, List<int>>();

            for (int i = 0; i < employees.Count; i++)
            {
                Employee employee = employees[i];

                double income = employee.Income;
                incomes[i] = income;

                TaxData.State state = employee.State;
                if (!temp.TryGetValue(state, out List<int>? list))
                {
                    list = new List<int>();
                    temp[state] = list;
                }
                list.Add(i);
            }

            indicesByState = new Dictionary<TaxData.State, int[]>(temp.Count);
            foreach (var kvp in temp)
                indicesByState[kvp.Key] = kvp.Value.ToArray();

            // Build full feature matrix once up-front (income + thresholds). Income column is populated here.
            employeeFeatureMatrix = FeatureMatrixBuilder.BuildIncomeFeatureMatrix(incomes, matrixPolicyPlan.SharedThresholds);

            // Preallocate reusable output buffers to avoid allocation during timed runs.
            federalTaxes = new double[employees.Count];
            stateTaxes = new double[employees.Count];
            payrollTaxesByPolicy = AllocatePayrollPolicyArrays(TaxData.PayrollPolicies.Count, employees.Count);
        }

        /// <summary>
        /// Executes the matrix-based tax calculation for a set of employees and writes all computed tax and general ledger results into preallocated output buffers.
        /// </summary>
        /// <param name="employees">
        /// The ordered list of employees whose income and tax inputs define the rows of the matrix calculation.
        /// </param>
        /// <param name="preprocess">
        /// Precomputed input buffers and feature matrix data used to execute the matrix policy plan without allocating during the timed calculation.
        /// </param>
        /// <param name="results">
        /// Prewarmed result objects that are mutated in-place to receive the final per-employee tax totals and general ledger postings.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any required input or output buffer is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when any provided buffer is smaller than the number of employees being processed.
        /// </exception>
        public void CalculateTaxesInto(IReadOnlyList<Employee> employees)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));


            int employeeCount = employees.Count;

            if (federalTaxes.Length < employeeCount)
                throw new ArgumentException("Federal taxes array is smaller than employee count.", nameof(federalTaxes));

            if (stateTaxes.Length < employeeCount)
                throw new ArgumentException("State taxes array is smaller than employee count.", nameof(stateTaxes));

            // Execute matrix policy plan
            MatrixExecutionEngine.Execute(
                employeeFeatureMatrix,
                matrixPolicyPlan,
                indicesByState,
                federalTaxes,
                stateTaxes,
                payrollTaxesByPolicy);
        }

        public void PackResultsInto(IReadOnlyList<Employee> employees, TaxResult[] results)
        {
            TaxResultPacker.Pack(
                employees,
                incomes,
                federalTaxes,
                stateTaxes,
                payrollTaxesByPolicy,
                matrixPolicyPlan.PayrollIndices,
                TaxData.GeneralLedgerPostingPolicies,
                results);
        }


        /// <summary>
        /// Allocates a per-policy output array for payroll taxes where each policy buffer is sized to the employee count.
        /// </summary>
        /// <param name="payrollPolicyCount">The number of payroll policies whose outputs must be captured.</param>
        /// <param name="employeeCount">The number of employees whose outputs must be captured.</param>
        /// <returns>A jagged array indexed by payroll policy index and then employee index.</returns>
        private double[][] AllocatePayrollPolicyArrays(int payrollPolicyCount, int employeeCount)
        {
            double[][] payrollTaxesByPolicy = new double[payrollPolicyCount][];
            for (int policyIndex = 0; policyIndex < payrollPolicyCount; policyIndex++)
                payrollTaxesByPolicy[policyIndex] = new double[employeeCount];
            return payrollTaxesByPolicy;
        }
    }
}
