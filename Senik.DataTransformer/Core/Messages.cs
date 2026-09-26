using System.Collections.ObjectModel;
using Senik.DataTransformer.Models;
using Senik.DataTransformer.Validation;

namespace Senik.DataTransformer.Core
{
    public class NavigateToExecutionPageMessage
    {
        public bool RunKala { get; set; }
        public string KalaFilePath { get; set; } = string.Empty;
        public string KalaSheetName { get; set; } = string.Empty;
        public ObservableCollection<ColumnMappingItem> KalaMappings { get; set; } = new ObservableCollection<ColumnMappingItem>();

        public bool RunPerson { get; set; }
        public string PersonFilePath { get; set; } = string.Empty;
        public string PersonSheetName { get; set; } = string.Empty;
        public ObservableCollection<ColumnMappingItem> PersonMappings { get; set; } = new ObservableCollection<ColumnMappingItem>();

        // ✨ آماده‌سازی متغیرهای تب چک
        public bool RunCheck { get; set; }
        public string CheckFilePath { get; set; } = string.Empty;
        public string CheckSheetName { get; set; } = string.Empty;
        public ObservableCollection<ColumnMappingItem> CheckMappings { get; set; } = new ObservableCollection<ColumnMappingItem>();

        public ValidationReport CombinedReport { get; set; } = new ValidationReport();

    }

    public class OpenConfirmDialogMessage
    {
        public NavigateToExecutionPageMessage ExecutionPayload { get; set; } = new NavigateToExecutionPageMessage();
    }

    public class NavigateToDiscoveryPageMessage { }
}