using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;

// https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile?view=netframework-4.7.2
// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-compress-and-extract-files

namespace BackupperKISS
{
  public class Backupper
  {
    Parameters parameters;

    public Backupper(Parameters parameters)
    {
      this.parameters = parameters;

      if (! Directory.Exists(parameters.sourceDir))
      {
        throw new Exception("Source directory isn't exists! {parameters.sourceDir}");
      }

      if (!Directory.Exists(parameters.targetDir))
      {
        if (parameters.createRootDirectory)
        {
          Directory.CreateDirectory(parameters.targetDir);
        }
        else
        {
          throw new Exception("Target directory isn't exists! {parameters.targetDir}");
        }
      }

      if (string.IsNullOrWhiteSpace(parameters.targetExtension))
      {
        this.parameters.targetExtension = Parameters.targetExtensionDefault;
      }

      if (parameters.includeFiles == null)
      {
        this.parameters.includeFiles = new List<string>();
      }

      if ((this.parameters.includeFiles.Count < 1) && (! String.IsNullOrWhiteSpace(Parameters.includeFileWildcardDefault)))
      {
        this.parameters.includeFiles.Add(Parameters.includeFileWildcardDefault);
      }

      this.parameters.sourceDir = AddDirectorySeparatorChar(parameters.sourceDir);
      this.parameters.targetDir = AddDirectorySeparatorChar(parameters.targetDir);
    }

    public (int copied, int droped) Backup()
    {
      string relativePath = String.Empty;        

      return Backup(relativePath);
    }  
    
    private const string timeFormater = "yyyyMMdd_hhmmss";
    private const char   partSelector = '~';

    private (int copied, int droped) Backup(string relativePath)
    {
      int copied = 0;
      int droped = 0;

      if (RequiredSubDirectory(relativePath))
      {
        var fullSourceFilenames = GetSelectedFilenames(relativePath);

        foreach (var fullSourceFilename in fullSourceFilenames)
        {
          string sourceFilename     = Path.GetFileName(fullSourceFilename);
          string fullTargetFilename = Path.Combine(parameters.targetDir + relativePath, sourceFilename + parameters.targetExtension);

          var    sourceLWT          = File.GetLastWriteTime(fullSourceFilename);
          string targetZipEntryName = sourceFilename + partSelector + sourceLWT.ToString(timeFormater);
          bool   updateZipDate      = false;

          if (File.Exists(fullTargetFilename))
          {
            if (parameters.quickCheck && (sourceLWT == File.GetLastWriteTime(fullTargetFilename)))
            { // Quick check [shortcut!]: if date equal, We *hope* the content is unchanged!
              updateZipDate = false;
            }
            else
            {
              bool found = false;

              using (ZipArchive archive = ZipFile.Open(fullTargetFilename, ZipArchiveMode.Update))
              {
                var entries = new List<ZipArchiveEntry>();

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                  if (entry.Name == targetZipEntryName)
                  {
                    found = true;
                  }
                  else
                  {
                    entries.Add(entry);
                  }
                }

                if (!found)
                {
                  archive.CreateEntryFromFile(fullSourceFilename, targetZipEntryName);
                  copied++;
                }

                //

                entries.Sort((a, b) => b.Name.CompareTo(a.Name));               // Reverse order! (because 'b'.compare and parameter 'a')

                int  keepCount = parameters.prevVerFilesCount;                  // keep count of stored archive files
                long keepSize  = parameters.prevVerFilesSize;                   // keep size in kilobytes
                int  keepDays  = parameters.prevVerFilesAge;                    // keep age in days

                if (keepCount < 1)
                {
                  keepCount = Parameters.prevVerFilesCountDefault;
                }

                if (keepSize < 1)
                {
                  keepSize = Parameters.prevVerFilesSizeDefault;
                }

                if (keepDays < 1)
                {
                  keepDays = Parameters.prevVerFilesAgeDefault;
                }

                int  sumCount  = 0;
                long sumSize   = 0;
                var  keepDate  = (DateTime.Now.AddDays(-keepDays)).ToString(timeFormater);

                foreach (var entry in entries)
                {
                  int partSelectorPos = entry.Name.LastIndexOf(partSelector);

                  if ((entry.Name.Length != targetZipEntryName.Length) ||
                      (partSelectorPos   != targetZipEntryName.LastIndexOf(partSelector)))
                  { // drop it, it's not my content
                    entry.Delete();
                    droped++;
                  }
                  else if (entry.Name.Substring(partSelectorPos + 1).CompareTo(keepDate) < 1)
                  { // drop it, it's too old
                    entry.Delete();
                    droped++;
                  }
                  else
                  {
                    sumCount++;
                    sumSize += entry.CompressedLength / 1024;

                    if ((sumCount > keepCount) || (sumSize > keepSize))
                    { // drop it
                      entry.Delete();
                      droped++;
                    }
                  }
                }
              }

              updateZipDate = true;
            }
          }
          else
          { // Create new ZIP
            using (ZipArchive archive = ZipFile.Open(fullTargetFilename, ZipArchiveMode.Create))
            {
              archive.CreateEntryFromFile(fullSourceFilename, targetZipEntryName);
              updateZipDate = true;
              copied++;
            }
          }

          if (updateZipDate)
          {
            File.SetAttributes(fullTargetFilename, FileAttributes.ReadOnly);
            File.SetLastWriteTime(fullTargetFilename, sourceLWT);
          }





          // TODO: ...!!!...
        }
      }

      return (copied, droped);
    }

    private bool RequiredSubDirectory(string relativePath)
    {
      string targetFilesPath = Path.Combine(parameters.targetDir + relativePath);

      if (! Directory.Exists(targetFilesPath))
      {
        if (parameters.existSubdirectories)
        {
          return false;                                                         // Backup only if target subdirectory exists too.
        }
        else
        {
          Directory.CreateDirectory(targetFilesPath);
        }
      }

      return true;
    }

    private string AddDirectorySeparatorChar(string path)
    {
      return (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) ? path : (path + Path.DirectorySeparatorChar);
    }

    private IEnumerable<string> GetSelectedFilenames(string relativePath)
    {
      var files = new HashSet<string>();

      foreach (var wildcard in parameters.includeFiles)
      {
        var tempNames = Directory.GetFiles(parameters.sourceDir + relativePath, wildcard);

        foreach (var tempName in tempNames)
        {
          bool enabled = true;

          if (parameters.checkArchiveBit)
          {
            var attr = File.GetAttributes(tempName);

            if ((attr & FileAttributes.Archive) != FileAttributes.Archive)
            {
              enabled = false;
            }
          }

          if (enabled)
          { 
            files.Add(tempName);                                                      // This will filter duplicate filenames
          }
        }        
      }

      return files;
    }
  }  
}


/*
  var attr = File.GetAttributes(filename);
  File.SetAttributes(filename, attr & ~FileAttributes.Archive);                         // removed
*/
