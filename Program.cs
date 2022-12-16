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

            // Enumerate Tables
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
            // Look for columns with names like password?

            // List Disks
            var fileService = storageAccount.GetFileService();
            var shares = fileService.GetFileShares();
            if (!shares.Any())
            {
                Console.WriteLine("\t\tNo shares...");
            }
            foreach (var share in shares)
            {
                Console.WriteLine($"\t\tShare: {share.Data.Name}");
                var shareClient = new ShareClient(connectionString, share.Data.Name);
                await ListDirectory(shareClient.GetRootDirectoryClient());

                
            }

            // List Blobs
            var blobClient = storageAccount.GetBlobService();
            var blobs = blobClient.GetBlobContainers();
            if (!blobs.Any())
            {
                Console.WriteLine("\t\tNo blobs...");
            }
            foreach (var blob in blobs)
            {
                Console.WriteLine($"\t\tBlob Container: {blob.Data.Name}");

                var blobContainerClient = new BlobContainerClient(connectionString, blob.Data.Name);
                var blobItems = blobContainerClient.GetBlobsAsync();

                await foreach (var blobItem in blobItems)
                {
                    Console.WriteLine($"\t\t\tBlob: {blobItem.Name}");
                    // Look for magic files?
                }
            }
        }
    }

    static List<string> ExcludedDirectories = new() { "IPC$", "PRINT$" };
    static List<string> InterestingDirectories = new() { "C$", "ADMIN$", "SCCMCONTENTLIB$" };

    static List<string> ExcludedExtensions = new() { 
        ".bmp", ".eps", ".gif", ".ico", ".jfi", ".jfif", ".jif",
        ".jpe", ".jpeg", ".jpg", ".png", ".psd", ".svg", ".tif",
        ".tiff", ".webp", ".xcf", ".ttf", ".otf", ".lock", ".css",
        ".less", ".admx", ".adml", ".xsd" 
    };
    static List<string> ExcludedFilepaths = new() { "jmxremote/.password/.template", "sceregvl/.inf" };
    static List<string> InterestingFilenames = new() { 
        "PASSW", "SECRET", "CREDENTIAL", "THYCOTIC", "CYBERARK", "ConsoleHost_history.txt", ".htpasswd",
        "LocalSettings.php", "database.yml", ".secret_token.rb", "knife.rb", "carrierwave.rb", "omniauth.rb",
        ".functions", ".exports", ".netrc", ".extra", ".npmrc", ".env", ".bashrc", ".profile", ".zshrc", ".bash_history",
        ".zsh_history", ".sh_history", "zhistory", ".irb_history", "credentials.xml", "SensorConfiguration.json", ".var",
        "Variables.dat", "Policy.xml", "unattend.xml", "Autounattend.xml", "proftpdpasswd", "filezilla.xml", "lsass.dmp",
        "lsass.exe.dmp", "hiberfil.sys", "MEMORY.DMP", "running-config.cfg", "startup-config.cfg", "running-config", 
        "startup-config", "cisco", "router", "firewall", "switch", "shadow", "pwd.db", "passwd", "Psmapp.cred", 
        "psmgw.cred", "backup.key", "MasterReplicationUser.pass", "RecPrv.key", "ReplicationUser.pass", "Server.key",
        "VaultEmergency.pass", "VaultUser.pass", "Vault.ini", "PADR.ini", "PARAgent.ini", "CACPMScanner.exe.config",
        "PVConfiguration.xml", "NTDS.DIT", "SYSTEM", "SAM", "SECURITY", ".tugboat", "logins.json", "SqlStudio.bin",
        ".mysql_history", ".psql_history", ".pgpass", ".dbeaver-data-sources.xml", "credentials-config.json", "dbvis.xml",
        "robomongo.json", ".git-credentials", "passwords.txt", "password.txt", "pass.txt", "accounts.txt", "passwords.doc",
        "passwords.docx", "pass.doc", "accounts.doc", "accounts.docx", "passwords.xls", "pass.xls", "accounts.xls", "pass.docx",
        "passwords.xlsx", "pass.xlsx", "accounts.xlsx", "secrets.txt", "secrets.doc", "secrets.xls", "secrets.docx",
        "secrets.xlsx", "recentservers.xml", "sftp-config.json", "mobaxterm.ini", "mobaxterm backup.zip", "confCons.xml",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "", ""
    };
    static List<string> InterestingExtensions = new() 
    { 
        ".psd1", ".psm1", ".ps1", ".aspx", ".ashx", ".asmx", ".asp", ".cshtml", ".cs", ".ascx", ".config" ,
        ".bat", ".cmd", ".yaml", ".yml", ".toml", ".xml", ".json", ".ini", ".inf", ".cnf", ".conf", ".properties", 
        ".env", ".dist", ".txt", ".sql", ".log", ".sqlite", ".sqlite3", ".fdb", ".tfvars", ".jsp", ".do", ".java",
        ".cfm", ".js", ".cjs", ".mjs", ".ts", ".tsx", ".ls", ".es6", ".es", ".php", ".phtml", ".inc", ".php3",
        ".php5", ".php7", ".pl", ".py", ".rb", ".vbs", ".vbe", ".wsf", ".wsc", ".hta", ".pem", ".der", ".pfx", ".pk12",
        ".p12", ".pkcs12", ".mdf", ".sdf", ".sqldump", ".bak", ".wim", ".ova", ".ovf", ".cscfg", ".dmp", ".cred", ".pass",
        ".pcap", ".cap", ".pcapng", ".kdbx", ".kdb", ".psafe3", ".kwallet", ".keychain", ".agilekeychain", ".rdg",
        ".rtsz", ".rtsx", ".ovpn", ".rdp", ".ppk"
    };
    static List<string> InterestingFilepaths = new()
    {
        "jenkins/.plugins/.publish_over_ssh/.BapSshPublisherPlugin.xml",
        "control/customsettings.ini",
        ".aws",
        "doctl/config.yaml",
        ".ssh",
        ".azure"
    };

    static async Task ListDirectory(ShareDirectoryClient directoryClient)
    {
        var filesAndFolders = directoryClient.GetFilesAndDirectoriesAsync();
        await foreach (var file in filesAndFolders)
        {
            var filePath = (string.IsNullOrEmpty(directoryClient.Path) ? "" : $"/{directoryClient.Path}") + "/" + file.Name;

            if (file.IsDirectory)
            {
                // Skip certain directories for opsec and speeeeed
                if (ExcludedDirectories.Contains(file.Name.ToUpper()))
                {
                    continue;
                }
                if (InterestingDirectories.Contains(file.Name.ToUpper()))
                {
                    Console.WriteLine($"\t\t\tFound interesting folder! ({filePath})");
                }

                // Don't bother printing folders, just move on :)
                await ListDirectory(directoryClient.GetSubdirectoryClient(file.Name));
            }
            else
            {
                // Check filename
                if(ExcludedExtensions.Any(f => file.Name.ToUpper().EndsWith(f.ToUpper())))
                {
                    continue;
                }
                if (ExcludedFilepaths.Any(f => filePath.ToUpper().EndsWith(f.ToUpper())))
                {
                    continue;
                }

                if (InterestingFilepaths.Any(f => filePath.ToUpper().EndsWith(f.ToUpper())))
                {
                    Console.WriteLine($"\t\t\tFound interesting file path! ({filePath})");

                }
                else if (InterestingFilenames.Any(f => f.ToUpper().Contains(file.Name.ToUpper())))
                {
                    Console.WriteLine($"\t\t\tFound interesting file name! ({filePath})");
                }
                else if (InterestingExtensions.Any(f => file.Name.ToUpper().EndsWith(f.ToUpper())))
                {
                    Console.WriteLine($"\t\t\tFound interesting file extension! ({filePath})");
                }

                // TODO: Extend this to blobs :)

            }
        }
    }
}