using static MatricesOneOperationThousandsOfChangesExample.Data.TaxData;

namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// An employee data class.
    /// </summary>
    public class Employee
    {
        public Employee(int id, string name, State state, double income)
        {
            Id = id;
            Name = name;
            State = state;
            Income = income;
        }

        /// <summary>
        /// The employee's unique identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The employee's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The employee's state/jurisdiction of residence (used for state tax selection).
        /// </summary>
        public State State { get; set; }

        /// <summary>
        /// The employees pretax income.
        /// </summary>
        [MatrixFeature]
        public double Income { get; set; }
    }
}
