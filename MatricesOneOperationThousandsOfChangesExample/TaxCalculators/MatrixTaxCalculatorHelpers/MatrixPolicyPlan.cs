using MatricesOneOperationThousandsOfChangesExample.Data;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Precomputes the shared threshold feature basis and the dense policy transform matrix used by the matrix tax calculator.
    /// This is built once per run so the hot path can remain “featureMatrix * transformMatrix”.
    /// </summary>
    public sealed class MatrixPolicyPlan
    {
        /// <summary>
        /// Initializes a plan containing the policy layout, shared thresholds, transform matrix, and payroll indices.
        /// </summary>
        public MatrixPolicyPlan(
            PolicyLayout layout,
            double[] sharedThresholds,
            DenseMatrix policyTransformMatrix,
            PayrollPolicyIndices payrollIndices)
        {
            Layout = layout;
            SharedThresholds = sharedThresholds;
            PolicyTransformMatrix = policyTransformMatrix;
            PayrollIndices = payrollIndices;
        }

        /// <summary>
        /// Gets the deterministic mapping from federal/state/payroll policies to transform-matrix columns.
        /// </summary>
        public PolicyLayout Layout { get; }

        /// <summary>
        /// Gets the sorted unique thresholds defining the feature basis columns 1..N.
        /// </summary>
        public double[] SharedThresholds { get; }

        /// <summary>
        /// Gets the dense transform matrix whose columns map the shared feature basis to each policy’s tax value.
        /// </summary>
        public DenseMatrix PolicyTransformMatrix { get; }

        /// <summary>
        /// Gets indices into TaxData.PayrollPolicies used to pack payroll fields out of the results.
        /// </summary>
        public PayrollPolicyIndices PayrollIndices { get; }

        /// <summary>
        /// Builds the full plan (layout, thresholds, transform matrix, and payroll indices) from TaxData.
        /// </summary>
        public static MatrixPolicyPlan Build()
        {
            PolicyLayout layout = PolicyLayout.Build();
            double[] sharedThresholds = BuildSharedThresholds(layout);
            DenseMatrix policyTransformMatrix = BuildPolicyTransformMatrix(sharedThresholds, layout);
            PayrollPolicyIndices payrollIndices = PayrollPolicyIndices.Build();
            return new MatrixPolicyPlan(layout, sharedThresholds, policyTransformMatrix, payrollIndices);
        }

        /// <summary>
        /// Describes how federal, state, and payroll policies map to the policy-axis columns in the transform matrix.
        /// </summary>
        public readonly struct PolicyLayout
        {
            /// <summary>
            /// Initializes a layout using a sorted state list and a payroll-policy column offset.
            /// </summary>
            public PolicyLayout(TaxData.State[] statesInOrder, int payrollPolicyOffset)
            {
                StatesInOrder = statesInOrder;
                PayrollPolicyOffset = payrollPolicyOffset;
            }

            /// <summary>
            /// Gets the sorted states used to assign deterministic state policy column indices.
            /// </summary>
            public TaxData.State[] StatesInOrder { get; }

            /// <summary>
            /// Gets the first policy column index that corresponds to payroll policies.
            /// </summary>
            public int PayrollPolicyOffset { get; }

            /// <summary>
            /// Gets the total number of policy columns (federal + states + payroll policies).
            /// </summary>
            public int TotalPolicyCount => PayrollPolicyOffset + TaxData.PayrollPolicies.Count;

            /// <summary>
            /// Returns the policy column index for the given state based on the sorted state ordering.
            /// </summary>
            public int GetStatePolicyColumn(TaxData.State state)
            {
                int stateIndex = Array.BinarySearch(StatesInOrder, state);
                if (stateIndex < 0)
                    throw new ArgumentOutOfRangeException(nameof(state), state, "State not found in policy layout.");

                return 1 + stateIndex;
            }

            /// <summary>
            /// Builds a deterministic layout from TaxData.StateTables keys and the payroll policy count.
            /// </summary>
            public static PolicyLayout Build()
            {
                int stateCount = TaxData.StateTables.Count;

                var states = new TaxData.State[stateCount];
                int nextIndex = 0;
                foreach (var kvp in TaxData.StateTables)
                    states[nextIndex++] = kvp.Key;

                Array.Sort(states);

                int payrollPolicyOffset = 1 + states.Length;
                return new PolicyLayout(states, payrollPolicyOffset);
            }
        }

        /// <summary>
        /// Holds indices into TaxData.PayrollPolicies for each payroll output field packed into TaxResult.
        /// </summary>
        public readonly struct PayrollPolicyIndices
        {
            /// <summary>
            /// Initializes payroll policy indices for employee-side and employer-side payroll outputs.
            /// </summary>
            private PayrollPolicyIndices(
                int socialSecurityEmployeePolicyIndex,
                int medicareEmployeePolicyIndex,
                int additionalMedicareEmployeePolicyIndex,
                int socialSecurityEmployerPolicyIndex,
                int medicareEmployerPolicyIndex,
                int futaEmployerPolicyIndex,
                int sutaEmployerPolicyIndex)
            {
                SocialSecurityEmployeePolicyIndex = socialSecurityEmployeePolicyIndex;
                MedicareEmployeePolicyIndex = medicareEmployeePolicyIndex;
                AdditionalMedicareEmployeePolicyIndex = additionalMedicareEmployeePolicyIndex;
                SocialSecurityEmployerPolicyIndex = socialSecurityEmployerPolicyIndex;
                MedicareEmployerPolicyIndex = medicareEmployerPolicyIndex;
                FutaEmployerPolicyIndex = futaEmployerPolicyIndex;
                SutaEmployerPolicyIndex = sutaEmployerPolicyIndex;
            }

            /// <summary>
            /// Gets the payroll policy index for employee-side Social Security.
            /// </summary>
            public int SocialSecurityEmployeePolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employee-side Medicare.
            /// </summary>
            public int MedicareEmployeePolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employee-side Additional Medicare.
            /// </summary>
            public int AdditionalMedicareEmployeePolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employer-side Social Security.
            /// </summary>
            public int SocialSecurityEmployerPolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employer-side Medicare.
            /// </summary>
            public int MedicareEmployerPolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employer-side FUTA.
            /// </summary>
            public int FutaEmployerPolicyIndex { get; }

            /// <summary>
            /// Gets the payroll policy index for employer-side SUTA.
            /// </summary>
            public int SutaEmployerPolicyIndex { get; }

            /// <summary>
            /// Builds payroll policy indices by scanning TaxData.PayrollPolicies for required tax and side pairs.
            /// </summary>
            public static PayrollPolicyIndices Build()
            {
                return new PayrollPolicyIndices(
                    FindPayrollPolicyIndex(TaxData.PayrollTax.SocialSecurity, TaxData.PayrollSide.Employee),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.Medicare, TaxData.PayrollSide.Employee),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.AdditionalMedicare, TaxData.PayrollSide.Employee),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.SocialSecurity, TaxData.PayrollSide.Employer),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.Medicare, TaxData.PayrollSide.Employer),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.FederalUnemploymentTax, TaxData.PayrollSide.Employer),
                    FindPayrollPolicyIndex(TaxData.PayrollTax.StateUnemploymentTax, TaxData.PayrollSide.Employer));
            }

            /// <summary>
            /// Finds the index of a payroll policy matching the given tax and side in TaxData.PayrollPolicies.
            /// </summary>
            private static int FindPayrollPolicyIndex(TaxData.PayrollTax tax, TaxData.PayrollSide side)
            {
                var payrollPolicies = TaxData.PayrollPolicies;

                for (int policyIndex = 0; policyIndex < payrollPolicies.Count; policyIndex++)
                {
                    TaxData.PayrollPolicy payrollPolicy = payrollPolicies[policyIndex];
                    if (payrollPolicy.Tax == tax && payrollPolicy.Side == side)
                        return policyIndex;
                }

                throw new InvalidOperationException($"No payroll policy found for tax={tax}, side={side}.");
            }
        }

        /// <summary>
        /// Builds a sorted unique array of thresholds used by progressive tables and payroll rules that reference a boundary.
        /// </summary>
        private static double[] BuildSharedThresholds(PolicyLayout layout)
        {
            var thresholdBuffer = new List<double>(capacity: 64);

            AddNonZeroLowerBounds(thresholdBuffer, TaxData.FederalTable.LowerBounds);

            TaxData.State[] statesInOrder = layout.StatesInOrder;
            for (int stateIndex = 0; stateIndex < statesInOrder.Length; stateIndex++)
                AddNonZeroLowerBounds(thresholdBuffer, TaxData.StateTables[statesInOrder[stateIndex]].LowerBounds);

            var payrollPolicies = TaxData.PayrollPolicies;
            for (int policyIndex = 0; policyIndex < payrollPolicies.Count; policyIndex++)
            {
                TaxData.PayrollPolicy payrollPolicy = payrollPolicies[policyIndex];

                switch (payrollPolicy.Rule)
                {
                    case TaxData.PayrollRule.Flat:
                        break;

                    case TaxData.PayrollRule.AboveThreshold:
                    case TaxData.PayrollRule.Capped:
                        thresholdBuffer.Add(payrollPolicy.Parameter);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(payrollPolicy.Rule), payrollPolicy.Rule, "Unhandled payroll rule.");
                }
            }

            if (thresholdBuffer.Count == 0)
                return Array.Empty<double>();

            thresholdBuffer.Sort();

            int uniqueCount = 1;
            for (int readIndex = 1; readIndex < thresholdBuffer.Count; readIndex++)
            {
                if (thresholdBuffer[readIndex] != thresholdBuffer[readIndex - 1])
                    uniqueCount++;
            }

            var sharedThresholds = new double[uniqueCount];
            sharedThresholds[0] = thresholdBuffer[0];

            int writeIndex = 1;
            for (int readIndex = 1; readIndex < thresholdBuffer.Count; readIndex++)
            {
                double value = thresholdBuffer[readIndex];
                if (value != thresholdBuffer[readIndex - 1])
                    sharedThresholds[writeIndex++] = value;
            }

            return sharedThresholds;
        }

        /// <summary>
        /// Adds non-zero progressive lower bounds to the threshold buffer to avoid a redundant feature column at 0.
        /// </summary>
        private static void AddNonZeroLowerBounds(List<double> thresholdBuffer, double[] lowerBounds)
        {
            for (int boundaryIndex = 1; boundaryIndex < lowerBounds.Length; boundaryIndex++)
                thresholdBuffer.Add(lowerBounds[boundaryIndex]);
        }

        /// <summary>
        /// Builds the dense transform matrix whose columns map the shared feature basis to each policy’s tax value.
        /// </summary>
        private static DenseMatrix BuildPolicyTransformMatrix(double[] sharedThresholds, PolicyLayout layout)
        {
            int featureCount = 1 + sharedThresholds.Length;
            int policyCount = layout.TotalPolicyCount;

            DenseMatrix transformMatrix = DenseMatrix.Create(featureCount, policyCount, 0.0);
            double[] transformValues = transformMatrix.Values;

            FillProgressiveTransformColumn(transformValues, 0, featureCount, sharedThresholds, TaxData.FederalTable);

            TaxData.State[] statesInOrder = layout.StatesInOrder;
            for (int stateIndex = 0; stateIndex < statesInOrder.Length; stateIndex++)
                FillProgressiveTransformColumn(transformValues, 1 + stateIndex, featureCount, sharedThresholds, TaxData.StateTables[statesInOrder[stateIndex]]);

            int payrollPolicyOffset = layout.PayrollPolicyOffset;
            var payrollPolicies = TaxData.PayrollPolicies;
            for (int payrollPolicyIndex = 0; payrollPolicyIndex < payrollPolicies.Count; payrollPolicyIndex++)
                FillPayrollTransformColumn(transformValues, payrollPolicyOffset + payrollPolicyIndex, featureCount, sharedThresholds, payrollPolicies[payrollPolicyIndex]);

            return transformMatrix;
        }

        /// <summary>
        /// Fills one transform-matrix column for a progressive tax table by merging shared thresholds with bracket boundaries.
        /// </summary>
        private static void FillProgressiveTransformColumn(double[] transformValues, int policyColumn, int featureCount, double[] sharedThresholds, TaxData.ProgressiveTaxTable table)
        {
            int columnOffset = policyColumn * featureCount;

            double[] lowerBounds = table.LowerBounds;
            double[] rates = table.Rates;

            transformValues[columnOffset + 0] = rates[0];

            int tableBoundaryIndex = 1;
            for (int thresholdIndex = 0; thresholdIndex < sharedThresholds.Length; thresholdIndex++)
            {
                double sharedThreshold = sharedThresholds[thresholdIndex];

                while (tableBoundaryIndex < lowerBounds.Length && lowerBounds[tableBoundaryIndex] < sharedThreshold)
                    tableBoundaryIndex++;

                if (tableBoundaryIndex < lowerBounds.Length && lowerBounds[tableBoundaryIndex] == sharedThreshold)
                    transformValues[columnOffset + 1 + thresholdIndex] = rates[tableBoundaryIndex] - rates[tableBoundaryIndex - 1];
            }
        }

        /// <summary>
        /// Fills one transform-matrix column for a payroll policy using shared threshold features for thresholded rules.
        /// </summary>
        private static void FillPayrollTransformColumn(double[] transformValues, int policyColumn, int featureCount, double[] sharedThresholds, TaxData.PayrollPolicy payrollPolicy)
        {
            int columnOffset = policyColumn * featureCount;

            switch (payrollPolicy.Rule)
            {
                case TaxData.PayrollRule.Flat:
                    transformValues[columnOffset + 0] = payrollPolicy.Rate;
                    return;

                case TaxData.PayrollRule.AboveThreshold:
                    {
                        int thresholdIndex = FindSharedThresholdIndex(sharedThresholds, payrollPolicy.Parameter);
                        transformValues[columnOffset + 1 + thresholdIndex] = payrollPolicy.Rate;
                        return;
                    }

                case TaxData.PayrollRule.Capped:
                    {
                        int capIndex = FindSharedThresholdIndex(sharedThresholds, payrollPolicy.Parameter);
                        transformValues[columnOffset + 0] = payrollPolicy.Rate;
                        transformValues[columnOffset + 1 + capIndex] = -payrollPolicy.Rate;
                        return;
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(payrollPolicy.Rule), payrollPolicy.Rule, "Unhandled payroll rule.");
            }
        }

        /// <summary>
        /// Finds a required threshold in the shared threshold array and throws if the value is missing.
        /// </summary>
        private static int FindSharedThresholdIndex(double[] sharedThresholds, double threshold)
        {
            int index = Array.BinarySearch(sharedThresholds, threshold);
            if (index < 0)
                throw new InvalidOperationException($"Threshold {threshold} was not found in sharedThresholds.");

            return index;
        }
    }
}
