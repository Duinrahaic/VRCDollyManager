namespace VRCDollyManager.Extensions;

/// <summary>
/// Provides utility methods for managing VRChat and DollyManager file system paths,
/// creating folders, and migrating the DollyManager database.
/// </summary>
public static class DollyManagerFilePaths
{
    /// <summary>
    /// Gets the path to the VRChat documents folder located in the user's "My Documents".
    /// </summary>
    /// <returns>The full path to the VRChat documents folder.</returns>
    public static string GetVRChatFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VRChat");

    /// <summary>
    /// Gets the path to the DollyManager folder located inside the VRChat documents folder.
    /// </summary>
    /// <returns>The full path to the DollyManager folder.</returns>
    public static string GetDollyManagerFolder() =>
        Path.Combine(GetVRChatFolder(), "DollyManager");

    /// <summary>
    /// Gets the path to the database file in its proper location inside the DollyManager folder.
    /// </summary>
    /// <returns>The full path to the DollyManager database file.</returns>
    public static string GetDatabaseFilePath() =>
        Path.Combine(GetDollyManagerFolder(), "vdm_database.sqlite");

    /// <summary>
    /// Gets the path to the old database file in the legacy VRChat folder location.
    /// </summary>
    /// <returns>The full path to the old database file.</returns>
    public static string GetOldDatabaseFilePath() =>
        Path.Combine(GetVRChatFolder(), "vdm_database.sqlite");

    /// <summary>
    /// Gets the path to the CameraPaths folder inside the VRChat documents folder.
    /// </summary>
    public static string GetCameraPathsFolder() =>
        Path.Combine(GetVRChatFolder(), "CameraPaths");
    
    
    public static string GetVDMConfigPath() =>
        Path.Combine(GetDollyManagerFolder(), "vdm_config.json");

    /// <summary>
    /// Creates a folder at the specified path if it does not already exist.
    /// </summary>
    /// <param name="path">The path of the folder to create.</param>
    /// <returns>The created folder path (or the existing path if already present).</returns>
    public static string CreateFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    /// <summary>
    /// Attempts to create a folder at the specified path if it does not already exist.
    /// </summary>
    /// <param name="path">The path of the folder to create.</param>
    /// <param name="createdPath">The resulting folder path, regardless of success or failure.</param>
    /// <returns>
    /// <c>true</c> if the folder was created successfully or already exists;
    /// <c>false</c> if an exception occurred while attempting to create the folder.
    /// </returns>
    public static bool TryCreateFolder(string path, out string createdPath)
    {
        createdPath = path;
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to migrate the DollyManager database from the old VRChat root folder
    /// into the DollyManager subfolder.
    /// </summary>
    /// <remarks>
    /// - If no old database exists, the method returns <c>true</c>.  
    /// - If the database is already in the new location, the method returns <c>true</c>.  
    /// - If migration fails (folder creation or file move errors), the method returns <c>false</c>.  
    /// </remarks>
    /// <returns><c>true</c> if the migration succeeded or was not needed; <c>false</c> otherwise.</returns>
    public static bool TryMigrateOldDatabase()
    {
        var oldDbPath = GetOldDatabaseFilePath();
        var newDbPath = GetDatabaseFilePath();

        // If there's no old DB, nothing to migrate → return true
        if (!File.Exists(oldDbPath))
            return true;

        // If the new DB already exists, treat it as "already migrated" → return true
        if (File.Exists(newDbPath))
            return true;

        // Ensure DollyManager folder exists
        if (!TryCreateFolder(GetDollyManagerFolder(), out var createdFolder))
            return false;

        try
        {
            File.Move(oldDbPath, newDbPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
