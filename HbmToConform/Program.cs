﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HbmToConform
{
    class Program
    {
        static void Main(string[] args)
        {
            var directory = new DirectoryInfo(args[0]);

            foreach (var fileInfo in directory.GetFiles("*.hbm.xml"))
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

                if (bool.TryParse(classElement.Attribute("lazy")?.Value, out bool lazy))
                    model.Lazy = lazy;

                ReadId(classElement, ns, model);
                ReadProperties(xml, ns, model);
                ReadBagsMaps(xml, ns, classElement, model);
                ReadManyToOnes(classElement, ns, model);

                var conversion = new MapTemplate();
                conversion.Model = model;

                var map = conversion.TransformText();

                File.WriteAllText(Path.Combine(directory.FullName, onlyClass + "Map.cs"), map);
            }
        }

        private static void ReadId(XElement classElement, XNamespace ns, MappingModel model)
        {
            var idElement = classElement.Element(ns.GetName("id"));
            var idInfo = new IdInfo();
            idInfo.ColumnName = idElement.Attribute("column").Value;
            idInfo.Name = idElement.Attribute("name").Value;

            var stringGenerator = idElement.Element(ns.GetName("generator")).Attribute("class").Value;
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

            idInfo.UnsavedValue = idElement.Attribute("unsaved-value")?.Value;
            model.Id = idInfo;
        }

        private static void ReadManyToOnes(XElement classElement, XNamespace ns, MappingModel model)
        {
            foreach (var manyToOne in classElement.Descendants(ns.GetName("many-to-one")))
            {
                var mtoModel = new ManyToOneInfo();
                mtoModel.Name = manyToOne.Attribute("name").Value;
                mtoModel.ColumnName = manyToOne.Attribute("column").Value;

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

                model.ManyToOnes.Add(mtoModel);
            }
        }

        private static void ReadProperties(XDocument xml, XNamespace ns, MappingModel model)
        {
            foreach (var propertyNode in xml.Root.Descendants(ns.GetName("property")))
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
                

                model.Properties.Add(propertyModel);
            }
        }

        private static void ReadBagsMaps(XDocument xml, XNamespace ns, XElement classElement, MappingModel model)
        {
            foreach (var bagNode in xml.Root.Descendants(ns.GetName("bag")))
            {
                var bagModel = new BagInfo();
                bagModel.Name = bagNode.Attribute("name")?.Value;

                if (bool.TryParse(bagNode.Attribute("inverse")?.Value, out bool inverse))
                {
                    bagModel.Inverse = inverse;
                }

                bagModel.Table = bagNode.Attribute("table")?.Value;
                if (bool.TryParse(bagNode.Attribute("lazy")?.Value, out bool bagLazy))
                    bagModel.Lazy = bagLazy;

                string cascade = bagNode.Attribute("cascade")?.Value;
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
                        case "none":
                            bagModel.Cascade = "Cascade.None";
                            break;
                    }
                }

                bagModel.OrderBy = bagNode.Attribute("order-by")?.Value;
                bagModel.KeyColumn = bagNode.Element(ns.GetName("key")).Attribute("column").Value;

                if (bagNode.Element(ns.GetName("one-to-many")) != null)
                {
                    bagModel.RelType = "OneToMany";
                }
                if (bagNode.Element(ns.GetName("many-to-many")) != null)
                {
                    bagModel.RelType = "ManyToMany";
                    bagModel.RelColumn = bagNode.Element(ns.GetName("many-to-many")).Attribute("column").Value;
                }

                model.Bags.Add(bagModel);
            }
        }
    }
}
