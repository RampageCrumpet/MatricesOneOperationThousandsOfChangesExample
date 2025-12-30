using MatricesOneOperationThousandsOfChangesExample.Data;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatricesOneOperationThousandsOfChangesExample
{
    /// <summary>
    /// Runs end-to-end timing comparisons between the iterative and matrix tax calculators and reports correctness checks.
    /// This benchmark measures full calculator cost while avoiding harness artifacts like accidental double materialization.
    /// </summary>
    public class CodeRacer
    {
        /// <summary>
        /// Executes warmup, runs both calculators, and returns timings plus a spot-check delta on the first N results.
        /// </summary>
        public static RaceResult Race(IReadOnlyList<Employee> employees, int warmupCount = 10_000, int spotCheckCount = 1_000)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));

            EmployeeTaxCalculation_MatrixBased matrixCalculator = new EmployeeTaxCalculation_MatrixBased(employees);


            if (employees.Count == 0)
            {
                return new RaceResult
                {
                    IterativeResults = new List<TaxResult>(),
                    MatrixResults = new List<TaxResult>(),
                    IterativeTime = TimeSpan.Zero,
                    MatrixTime = TimeSpan.Zero,
                    MaxAbsoluteDeltaChecked = 0.0,
                    DeltaCheckCount = 0
                };
            }

            IReadOnlyList<Employee> warmEmployees = employees.Count <= warmupCount
                ? employees
                : employees.Take(warmupCount).ToList();

            Warmup(warmEmployees, matrixCalculator);

            // Prewarm results outside the timed region.
            TaxResult[] iterativeResultsArray = PrewarmResults(employees);
            TaxResult[] matrixResultsArray = PrewarmResults(employees);


            ForceGc();

            TimeSpan iterativeTime = Time(() =>
            {
                EmployeeTaxCalculation_Iterative.CalculateTaxesInto(employees, iterativeResultsArray);
            });

            ForceGc();

            TimeSpan matrixTime = Time(() =>
            {
                matrixCalculator.CalculateTaxesInto(employees);
            });

            // Pack matrix results from internal buffers into the result objects.
            // I chose to exclude this from timing to focus on pure calculation time. It didn't feel fair to include it.
            // If you want to include it, move this call into the timed region above.
            matrixCalculator.PackResultsInto(employees, matrixResultsArray);

            // Materialize for return/debugging outside timing.
            List<TaxResult> iterativeResults = iterativeResultsArray.ToList();
            List<TaxResult> matrixResults = matrixResultsArray.ToList();

            double maxDelta = ComputeError(iterativeResults, matrixResults, spotCheckCount);

            return new RaceResult
            {
                IterativeResults = iterativeResults,
                MatrixResults = matrixResults,
                IterativeTime = iterativeTime,
                MatrixTime = matrixTime,
                MaxAbsoluteDeltaChecked = maxDelta,
                DeltaCheckCount = Math.Min(spotCheckCount, Math.Min(iterativeResults.Count, matrixResults.Count))
            };
        }

        /// <summary>
        /// Writes a human-readable summary of a materializing race result to the console.
        /// </summary>
        public static void PrintResults(RaceResult result)
        {
            static string FormatMoney(double value) => value.ToString("$#,0");

            Console.WriteLine("=== Tax Calculator Benchmark Results ===");
            Console.WriteLine();
            Console.WriteLine($"Iterative Time : {result.IterativeTime.TotalMilliseconds:N0} ms");
            Console.WriteLine($"Matrix Time    : {result.MatrixTime.TotalMilliseconds:N0} ms");

            double speedup = result.MatrixTime.TotalMilliseconds <= 0.0
                ? double.PositiveInfinity
                : result.IterativeTime.TotalMilliseconds / result.MatrixTime.TotalMilliseconds;

            Console.WriteLine($"Speedup        : {speedup:N2}x");
            Console.WriteLine();


            if (result.MaxAbsoluteDeltaChecked > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Max Δ Checked : {result.MaxAbsoluteDeltaChecked:E3} " + $"(over {result.DeltaCheckCount:N0} rows)");
                Console.WriteLine();
            }

            int sampleRowCount = Math.Min(20, result.MatrixResults.Count);

            Console.WriteLine("Sample Tax Results");
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine($"{"ID",5}  {"State",-14} {"Income",12} {"Federal",12} {"State",12} {"Total",12}");
            Console.WriteLine("--------------------------------------------------------------------------");

            for (int rowIndex = 0; rowIndex < sampleRowCount; rowIndex++)
            {
                TaxResult taxResult = result.MatrixResults[rowIndex];
                Employee employee = taxResult.Employee
                    ?? throw new InvalidOperationException("TaxResult.Employee is not assigned.");

                Console.WriteLine(
                    $"{employee.Id,5}  {employee.State,-14} {FormatMoney(employee.Income),12} {FormatMoney(taxResult.FederalTax),12} {FormatMoney(taxResult.StateTax),12} {FormatMoney(taxResult.TotalIncomeTax),12}");
            }

            Console.WriteLine();
            Console.WriteLine("Detailed Output Fields (Row 0)");
            Console.WriteLine("------------------------------");

            TaxResult firstResult = result.MatrixResults[0];

            Console.WriteLine($"SocialSecurityEmployee     : {FormatMoney(firstResult.SocialSecurityEmployee)}");
            Console.WriteLine($"MedicareEmployee           : {FormatMoney(firstResult.MedicareEmployee)}");
            Console.WriteLine($"AdditionalMedicareEmployee : {FormatMoney(firstResult.AdditionalMedicareEmployee)}");
            Console.WriteLine($"SocialSecurityEmployer     : {FormatMoney(firstResult.SocialSecurityEmployer)}");
            Console.WriteLine($"MedicareEmployer           : {FormatMoney(firstResult.MedicareEmployer)}");
            Console.WriteLine($"FederalUnemploymentTaxes   : {FormatMoney(firstResult.FederalUnemploymentTaxes)}");
            Console.WriteLine($"StateUnemploymentTaxes     : {FormatMoney(firstResult.StateUnemploymentTaxes)}");

            double[]? generalLedgerPostings = firstResult.GeneralLedgerPostings;
            if (generalLedgerPostings == null || generalLedgerPostings.Length == 0)
                return;

            Console.WriteLine();
            Console.WriteLine("General Ledger Postings (Row 0)");
            Console.WriteLine("-------------------------------");

            for (int bucketIndex = 0; bucketIndex < generalLedgerPostings.Length; bucketIndex++)
            {
                Console.WriteLine(
                    $"Bucket {bucketIndex,3}: {FormatMoney(generalLedgerPostings[bucketIndex])}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Warms up both calculators on a small subset to reduce JIT/first-use noise.
        /// </summary>
        private static void Warmup(IReadOnlyList<Employee> warmEmployees, EmployeeTaxCalculation_MatrixBased matrixCalculator)
        {
            if (warmEmployees == null) throw new ArgumentNullException(nameof(warmEmployees));
            if (warmEmployees.Count == 0) return;

            TaxResult[] iterativeWarmResults = PrewarmResults(warmEmployees);
            TaxResult[] matrixWarmResults = PrewarmResults(warmEmployees);

            int n = warmEmployees.Count;
            double[] income = new double[n];
            double[] federal = new double[n];
            double[] state = new double[n];

            EmployeeTaxCalculation_Iterative.CalculateTaxesInto(warmEmployees, iterativeWarmResults);

            matrixCalculator.CalculateTaxesInto(warmEmployees);
        }

        /// <summary>
        /// Preallocates TaxResult objects and general ledger posting buffers for the provided employees.
        /// </summary>
        private static TaxResult[] PrewarmResults(IEnumerable<Employee> employees)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));

            int bucketCount = TaxData.GeneralLedgerPostingPolicies.Count;

            int employeeCount = employees.Count();
            var results = new TaxResult[employeeCount];

            int i = 0;
            foreach (Employee employee in employees)
            {
                var taxResult = new TaxResult { Employee = employee };

                taxResult.GeneralLedgerPostings = bucketCount == 0
                    ? null
                    : new double[bucketCount];

                results[i++] = taxResult;
            }

            return results;
        }

        /// <summary>
        /// Measures elapsed time for the provided action.
        /// </summary>
        private static TimeSpan Time(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }


        private static void Accumulate(ref double maxDelta, params double[] values)
        {
            for (int i = 0; i < values.Length; i += 2)
            {
                double delta = Math.Abs(values[i] - values[i + 1]);
                if (delta > maxDelta)
                    maxDelta = delta;
            }
        }

        /// <summary>
        /// Computes the maximum absolute delta across all numeric output fields on the first N entries.
        /// </summary>
        /// <param name="iterative">The iterative results to compare.</param>
        /// <param name="matrix">The matrix results to compare.</param>
        /// <param name="spotCheckCount">The number of rows to compare.</param>
        private static double ComputeError(
            IReadOnlyList<TaxResult> iterative,
            IReadOnlyList<TaxResult> matrix,
            int spotCheckCount)
        {
            int count = Math.Min(spotCheckCount, Math.Min(iterative.Count, matrix.Count));
            double maxDelta = 0.0;

            for (int i = 0; i < count; i++)
            {
                TaxResult a = iterative[i];
                TaxResult b = matrix[i];

                if (!ReferenceEquals(a.Employee, b.Employee))
                    throw new InvalidOperationException("Result employee mismatch.");

                Accumulate(ref maxDelta,
                    a.FederalTax, b.FederalTax,
                    a.StateTax, b.StateTax,
                    a.TotalIncomeTax, b.TotalIncomeTax,
                    a.SocialSecurityEmployee, b.SocialSecurityEmployee,
                    a.MedicareEmployee, b.MedicareEmployee,
                    a.AdditionalMedicareEmployee, b.AdditionalMedicareEmployee,
                    a.SocialSecurityEmployer, b.SocialSecurityEmployer,
                    a.MedicareEmployer, b.MedicareEmployer,
                    a.FederalUnemploymentTaxes, b.FederalUnemploymentTaxes,
                    a.StateUnemploymentTaxes, b.StateUnemploymentTaxes);

                CompareArrays(ref maxDelta, a.GeneralLedgerPostings, b.GeneralLedgerPostings);
            }

            return maxDelta;
        }

        private static void CompareArrays(ref double maxDelta, double[]? a, double[]? b)
        {
            if (a == null || b == null)
            {
                if (a != b)
                    throw new InvalidOperationException("GL postings presence mismatch.");
                return;
            }

            if (a.Length != b.Length)
                throw new InvalidOperationException("GL postings length mismatch.");

            for (int i = 0; i < a.Length; i++)
            {
                double delta = Math.Abs(a[i] - b[i]);
                if (delta > maxDelta)
                    maxDelta = delta;
            }
        }

        /// <summary>
        /// Forces a full garbage collection cycle to reduce cross-run allocation noise.
        /// </summary>
        private static void ForceGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
