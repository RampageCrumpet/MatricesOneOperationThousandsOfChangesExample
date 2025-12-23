namespace MatricesOneOperationThousandsOfChangesExample.Data
{
    /// <summary>
    /// This class holds the result of a race between two tax calculation implementations.
    /// </summary>
    public class RaceResult
    {
        public required List<TaxResult> IterativeResults { get; init; }
        public required List<TaxResult> MatrixResults { get; init; }

        public required TimeSpan IterativeTime { get; init; }
        public required TimeSpan MatrixTime { get; init; }

        public double SpeedupIterativeOverMatrix =>
            MatrixTime.TotalMilliseconds <= 0 ? double.PositiveInfinity :
            IterativeTime.TotalMilliseconds / MatrixTime.TotalMilliseconds;

        public required double MaxAbsoluteDeltaChecked { get; init; }
        public required int DeltaCheckCount { get; init; }
    }
}
