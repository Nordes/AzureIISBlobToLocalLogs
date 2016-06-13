# AzureIISBlobToLocalLogs
Currently a POC, nothing more. Azure WebApp are nice but the IIS logs are stored in a Blob type instead of a Azure Table, it makes it hard to keep in synch on a local machine in order to analyse the logs.

# What is the point of ....  
It should download the Azure Storage Blob "IIS Logs" for the WebApps and then keep in synch once it is started.

# Todo
Create a small db file that says what have been done to be sure to not reparse more than once the files.
Integrate with LogStash the result in order to have nice curves in Kibana
Refactor the code to have something "nice", since it does not correspond to my standards at all.
