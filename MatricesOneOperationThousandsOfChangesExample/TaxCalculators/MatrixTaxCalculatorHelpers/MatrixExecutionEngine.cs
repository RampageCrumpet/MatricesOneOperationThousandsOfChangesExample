using MathNet.Numerics.LinearAlgebra.Double;
using MatricesOneOperationThousandsOfChangesExample.Data;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Executes the blocked matrix multiply and extracts federal, state, and payroll results into per-employee output arrays.
    /// </summary>
    public static class MatrixExecutionEngine
    {
        /// <summary>
        /// Limits the number of policy columns multiplied per block to cap working-set size.
        /// If you choose to play with this value it should be set to some power of two for best performance. 
        /// We're not doing anything magic here, just chopping the matrix up into smaller pieces since we're working with such ludicrously large matrices.
        /// </summary>
        public const int MaxPolicyBlockWidth = 64;

        /// <summary>
        /// Applies all policies by multiplying the employee feature matrix by the plan’s transform matrix in blocks and extracting outputs.
        /// Also computes general ledger postings using matrix multiplication (outer product) into a column-major output buffer.
        /// </summary>
        /// <param name="employeeFeatureMatrix">
        /// Employee-by-feature matrix where rows are employees and columns are shared features.
        /// </param>
        /// <param name="plan">
        /// Precomputed plan containing policy layout and the full transform matrix.
        /// </param>
        /// <param name="indicesByState">
        /// Precomputed employee index groupings by state used to extract state outputs without branching per employee.
        /// </param>
        /// <param name="federalTaxes">
        /// Output buffer receiving federal tax per employee.
        /// </param>
        /// <param name="stateTaxes">
        /// Output buffer receiving state tax per employee (only for that employee’s state).
        /// </param>
        /// <param name="payrollTaxesByPolicy">
        /// Output buffers receiving payroll taxes per payroll policy, each sized to employee count.
        /// </param>
        /// <param name="incomeColumnMatrix">
        /// A [employeeCount x 1] matrix containing employee incomes, used for the general ledger outer product.
        /// </param>
        /// <param name="generalLedgerRateBlocks">
        /// Precomputed [1 x MaxPolicyBlockWidth] rate blocks (zero padded in unused columns) used to compute general ledger postings.
        /// </param>
        /// <param name="generalLedgerBucketCount">
        /// Total number of general ledger posting buckets.
        /// </param>
        /// <param name="generalLedgerPostingsColumnMajor">
        /// Output buffer receiving general ledger postings in column-major layout:
        /// offset = (bucketIndex * employeeCount) + employeeIndex.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any required input or output buffer is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when any provided buffer is incorrectly sized.
        /// </exception>
        public static void Execute(
            DenseMatrix employeeFeatureMatrix,
            MatrixPolicyPlan plan,
            Dictionary<TaxData.State, int[]> indicesByState,
            double[] federalTaxes,
            double[] stateTaxes,
            double[][] payrollTaxesByPolicy,
            DenseMatrix incomeColumnMatrix,
            DenseMatrix generalLedgerRatesRowMatrix,
            DenseMatrix generalLedgerPostingsMatrix,
            int generalLedgerBucketCount,
            double[] generalLedgerPostingsColumnMajor)
        {
            if (employeeFeatureMatrix == null) throw new ArgumentNullException(nameof(employeeFeatureMatrix));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (indicesByState == null) throw new ArgumentNullException(nameof(indicesByState));
            if (federalTaxes == null) throw new ArgumentNullException(nameof(federalTaxes));
            if (stateTaxes == null) throw new ArgumentNullException(nameof(stateTaxes));
            if (payrollTaxesByPolicy == null) throw new ArgumentNullException(nameof(payrollTaxesByPolicy));
            if (incomeColumnMatrix == null) throw new ArgumentNullException(nameof(incomeColumnMatrix));
            if (generalLedgerPostingsColumnMajor == null) throw new ArgumentNullException(nameof(generalLedgerPostingsColumnMajor));

            int employeeCount = employeeFeatureMatrix.RowCount;
            int featureCount = employeeFeatureMatrix.ColumnCount;
            int policyCount = plan.PolicyTransformMatrix.ColumnCount;

            if (federalTaxes.Length < employeeCount)
                throw new ArgumentException("Federal taxes output is smaller than employee count.", nameof(federalTaxes));

            if (stateTaxes.Length < employeeCount)
                throw new ArgumentException("State taxes output is smaller than employee count.", nameof(stateTaxes));

            // Note: payrollTaxesByPolicy buffers are assumed to be sized correctly (existing behavior).

            int policyBlockWidth = Math.Min(MaxPolicyBlockWidth, policyCount);

            DenseMatrix policyTransformBlock = DenseMatrix.Create(featureCount, policyBlockWidth, 0.0);
            DenseMatrix taxesByEmployeeForPolicyBlock = DenseMatrix.Create(employeeCount, policyBlockWidth, 0.0);

            double[] taxesBlockValues = taxesByEmployeeForPolicyBlock.Values;

            for (int blockStart = 0; blockStart < policyCount; blockStart += policyBlockWidth)
            {
                int blockCount = Math.Min(policyBlockWidth, policyCount - blockStart);

                CopyPolicyTransformBlock(plan.PolicyTransformMatrix, policyTransformBlock, featureCount, blockStart, blockCount);

                employeeFeatureMatrix.Multiply(policyTransformBlock, taxesByEmployeeForPolicyBlock);

                ExtractFederalIfPresent(blockStart, blockCount, employeeCount, taxesBlockValues, federalTaxes);

                ExtractStatesIfPresent(
                    blockStart,
                    blockCount,
                    employeeCount,
                    taxesBlockValues,
                    plan.Layout,
                    indicesByState,
                    stateTaxes);

                ExtractPayrollIfPresent(
                    blockStart,
                    blockCount,
                    employeeCount,
                    taxesBlockValues,
                    plan.Layout,
                    payrollTaxesByPolicy);
            }

            if (generalLedgerBucketCount > 0)
            {
                if (generalLedgerRatesRowMatrix == null)
                    throw new ArgumentNullException(nameof(generalLedgerRatesRowMatrix));
                if (generalLedgerPostingsMatrix == null)
                    throw new ArgumentNullException(nameof(generalLedgerPostingsMatrix));

                if (incomeColumnMatrix.RowCount != employeeCount || incomeColumnMatrix.ColumnCount != 1)
                    throw new ArgumentException(
                        "Income column matrix must be sized [employeeCount x 1].",
                        nameof(incomeColumnMatrix));

                if (generalLedgerRatesRowMatrix.RowCount != 1 || generalLedgerRatesRowMatrix.ColumnCount != generalLedgerBucketCount)
                    throw new ArgumentException(
                        "Rates row matrix must be sized [1 x bucketCount].",
                        nameof(generalLedgerRatesRowMatrix));

                if (generalLedgerPostingsMatrix.RowCount != employeeCount || generalLedgerPostingsMatrix.ColumnCount != generalLedgerBucketCount)
                    throw new ArgumentException(
                        "Postings matrix must be sized [employeeCount x bucketCount].",
                        nameof(generalLedgerPostingsMatrix));

                int requiredPostingLength = employeeCount * generalLedgerBucketCount;
                if (generalLedgerPostingsColumnMajor.Length < requiredPostingLength)
                    throw new ArgumentException(
                        "General ledger postings buffer must be sized employeeCount * bucketCount.",
                        nameof(generalLedgerPostingsColumnMajor));

                // Ensure overwrite semantics even if MathNet ever uses += internally in some provider path.
                // (Also avoids stale results if someone later changes dimensions between runs.)
                generalLedgerPostingsMatrix.Clear();

                // [employeeCount x 1] * [1 x bucketCount] -> [employeeCount x bucketCount]
                // Writes directly into generalLedgerPostingsColumnMajor because generalLedgerPostingsMatrix is a view over it.
                incomeColumnMatrix.Multiply(generalLedgerRatesRowMatrix, generalLedgerPostingsMatrix);
            }
        }

        /// <summary>
        /// Copies a contiguous block of policy columns from the full transform matrix into the reusable block transform matrix.
        /// </summary>
        private static void CopyPolicyTransformBlock(
            DenseMatrix fullTransformMatrix,
            DenseMatrix blockTransformMatrix,
            int featureCount,
            int blockStart,
            int blockCount)
        {
            double[] fullValues = fullTransformMatrix.Values;
            double[] blockValues = blockTransformMatrix.Values;

            Array.Clear(blockValues, 0, blockValues.Length);

            for (int localColumn = 0; localColumn < blockCount; localColumn++)
            {
                int sourceOffset = (blockStart + localColumn) * featureCount;
                int destinationOffset = localColumn * featureCount;
                Array.Copy(fullValues, sourceOffset, blockValues, destinationOffset, featureCount);
            }
        }

        /// <summary>
        /// Extracts federal tax results from the current policy block into the federal output buffer if the federal column is present.
        /// </summary>
        private static void ExtractFederalIfPresent(
            int blockStart,
            int blockCount,
            int employeeCount,
            double[] taxesBlockValues,
            double[] federalTaxes)
        {
            const int federalGlobalColumn = 0;

            if (federalGlobalColumn < blockStart || federalGlobalColumn >= blockStart + blockCount)
                return;

            int localColumn = federalGlobalColumn - blockStart;
            int columnOffset = localColumn * employeeCount;

            for (int i = 0; i < employeeCount; i++)
                federalTaxes[i] = taxesBlockValues[columnOffset + i];
        }

        /// <summary>
        /// Extracts state tax results from the current policy block into the state output buffer for employees in each state group.
        /// </summary>
        private static void ExtractStatesIfPresent(
            int blockStart,
            int blockCount,
            int employeeCount,
            double[] taxesBlockValues,
            MatrixPolicyPlan.PolicyLayout layout,
            Dictionary<TaxData.State, int[]> indicesByState,
            double[] stateTaxes)
        {
            foreach (var kvp in indicesByState)
            {
                TaxData.State state = kvp.Key;
                int[] indices = kvp.Value;

                int stateGlobalColumn = layout.GetStatePolicyColumn(state);

                if (stateGlobalColumn < blockStart || stateGlobalColumn >= blockStart + blockCount)
                    continue;

                int localColumn = stateGlobalColumn - blockStart;
                int columnOffset = localColumn * employeeCount;

                for (int i = 0; i < indices.Length; i++)
                {
                    int employeeIndex = indices[i];
                    stateTaxes[employeeIndex] = taxesBlockValues[columnOffset + employeeIndex];
                }
            }
        }

        /// <summary>
        /// Extracts payroll tax results from the current policy block into per-policy payroll output buffers when those columns are present.
        /// </summary>
        private static void ExtractPayrollIfPresent(
            int blockStart,
            int blockCount,
            int employeeCount,
            double[] taxesBlockValues,
            MatrixPolicyPlan.PolicyLayout layout,
            double[][] payrollTaxesByPolicy)
        {
            int payrollPolicyCount = TaxData.PayrollPolicies.Count;
            int payrollPolicyOffset = layout.PayrollPolicyOffset;

            for (int payrollPolicyIndex = 0; payrollPolicyIndex < payrollPolicyCount; payrollPolicyIndex++)
            {
                int payrollGlobalColumn = payrollPolicyOffset + payrollPolicyIndex;

                if (payrollGlobalColumn < blockStart || payrollGlobalColumn >= blockStart + blockCount)
                    continue;

                int localColumn = payrollGlobalColumn - blockStart;
                int columnOffset = localColumn * employeeCount;

                Array.Copy(taxesBlockValues, columnOffset, payrollTaxesByPolicy[payrollPolicyIndex], 0, employeeCount);
            }
        }
    }
}
