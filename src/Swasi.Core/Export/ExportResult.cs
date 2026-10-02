using System.Collections.Generic;
using System.Linq;

namespace SolidWorks_ASsembly_Instructor
{
    public enum ExportStatus { Succeeded, Skipped, Failed }
    public sealed class ExportItemResult
    {
        public string Name { get; }
        public ExportStatus Status { get; }
        public string Message { get; }
        public ExportItemResult(string name, ExportStatus status, string message)
        { Name = name; Status = status; Message = message; }
    }
    public sealed class ExportResult
    {
        private readonly List<ExportItemResult> items = new List<ExportItemResult>();
        public IReadOnlyList<ExportItemResult> Items => items.AsReadOnly();
        public int SucceededCount => items.Count(i => i.Status == ExportStatus.Succeeded);
        public int SkippedCount => items.Count(i => i.Status == ExportStatus.Skipped);
        public int FailedCount => items.Count(i => i.Status == ExportStatus.Failed);
        public bool IsCompleteSuccess => SucceededCount > 0 && SkippedCount == 0 && FailedCount == 0;
        public void Add(string name, ExportStatus status, string message) => items.Add(new ExportItemResult(name, status, message));
        public override string ToString() => $"Export finished: {SucceededCount} succeeded, {SkippedCount} skipped, {FailedCount} failed.";
    }
}
