using System.Collections.Generic;

namespace HbmToConform
{
    class MappingModel
    {
        public MappingModel()
        {
            this.Properties = new List<Property>();
            this.Collections = new List<CollectionInfo>();
            this.ManyToOnes = new List<ManyToOneInfo>();
            this.Subclasses = new List<SubclassModel>();
        }

        public List<ManyToOneInfo> ManyToOnes { get; set; }

        public string DomainClassName { get; set; }

        public string ClassTable { get; set; }

        public bool Lazy { get; set; }

        public List<Property> Properties { get; set; }

        public List<CollectionInfo> Collections { get; set; }

        public IdInfo Id { get; set; }
        public string FullType { get; set; }
        public DiscriminatorModel Discriminator { get; set; }
        public List<SubclassModel> Subclasses { get; set; }
    }
}