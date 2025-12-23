using MathNet.Numerics.LinearAlgebra.Double;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Builds and fills the employee feature matrix used by the matrix tax path.
    /// The basis is: [income, max(0, income - t1), max(0, income - t2), ...] for shared thresholds.
    /// </summary>
    public static class FeatureMatrixBuilder
    {
        /// <summary>
        /// Builds a new employee-by-feature matrix from incomes and shared thresholds by filling income and threshold columns.
        /// </summary>
        /// <param name="incomes">The per-employee income values.</param>
        /// <param name="sharedThresholds">The sorted shared thresholds defining the threshold feature columns.</param>
        /// <returns>A dense employee-by-feature matrix populated for the shared threshold basis.</returns>
        public static DenseMatrix BuildIncomeFeatureMatrix(double[] incomes, double[] sharedThresholds)
        {
            if (incomes == null) throw new ArgumentNullException(nameof(incomes));
            if (sharedThresholds == null) throw new ArgumentNullException(nameof(sharedThresholds));

            int employeeCount = incomes.Length;
            int featureCount = 1 + sharedThresholds.Length;

            DenseMatrix matrix = DenseMatrix.Create(employeeCount, featureCount, 0.0);

            FillIncomeColumn(incomes, matrix);
            FillThresholdColumns(incomes, sharedThresholds, matrix);

            return matrix;
        }

        /// <summary>
        /// Fills the income feature column (column 0) of an existing feature matrix from the incomes array.
        /// </summary>
        /// <param name="incomes">The per-employee income values.</param>
        /// <param name="matrix">The employee-by-feature matrix whose column 0 will be filled.</param>
        public static void FillIncomeColumn(double[] incomes, DenseMatrix matrix)
        {
            if (incomes == null) throw new ArgumentNullException(nameof(incomes));
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));

            Array.Copy(incomes, 0, matrix.Values, 0, incomes.Length);
        }

        /// <summary>
        /// Fills only the threshold feature columns (columns 1..N) of an existing feature matrix using shared thresholds.
        /// </summary>
        /// <param name="incomes">The per-employee income values.</param>
        /// <param name="sharedThresholds">The sorted shared thresholds defining threshold columns.</param>
        /// <param name="matrix">The employee-by-feature matrix whose threshold columns will be filled.</param>
        public static void FillThresholdColumns(double[] incomes, double[] sharedThresholds, DenseMatrix matrix)
        {
            if (incomes == null) throw new ArgumentNullException(nameof(incomes));
            if (sharedThresholds == null) throw new ArgumentNullException(nameof(sharedThresholds));
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));

            int employeeCount = incomes.Length;
            double[] values = matrix.Values;

            for (int thresholdIndex = 0; thresholdIndex < sharedThresholds.Length; thresholdIndex++)
            {
                double threshold = sharedThresholds[thresholdIndex];
                int columnOffset = (1 + thresholdIndex) * employeeCount;

                for (int employeeIndex = 0; employeeIndex < employeeCount; employeeIndex++)
                {
                    double above = incomes[employeeIndex] - threshold;
                    if (above > 0.0)
                        values[columnOffset + employeeIndex] = above;
                }
            }
        }
    }
}
