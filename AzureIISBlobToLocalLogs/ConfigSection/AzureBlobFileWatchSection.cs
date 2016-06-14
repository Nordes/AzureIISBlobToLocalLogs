using System;
using System.Configuration;

namespace AzureIISBlobToLocalLogs.ConfigSection
{
    /// <summary>
    /// https://msdn.microsoft.com/en-us/library/2tw134k3.aspx
    /// </summary>
    /// <seealso cref="System.Configuration.ConfigurationSection" />
    public class AzureBlobFileWatchSection : ConfigurationSection
    {
        // Create a "remoteOnly" attribute.
        [ConfigurationProperty("localLogPath", DefaultValue = "c:\\logs\\", IsRequired = false)]
        public string LocalLogPath
        {
            get
            {
                return (string)this["localLogPath"];
            }
            set
            {
                this["localLogPath"] = value;
            }
        }

        [ConfigurationProperty("initFrom", IsRequired = false)]
        public string InitFrom
        {
            get
            {
                return (string)this["initFrom"];
            }
            set
            {
                this["initFrom"] = value;
            }
        }

        [ConfigurationProperty("paths", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PathElements), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public PathElements Paths
        {
            get
            {
                PathElements paths = (PathElements)base["paths"];

                return paths;
            }

            set
            {
                PathElements paths = value;
            }

        }
    }

    public class PathElements : ConfigurationElementCollection
    {
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.AddRemoveClearMap;
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new PathElement();
        }

        protected override Object GetElementKey(ConfigurationElement element)
        {
            return ((PathElement)element).Name;
        }

        public PathElement this[int index]
        {
            get
            {
                return (PathElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        new public PathElement this[string Name]
        {
            get
            {
                return (PathElement)BaseGet(Name);
            }
        }

        protected override void BaseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);

            // Your custom code goes here.

        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);

            // Your custom code goes here.

        }

        public void Remove(string name)
        {
            BaseRemove(name);

            // Your custom code goes here.

        }

        public void Clear()
        {
            BaseClear();

            // Your custom code goes here.
            Console.WriteLine("UrlsCollection: {0}", "Removed entire collection!");
        }

    }

    public class PathElement : ConfigurationElement
    {
        //[StringValidator(InvalidCharacters = "~!@#$%^&*()[]{}/;'\"|\\", MinLength = 1, MaxLength = 60)]
        [ConfigurationProperty("name", IsRequired = true)]
        public String Name
        {
            get
            {
                return (String)this["name"];
            }
            set
            {
                this["name"] = value;
            }
        }

        [ConfigurationProperty("basePath", IsRequired = true)]
        public string BasePath
        {
            get
            { return (string)this["basePath"]; }
            set
            { this["basePath"] = value; }
        }

        [ConfigurationProperty("container", IsRequired = true)]
        public string Container
        {
            get
            { return (string)this["container"]; }
            set
            { this["container"] = value; }
        }
    }
}
