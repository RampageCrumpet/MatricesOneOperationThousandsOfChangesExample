
namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// This class holds the result of a tax calculation.
    /// </summary>
    public class TaxResult
    {
        /// <summary>
        /// The employee associated with this tax result.
        /// </summary>
        required public Employee Employee { get; set; }

        /// <summary>
        /// Gets or sets the per-employee general ledger postings aligned to TaxData.GeneralLedgerPostingPolicies order.
        /// </summary>
        public double[]? GeneralLedgerPostings { get; set; }

        // Employee-side payroll
        public double SocialSecurityEmployee { get; set; }
        public double MedicareEmployee { get; set; }
        public double AdditionalMedicareEmployee { get; set; }

        // Employer-side payroll
        public double SocialSecurityEmployer { get; set; }
        public double MedicareEmployer { get; set; }
        public double FederalUnemploymentTaxes { get; set; }
        public double StateUnemploymentTaxes { get; set; }

        public double EmployeePayrollTotal =>
        SocialSecurityEmployee + MedicareEmployee + AdditionalMedicareEmployee;

        public double EmployerPayrollTotal =>
            SocialSecurityEmployer + MedicareEmployer + FederalUnemploymentTaxes + StateUnemploymentTaxes;

        /// <summary>
        /// The total federal withholding amount to be paid.
        /// </summary>
        public double FederalTax { get; set; }

        /// <summary>
        /// The total state withholding amount to be paid.
        /// </summary>
        public double StateTax { get; set; }

        /// <summary>
        /// A quick calculation for the toal amount of income tax paid.
        /// </summary>
        public double TotalIncomeTax => FederalTax + StateTax;
    }
}
