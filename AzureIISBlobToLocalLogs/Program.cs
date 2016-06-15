using System;
using System.IO;
using AzureIISBlobToLocalLogs.ConfigSection;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Globalization;
using System.Data.SQLite;
using AzureIISBlobToLocalLogs.DataModel;
using System.Collections.Concurrent;

namespace AzureIISBlobToLocalLogs
{
    /// <summary>
    /// POC in order to fetch the IIS logs from Azure Storage Blobs (Basically the WebApp or API... stats)
    /// 
    /// Idea come originally from http://madstt.dk/iis-logs-in-elasticsearch/, but quite modified
    /// 
    /// Work for now... Not totally stateful, since not saving in a database.
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
        private static readonly ConcurrentDictionary<string, IISBlobFileInfo> _blobInfoCache = new ConcurrentDictionary<string, IISBlobFileInfo>();

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
            // Do polling... thread fun starts here.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Updating each 10 seconds... (test)");
            Console.WriteLine("To stop the process please push \"ctrl+c\" (quit) since the job is running in the background");
            while (true)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10));

                FetchForPathElement(DateTime.UtcNow);
            }
        }

        /// <summary>
        /// Warm up and fetch the logs until now.
        /// </summary>
        private static void WarmUp()
        {
            GetAzureSection();
            CreateLocalDb();
            FetchBlobsUntilNow();
        }

        private static void CreateLocalDb()
        {
            // Model first
            // http://system.data.sqlite.org/downloads/1.0.101.0/sqlite-netFx46-setup-bundle-x86-2015-1.0.101.0.exe
            //bool isNew = false;
            //string sinceDbFileName = _config.SinceDbPath + "sinceDb.sqlite";
            //if (!File.Exists(sinceDbFileName))
            //{
            //    SQLiteConnection.CreateFile(sinceDbFileName);
            //    isNew = true;
            //}

            //if (isNew)
            //{
            //    using (var db_conn = new SQLiteConnection("Data Source=" + sinceDbFileName + ";Version=3;"))
            //    {
            //        db_conn.Open();
            //        string sql = "create table sinceDb (containerName varchar(255), uri text, dateSubFolder text, etag varchar(50), lastUpdate datetime, currentPosition bigint)";
            //        SQLiteCommand command = new SQLiteCommand(sql, db_conn);
            //        command.ExecuteNonQuery();
            //        db_conn.Close();
            //    }
            //}
        }

        private static CloudBlobClient _blobClient;
        private static void FetchBlobsUntilNow()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("storageIIS"));
            _blobClient = storageAccount.CreateCloudBlobClient();
            DateTime iisLogFetchDateTime = GetStartDateFromConfig();

            FetchForPathElement(iisLogFetchDateTime);
        }

        private static void FetchForPathElement(DateTime iisLogFetchDateTime)
        {
            // Improvement with a parallel foreach in order to play with multiple path (server logs) at once.
            foreach (PathElement pathElement in _config.Paths)
            {
                iisLogFetchDateTime = GetDataFromWatchPath(_blobClient, iisLogFetchDateTime, pathElement);
            }

            return;
        }

        private static DateTime GetDataFromWatchPath(CloudBlobClient blobClient, DateTime iisLogFetchDateTime, PathElement pathElement)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(pathElement.Container);

            // We want to fetch all until now, after we will attach and fetch only what's needed.
            while (iisLogFetchDateTime.CompareTo(DateTime.UtcNow) <= 0)
            {
                string hourPath = GetHourPath(pathElement.BasePath, iisLogFetchDateTime);

                // Loop over items within the container and output the length and URI.
                foreach (IListBlobItem item in container.ListBlobs(hourPath, true))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob cloudBlockBlob = (CloudBlockBlob)item;
                        IISBlobFileInfo ibfi;
                        if (!_blobInfoCache.TryGetValue(cloudBlockBlob.Uri.AbsoluteUri, out ibfi))
                        {
                            ibfi = new IISBlobFileInfo
                            {
                                ContainerName = container.Name,
                                CurrentPosition = 0, // Updated once finished.
                                Uri = cloudBlockBlob.Uri,
                                DateSubFolder = hourPath,
                                ETag = cloudBlockBlob.Properties.ETag,
                                LastModified = cloudBlockBlob.Properties.LastModified,
                                CurrentBlob = cloudBlockBlob,
                            };
                        }
                        else 
                            ibfi.CurrentBlob = cloudBlockBlob;
 
                        ProcessBlobFromPath(iisLogFetchDateTime, container, cloudBlockBlob, ibfi);
                    }
                }

                iisLogFetchDateTime = iisLogFetchDateTime.AddHours(1); // IIS Log paths are made by hours
            }

            return iisLogFetchDateTime;
        }

        private static void ProcessBlobFromPath(DateTime iisLogFetchDateTime, CloudBlobContainer container, CloudBlockBlob cloudBlockBlob, IISBlobFileInfo ibfi)
        {
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
                    // ibfi gets updated regarding the position he is in the blob.
                    WriteIISLogs(cloudBlockBlob, azureBlobReader, logFw, ibfi);
                }

                _blobInfoCache.AddOrUpdate(ibfi.Uri.AbsoluteUri, ibfi, (a, b) => ibfi);
            }
            catch (IOException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR THROWN");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        /// <summary>
        /// Write the IIS Logs into a file writer
        /// </summary>
        /// <param name="cloudBlockBlob">Azure block blob</param>
        /// <param name="azureBlobReader">The reader with the handle on the local log file</param>
        /// <param name="logSw">The writer on the log file</param>
        /// <param name="position">Poisition in the file</param>
        private static void WriteIISLogs(CloudBlockBlob cloudBlockBlob, StreamReader azureBlobReader, StreamWriter logSw, IISBlobFileInfo blobInfo)
        {
            // The position will become handy when we want to fetch only the last part of the log.
            azureBlobReader.BaseStream.Seek(blobInfo.CurrentPosition, SeekOrigin.Begin);

            try
            {
                while (!azureBlobReader.EndOfStream)
                {
                    var line = azureBlobReader.ReadLine();

                    // Ignore line that starts with a comment.
                    if (!line.StartsWith("#"))
                    {
                        logSw.WriteLine(line + "\t" + blobInfo.CurrentBlob.Name);
                    }
                }
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error while processing blob {0}", blobInfo.Uri);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            finally
            {
                logSw.Flush();

                // It's only when we flush that we can be certain that the data is writen. Better push the position here.
                blobInfo.CurrentPosition = cloudBlockBlob.Properties.Length;
            }
        }

        /// <summary>
        /// Go get the date we want to init the app from in order to fetch data
        /// </summary>
        /// <returns>The date</returns>
        private static DateTime GetStartDateFromConfig()
        {
            // We should start from choosen date... 
            DateTime dtStart = DateTime.Today;
            if (!string.IsNullOrEmpty(_config.InitFrom))
            {
                if (!DateTime.TryParseExact(_config.InitFrom, "yyyy/MM/dd/HH", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtStart))
                {
                    Console.WriteLine("Maybe an invalid date and start with today: {0}", dtStart);
                }

                Console.WriteLine("Start fetching from : {0}", dtStart);
            }

            return dtStart;
        }

        /// <summary>
        /// Generate the full path with the hour path (IIS Azure Blob Storage style)
        /// </summary>
        /// <param name="basePath">Base path in the container</param>
        /// <param name="dateTime">The date that we want to convert to a path</param>
        /// <returns>The full path hour path</returns>
        private static string GetHourPath(string basePath, DateTime dateTime)
        {
            return string.Format("{0}/{1}", basePath, dateTime.ToString("yyyy/MM/dd/HH", CultureInfo.InvariantCulture));
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
