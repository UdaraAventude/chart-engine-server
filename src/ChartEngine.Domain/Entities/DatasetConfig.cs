using System;

namespace ChartEngine.Domain.Entities
{
	public class DatasetConfig
	{
		public Guid DatasetId { get; private set; }
		public int MaxDimCardinality { get; private set; }
		public int MaxHierarchyDepth { get; private set; }
		public int FallbackTopValues { get; private set; }

		public Dataset Dataset { get; private set; } = null!;

		private DatasetConfig() { }

		public static DatasetConfig Create(Guid datasetId, int maxDimCardinality, int maxHierarchyDepth, int fallbackTopValues)
		{
			return new DatasetConfig
			{
				DatasetId = datasetId,
				MaxDimCardinality = maxDimCardinality,
				MaxHierarchyDepth = maxHierarchyDepth,
				FallbackTopValues = fallbackTopValues
			};
		}
	}
}
