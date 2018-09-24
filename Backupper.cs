using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;

// https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile?view=netframework-4.7.2
// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-compress-and-extract-files

namespace BackupperKISS
{
  public class Backupper
  {
    private Parameters parameters;

    public Backupper(Parameters parameters)
    {
      this.parameters = parameters;

      if (!Directory.Exists(parameters.sourceDir))
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

      parameters.Reviser();      

      this.parameters.sourceDir = AddDirectorySeparatorChar(parameters.sourceDir);
      this.parameters.targetDir = AddDirectorySeparatorChar(parameters.targetDir);
    }

    static Backupper()
    {
      bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

      isCaseInsensitiveFilenames = isWindows;              
    }

    private Stack<Parameters> paramStack = new Stack<Parameters>();

    public (int copied, int droped) Backup(string relativePath = null)
    {
      (int copied, int droped) result = (0, 0);

      if (string.IsNullOrWhiteSpace(relativePath))
      {
        relativePath = String.Empty;
      }
      else
      {
        relativePath = AddDirectorySeparatorChar(relativePath);
      }

      //

      string parametersFilename      = Path.Combine(SourceAbsolutePath(relativePath), Parameters.parameterFileNameDefault);
      bool isParameterDefinitionFile = File.Exists(parametersFilename);

      if (isParameterDefinitionFile)
      {
        paramStack.Push(parameters);
        parameters = parameters.Clone();
        parameters.LoadFrom(parametersFilename);
      }

      if (string.IsNullOrWhiteSpace(relativePath))
      {
        parameters.Display("--- parameters (root) ---");
      }

      if (parameters.writeDebugText)
      {
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
          parameters.Display($"--- parameters ({relativePath}) ---");
        }

        var debugFilename = parametersFilename + "__Debug.txt";
        parameters.SaveTo(debugFilename);

        File.AppendAllText(debugFilename, $"// RelativePath: {relativePath}\n" +
                                          $"// Timestamp: {DateTime.Now.ToString("o")}");
      }

      //     

      if (RequiredSubDirectory(relativePath))
      {
        try
        {
          switch (parameters.method)
          {
            case MethodParameter.Singly:
              result = BackupSingly(relativePath);

              break;

            case MethodParameter.Groups:
              throw new NotImplementedException("MethodParameter.Groups");

            case MethodParameter.Copy:
              throw new NotImplementedException("MethodParameter.Copy");

            case MethodParameter.Dont:
              break;

            default:
              Debug.Fail($"Internal error! Invalid parameters.method value: {parameters.method}");
              break;
          }
        }
        catch (Exception exc)
        {
          Program.ShowErrorMessageAndUsage(exc.Message, (int)ExitCode.ZipError, false);
        }
      }

      if (isParameterDefinitionFile)
      {
        parameters = paramStack.Pop();
      }

      if (parameters.writeDebugText)
      {
        var debugFilename = parametersFilename + "__Debug.txt";

        File.AppendAllText(debugFilename, $"// summary >> copied: {result.copied}  droped: {result.droped}");
      }

      return result;
    }

    private const string timeFormater = "yyyyMMdd_HHmmss";
    private const char partSelector = '~';

    public static readonly bool isCaseInsensitiveFilenames = true;

    private (int copied, int droped) BackupSingly(string relativePath)
    {
      int copied = 0;
      int droped = 0;

      if (RequiredSubDirectory(relativePath))
      {
        var fullSourceFilenames = GetSelectedFilenames(relativePath);

        foreach (var fullSourceFilename in fullSourceFilenames)
        {
          string sourceFilename        = Path.GetFileName(fullSourceFilename);
          string fullTargetFilename = Path.Combine(TargetAbsolutePath(relativePath), sourceFilename + parameters.targetExtension);

          var result = BackupToZip(fullSourceFilename, fullTargetFilename, false);

          copied += result.copied;
          droped += result.droped;
        }

        if (parameters.copySubdirectories)
        {
          var directories = Directory.GetDirectories(SourceAbsolutePath(relativePath));

          foreach (var subdir in directories)
          {
            var relSubDir = subdir.Substring(parameters.sourceDir.Length);

            var result = Backup(relSubDir);                                                     // recursion / half recursion

            copied += result.copied;
            droped += result.droped;
          }
        }
      }

      return (copied, droped);
    }

