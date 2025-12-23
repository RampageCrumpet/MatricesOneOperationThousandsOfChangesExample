using MathNet.Numerics;
using MatricesOneOperationThousandsOfChangesExample;
using MatricesOneOperationThousandsOfChangesExample.Data;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators;
using MatricesOneOperationThousandsOfChangesExample.TaxCalculators.MatrixTaxCalculatorHelpers;

// The total sample size to print at the end.
const int sampleSize = 20;

// The number of employees we want to generate to calculate taxes for.
const int employeeCount = 1_000_000;
EmployeeGenerator employeeGenerator = new EmployeeGenerator(1234);
List<Employee> employees = employeeGenerator.GetEmployees(employeeCount);

try
{
    // Setup our Basic Linnear Algebra Subprogram provider to use native MKL with multi-threading enabled.
    // This is optional, but provides significant speedups for large matrix operations.
    Control.UseNativeMKL();
}
catch (Exception ex)
{
    // This is okay, we just wont be using any optomizations for our linnear algebra. Expect to see the iterative style outperform the matrix math.
    Console.WriteLine($"Failed to load OpenBLAS provider: {ex.Message}");
}

var iterativeCalculator = new EmployeeTaxCalculation_Iterative();
var matrixCalculator = new EmployeeTaxCalculation_MatrixBased();

var raceResult = CodeRacer.Race(employees, iterativeCalculator, matrixCalculator);
CodeRacer.PrintResults(raceResult, 1000);

Console.WriteLine('\n');

EmployeeDataPrinter.PrintEmployeeData(raceResult.MatrixResults, sampleSize);
