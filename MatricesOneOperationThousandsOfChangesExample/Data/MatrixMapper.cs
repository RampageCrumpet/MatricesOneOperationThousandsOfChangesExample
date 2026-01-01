namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// Provides fast row-major mapping from typed objects into dense numeric buffers using a pre-built schema.
    /// </summary>
    public static class MatrixMapper
    {
        /// <summary>
        /// Writes all schema columns for all items into a row-major destination buffer.
        /// </summary>
        /// <typeparam name="T">
        /// The item type being mapped.
        /// </typeparam>
        /// <param name="items">
        /// The items whose values will be written into the destination buffer.
        /// </param>
        /// <param name="schema">
        /// The schema defining column order and accessors for values to write.
        /// </param>
        /// <param name="destinationRowMajor">
        /// The destination buffer receiving values in row-major order.
        /// </param>
        public static void FillRowMajor<T>(
            IReadOnlyList<T> items,
            MatrixSchema<T> schema,
            double[] destinationRowMajor)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (destinationRowMajor == null) throw new ArgumentNullException(nameof(destinationRowMajor));

            int rowCount = items.Count;
            int colCount = schema.ColumnCount;

            int requiredLength = checked(rowCount * colCount);
            if (destinationRowMajor.Length != requiredLength)
            {
                throw new ArgumentException(
                    $"Destination length must be exactly items.Count * schema.ColumnCount ({rowCount} * {colCount} = {requiredLength}).",
                    nameof(destinationRowMajor));
            }

            if (rowCount == 0 || colCount == 0)
                return;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                int rowOffset = rowIndex * colCount;
                T item = items[rowIndex];

                for (int colIndex = 0; colIndex < colCount; colIndex++)
                    destinationRowMajor[rowOffset + colIndex] = schema.GetGetter(colIndex)(item);
            }
        }

        /// <summary>
        /// Writes a single schema column for all items into a contiguous destination buffer.
        /// </summary>
        /// <typeparam name="T">
        /// The item type being mapped.
        /// </typeparam>
        /// <param name="items">
        /// The items whose values will be written into the destination buffer.
        /// </param>
        /// <param name="schema">
        /// The schema defining column order and accessors for values to write.
        /// </param>
        /// <param name="columnIndex">
        /// The zero-based index of the schema column to write.
        /// </param>
        /// <param name="destination">
        /// The destination buffer receiving values for the requested column.
        /// </param>
        public static void FillColumn<T>(
            IReadOnlyList<T> items,
            MatrixSchema<T> schema,
            int columnIndex,
            double[] destination)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if ((uint)columnIndex >= (uint)schema.ColumnCount) throw new ArgumentOutOfRangeException(nameof(columnIndex));

            int rowCount = items.Count;
            if (destination.Length != rowCount)
                throw new ArgumentException($"Destination length must be exactly items.Count ({rowCount}).", nameof(destination));

            if (rowCount == 0)
                return;

            var getter = schema.GetGetter(columnIndex);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                destination[rowIndex] = getter(items[rowIndex]);
        }

        /// <summary>
        /// Copies a row slice from a row-major source buffer into the destination buffer.
        /// </summary>
        /// <param name="sourceRowMajor">
        /// The source buffer containing values in row-major order.
        /// </param>
        /// <param name="rowIndex">
        /// The zero-based row index to copy.
        /// </param>
        /// <param name="columnCount">
        /// The number of columns per row in the source buffer.
        /// </param>
        /// <param name="destination">
        /// The destination buffer receiving the row slice.
        /// </param>
        public static void CopyRow(double[] sourceRowMajor, int rowIndex, int columnCount, double[] destination)
        {
            if (sourceRowMajor == null) throw new ArgumentNullException(nameof(sourceRowMajor));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (columnCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount));
            if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            if (destination.Length != columnCount)
                throw new ArgumentException($"Destination length must be exactly columnCount ({columnCount}).", nameof(destination));

            int rowOffset = checked(rowIndex * columnCount);
            int requiredLength = checked(rowOffset + columnCount);

            if (requiredLength > sourceRowMajor.Length)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            Array.Copy(sourceRowMajor, rowOffset, destination, 0, columnCount);
        }
    }
}
