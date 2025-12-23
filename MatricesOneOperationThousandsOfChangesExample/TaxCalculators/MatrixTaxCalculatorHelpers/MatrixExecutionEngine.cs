using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra;
using MatricesOneOperationThousandsOfChangesExample.Data;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Executes the blocked matrix multiply and extracts federal, state, and payroll results into per-employee output arrays.
    /// </summary>
    public static class MatrixExecutionEngine
    {
        /// <summary>
        /// Limits the number of policy columns multiplied per block to cap working-set size.\
        /// If you choose to play with this value it should be set to some power of two for best performance.
        /// </summary>
        private const int MaxPolicyBlockWidth = 128;

        /// <summary>
        /// Applies all policies by multiplying the employee feature matrix by the plan’s transform matrix in blocks and extracting outputs.
        /// </summary>
        /// <param name="employeeFeatureMatrix">Employee-by-feature matrix where rows are employees and columns are shared features.</param>
        /// <param name="plan">Precomputed plan containing policy layout and the full transform matrix.</param>
        /// <param name="inputs">Precomputed employee inputs including per-state employee index groupings.</param>
        /// <param name="federalTaxes">Output buffer receiving federal tax per employee.</param>
        /// <param name="stateTaxes">Output buffer receiving state tax per employee (only for that employee’s state).</param>
        /// <param name="payrollTaxesByPolicy">Output buffers receiving payroll taxes per payroll policy, each sized to employee count.</param>
        public static void Execute(
            DenseMatrix employeeFeatureMatrix,
            MatrixPolicyPlan plan,
            EmployeeInputs inputs,
            double[] federalTaxes,
            double[] stateTaxes,
            double[][] payrollTaxesByPolicy)
        {
            int employeeCount = employeeFeatureMatrix.RowCount;
            int featureCount = employeeFeatureMatrix.ColumnCount;
            int policyCount = plan.PolicyTransformMatrix.ColumnCount;

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
                    inputs.IndicesByState,
                    stateTaxes);

                ExtractPayrollIfPresent(
                    blockStart,
                    blockCount,
                    employeeCount,
                    taxesBlockValues,
                    plan.Layout,
                    payrollTaxesByPolicy);
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
