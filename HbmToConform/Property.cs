namespace HbmToConform
{
    internal class Property : ColumnInfo
    {
        public bool NotNull { get; set; }
        public bool Unique { get; set; }
        public bool NoUpdate { get; set; }
        public bool NoInsert { get; set; }
        public int? Length { get; set; }
        public string Generated { get; set; }
        public string Access { get; set; }
        public string UniqueKey { get; set; }

        public string SqlType { get; set; }
    }
}