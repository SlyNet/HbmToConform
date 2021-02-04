namespace HbmToConform
{
    internal class ManyToOneInfo : ColumnInfo
    {
        public string NotFoundMode { get; set; }
        public string Lazy { get; set; }
        public bool NoUpdate { get; set; }
        public bool NoInsert { get; set; }
        public bool NotNull { get; set; }
        public string Cascade { get; set; }
    }
}