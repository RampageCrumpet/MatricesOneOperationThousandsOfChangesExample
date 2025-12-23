using MatricesOneOperationThousandsOfChangesExample.Data;
using MatricesOneOperationThousandsOfChangesExample.Data.TaxData;
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
        public static RaceResult Race(
            IReadOnlyList<Employee> employees,
            int warmupCount = 10_000,
            int spotCheckCount = 1_000)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));

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
                iterativeResults = MaterializeOnce(EmployeeTaxCalculation_Iterative.CalculateTaxes(employees));
            });

            ForceGc();

            List<TaxResult> matrixResults = new List<TaxResult>();
            TimeSpan matrixTime = Time(() =>
            {
                matrixResults = MaterializeOnce(EmployeeTaxCalculation_MatrixBased.CalculateTaxes(employees));
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
        /// Writes a human-readable summary of a materializing race result to the console.
        /// </summary>
        public static void PrintResults(RaceResult result)
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
        }

        /// <summary>
        /// Warms up both calculators by fully materializing results for a small employee subset to reduce first-use effects.
        /// </summary>
        private static void Warmup(IReadOnlyList<Employee> warmEmployees)
        {
            _ = MaterializeOnce(EmployeeTaxCalculation_Iterative.CalculateTaxes(warmEmployees));
            _ = MaterializeOnce(EmployeeTaxCalculation_MatrixBased.CalculateTaxes(warmEmployees));
        }

        /// <summary>
        /// Materializes an enumerable exactly once, avoiding an accidental second full copy when the source is already a list or collection.
        /// </summary>
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
    }
}
