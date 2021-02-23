using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HbmToConform
{
    class Program
    {
        static void Main(string[] args)
        {
            if (File.Exists(args[0]))
            {
                ProcessSingleFile(new FileInfo(args[0]));
            }
            else
            {
                var directory = new DirectoryInfo(args[0]);
                foreach (var fileInfo in directory.GetFiles("*.hbm.xml", SearchOption.AllDirectories))
                {
                    if(fileInfo.Name != "bia.hbm.xml")
                    ProcessSingleFile(fileInfo);
                }
            }
        }

        private static void ProcessSingleFile(FileInfo fileInfo)
        {
            var xml = XDocument.Parse(File.ReadAllText(fileInfo.FullName));

            var model = new MappingModel();
            var ns = xml.Root.Name.Namespace;

            var classElement = xml.Root.Element(ns.GetName("class"));

            var fullClassName = classElement.Attribute("name").Value.Split(",")[0];
            var onlyClass = fullClassName.Split(".", StringSplitOptions.RemoveEmptyEntries).Last();

            model.DomainClassName = onlyClass;
            model.FullType = fullClassName;
            model.ClassTable = classElement.Attribute("table")!.Value;
            model.BatchSize = classElement.Attribute("batch-size")?.Value;

            if (bool.TryParse(classElement.Attribute("lazy")?.Value, out bool lazy))
                model.Lazy = lazy;

            ReadId(classElement, ns, model);
            ReadDiscriminator(classElement, ns, model);

            var properties = ReadProperties(xml.Root.Descendants(ns.GetName("class")).FirstOrDefault(), ns);
            model.Properties.AddRange(properties);
            ReadBagsMaps(xml, ns, model);
            ReadSetMaps(xml, ns, model);
            model.ManyToOnes.AddRange(ReadManyToOnes(xml.Root.Descendants(ns.GetName("class")).FirstOrDefault(), ns, model));
            ReadSubClasses(classElement, ns, model);

            var conversion = new MapTemplate();
            conversion.Model = model;

            Console.WriteLine($"Transforming file {Path.GetFileName(fileInfo.Name)}");

            var map = conversion.TransformText();

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(fileInfo.FullName), onlyClass + "Map.cs"), map);
        }

        private static void ReadSubClasses(XElement classElement, XNamespace ns, MappingModel model)
        {
            foreach (XElement subclassNode in classElement.Descendants(ns.GetName("subclass")))
            {
                //<subclass name="ncontinuity2.core.domain.SupplierInterimMeasures,ncontinuity2.core" discriminator-value="1" />
                var subclassModel = new SubclassModel();
                var fullClassName = subclassNode.Attribute("name").Value.Split(",")[0];
                var onlyClass = fullClassName.Split(".", StringSplitOptions.RemoveEmptyEntries).Last();

                subclassModel.ClassName = onlyClass;
                subclassModel.FullClassName = fullClassName;
                subclassModel.DiscriminatorValue = subclassNode.Attribute("discriminator-value")?.Value;
                model.Subclasses.Add(subclassModel);
            }
        }

        private static void ReadDiscriminator(XElement classElement, XNamespace ns, MappingModel model)
        {
            var discriminator = classElement.Descendants(ns.GetName("discriminator")).FirstOrDefault();
            if (discriminator != null)
            {
                var discriminatorModel = new DiscriminatorModel();
                discriminatorModel.ColumnName = discriminator.Attribute("column")?.Value;

                model.Discriminator = discriminatorModel;
            }
        }

        private static void ReadSetMaps(XDocument xml, XNamespace ns, MappingModel model)
        {
            CollectionType collectionType = CollectionType.Set;

            List<CollectionInfo> foundCollections = ReadCollections(xml.Root.Descendants(ns.GetName("class")).FirstOrDefault(), ns, collectionType);
            model.Collections.AddRange(foundCollections);
        }

        private static void ReadBagsMaps(XDocument xml, XNamespace ns, MappingModel model)
        {
            CollectionType collectionType = CollectionType.Bag;

            List<CollectionInfo> foundCollections = ReadCollections(xml.Root.Descendants(ns.GetName("class")).FirstOrDefault(), ns, collectionType);
            model.Collections.AddRange(foundCollections);
        }

        private static void ReadId(XElement classElement, XNamespace ns, MappingModel model)
        {
            var idElement = classElement.Element(ns.GetName("id"));
            var idInfo = new IdInfo();
            idInfo.ColumnName = idElement.Attribute("column")?.Value;
            idInfo.Name = idElement.Attribute("name").Value;

            var stringGenerator = idElement.Element(ns.GetName("generator"))?.Attribute("class")?.Value;
            switch (stringGenerator)
            {
                case "guid.comb":
                    idInfo.Generator = "Generators.GuidComb";
                    break;
                case "assigned":
                    idInfo.Generator = "Generators.Assigned";
                    break;
                case "identity":
                    idInfo.Generator = "Generators.Identity";
                    break;
                case "native":
                    idInfo.Generator = "Generators.Native";
                    break;
            }

            if (idElement.Attribute("access")?.Value != null)
            {
                var xmlAccess = idElement.Attribute("access").Value;
                switch (xmlAccess.ToLower())
                {
                    case "property":
                        idInfo.Access = "Accessor.Property";
                        break;
                    case "field.camelcase-underscore":
                        idInfo.Access = "Accessor.Field";
                        break;
                }
            }

            idInfo.UnsavedValue = idElement.Attribute("unsaved-value")?.Value;
            if (idInfo.UnsavedValue == Guid.Empty.ToString())
            {
                idInfo.UnsavedValue = "System.Guid.Empty";
            }
            model.Id = idInfo;
        }

        private static IEnumerable<ManyToOneInfo> ReadManyToOnes(XElement classElement, XNamespace ns, MappingModel model)
        {
            foreach (var manyToOne in classElement.Descendants(ns.GetName("many-to-one")))
            {
                var mtoModel = new ManyToOneInfo();
                mtoModel.Name = manyToOne.Attribute("name").Value;
                mtoModel.ColumnName = manyToOne.Attribute("column")?.Value;

                if (mtoModel.ColumnName == null)
                {
                   mtoModel.ColumnName =  manyToOne.Descendants(ns.GetName("column")).FirstOrDefault()?.Attribute("name")?.Value;
                }

                string notFoundModel = manyToOne.Attribute("not-found")?.Value;
                switch (notFoundModel)
                {
                    case "ignore":
                        mtoModel.NotFoundMode = "NotFoundMode.Ignore";
                        break;
                    case "exception":
                        mtoModel.NotFoundMode = "NotFoundMode.Exception";
                        break;
                }

                string mtoLazy = manyToOne.Attribute("lazy")?.Value;

                switch (mtoLazy)
                {
                    case "proxy":
                        mtoModel.Lazy = "LazyRelation.Proxy";
                        break;
                    case "no-proxy":
                        mtoModel.Lazy = "LazyRelation.NoProxy";
                        break;
                    case "false":
                        mtoModel.Lazy = "LazyRelation.NoLazy";
                        break;
                }

                string cascade = manyToOne.Attribute("cascade")?.Value;
                if (cascade != null)
                {
                    switch (cascade)
                    {
                        case "all-delete-orphan":
                            mtoModel.Cascade = "Cascade.All | Cascade.DeleteOrphans";
                            break;
                        case "all":
                            mtoModel.Cascade = "Cascade.All";
                            break;
                        case "save-update":
                            mtoModel.Cascade = "Cascade.Persist";
                            break;
                        case "none":
                            mtoModel.Cascade = "Cascade.None";
                            break;
                    }
                }

                mtoModel.NoUpdate = manyToOne.Attribute("update")?.Value == "false";
                mtoModel.NoInsert = manyToOne.Attribute("insert")?.Value == "false";
                mtoModel.NotNull = manyToOne.Attribute("not-null")?.Value == "true";
                mtoModel.Unique = manyToOne.Attribute("unique")?.Value;

                yield return mtoModel;
            }
        }

        private static IEnumerable<Property> ReadProperties(XElement root, XNamespace ns)
        {
            foreach (var propertyNode in root.Elements(ns.GetName("property")))
            {
                var propertyModel = new Property();

                propertyModel.Name = propertyNode.Attribute("name").Value;
                propertyModel.ColumnName = propertyNode.Attribute("column")?.Value;
                if (propertyModel.ColumnName == null)
                {
                    propertyModel.ColumnName = propertyNode.Element(ns.GetName("column"))?.Attribute("name")?.Value;
                }

                propertyModel.Unique = propertyNode.Attribute("unique")?.Value == "true";

                propertyModel.NoUpdate = propertyNode.Attribute("update")?.Value == "false";
                propertyModel.NoInsert = propertyNode.Attribute("insert")?.Value == "false";

                if (bool.TryParse(propertyNode.Attribute("not-null")?.Value, out bool notNull))
                {
                    propertyModel.NotNull = notNull;
                }
                else
                {
                   var notNullAttr = propertyNode.Element(ns.GetName("column"))?.Attribute("not-null")?.Value;
                   if (bool.TryParse(notNullAttr, out bool notNullPrp))
                   {
                       propertyModel.NotNull = notNullPrp;
                   }
                }

                if (int.TryParse(propertyNode.Attribute("length")?.Value, out int length))
                {
                    propertyModel.Length = length;
                }


                propertyModel.UniqueKey = propertyNode.Attribute("unique-key")?.Value
                    ?? propertyNode.Element(ns.GetName("column"))?.Attribute("unique-key")?.Value;;

                propertyModel.Formula = propertyNode.Attribute("formula")?.Value;
                
                if (propertyNode.Attribute("generated")?.Value != null)
                {
                    propertyModel.Generated = propertyNode.Attribute("generated").Value;
                }

                if (propertyNode.Attribute("access")?.Value != null)
                {
                    var xmlAccess = propertyNode.Attribute("access").Value;
                    switch (xmlAccess.ToLower())
                    {
                        case "property":
                            propertyModel.Access = "Accessor.Property";
                            break;
                        case "field.camelcase-underscore":
                            propertyModel.Access = "Accessor.Field";
                            break;
                    }
                }

                propertyModel.SqlType = propertyNode.Attribute("sql-type")?.Value;
                if (propertyModel.SqlType == null)
                {
                    propertyModel.SqlType = propertyNode.Descendants(ns.GetName("column")).FirstOrDefault()?.Attribute("sql-type")?.Value;
                }


                yield return propertyModel;
            }
        }

        private static List<CollectionInfo> ReadCollections(XElement xml, XNamespace ns, CollectionType collectionType)
        {
            List<CollectionInfo> foundCollections = new List<CollectionInfo>();
            foreach (var collectionNode in xml.Descendants(ns.GetName(collectionType.ToString().ToLower())))
            {
                var bagModel = new CollectionInfo();
                bagModel.Name = collectionNode.Attribute("name")?.Value;

                if (bool.TryParse(collectionNode.Attribute("inverse")?.Value, out bool inverse))
                {
                    bagModel.Inverse = inverse;
                }

                bagModel.Table = collectionNode.Attribute("table")?.Value;
                if (bool.TryParse(collectionNode.Attribute("lazy")?.Value, out bool bagLazy))
                    bagModel.Lazy = bagLazy;

                bagModel.BatchSize = collectionNode.Attribute("batch-size")?.Value;

                string cascade = collectionNode.Attribute("cascade")?.Value;
                if (cascade != null)
                {
                    switch (cascade)
                    {
                        case "all-delete-orphan":
                            bagModel.Cascade = "Cascade.All | Cascade.DeleteOrphans";
                            break;
                        case "all":
                            bagModel.Cascade = "Cascade.All";
                            break;
                        case "save-update":
                            bagModel.Cascade = "Cascade.Persist";
                            break;
                        case "delete":
                            bagModel.Cascade = "Cascade.Remove";
                            break;
                        case "none":
                            bagModel.Cascade = "Cascade.None";
                            break;
                    }
                }

                bagModel.OrderBy = collectionNode.Attribute("order-by")?.Value;
                bagModel.KeyColumn = collectionNode.Element(ns.GetName("key")).Attribute("column").Value;


                if (collectionNode.Element(ns.GetName("one-to-many")) != null)
                {
                    bagModel.RelType = "OneToMany";
                }

                var manyToManyRelation = collectionNode.Element(ns.GetName("many-to-many"));
                if (manyToManyRelation != null)
                {
                    bagModel.RelType = "ManyToMany";
                    bagModel.RelColumn = manyToManyRelation.Attribute("column").Value;
                    var notFound = manyToManyRelation.Attribute("not-found");
                    switch (notFound?.Value)
                    {
                        case "ignore":
                            bagModel.NotFound = "NotFoundMode.Ignore";
                            break;
                        case "exception":
                            bagModel.NotFound = "NotFoundMode.Exception";
                            break;
                        case null:
                            break;
                    }
                }

                var compositeElement = collectionNode.Element(ns.GetName("composite-element"));
                if (compositeElement != null)
                {
                    var compositeElementModel = new CompositeElementModel();
                    compositeElementModel.Parent = compositeElement.Element(ns.GetName("parent"))?.Attribute("name")?.Value;
                    var childProperties = ReadProperties(compositeElement, ns);
                    compositeElementModel.Properties.AddRange(childProperties);

                    bagModel.CompositeElement = compositeElementModel;
                }

                

                bagModel.CollectionType = collectionType;
                foundCollections.Add(bagModel);
            }

            return foundCollections;
        }
    }

    internal class CompositeElementModel 
    {
        public CompositeElementModel()
        {
            this.Properties = new List<Property>();
        }

        public string Parent { get; set; }
        public List<Property> Properties { get; set; }
    }

    internal class SubclassModel
    {
        public string ClassName { get;set; }

        public string DiscriminatorValue { get; set; }
        public string FullClassName { get; set; }
    }
}
