using MathNet.Numerics.LinearAlgebra.Double;
using MatricesOneOperationThousandsOfChangesExample.Data;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Stores per-employee primitive inputs and index groupings that the matrix tax path reuses without re-reading Employee objects.
    /// This exists to make the hot path operate on arrays and grouped indices rather than object graphs.
    /// </summary>
    public sealed class EmployeeInputs
    {
        /// <summary>
        /// Bundles the fully-built EmployeeInputs with a preallocated feature matrix whose income column is already populated.
        /// This is used to preserve the single-pass fast path for the matrix calculator.
        /// </summary>
        public readonly struct EmployeePreprocessResult
        {
            /// <summary>
            /// Initializes a preprocessing result that contains both inputs and the feature matrix populated with income in column 0.
            /// </summary>
            /// <param name="inputs">The fully-built employee inputs containing incomes and state groupings.</param>
            /// <param name="employeeFeatureMatrix">The employee feature matrix with column 0 already filled with income.</param>
            public EmployeePreprocessResult(EmployeeInputs inputs, DenseMatrix employeeFeatureMatrix)
            {
                Inputs = inputs;
                EmployeeFeatureMatrix = employeeFeatureMatrix;
            }

            /// <summary>
            /// Gets the fully-built employee inputs containing incomes and state index groupings.
            /// </summary>
            public EmployeeInputs Inputs { get; }

            /// <summary>
            /// Gets the feature matrix whose column 0 is populated with income for each employee.
            /// </summary>
            public DenseMatrix EmployeeFeatureMatrix { get; }
        }

        /// <summary>
        /// Initializes an input bundle containing per-employee incomes and per-state employee index groupings.
        /// </summary>
        /// <param name="incomes">The per-employee incomes aligned to the input employee ordering.</param>
        /// <param name="indicesByState">The per-state employee index arrays aligned to the input employee ordering.</param>
        private EmployeeInputs(double[] incomes, Dictionary<TaxData.State, int[]> indicesByState)
        {
            Incomes = incomes;
            IndicesByState = indicesByState;
        }

        /// <summary>
        /// Gets the per-employee incomes aligned to the input employee ordering.
        /// </summary>
        public double[] Incomes { get; }

        /// <summary>
        /// Gets the per-state employee index arrays used to scatter the correct state tax column into the per-employee state output.
        /// </summary>
        public Dictionary<TaxData.State, int[]> IndicesByState { get; }

        /// <summary>
        /// Builds EmployeeInputs by extracting incomes and grouping employee indices by state.
        /// </summary>
        /// <param name="employees">The employees whose inputs are extracted and grouped.</param>
        /// <returns>A fully-built EmployeeInputs instance.</returns>
        public static EmployeeInputs Build(IReadOnlyList<Employee> employees)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));

            BuildCore(
                employees,
                featureCount: 0,
                buildFeatureMatrix: false,
                out double[] incomes,
                out Dictionary<TaxData.State, int[]> indicesByState,
                out DenseMatrix? employeeFeatureMatrix);

            return new EmployeeInputs(incomes, indicesByState);
        }

        /// <summary>
        /// Builds EmployeeInputs and allocates a feature matrix with income in column 0 in a single pass over employees.
        /// </summary>
        /// <param name="employees">The employees whose inputs are extracted and grouped.</param>
        /// <param name="featureCount">The total number of feature columns in the employee feature matrix.</param>
        /// <returns>A preprocessing result containing fully-built inputs and a feature matrix with income already populated.</returns>
        public static EmployeePreprocessResult BuildForMatrix(IReadOnlyList<Employee> employees, int featureCount)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (featureCount <= 0) throw new ArgumentOutOfRangeException(nameof(featureCount));

            BuildCore(
                employees,
                featureCount,
                buildFeatureMatrix: true,
                out double[] incomes,
                out Dictionary<TaxData.State, int[]> indicesByState,
                out DenseMatrix? employeeFeatureMatrix);

            var inputs = new EmployeeInputs(incomes, indicesByState);

            if (employeeFeatureMatrix == null)
                throw new InvalidOperationException("BuildForMatrix must produce a feature matrix.");

            return new EmployeePreprocessResult(inputs, employeeFeatureMatrix);
        }

        /// <summary>
        /// Extracts incomes, groups indices by state, and optionally allocates and fills the feature matrix income column in one pass.
        /// </summary>
        /// <param name="employees">The employees to scan once to extract primitives and build groupings.</param>
        /// <param name="featureCount">The feature column count for the matrix when building a matrix; ignored otherwise.</param>
        /// <param name="buildFeatureMatrix">Whether to allocate and fill a feature matrix with income in column 0.</param>
        /// <param name="incomes">Receives the per-employee income array aligned to input ordering.</param>
        /// <param name="indicesByState">Receives the per-state employee index arrays aligned to input ordering.</param>
        /// <param name="employeeFeatureMatrix">Receives the allocated feature matrix when requested; otherwise null.</param>
        private static void BuildCore(
            IReadOnlyList<Employee> employees,
            int featureCount,
            bool buildFeatureMatrix,
            out double[] incomes,
            out Dictionary<TaxData.State, int[]> indicesByState,
            out DenseMatrix? employeeFeatureMatrix)
        {
            int employeeCount = employees.Count;

            incomes = new double[employeeCount];

            employeeFeatureMatrix = buildFeatureMatrix
                ? DenseMatrix.Create(employeeCount, featureCount, 0.0)
                : null;

            double[]? featureValues = employeeFeatureMatrix?.Values;

            var indicesByStateLists = new Dictionary<TaxData.State, List<int>>();

            for (int employeeIndex = 0; employeeIndex < employeeCount; employeeIndex++)
            {
                Employee employee = employees[employeeIndex];

                double income = employee.Income;
                incomes[employeeIndex] = income;

                if (featureValues != null)
                    featureValues[employeeIndex] = income;

                TaxData.State state = employee.State;

                if (!indicesByStateLists.TryGetValue(state, out List<int> list))
                {
                    list = new List<int>();
                    indicesByStateLists[state] = list;
                }

                list.Add(employeeIndex);
            }

            indicesByState = new Dictionary<TaxData.State, int[]>(indicesByStateLists.Count);
            foreach (var kvp in indicesByStateLists)
                indicesByState[kvp.Key] = kvp.Value.ToArray();
        }
    }
}
