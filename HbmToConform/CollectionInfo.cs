using System.Xml.Linq;

namespace HbmToConform
{
    internal class CollectionInfo : Named
    {
        public bool Inverse { get; set; }

        public string Table { get; set; }
        public bool Lazy { get; set; }
        public string Cascade { get; set; }
        public string KeyColumn { get; set; }
        public string RelType { get; set; }
        public string OrderBy { get; set; }
        public string RelColumn { get; set; }

        public CollectionType CollectionType { get; set; }
        public CompositeElementModel CompositeElement { get; set; }
        public string NotFound { get; set; }
    }

    internal enum CollectionType
    {
        Bag,
        Set
    }
}