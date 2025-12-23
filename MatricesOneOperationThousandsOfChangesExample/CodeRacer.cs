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
    /// Runs an end-to-end timing comparison between the iterative and matrix-based tax calculators and spot-checks correctness.
    /// This is intended to measure the full cost of each path while avoiding benchmark artifacts like double materialization.
    /// </summary>
    public partial class CodeRacer
    {
        /// <summary>
        /// Executes warmup, runs both calculators, and returns timings plus a spot-check delta on the first N results.
        /// </summary>
        /// <param name="employees">The employees to run through both calculators.</param>
        /// <param name="iterativeCalculator">The iterative calculator implementation.</param>
        /// <param name="matrixCalculator">The matrix calculator implementation.</param>
        /// <param name="warmupCount">The number of employees to use for warmup to reduce JIT/first-use noise.</param>
        /// <param name="spotCheckCount">The number of result rows to compare for correctness.</param>
        /// <returns>A race result containing timings, the materialized result lists, and a max absolute delta for the spot-check.</returns>
        public static RaceResult Race(
            IReadOnlyList<Employee> employees,
            EmployeeTaxCalculation_Iterative iterativeCalculator,
            EmployeeTaxCalculation_MatrixBased matrixCalculator,
            int warmupCount = 10_000,
            int spotCheckCount = 1_000)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (iterativeCalculator == null) throw new ArgumentNullException(nameof(iterativeCalculator));
            if (matrixCalculator == null) throw new ArgumentNullException(nameof(matrixCalculator));

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

            Warmup(warmEmployees);

            ForceGc();

            List<TaxResult> iterativeResults = new List<TaxResult>();
            TimeSpan iterativeTime = Time(() =>
            {
                iterativeResults = MaterializeOnce(EmployeeTaxCalculation_MatrixBased.CalculateTaxes(employees));
            });

            ForceGc();

            List<TaxResult> matrixResults = new List<TaxResult>();
            TimeSpan matrixTime = Time(() =>
            {
                matrixResults = MaterializeOnce(EmployeeTaxCalculation_Iterative.CalculateTaxes(employees));
            });

            double maxDelta = ComputeMaxAbsoluteDeltaOnTotalIncomeTax(iterativeResults, matrixResults, spotCheckCount);

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
        /// Warms up both calculators by fully consuming results for a small employee subset to reduce JIT and first-use effects.
        /// </summary>
        /// <param name="warmEmployees">The warmup employee subset.</param>
        /// <param name="iterativeCalculator">The iterative calculator implementation.</param>
        /// <param name="matrixCalculator">The matrix calculator implementation.</param>
        private static void Warmup(
            IReadOnlyList<Employee> warmEmployees)
        {
            _ = MaterializeOnce(EmployeeTaxCalculation_Iterative.CalculateTaxes(warmEmployees));
            _ = MaterializeOnce(EmployeeTaxCalculation_MatrixBased.CalculateTaxes(warmEmployees));
        }

        /// <summary>
        /// Materializes an enumerable exactly once, avoiding an accidental second full copy when the source is already a list or collection.
        /// </summary>
        /// <param name="results">The tax results enumeration produced by a calculator.</param>
        /// <returns>A single materialized list containing the results.</returns>
        private static List<TaxResult> MaterializeOnce(IEnumerable<TaxResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            if (results is List<TaxResult> list)
                return list;

            if (results is ICollection<TaxResult> collection)
            {
                var materialized = new List<TaxResult>(collection.Count);
                materialized.AddRange(collection);
                return materialized;
            }

            return results.ToList();
        }

        /// <summary>
        /// Computes the maximum absolute delta in TotalIncomeTax between two result lists on the first N entries.
        /// </summary>
        /// <param name="iterativeResults">The iterative results.</param>
        /// <param name="matrixResults">The matrix results.</param>
        /// <param name="spotCheckCount">The number of entries to check.</param>
        /// <returns>The maximum absolute delta observed across the checked entries.</returns>
        private static double ComputeMaxAbsoluteDeltaOnTotalIncomeTax(
            List<TaxResult> iterativeResults,
            List<TaxResult> matrixResults,
            int spotCheckCount)
        {
            int checkCount = Math.Min(spotCheckCount, Math.Min(iterativeResults.Count, matrixResults.Count));
            double maxDelta = 0.0;

            for (int i = 0; i < checkCount; i++)
            {
                double delta = Math.Abs(iterativeResults[i].TotalIncomeTax - matrixResults[i].TotalIncomeTax);
                if (delta > maxDelta) maxDelta = delta;
            }

            return maxDelta;
        }

        /// <summary>
        /// Times a synchronous action using Stopwatch and returns the elapsed duration.
        /// </summary>
        /// <param name="action">The action to time.</param>
        /// <returns>The elapsed duration of the action.</returns>
        private static TimeSpan Time(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
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


        /// <summary>
        /// Writes a human-readable summary of a race result to the console, including timings and correctness checks.
        /// </summary>
        /// <param name="result">The race result to print.</param>
        /// <param name="sampleCount">
        /// The number of result rows to print for inspection.
        /// Set to 0 to disable row-level printing.
        /// </param>
        public static void PrintResults(RaceResult result, int sampleCount = 0)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            Console.WriteLine("=== Tax Calculator Benchmark Results ===");
            Console.WriteLine();

            Console.WriteLine($"Iterative Time : {result.IterativeTime.TotalMilliseconds:N0} ms");
            Console.WriteLine($"Matrix Time    : {result.MatrixTime.TotalMilliseconds:N0} ms");

            if (result.MatrixTime.TotalMilliseconds > 0)
            {
                double speedup = result.IterativeTime.TotalMilliseconds / result.MatrixTime.TotalMilliseconds;
                Console.WriteLine($"Speedup        : {speedup:N2}x");
            }

            Console.WriteLine();
            Console.WriteLine("Correctness Check");
            Console.WriteLine("-----------------");
            Console.WriteLine($"Rows Checked   : {result.DeltaCheckCount:N0}");
            Console.WriteLine($"Max |Δ|        : {result.MaxAbsoluteDeltaChecked:E6}");

            Console.WriteLine();
        }
    }
}
