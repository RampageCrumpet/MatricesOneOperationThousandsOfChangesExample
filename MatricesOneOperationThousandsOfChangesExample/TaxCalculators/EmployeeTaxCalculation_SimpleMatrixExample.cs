//using MathNet.Numerics.LinearAlgebra;

//namespace MatricesOneOperationThousandsOfChangesExample
//{
//    /// <summary>
//    /// This class calculates employee tax withholding using matrix operations. 
//    /// It isn't as the Iterative approach, but is simpler to understand as an example of matrix usage and should be treated as a halfway step between iterative and fully matrix-optimized that only exists for the purposes of building understanding.
//    /// </summary>
//    public class EmployeeTaxCalculation_SimpleMatrixExample
//    {
//        /// <summary>
//        /// Builds a transform to calculate progressive tax via matrix multiplication.
//        /// </summary>
//        /// <param name="lowerBounds"> A collection containing the lower bounds of each tax bracket.</param>
//        /// <param name="rates"> The tax rate at each tax bracket.</param>
//        /// <returns>A matrix containing the lower bounds and tax rate for each tax bracket.</returns>
//        private Matrix<double> BuildProgressiveTaxTransform(double[] lowerBounds, double[] rates)
//        {
//            int bracketCount = rates.Length;
//            var matrixBuilder = Matrix<double>.Build;

//            double baseTaxAtLower = 0.0;
//            var transform = matrixBuilder.Dense(2 * bracketCount, 1);

//            for (int i = 0; i < bracketCount; i++)
//            {
//                double lower = lowerBounds[i];
//                double rate = rates[i];

//                double baseTaxOffset = baseTaxAtLower - rate * lower;

//                transform[i, 0] = rate;
//                transform[i + bracketCount, 0] = baseTaxOffset;

//                if (i + 1 < bracketCount)
//                    baseTaxAtLower += (lowerBounds[i + 1] - lower) * rate;
//            }

//            return transform;
//        }

//        /// <summary>
//        /// Expands the matrix inputs to support progressive tax calculation
//        /// </summary>
//        /// <param name="employees"> The collection of employees we want to calculate taxes for. </param>
//        /// <param name="lowerBounds"> A collection containing the lower bounds of each tax bracket.</param>
//        /// <returns>Builds the matrix for the given collection of employees. We expand this matrix out to give space for calculating the tax owed at each bracket of pay.</returns>
//        private Matrix<double> BuildExpandedTaxInputs(IReadOnlyList<Employee> employees, double[] lowerBounds)
//        {
//            var matrixBuilder = Matrix<double>.Build;
//            var extendedInputs = matrixBuilder.Dense(employees.Count, 2 * lowerBounds.Length);

//            for (int row = 0; row < employees.Count; row++)
//            {
//                double income = employees[row].Income;

//                int bracketIndex = lowerBounds.Length - 1;
//                for (int i = 0; i < lowerBounds.Length - 1; i++)
//                    if (income < lowerBounds[i + 1]) { bracketIndex = i; break; }

//                extendedInputs[row, bracketIndex] = income;
//                extendedInputs[row, bracketIndex + lowerBounds.Length] = 1.0;
//            }

//            return extendedInputs;
//        }

//        /// <summary>
//        /// Calculates the federal and state taxes for a collection of employees.
//        /// </summary>
//        /// <param name="employees"> The collection of employees we want to calculate tax for.</param>
//        /// <returns> A collection of <see cref="TaxResult"/>'s containing the employees calculated tax.</returns>
//        public IEnumerable<TaxResult> CalculateTaxes(IReadOnlyList<Employee> employees)
//        {
//            // Build the transforms for federal and state taxes. This is the rule that tells us how to calculate tax from income.
//            var federalTaxTransform = BuildProgressiveTaxTransform(TaxData.FederalLowerBounds, TaxData.FederalRates);
//            var californiaTaxTransform = BuildProgressiveTaxTransform(TaxData.CaliforniaLowerBounds, TaxData.CaliforniaRates);
//            var newYorkTaxTransform = BuildProgressiveTaxTransform(TaxData.NewYorkLowerBounds, TaxData.NewYorkRates);

//            // Build our input matrix for federal tax. This holds the actual data for each employee like the dollar ammount paid for each tax bracket and their income.
//            var federalFeatures = BuildExpandedTaxInputs(employees, TaxData.FederalLowerBounds);
//            var federalTax = federalFeatures * federalTaxTransform;

//            // Separate out employees by state for state tax calculation. We don't want to apply NY tax rules to CA employees and vice versa.
//            var californiaEmployees = employees.Where(e => e.State == "CA").ToList();
//            var newYorkEmployees = employees.Where(e => e.State == "NY").ToList();
//            // Texas has no state income tax, so we skip it.

//            var californiaTax = BuildExpandedTaxInputs(californiaEmployees, TaxData.CaliforniaLowerBounds) * californiaTaxTransform;
//            var newYorkTax = BuildExpandedTaxInputs(newYorkEmployees, TaxData.NewYorkLowerBounds) * newYorkTaxTransform;

//            // Zip the per-state tax results back into the full employee list. This is only nescessary because we're bulk printing the data and wouldn't typically be done.
//            var stateTaxByEmployeeIndex = new double[employees.Count];
//            int caRow = 0;
//            int nyRow = 0;

//            for (int i = 0; i < employees.Count; i++)
//            {
//                stateTaxByEmployeeIndex[i] =
//                    employees[i].State == "CA" ? californiaTax[caRow++, 0] :
//                    employees[i].State == "NY" ? newYorkTax[nyRow++, 0] :
//                    0.0;
//            }

//            // Shove the data into a container we can easily work with.
//            var results = new List<TaxResult>(employees.Count);
//            for (int i = 0; i < employees.Count; i++)
//            {
//                results.Add(new TaxResult(
//                    employee: employees[i],
//                    federalTax: federalTax[i, 0],
//                    stateTax: stateTaxByEmployeeIndex[i]));
//            }

//            return results;
//        }
//    }
//}
