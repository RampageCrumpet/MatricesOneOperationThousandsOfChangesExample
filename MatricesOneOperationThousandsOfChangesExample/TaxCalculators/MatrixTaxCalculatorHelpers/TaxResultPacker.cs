using MatricesOneOperationThousandsOfChangesExample.Data;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    /// <summary>
    /// Packs per-employee tax outputs into TaxResult objects.
    /// </summary>
    public static class TaxResultPacker
    {
        /// <summary>
        /// Packs precomputed tax arrays into prewarmed TaxResult instances, including payroll fields and general ledger postings, without allocating per-employee objects.
        /// </summary>
        /// <param name="employees">
        /// The ordered list of employees corresponding to each row in the computed tax buffers.
        /// </param>
        /// <param name="federalTaxes">
        /// A per-employee array containing computed federal income tax values.
        /// </param>
        /// <param name="stateTaxes">
        /// A per-employee array containing computed state income tax values.
        /// </param>
        /// <param name="payrollTaxesByPolicy">
        /// A set of per-policy payroll tax buffers indexed by policy and employee.
        /// </param>
        /// <param name="payrollPolicyIndices">
        /// Precomputed indices that map logical payroll tax fields to their corresponding policy buffers.
        /// </param>
        /// <param name="generalLedgerPostingPolicies">
        /// The ordered set of general ledger posting policies defining the posting buckets to populate.
        /// </param>
        /// <param name="generalLedgerPostingsColumnMajor">
        /// A precomputed general ledger postings buffer, stored in column-major layout:
        /// offset = (bucketIndex * employeeCount) + employeeIndex.
        /// This method only copies from this buffer; it does not compute posting values.
        /// </param>
        /// <param name="results">
        /// Prewarmed TaxResult objects that are mutated in-place to receive all packed tax and general ledger values.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any required input or output buffer is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when any provided buffer is smaller than the number of employees being processed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required prewarmed result or general ledger buffer is missing or incorrectly sized.
        /// </exception>
        public static void Pack(
            IReadOnlyList<Employee> employees,
            double[] federalTaxes,
            double[] stateTaxes,
            double[][] payrollTaxesByPolicy,
            MatrixPolicyPlan.PayrollPolicyIndices payrollPolicyIndices,
            IReadOnlyList<TaxData.GeneralLedgerPostingPolicy> generalLedgerPostingPolicies,
            double[] generalLedgerPostingsColumnMajor,
            TaxResult[] results)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (federalTaxes == null) throw new ArgumentNullException(nameof(federalTaxes));
            if (stateTaxes == null) throw new ArgumentNullException(nameof(stateTaxes));
            if (payrollTaxesByPolicy == null) throw new ArgumentNullException(nameof(payrollTaxesByPolicy));
            if (generalLedgerPostingPolicies == null) throw new ArgumentNullException(nameof(generalLedgerPostingPolicies));
            if (generalLedgerPostingsColumnMajor == null) throw new ArgumentNullException(nameof(generalLedgerPostingsColumnMajor));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int employeeCount = employees.Count;

            if (federalTaxes.Length < employeeCount)
                throw new ArgumentException("Federal taxes array is smaller than employee count.", nameof(federalTaxes));

            if (stateTaxes.Length < employeeCount)
                throw new ArgumentException("State taxes array is smaller than employee count.", nameof(stateTaxes));

            if (results.Length < employeeCount)
                throw new ArgumentException("Results array is smaller than employee count.", nameof(results));

            // Cached payroll policy arrays to avoid repeated jagged lookups inside the hot loop.
            double[] socialSecurityEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SocialSecurityEmployeePolicyIndex];
            double[] medicareEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.MedicareEmployeePolicyIndex];
            double[] additionalMedicareEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.AdditionalMedicareEmployeePolicyIndex];

            double[] socialSecurityEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SocialSecurityEmployerPolicyIndex];
            double[] medicareEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.MedicareEmployerPolicyIndex];
            double[] federalUnemploymentEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.FutaEmployerPolicyIndex];
            double[] stateUnemploymentEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SutaEmployerPolicyIndex];

            if (generalLedgerPostingPolicies.Count > 0)
            {
                // generalLedgerPostingsColumnMajor is column-major [employeeCount x bucketCount]:
                // offset = bucketIndex * employeeCount + employeeIndex.
                int requiredLength = employeeCount * generalLedgerPostingPolicies.Count;
                if (generalLedgerPostingsColumnMajor.Length < requiredLength)
                    throw new ArgumentException("General ledger postings buffer is smaller than employeeCount * bucketCount.", nameof(generalLedgerPostingsColumnMajor));
            }

            for (int employeeIndex = 0; employeeIndex < employeeCount; employeeIndex++)
            {
                Employee employee = employees[employeeIndex];

                TaxResult result = results[employeeIndex]
                    ?? throw new InvalidOperationException("Prewarmed TaxResult entry is null.");

                result.Employee = employee;
                result.FederalTax = federalTaxes[employeeIndex];
                result.StateTax = stateTaxes[employeeIndex];
                result.SocialSecurityEmployee = socialSecurityEmployeeTaxes[employeeIndex];
                result.MedicareEmployee = medicareEmployeeTaxes[employeeIndex];
                result.AdditionalMedicareEmployee = additionalMedicareEmployeeTaxes[employeeIndex];
                result.SocialSecurityEmployer = socialSecurityEmployerTaxes[employeeIndex];
                result.MedicareEmployer = medicareEmployerTaxes[employeeIndex];
                result.FederalUnemploymentTaxes = federalUnemploymentEmployerTaxes[employeeIndex];
                result.StateUnemploymentTaxes = stateUnemploymentEmployerTaxes[employeeIndex];

                // General ledger postings: one entry per policy bucket, prewarmed (no allocation here).
                if (generalLedgerPostingPolicies.Count > 0)
                {
                    double[] postings = result.GeneralLedgerPostings
                        ?? throw new InvalidOperationException("TaxResult.GeneralLedgerPostings was not prewarmed.");

                    if (postings.Length != generalLedgerPostingPolicies.Count)
                        throw new InvalidOperationException("TaxResult.GeneralLedgerPostings length does not match policy bucket count.");

                    // Copy precomputed postings values into the per-employee bucket array.
                    // generalLedgerPostingsColumnMajor: offset = bucketIndex * employeeCount + employeeIndex
                    for (int bucketIndex = 0; bucketIndex < generalLedgerPostingPolicies.Count; bucketIndex++)
                        postings[bucketIndex] = generalLedgerPostingsColumnMajor[(bucketIndex * employeeCount) + employeeIndex];
                }
            }
        }
    }
}
