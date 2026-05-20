using System;

namespace ChartEngine.Domain.Entities
{
    public class ExportJob
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid DatasetId { get; private set; }
        public string Format { get; private set; } = string.Empty;
        public string? DrillPath { get; private set; }
        public string Status { get; private set; } = "Pending";
        public string? DownloadPath { get; private set; }
        public string? ErrorMessage { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; private set; }

        private ExportJob() { }

        public static ExportJob Create(Guid datasetId, string format, string? drillPath)
        {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("Format cannot be empty.", nameof(format));

            return new ExportJob
            {
                DatasetId = datasetId,
                Format = format,
                DrillPath = drillPath
            };
        }

        public void MarkAsProcessing()
        {
            Status = "Processing";
        }

        public void MarkAsCompleted(string downloadPath)
        {
            if (string.IsNullOrWhiteSpace(downloadPath))
                throw new ArgumentException("Download path cannot be empty.", nameof(downloadPath));

            Status = "Completed";
            DownloadPath = downloadPath;
            ProcessedAt = DateTime.UtcNow;
        }

        public void MarkAsFailed(string errorMessage)
        {
            Status = "Failed";
            ErrorMessage = errorMessage;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
