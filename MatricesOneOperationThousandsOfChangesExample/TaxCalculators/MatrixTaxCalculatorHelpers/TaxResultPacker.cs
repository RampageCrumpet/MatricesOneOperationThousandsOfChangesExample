using MatricesOneOperationThousandsOfChangesExample.Data;

namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
{
    namespace MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers
    {
        /// <summary>
        /// Packs per-employee tax outputs into TaxResult objects.
        /// </summary>
        public static class TaxResultPacker
        {
            /// <summary>
            /// Packs tax arrays into TaxResult instances in employee order.
            /// </summary>
            public static IEnumerable<TaxResult> Pack(IReadOnlyList<Employee> employees, double[] federalTaxes, double[] stateTaxes, double[][] payrollTaxesByPolicy, MatrixPolicyPlan.PayrollPolicyIndices payrollPolicyIndices)
            {
                if (employees == null) throw new ArgumentNullException(nameof(employees));
                if (federalTaxes == null) throw new ArgumentNullException(nameof(federalTaxes));
                if (stateTaxes == null) throw new ArgumentNullException(nameof(stateTaxes));
                if (payrollTaxesByPolicy == null) throw new ArgumentNullException(nameof(payrollTaxesByPolicy));

                // results: Output list sized up-front to avoid resizing.
                var results = new List<TaxResult>(employees.Count);

                // Cached payroll policy arrays to avoid repeated jagged lookups inside the hot loop.
                double[] socialSecurityEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SocialSecurityEmployeePolicyIndex];
                double[] medicareEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.MedicareEmployeePolicyIndex];
                double[] additionalMedicareEmployeeTaxes = payrollTaxesByPolicy[payrollPolicyIndices.AdditionalMedicareEmployeePolicyIndex];

                double[] socialSecurityEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SocialSecurityEmployerPolicyIndex];
                double[] medicareEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.MedicareEmployerPolicyIndex];
                double[] federalUnemploymentEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.FutaEmployerPolicyIndex];
                double[] stateUnemploymentEmployerTaxes = payrollTaxesByPolicy[payrollPolicyIndices.SutaEmployerPolicyIndex];

                for (int employeeIndex = 0; employeeIndex < employees.Count; employeeIndex++)
                {
                    // employee: Current employee to associate with this result row.
                    Employee employee = employees[employeeIndex];

                    results.Add(new TaxResult
                    {
                        Employee = employee,
                        FederalTax = federalTaxes[employeeIndex],
                        StateTax = stateTaxes[employeeIndex],

                        SocialSecurityEmployee = socialSecurityEmployeeTaxes[employeeIndex],
                        MedicareEmployee = medicareEmployeeTaxes[employeeIndex],
                        AdditionalMedicareEmployee = additionalMedicareEmployeeTaxes[employeeIndex],

                        SocialSecurityEmployer = socialSecurityEmployerTaxes[employeeIndex],
                        MedicareEmployer = medicareEmployerTaxes[employeeIndex],
                        FederalUnemploymentTaxes = federalUnemploymentEmployerTaxes[employeeIndex],
                        StateUnemploymentTaxes = stateUnemploymentEmployerTaxes[employeeIndex],
                    });
                }

                return results;
            }
        }
    }
}
