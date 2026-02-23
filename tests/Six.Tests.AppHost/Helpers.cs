namespace Six.KeycloakAdmin.Tests.AppHost;

/// <summary>
/// Provides default configuration values for Keycloak integration tests.
/// </summary>
internal static class KeycloakAdminDefaults
{
  /// <summary>
  /// Determines if the specified directory is a solution root directory by checking for .sln or .slnx files.
  /// </summary>
  /// <param name="directoryPath">The directory path to check.</param>
  /// <returns>True if the directory contains solution files, false otherwise.</returns>
  public static bool IsSolutionRootDirectory(string directoryPath)
  {
    var solutionFiles = Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);
    var solutionXFiles = Directory.GetFiles(directoryPath, "*.slnx", SearchOption.TopDirectoryOnly);
    
    return solutionFiles.Length > 0 || solutionXFiles.Length > 0;
  }
  
  
  /// <summary>
  /// Recursively searches for a file in the specified directory and all its subdirectories.
  /// </summary>
  /// <param name="directoryPath">The directory path to search in.</param>
  /// <param name="fileName">The name of the file to search for.</param>
  /// <returns>The absolute path to the file if found, null otherwise.</returns>
  public static string? SearchFileRecursively(string directoryPath, string fileName)
  {
    try
    {
      // Search in current directory
      var filePath = Path.Combine(directoryPath, fileName);
      if (File.Exists(filePath))
      {
        return filePath;
      }
      
      // Search in subdirectories
      var subdirectories = Directory.GetDirectories(directoryPath);
      foreach (var subdirectory in subdirectories)
      {
        var foundFilePath = SearchFileRecursively(subdirectory, fileName);
        if (foundFilePath is not null)
        {
          return foundFilePath;
        }
      }
    }
    catch (UnauthorizedAccessException)
    {
      // Skip directories we don't have access to
    }
    catch (DirectoryNotFoundException)
    {
      // Skip directories that no longer exist
    }
    
    return null;
  }
}