using Microsoft.Identity.Web;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Resources;
using Azure.Storage.Sas;
using System.Threading;
using Azure.ResourceManager.Storage.Models;
using System.Reflection.Metadata;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace AzureSnaffler;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Authenticate to Azure and create the top-level ArmClient
        var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
        var armClient = new ArmClient(credential);

        // Get subscriptions
        var subscriptions = armClient.GetSubscriptions();

        if (!subscriptions.Any())
        {
            Console.WriteLine("No subscriptions found!");
            return;
        }

        Console.WriteLine("Subscriptions found:");
        foreach (var sub in subscriptions)
        {
            Console.WriteLine($"\t- {sub.Data.DisplayName} ({sub.Id})");
        }
        Console.WriteLine();

        // Create a resource identifier, then get the subscription resource
        var resourceIdentifier = new ResourceIdentifier(subscriptions.First().Id);
        var subscription = armClient.GetSubscriptionResource(resourceIdentifier);

        // Get all storage accounts for subscription
        var storageAccounts = subscription.GetStorageAccountsAsync();

        // Iterate storage accounts
        Console.WriteLine("Storage accounts found:");
        await foreach (var storageAccount in storageAccounts)
        {
            Console.WriteLine($"\t+ {storageAccount.Data.Name}");

            // Create connection string for hitting all these resources properly (and it's opsec safer!)
            var accessKey = storageAccount.GetKeys().FirstOrDefault();
            if (accessKey == null)
            {
                Console.WriteLine($"No access keys found for storage account :(");
                continue;
            }
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccount.Data.Name};AccountKey={accessKey.Value};EndpointSuffix=core.windows.net";

            // Go through all the tables looking for juicy things
            //TODO: Uncomment this once snaffling logic is implemented
            //EnumerateTables(storageAccount);

            // Go through all the shares looking for juicy things
            await EnumerateShares(storageAccount, connectionString);

            // Go through all the blobs looking for juicy things
            await EnumerateBlobs(storageAccount, connectionString);
        }
    }

    static async Task EnumerateBlobs(StorageAccountResource storageAccount, string connectionString)
    {
        try
        {
            var blobClient = storageAccount.GetBlobService();
            var blobs = blobClient.GetBlobContainers();
            if (!blobs.Any())
            {
                Console.WriteLine("\t\tNo blobs...");
            }
            foreach (var blob in blobs)
            {
                try
                {
                    Console.WriteLine($"\t\tBlob Container: {blob.Data.Name}");

                    var blobContainerClient = new BlobContainerClient(connectionString, blob.Data.Name);
                    var blobItems = blobContainerClient.GetBlobsAsync();

                    await foreach (var blobItem in blobItems)
                    {
                        try
                        {
                            // Blob items are always fully nested, there's no "folder" to search in the middle
                            // TODO: Investigate if we can (or should) scan the folders

                            // Skip certain file extensions and paths to save resources
                            if (Rules.ShouldSkipBlob(blobItem.Name))
                            {
                                continue;
                            }

                            // Look for anything cool left over
                            if (Rules.ShouldRaiseBlob(blobItem.Name, out string interestReason))
                            {
                                Console.WriteLine($"\t\t\tFound interesting blob {interestReason}! ({blobItem.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Something went wrong accessing blobs for container ({blob.Data.Name}): {ex.Message}");
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Something went wrong getting blobs: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something went wrong enumerating blobs: " + ex.Message);
        }
    }

    static async Task EnumerateShares(StorageAccountResource storageAccount, string connectionString)
    {
        try
        {
            var fileService = storageAccount.GetFileService();
            var shares = fileService.GetFileShares();
            if (!shares.Any())
            {
                Console.WriteLine("\t\tNo shares...");
            }
            foreach (var share in shares)
            {
                try
                {
                    Console.WriteLine($"\t\tShare: {share.Data.Name}");
                    var shareClient = new ShareClient(connectionString, share.Data.Name);
                    await ListDirectory(shareClient.GetRootDirectoryClient());
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Something went wrong accessing share (" + share.Data.Name + "): " + ex.Message);
                }
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine("Something went wrong enumerating shares: " + ex.Message);
        }
    }

    static void EnumerateTables(StorageAccountResource storageAccount)
    {
        try
        {
            var tableService = storageAccount.GetTableService();
            var tables = tableService.GetTables();
            if (!tables.Any())
            {
                Console.WriteLine("\t\tNo tables...");
            }
            foreach (var table in tables)
            {
                Console.WriteLine($"\t\tTable: {table.Data.Name} {table.Data.TableName}");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine("Something went wrong enumerating tables: " + ex.Message);
        }
        // Look for columns with names like password?
    }

    static async Task ListDirectory(ShareDirectoryClient directoryClient)
    {
        try
        {
            var filesAndFolders = directoryClient.GetFilesAndDirectoriesAsync();
            await foreach (var file in filesAndFolders)
            {
                var filePath = (string.IsNullOrEmpty(directoryClient.Path) ? "" : $"/{directoryClient.Path}") + "/" + file.Name;

                if (file.IsDirectory)
                {
                    // Skip certain directories for opsec and speeeeed
                    if (Rules.ShouldSkipFolder(file.Name))
                    {
                        continue;
                    }
                    if (Rules.ShouldRaiseFolder(file.Name))
                    {
                        Console.WriteLine($"\t\t\tFound interesting folder! ({filePath})");
                    }

                    // Don't bother printing folders, just move on :)
                    await ListDirectory(directoryClient.GetSubdirectoryClient(file.Name));
                }
                else
                {
                    // Skip certain file extensions and paths to save resources
                    if (Rules.ShouldSkipFile(filePath, file.Name))
                    {
                        continue;
                    }

                    // Look for anything cool left over
                    if (Rules.ShouldRaiseFile(filePath, file.Name, out string interestReason))
                    {
                        Console.WriteLine($"\t\t\tFound interesting file {interestReason}! ({filePath})");
                    }
                }
            }
        }
        catch(RequestFailedException)
        {
            // This is fine, not allowed to see in there :)
        }
    }
}