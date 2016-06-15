using Microsoft.WindowsAzure.Storage.Blob;
using System;

namespace AzureIISBlobToLocalLogs.DataModel
{
    /// <summary>
    /// IIS blob file info. It gives the information about the blob and where we are. We will store this in our sinceDb
    /// </summary>
    public class IISBlobFileInfo
    {
        public string ContainerName { get; set; }
        public Uri Uri { get; set; }
        public string DateSubFolder { get; set; }
        public string ETag { get; set; }

        /// <summary>
        /// Expressed in UTC value
        /// </summary>
        public DateTimeOffset? LastModified { get; set; }

        public CloudBlockBlob CurrentBlob { get; set; }

        public long CurrentPosition { get; set; }
    }
}
