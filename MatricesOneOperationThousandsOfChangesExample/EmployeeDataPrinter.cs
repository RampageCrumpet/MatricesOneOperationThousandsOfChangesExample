using MatricesOneOperationThousandsOfChangesExample.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample
{
    public static class EmployeeDataPrinter
    {
        /// <summary>
        /// Print out a sample of employee tax data.
        /// </summary>
        /// <param name="taxResults"> Our taxdata collection that containing information about the employee and the tax they've paid.</param>
        /// <param name="sampleSize"> The number of employees to print.</param>
        public static void PrintEmployeeData(IEnumerable<TaxResult> taxResults, int sampleSize)
        {
            Console.WriteLine();
            Console.WriteLine("Sample Tax Results");
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine($"{"ID",6} {"State",6} {"Income",12} {"Federal",12} {"State",12} {"Total",12}");
            Console.WriteLine("--------------------------------------------------------------------------");

            foreach (var result in taxResults.Take(sampleSize))
            {
                var employee = result.Employee;

                Console.WriteLine(
                    $"{employee.Id,6} " +
                    $"{employee.State,6} " +
                    $"{employee.Income,12:C0} " +
                    $"{result.FederalTax,12:C0} " +
                    $"{result.StateTax,12:C0} " +
                    $"{result.TotalIncomeTax,12:C0}"
                );
            }
        }
    }
}
