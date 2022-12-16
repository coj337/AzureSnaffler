using Microsoft.Identity.Client;
using System.Linq;

namespace AzureSnaffler;

public static class Rules
{
    public static List<string> ExcludedDirectories = new() { "IPC$", "PRINT$" };

    public static List<string> InterestingDirectories = new() { "C$", "ADMIN$", "SCCMCONTENTLIB$" };

    public static List<string> ExcludedExtensions = new() 
    { 
        ".bmp", ".eps", ".gif", ".ico", ".jfi", ".jfif", ".jif",
        ".jpe", ".jpeg", ".jpg", ".png", ".psd", ".svg", ".tif",
        ".tiff", ".webp", ".xcf", ".ttf", ".otf", ".lock", ".css",
        ".less", ".admx", ".adml", ".xsd" 
    };

    public static List<string> ExcludedFilepaths = new() { "jmxremote/.password/.template", "sceregvl/.inf" };

    public static List<string> InterestingFilenames = new() 
    { 
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
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"
    };

    public static List<string> InterestingExtensions = new() 
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

    public static List<string> InterestingFilepaths = new()
    {
        "jenkins/.plugins/.publish_over_ssh/.BapSshPublisherPlugin.xml",
        "control/customsettings.ini",
        ".aws",
        "doctl/config.yaml",
        ".ssh",
        ".azure"
    };

    public static bool ShouldSkipFolder(string folderName)
    {
        if (ExcludedDirectories.Any(f => f.ToUpper().Contains(folderName.ToUpper())))
        {
            return true;
        }
        return false;
    }

    public static bool ShouldRaiseFolder(string folderName)
    {
        if (InterestingDirectories.Any(f => f.ToUpper().Contains(folderName.ToUpper())))
        {
            return true;
        }
        return false;
    }

    public static bool ShouldSkipFile(string filePath, string fileName)
    {
        if (
            ExcludedExtensions.Any(f => fileName.ToUpper().EndsWith(f.ToUpper())) ||
            ExcludedFilepaths.Any(f => filePath.ToUpper().EndsWith(f.ToUpper()))
        )
        {
            return true;
        }
        return false;
    }

    public static bool ShouldRaiseFile(string filePath, string fileName, out string interestReason)
    {
        if (InterestingFilepaths.Any(f => filePath.ToUpper().EndsWith(f.ToUpper())))
        {
            interestReason = "path";
            return true;
        }
        else if (InterestingFilenames.Any(f => f.ToUpper().Contains(fileName.ToUpper())))
        {
            interestReason = "name";
            return true;
        }
        else if (InterestingExtensions.Any(f => fileName.ToUpper().EndsWith(f.ToUpper())))
        {
            interestReason = "extension";
            return true;
        }

        interestReason = "";
        return false;
    }

    public static bool ShouldSkipBlob(string blobPath)
    {
        if (
            ExcludedDirectories.Any(f => blobPath.ToUpper().Contains(f.ToUpper())) ||
            ExcludedExtensions.Any(f => blobPath.ToUpper().EndsWith(f.ToUpper())) ||
            ExcludedFilepaths.Any(f => blobPath.ToUpper().EndsWith(f.ToUpper()))
        )
        {
            return true;
        }

        return false;
    }

    public static bool ShouldRaiseBlob(string blobPath, out string interestReason)
    {
        if (InterestingDirectories.Any(f => blobPath.ToUpper().Contains(f.ToUpper())))
        {
            interestReason = "directory";
            return true;
        }
        if (InterestingFilepaths.Any(f => blobPath.ToUpper().EndsWith(f.ToUpper())))
        {
            interestReason = "path";
            return true;
        }
        else if (InterestingFilenames.Any(f => blobPath.ToUpper().EndsWith(f.ToUpper())))
        {
            interestReason = "name";
            return true;
        }
        else if (InterestingExtensions.Any(f => blobPath.ToUpper().EndsWith(f.ToUpper())))
        {
            interestReason = "extension";
            return true;
        }

        interestReason = "";
        return false;
    }
}
