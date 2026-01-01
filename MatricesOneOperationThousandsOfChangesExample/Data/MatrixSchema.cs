using System.Linq.Expressions;
using System.Reflection;

namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// Describes a set of matrix-mapped features for a type and provides fast accessors for those features.
    /// </summary>
    /// <typeparam name="T">
    /// The type whose annotated properties define the schema.
    /// </typeparam>
    public class MatrixSchema<T>
    {
        private readonly Column[] _columns;
        private readonly string[] _columnNames;
        private readonly Dictionary<string, int> _columnIndexByName;

        private MatrixSchema(Column[] columns)
        {
            _columns = columns;
            _columnNames = columns.Select(c => c.Name).ToArray();

            _columnIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < columns.Length; i++)
                _columnIndexByName[columns[i].Name] = i;
        }

        /// <summary>
        /// Gets the number of columns defined by this schema.
        /// </summary>
        public int ColumnCount => _columns.Length;

        /// <summary>
        /// Gets the column names defined by this schema in column order.
        /// </summary>
        public IReadOnlyList<string> ColumnNames => _columnNames;

        /// <summary>
        /// Creates a schema by discovering properties on <typeparamref name="T"/> marked with <see cref="MatrixFeatureAttribute"/>.
        /// </summary>
        public static MatrixSchema<T> Create()
        {
            Type targetType = typeof(T);

            PropertyInfo[] properties = targetType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToArray();

            var annotated = new List<PropertyInfo>();

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];

                MatrixFeatureAttribute? attribute = property.GetCustomAttribute<MatrixFeatureAttribute>(inherit: true);
                if (attribute == null)
                    continue;

                if (property.GetMethod == null || !property.GetMethod.IsPublic)
                    throw new InvalidOperationException($"{targetType.Name}.{property.Name} is marked with [{nameof(MatrixFeatureAttribute)}] but does not have a public getter.");

                if (property.PropertyType != typeof(double))
                    throw new InvalidOperationException($"{targetType.Name}.{property.Name} is marked with [{nameof(MatrixFeatureAttribute)}] but is not a double.");

                annotated.Add(property);
            }

            if (annotated.Count == 0)
                throw new InvalidOperationException($"No public double properties on {targetType.Name} are marked with [{nameof(MatrixFeatureAttribute)}].");

            PropertyInfo[] ordered = annotated
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

            Column[] columns = new Column[ordered.Length];

            for (int columnIndex = 0; columnIndex < ordered.Length; columnIndex++)
            {
                PropertyInfo property = ordered[columnIndex];

                columns[columnIndex] = new Column(
                    name: property.Name,
                    getter: CompileDoubleGetter(property)
                );
            }

            return new MatrixSchema<T>(columns);
        }

        /// <summary>
        /// Gets the column index for a named column.
        /// </summary>
        /// <param name="columnName">
        /// The name of the column to look up.
        /// </param>
        public int GetColumnIndex(string columnName)
        {
            if (columnName == null) throw new ArgumentNullException(nameof(columnName));

            if (!_columnIndexByName.TryGetValue(columnName, out int index))
                throw new KeyNotFoundException($"Column '{columnName}' was not found on schema '{typeof(T).Name}'.");

            return index;
        }

        /// <summary>
        /// Gets a compiled accessor for the column at the specified index.
        /// </summary>
        /// <param name="columnIndex">
        /// The zero-based index of the column.
        /// </param>
        public Func<T, double> GetGetter(int columnIndex)
        {
            if ((uint)columnIndex >= (uint)_columns.Length)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));

            return _columns[columnIndex].Getter;
        }

        /// <summary>
        /// Reads all column values from the given instance into the destination buffer.
        /// </summary>
        /// <param name="instance">
        /// The instance to read column values from.
        /// </param>
        /// <param name="destination">
        /// The destination buffer to fill with column values.
        /// </param>
        public void ReadRow(T instance, double[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length != _columns.Length)
                throw new ArgumentException($"Destination length must be exactly {ColumnCount}.", nameof(destination));

            for (int columnIndex = 0; columnIndex < _columns.Length; columnIndex++)
                destination[columnIndex] = _columns[columnIndex].Getter(instance);
        }

        private static Func<T, double> CompileDoubleGetter(PropertyInfo property)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(T), "instance");

            Expression instanceExpression = typeof(T).IsValueType
                ? (Expression)instanceParameter
                : Expression.Convert(instanceParameter, property.DeclaringType!);

            MemberExpression propertyAccess = Expression.Property(instanceExpression, property);

            Expression body = propertyAccess.Type == typeof(double)
                ? (Expression)propertyAccess
                : Expression.Convert(propertyAccess, typeof(double));

            var lambda = Expression.Lambda<Func<T, double>>(body, instanceParameter);
            return lambda.Compile();
        }

        private readonly struct Column
        {
            public Column(string name, Func<T, double> getter)
            {
                Name = name;
                Getter = getter;
            }

            public string Name { get; }
            public Func<T, double> Getter { get; }
        }
    }
}
