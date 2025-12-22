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
        /// <param name="employees"> Our collection of employees to print.</param>
        /// <param name="sampleSize"> The number of employees to print.</param>
        public static void PrintEmployeeData(IEnumerable<Employee> employees, IEnumerable<double> federalWithholding, IEnumerable<double> stateWithholding, int sampleSize)
        {
            Console.WriteLine();
            Console.WriteLine("Sample Tax Results");
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine($"{"ID",6} {"State",6} {"Income",12} {"Federal",12} {"State",12} {"Total",12}");
            Console.WriteLine("--------------------------------------------------------------------------");

            foreach (var (employee, federal, state) in
             employees
                 .Zip(federalWithholding, (e, f) => (e, f))
                 .Zip(stateWithholding, (ef, s) => (ef.e, ef.f, s))
                 .Take(sampleSize))
            {
                double total = federal + state;

                Console.WriteLine(
                    $"{employee.Id,6} " +
                    $"{employee.State,6} " +
                    $"{employee.Income,12:C0} " +
                    $"{federal,12:C0} " +
                    $"{state,12:C0} " +
                    $"{total,12:C0}"
                );
            }
        }
    }
}
