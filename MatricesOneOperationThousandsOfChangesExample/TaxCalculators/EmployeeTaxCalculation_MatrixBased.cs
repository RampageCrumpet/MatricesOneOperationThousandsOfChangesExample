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
        private static  MatrixPolicyPlan matrixPolicyPlan = MatrixPolicyPlan.Build();

        private double[] incomes;
        private DenseMatrix employeeFeatureMatrix;

        private Dictionary<TaxData.State, int[]> indicesByState;

        private double[] federalTaxes;
        private double[] stateTaxes;
        private double[][] payrollTaxesByPolicy;

        /// <summary>
        /// Number of General Ledger posting "buckets" (a.k.a. posting policies).
        /// </summary>
        private int generalLedgerBucketCount;

        /// <summary>
        /// Rates for each GL bucket (length = generalLedgerBucketCount).
        /// Pulled once from TaxData.GeneralLedgerPostingPolicies.
        /// </summary>
        private double[] generalLedgerRates;

        /// <summary>
        /// Column-major GL postings buffer sized to [employeeCount x bucketCount].
        /// </summary>
        private double[] generalLedgerPostingsColumnMajor;

        /// <summary>
        /// Income column matrix [employeeCount x 1] used by the execution engine to compute general ledger postings.
        /// </summary>
        private DenseMatrix incomeColumnMatrix;

        private DenseMatrix? generalLedgerRatesRowMatrix;
        private DenseMatrix? generalLedgerPostingsMatrix;

        public EmployeeTaxCalculation_MatrixBased(IReadOnlyList<Employee> employees)
        {
            SetupArrays(employees);
        }

        /// <summary>
        /// Sets up all input and output arrays used during the matrix-based tax calculation.
        /// This is only nescessary to avoid computing these arrays during timed runs.
        /// </summary>
        /// <param name="employees"> The list of <see cref="Employee"/>'s we want to set up the arrays to hold.</param>
        private void SetupArrays(IReadOnlyList<Employee> employees)
        {
            indicesByState = Enum.GetValues<TaxData.State>().ToDictionary(
                state => state,
                state => employees
                    .Select((employee, index) => (employee, index))
                    .Where(x => x.employee.State == state)
                    .Select(x => x.index)
                    .ToArray());

            incomes = new double[employees.Count];
            for (int i = 0; i < employees.Count; i++)
                incomes[i] = employees[i].Income;

            employeeFeatureMatrix = FeatureMatrixBuilder.BuildIncomeFeatureMatrix(incomes, matrixPolicyPlan.SharedThresholds);

            incomeColumnMatrix = DenseMatrix.Create(employees.Count, 1, 0.0);
            Array.Copy(incomes, 0, incomeColumnMatrix.Values, 0, employees.Count);

            // Preallocate reusable output buffers to avoid allocation during timed runs.
            federalTaxes = new double[employees.Count];
            stateTaxes = new double[employees.Count];
            payrollTaxesByPolicy = AllocatePayrollPolicyArrays(TaxData.PayrollPolicies.Count, employees.Count);

            // Preallocate buckets to stick the general ledger postings into.
            generalLedgerBucketCount = TaxData.GeneralLedgerPostingPolicies.Count;

            if (generalLedgerBucketCount > 0)
            {
                generalLedgerRates = new double[generalLedgerBucketCount];
                for (int i = 0; i < generalLedgerBucketCount; i++)
                    generalLedgerRates[i] = TaxData.GeneralLedgerPostingPolicies[i].Rate;


                // Allocate the matrix once. We'll use its internal column-major storage as our buffer.
                generalLedgerPostingsMatrix = DenseMatrix.Create(employees.Count, generalLedgerBucketCount, 0.0);

                generalLedgerPostingsColumnMajor = generalLedgerPostingsMatrix.Values;

                // Build a single row matrix [1 x bucketCount] for GL rates
                generalLedgerRatesRowMatrix = DenseMatrix.Create(1, generalLedgerBucketCount, 0.0);
                double[] rateValues = generalLedgerRatesRowMatrix.Values;
                for (int bucket = 0; bucket < generalLedgerBucketCount; bucket++)
                    rateValues[bucket] = generalLedgerRates[bucket];
            }
            else
            {
                generalLedgerRates = Array.Empty<double>();
                generalLedgerPostingsColumnMajor = Array.Empty<double>();
                generalLedgerRatesRowMatrix = null;
                generalLedgerPostingsMatrix = null;
            }
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
                payrollTaxesByPolicy,
                incomeColumnMatrix,
                generalLedgerRatesRowMatrix,
                generalLedgerPostingsMatrix,
                generalLedgerBucketCount,
                generalLedgerPostingsColumnMajor);
        }

        public void PackResultsInto(IReadOnlyList<Employee> employees, TaxResult[] results)
        {
            TaxResultPacker.Pack(
                employees,
                federalTaxes,
                stateTaxes,
                payrollTaxesByPolicy,
                matrixPolicyPlan.PayrollIndices,
                TaxData.GeneralLedgerPostingPolicies,
                generalLedgerPostingsColumnMajor,
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
