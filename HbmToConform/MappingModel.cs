using System.Collections.Generic;

namespace HbmToConform
{
    class MappingModel
    {
        public MappingModel()
        {
            this.Properties = new List<Property>();
            this.Bags = new List<BagInfo>();
            this.ManyToOnes = new List<ManyToOneInfo>();
        }

        public List<ManyToOneInfo> ManyToOnes { get; set; }

        public string DomainClassName { get; set; }

        public string ClassTable { get; set; }

        public bool Lazy { get; set; }

        public List<Property> Properties { get; set; }

        public List<BagInfo> Bags { get; set; }

        public IdInfo Id { get; set; }
        public string FullType { get; set; }
    }
}