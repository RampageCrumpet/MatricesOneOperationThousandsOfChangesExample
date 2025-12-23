using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        required public Employee Employee { get; init; }



        // Employee-side payroll
        public double SocialSecurityEmployee { get; init; }
        public double MedicareEmployee { get; init; }
        public double AdditionalMedicareEmployee { get; init; }

        // Employer-side payroll
        public double SocialSecurityEmployer { get; init; }
        public double MedicareEmployer { get; init; }
        public double FederalUnemploymentTaxes { get; init; }
        public double StateUnemploymentTaxes { get; init; }

        public double EmployeePayrollTotal =>
        SocialSecurityEmployee + MedicareEmployee + AdditionalMedicareEmployee;

        public double EmployerPayrollTotal =>
            SocialSecurityEmployer + MedicareEmployer + FederalUnemploymentTaxes + StateUnemploymentTaxes;

        /// <summary>
        /// The total federal withholding amount to be paid.
        /// </summary>
        public double FederalTax { get; init; }

        /// <summary>
        /// The total state withholding amount to be paid.
        /// </summary>
        public double StateTax { get; init; }

        /// <summary>
        /// A quick calculation for the toal amount of income tax paid.
        /// </summary>
        public double TotalIncomeTax => FederalTax + StateTax;
    }
}
