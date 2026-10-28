namespace FabriTrack.Models
{
    public class ExportDataViewModel
    {
        public bool HasPreview { get; set; }
        public string DataType { get; set; } = "inventory";
        public string DataTypeLabel { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
        public int TotalRows { get; set; }
        public bool PreviewIsLimited { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CategoryId { get; set; }
        public int? SupplierId { get; set; }
        public bool IncludeArchived { get; set; }
        public bool IncludeVoided { get; set; }
    }
}
