using MathNet.Numerics.LinearAlgebra;
using MatricesOneOperationThousandsOfChangesExample;

// The total sample size to print at the end.
const int sampleSize = 20;
EmployeeGenerator employeeGenerator = new EmployeeGenerator(1234);
List<Employee> employees = employeeGenerator.GetEmployees(10_000_000);

// 1) Build the progressive tax transform.
Matrix<double> BuildProgressiveTaxTransform(double[] lowerBounds, double[] rates)
{
    int bracketCount = rates.Length;
    var matrixBuilder = Matrix<double>.Build;

    double baseTaxAtLower = 0.0;
    var transform = matrixBuilder.Dense(2 * bracketCount, 1);

    for (int i = 0; i < bracketCount; i++)
    {
        double lower = lowerBounds[i];
        double rate = rates[i];

        double baseTaxOffset = baseTaxAtLower - rate * lower;

        transform[i, 0] = rate;
        transform[i + bracketCount, 0] = baseTaxOffset;

        if (i + 1 < bracketCount)
            baseTaxAtLower += (lowerBounds[i + 1] - lower) * rate;
    }

    return transform;
}

// 2) Expand each employee row into bracket columns so progressive tax becomes a matrix multiply.
Matrix<double> BuildExpandedTaxInputs(IReadOnlyList<Employee> employees, double[] lowerBounds, int bracketCount)
{
    var matrixBuilder = Matrix<double>.Build;
    var extendedInputs = matrixBuilder.Dense(employees.Count, 2 * bracketCount);

    for (int row = 0; row < employees.Count; row++)
    {
        double income = employees[row].Income;

        int bracketIndex = bracketCount - 1;
        for (int i = 0; i < bracketCount - 1; i++)
            if (income < lowerBounds[i + 1]) { bracketIndex = i; break; }

        extendedInputs[row, bracketIndex] = income;
        extendedInputs[row, bracketIndex + bracketCount] = 1.0;
    }

    return extendedInputs;
}

// 3) Build each transform once.
var federalTaxTransform = BuildProgressiveTaxTransform(TaxData.FederalLowerBounds, TaxData.FederalRates);
var californiaTaxTransform = BuildProgressiveTaxTransform(TaxData.CaliforniaLowerBounds, TaxData.CaliforniaRates);
var newYorkTaxTransform = BuildProgressiveTaxTransform(TaxData.NewYorkLowerBounds, TaxData.NewYorkRates);

// 4) Apply federal to ALL employees (one matrix multiply)
var federalFeatures = BuildExpandedTaxInputs(employees, TaxData.FederalLowerBounds, TaxData.FederalRates.Length);
var federalTax = federalFeatures * federalTaxTransform;

// 5) Apply state tax per cohort.
var californiaEmployees = employees.Where(e => e.State == "CA").ToList();
var newYorkEmployees = employees.Where(e => e.State == "NY").ToList();
// Texas has no state income tax, so we skip it.

var californiaTax = BuildExpandedTaxInputs(californiaEmployees, TaxData.CaliforniaLowerBounds, TaxData.CaliforniaRates.Length) * californiaTaxTransform;
var newYorkTax = BuildExpandedTaxInputs(newYorkEmployees, TaxData.NewYorkLowerBounds, TaxData.NewYorkRates.Length) * newYorkTaxTransform;


// Flatten the data post process so we can print it out.
var stateWithholdingArray = new double[employees.Count];

int caRow = 0;
int nyRow = 0;
for (int i = 0; i < employees.Count; i++)
{
    stateWithholdingArray[i] =
        employees[i].State == "CA" ? californiaTax[caRow++, 0] :
        employees[i].State == "NY" ? newYorkTax[nyRow++, 0] :
        0.0; // TX
}

var federalWithholdingArray = federalTax.Column(0).ToArray();

EmployeeDataPrinter.PrintEmployeeData(employees, federalWithholdingArray, stateWithholdingArray, sampleSize);
