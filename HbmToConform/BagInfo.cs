namespace HbmToConform
{
    internal class BagInfo : Named
    {
        public bool Inverse { get; set; }

        public string Table { get; set; }
        public bool Lazy { get; set; }
        public string Cascade { get; set; }
        public string KeyColumn { get; set; }
        public string RelType { get; set; }
        public string OrderBy { get; set; }
        public string RelColumn { get; set; }
    }
}