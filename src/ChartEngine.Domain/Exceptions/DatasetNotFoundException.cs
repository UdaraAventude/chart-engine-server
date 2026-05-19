namespace ChartEngine.Domain.Exceptions
{
    internal class DatasetNotFoundException : Exception
    {
        public Guid DatasetId { get; }

        public DatasetNotFoundException(Guid datasetId)
            : base($"Dataset with ID '{datasetId}' was not found.")
        {
            DatasetId = datasetId;
        }
    }
}
