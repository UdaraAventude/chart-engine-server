using ChartEngine.Domain.Enums;

namespace ChartEngine.Domain.Entities
{
    public class Dataset
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string FileName { get; private set; } = string.Empty;
        public long FileSizeBytes { get; private set; }
        public int TotalRows { get; private set; }
        public string StoragePath { get; private set; } = string.Empty;
        public DatasetStatus Status { get; private set; } = DatasetStatus.Pending;
        public string? ErrorMessage { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; private set; }

        private Dataset() { }

        public static Dataset Create(string fileName, long fileSizeBytes, string storagePath)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            if (fileSizeBytes <= 0)
                throw new ArgumentException("File size must be positive.", nameof(fileSizeBytes));

            return new Dataset
            {
                FileName = fileName,
                FileSizeBytes = fileSizeBytes,
                StoragePath = storagePath
            };
        }

        public void MarkAsProcessing()
        {
            if (Status != DatasetStatus.Pending)
                throw new InvalidOperationException($"Cannot start processing a dataset in {Status} state.");
            Status = DatasetStatus.Processing;
        }

        public void MarkAsReady(int totalRows)
        {
            if (Status != DatasetStatus.Processing)
                throw new InvalidOperationException($"Cannot mark as ready a dataset in {Status} state.");
            Status = DatasetStatus.Ready;
            TotalRows = totalRows;
            ProcessedAt = DateTime.UtcNow;
        }

        public void MarkAsFailed(string errorMessage)
        {
            Status = DatasetStatus.Failed;
            ErrorMessage = errorMessage;
            ProcessedAt = DateTime.UtcNow;
        }

        public void SetStoragePath(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));
            StoragePath = storagePath;
        }
    }
}
