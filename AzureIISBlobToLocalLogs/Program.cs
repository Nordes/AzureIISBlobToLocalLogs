using System;
using System.Collections.Generic;
using System.IO;
using AzureIISBlobToLocalLogs.ConfigSection;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Globalization;

namespace AzureIISBlobToLocalLogs
{
    /// <summary>
    /// POC in order to fetch the IIS logs from Azure Storage Blobs (Basically the WebApp or API... stats)
    /// 
    /// Idea come originally from http://madstt.dk/iis-logs-in-elasticsearch/, but quite modified
    /// </summary>
    class Program
    {
        /// <summary>
        /// Global for now, but it should be put elsewhere, like the majority of the code. It's a POC, don't look ^^.
        /// </summary>
        private static AzureBlobFileWatchSection _config;

        /// <summary>
        /// Not yet in use
        /// </summary>
        private static readonly IDictionary<string, long> _positionCache = new Dictionary<string, long>();

        static void Main(string[] args)
        {
            WarmUp();
            RunAndFetchData();
            Console.WriteLine("Push a key");
            Console.ReadKey();
        }

        /// <summary>
        /// Should fetch the data periodically having some sleep time
        /// </summary>
        private static void RunAndFetchData()
        {
            // TODO Idea is to have a configuration with a sleep time. (Min 10sec, max...)

            // Do polling... thread fun starts here.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Warm up and fetch the logs until now.
        /// </summary>
        private static void WarmUp()
        {
            GetAzureSection();
            FetchBlobsUntilNow();
        }

        private static void FetchBlobsUntilNow()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("storageIIS"));
            var blobClient = storageAccount.CreateCloudBlobClient();
            DateTime iisLogFetchDateTime = GetStartDateFromConfig();

            // Retrieve a reference to a container.
            foreach (PathElement pathElement in _config.Paths)
            {
                CloudBlobContainer container = blobClient.GetContainerReference(pathElement.Container);
                var blobs = new List<CloudBlockBlob>();

                // We want to fetch all until now, after we will attach and fetch only what's needed.
                while (iisLogFetchDateTime.CompareTo(DateTime.UtcNow) <= 0)
                {
                    string hourPath = GetHourPath(pathElement.Value, iisLogFetchDateTime);

                    // Loop over items within the container and output the length and URI.
                    foreach (IListBlobItem item in container.ListBlobs(hourPath, true))
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob cloudBlockBlob = (CloudBlockBlob)item;
                            Console.WriteLine("Block blob of length {0}: {1} // {2}", cloudBlockBlob.Properties.Length, cloudBlockBlob.Uri, cloudBlockBlob.Properties.ETag);
                            Console.WriteLine("Blob found: {0}", cloudBlockBlob.Uri);
                            blobs.Add(cloudBlockBlob);

                            try
                            {
                                string fileName = _config.LocalLogPath + container.Name + "-" + iisLogFetchDateTime.ToString("yyyy-MM-dd-HH") + ".log";

                                // TODO have a cleaner code
                                if (!File.Exists(fileName))
                                {
                                    using (var f = File.Create(fileName))
                                    {
                                        // For some reason the OpenCreate does not happen since I use file stream. Best to create and then Append.
                                    }
                                }
                                using (var azureBlobStream = cloudBlockBlob.OpenRead())
                                using (var azureBlobReader = new StreamReader(azureBlobStream))
                                using (var logFs = new FileStream(fileName, FileMode.Append, FileAccess.Write))
                                using (var logFw = new StreamWriter(logFs))
                                {
                                    WriteIISLogs(cloudBlockBlob, azureBlobReader, logFw);
                                }
                            }
                            catch (IOException)
                            {
                                Console.WriteLine("ERROR THROWN");
                            }
                        }
                    }

                    iisLogFetchDateTime = iisLogFetchDateTime.AddHours(1); // IIS Log paths are made by hours
                }
            }
        }

        private static void WriteIISLogs(CloudBlockBlob cloudBlockBlob, StreamReader azureBlobReader, StreamWriter logFw)
        {
            // The position will become handy when we want to fetch only the last part of the log.
            var originalPosition = GetPosition(cloudBlockBlob.Uri.AbsoluteUri);
            azureBlobReader.BaseStream.Seek(originalPosition, SeekOrigin.Begin);

            while (!azureBlobReader.EndOfStream)
            {
                var line = azureBlobReader.ReadLine();
                if (!line.StartsWith("#"))
                {
                    // Append to the log file.
                    logFw.WriteLine(line);
                }
            }

            logFw.Flush();
            // SavePositionCurrentPosition...
        }

        private static DateTime GetStartDateFromConfig()
        {
            // We should start from choosen date... 
            DateTime dtStart = DateTime.Today;
            if (!string.IsNullOrEmpty(_config.InitFrom))
            {
                dtStart = DateTime.ParseExact(_config.InitFrom, "yyyy/MM/dd/HH", CultureInfo.InvariantCulture);
                Console.WriteLine("Start fetching from : {0}", dtStart);
            }

            return dtStart;
        }

        private static string GetHourPath(string basePath, DateTime dtStart)
        {
            return basePath + "/" + dtStart.ToString("yyyy/MM/dd/HH", CultureInfo.InvariantCulture);
        }

        private static long GetPosition(string blobUri)
        {
            var key = "position_" + blobUri;

            long position;
            if (!_positionCache.TryGetValue(key, out position))
            {
                return 0; // Not found, so let's take the beginning of the file.
            }
            return position;
        }

        /// <summary>
        /// Get the config section.
        /// </summary>
        private static void GetAzureSection()
        {
            _config = (ConfigSection.AzureBlobFileWatchSection)System.Configuration.ConfigurationManager.GetSection("azureIISBlobFileWatchGroup/watch");
        }
    }
}