    private (int copied, int droped) BackupToZip(string fullSourceFilename, string fullTargetFilename, bool moreThanOneFileMode)
    {  
      int copied = 0;
      int droped = 0;

      string  sourceFilename     = Path.GetFileName(fullSourceFilename);
      var     sourceLWT          = File.GetLastWriteTime(fullSourceFilename);
      string  targetZipEntryName = Path.GetFileNameWithoutExtension(sourceFilename) + partSelector + sourceLWT.ToString(timeFormater) + Path.GetExtension(sourceFilename);

      bool updateZipDate = false;

      if (File.Exists(fullTargetFilename))
      {
        if (!moreThanOneFileMode && parameters.quickCheck && (sourceLWT == File.GetLastWriteTime(fullTargetFilename)))
        { // Quick check [shortcut!]: if date equal, We *hope* the content is unchanged!
          updateZipDate = false;
        }
        else
        {
          bool foundThisTargetFile = false;

          File.SetAttributes(fullTargetFilename, File.GetAttributes(fullTargetFilename) & ~FileAttributes.ReadOnly);   // remove readonly bit

          var cancellationTokenSource = new CancellationTokenSource();
          var cancellationToken = cancellationTokenSource.Token;
          Task.Factory.StartNew(() =>
          {
            Thread.Sleep(5000);
            if (!cancellationToken.IsCancellationRequested)
            { // because: Unfortunately ZipFile.Open() can run to infinitive loop.
              Program.ShowErrorMessageAndUsage($"Infinitive loop error at open {targetZipEntryName} target archive file!\n  (for store {sourceFilename} source file)", (int)ExitCode.ZipError, false);
              //throw new Exception($"Infinitive loop error at open {targetZipEntryName} archive file! (for store {sourceFilename} source file)");      
            }
          },
                cancellationToken);


          using (ZipArchive archive = ZipFile.Open(fullTargetFilename, ZipArchiveMode.Update))                // Unfortunately it can run to infinitive loop.
          {
            cancellationTokenSource.Cancel();                             // ZipFile.Open() succesfull, don't throw exception in 'protectiveTask'

            var entries = new List<ZipArchiveEntry>();                    // for select old and above the headcount existing entries of Zip to delete these.

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
              if (entry.Name == targetZipEntryName)
              {
                foundThisTargetFile = true;
              }
              else
              {
                entries.Add(entry);
              }
            }

            if (!foundThisTargetFile)
            {
              archive.CreateEntryFromFile(fullSourceFilename, targetZipEntryName);
              copied++;

              if (parameters.logEcho)
              {
                Console.WriteLine(fullSourceFilename);
              }
            }

            //

            entries.Sort((a, b) => b.Name.CompareTo(a.Name));               // Reverse order! (because 'b'.compare and parameter 'a')

            PrevVersParameters prevVersParams = parameters.prevVers.GetPrevVersParameters(sourceFilename);

            int sumCount = 0;
            long sumSize = 0;
            var keepDate = (DateTime.Now.AddDays(-prevVersParams.age)).ToString(timeFormater);

            foreach (var entry in entries)
            {
              if (!IsPairedFilenames(entry.Name, targetZipEntryName))
              { // drop it, it's not my content   
                if (!moreThanOneFileMode)
                { 
                  entry.Delete();
                  droped++;

                  Trace.TraceWarning($"{fullTargetFilename}: entry '{entry.Name}' is deleted!");
                  // TODO: display info
                }
              }
              else if (isFilenameDatePartOlderThanKeepDate(entry.Name, keepDate))
              { // drop it, it's too old
                entry.Delete();
                droped++;
              }
              else
              {
                sumCount++;
                sumSize += entry.CompressedLength / 1024;

                if ((sumCount > prevVersParams.count) || (sumSize > prevVersParams.size))
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

          if (parameters.logEcho)
          {
            Console.WriteLine(fullSourceFilename);
          }
        }
      }

      if (updateZipDate)
      {
        File.SetLastWriteTime(fullTargetFilename, sourceLWT);
        File.SetAttributes(fullTargetFilename, FileAttributes.ReadOnly);
      }

      if (parameters.clearArchiveBit)
      {
        File.SetAttributes(fullSourceFilename, File.GetAttributes(fullSourceFilename) & ~FileAttributes.Archive);   // remove archive bit
      }


      return (copied, droped);
    }

    private bool isFilenameDatePartOlderThanKeepDate(string name, string keepDate)
    {
      int partSelectorPos = name.LastIndexOf(partSelector);

      return (name.Substring(partSelectorPos + 1, keepDate.Length).CompareTo(keepDate) < 0);
    }

    private bool IsPairedFilenames(string name1, string name2)
    {
      int partSelectorPos = name1.LastIndexOf(partSelector);

      if ((partSelectorPos > 0) && (partSelectorPos == name2.LastIndexOf(partSelector)))
      {
        if (name1.Length == name2.Length)
        {
          if (isCaseInsensitiveFilenames)
          {
            name1 = name1.ToLower();
            name2 = name2.ToLower();
          }

          if (name1.Substring(0, partSelectorPos) == name2.Substring(0, partSelectorPos))
          {
            if (Path.GetExtension(name1) == Path.GetExtension(name2))
            {
              return true;
            }
          }
        }
      }             

      return false;
    }

    private string TargetAbsolutePath(string relativePath) 
    {
      return Path.Combine(parameters.targetDir + relativePath); 
    }

    private string SourceAbsolutePath(string relativePath)
    {
      return Path.Combine(parameters.sourceDir + relativePath);
    }

    private bool RequiredSubDirectory(string relativePath)
    {
      string targetFilesPath = TargetAbsolutePath(relativePath);

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
        var includeNames = Directory.GetFiles(SourceAbsolutePath(relativePath), wildcard);

        foreach (var tempName in includeNames)
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

      //

      foreach (var wildcard in parameters.excludeFiles)
      {
        var excludeNames = Directory.GetFiles(SourceAbsolutePath(relativePath), wildcard);

        foreach (var tempName in excludeNames)
        {
          files.Remove(tempName);                                                      // This will filter duplicate filenames
        }
      }

      return files;
    }

    //

    private class Disp
    {
      void Out(string text = ".", bool clearPrev = false)
      {
        // TODO: ...!!!...
      }

      void Log(string text)
      {
        // TODO: ...!!!...
      }
    }
  }  
}

