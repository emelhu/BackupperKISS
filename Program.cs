using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Extensions.CommandLineUtils;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml.Serialization;


// Install-Package Microsoft.Extensions.CommandLineUtils

// Test parameters: c:\_work_\test\*.*   C:\_work_\BackupperKISS_test -a --subdir -pv  "10d,20m5"  -cr --exclude "*.tmp"  -le   -qc -wd  --saveparams c:\_work_\proba.param.txt

// http://alphavss.alphaleonis.com/index.html   --- AlphaVSS is a .NET class library providing a managed API for the Volume Shadow Copy Service.

namespace BackupperKISS
{
  class Program
  {
    static CommandLineApplication commandLineApp = new CommandLineApplication();
    static Parameters             parameters;

    //

    static void Main(string[] args)
    {
      if (!parameters.silence)
      {
        var colBG = Console.BackgroundColor;
        var colFG = Console.ForegroundColor;

        Console.BackgroundColor = ConsoleColor.Gray;
        Console.ForegroundColor = ConsoleColor.DarkRed;   

        Console.WriteLine("*** BackupperKISS - Copy files/directories to ZIP archive and keeps previous versions ***");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("                                                                 \"Keep it simple, stupid\"");
        Console.WriteLine("                                                            FreeWare by eMeL, www.emel.hu\n");
        
        Console.BackgroundColor = colBG;
        Console.ForegroundColor = colFG;
      }

      GetParams(args);      

      try
      {
        var backupper = new Backupper(parameters);

        var status = backupper.Backup();

        if (!parameters.silence || parameters.logEcho)
        {
          Console.WriteLine($"// FINISHED: {status.copied} files copied, {status.droped} files droped.\n");
        }
      }
      catch (Exception e)
      {
        ShowErrorMessageAndUsage(e.Message, (int)ExitCode.Error);
      }

      #if DEBUG
      Console.ReadKey();
      #endif
    }

    static void GetParams(string[] args)
    { // https://github.com/anthonyreilly/ConsoleArgs/blob/master/Program.cs
      commandLineApp.Name              = "BackupperKISS";
      commandLineApp.Description       = "Copies files/directories and archives previous versions";
      commandLineApp.ExtendedHelpText  = "\nThis is a simple app to copy files into backup target and archive previous versions of newer copied files." + Environment.NewLine + 
                                         "There are parameters for control size and/or count and/or age of archive files.\n\n" +
                                         "Examples:\n" +
                                         "BackupperKISS.exe  *.*   \\backup\\BackupperKISS_test   -a  --subdir  -pv=10d20m3\n" +
                                         "BackupperKISS.exe  *.*   \\backup\\BackupperKISS_test   -a  --subdir  -pv \"10d,20m,3q\" -cr \n" +
                                         "BackupperKISS.exe  \\work\\test\\*.* \\work\\BackupperKISS_test -a --subdir -pv \"10d,20m,3q\" -e \"*.tmp\" -e *.bak|*.old/*.save";

      commandLineApp.HelpOption("-?|-h|--h|--help");

      commandLineApp.VersionOption("-v|--version", () => 
      {
        return string.Format("Version {0}", Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
      });

      var methodOption  = commandLineApp.Option("-m|--method <Singly/Groups/Copy/Dont>", "Methode of backup and archive files.", CommandOptionType.SingleValue);

      var archiveOption = commandLineApp.Option("-a|--archive", "Check archive bit for select files to copy.", CommandOptionType.NoValue);      

      var clearOption   = commandLineApp.Option("-c|--clear",   "Clear archive bit of files after copy.",      CommandOptionType.NoValue);
       
      var subdirOption  = commandLineApp.Option("-s|--subdir",  "Archive/Backup directories and subdirectories.",      CommandOptionType.NoValue);

      var createRootDirectoryOption   = commandLineApp.Option("-cr|--createrootdir", "Create target root directory.", CommandOptionType.NoValue); 
      var existSubdirectoriesOption   = commandLineApp.Option("-es|--existsubdir",   "Backup only exists target subdirectories.", CommandOptionType.NoValue);

      var prevVersionOption           = commandLineApp.Option("-pv|--prevver", "Storage parameters of previous versions.", CommandOptionType.MultipleValue);

      var includeFilesOption          = commandLineApp.Option("-i|--include <wildcards>", "Include files's wildcards", CommandOptionType.MultipleValue);
      var excludeFilesOption          = commandLineApp.Option("-e|--exclude <wildcards>", "Exclude files's wildcards", CommandOptionType.MultipleValue);


      var sourceDirArgument = commandLineApp.Argument("sourceDir", "Source path of files with or without wildcard filename definition.");
      var targetDirArgument = commandLineApp.Argument("targetDir", "Target path of files to copy.");

      var silenceOption     = commandLineApp.Option("-si|--silence", "Supress info write to display.",            CommandOptionType.NoValue);
      var logEchoOption     = commandLineApp.Option("-le|--logecho", "Echo name of copied files to display.",     CommandOptionType.NoValue);

      var quickCheckOption  = commandLineApp.Option("-qc|--quickcheck", "If date of ZIP is unchanged, assume the content is unchanged too!", CommandOptionType.NoValue);
      
      var backupOfBackupOption = commandLineApp.Option("-bb|--backupbackup", "Backup of own backup files too.",       CommandOptionType.NoValue);

      var saveParamsOption  = commandLineApp.Option("-sp|--saveparams <filename>", "Save parameters to filename or use '*' for default name. Warning! The backup will not run!", CommandOptionType.SingleValue);   // SingleOrNoValue      

      var writeDebugTextOption = commandLineApp.Option("-wd|--writedebug", "Write debug text to file is subdirectories.", CommandOptionType.NoValue);

      try
      {
        commandLineApp.Execute(args);
      }
      catch (Exception e)
      {
        ShowErrorMessageAndUsage(e.Message, (int)ExitCode.ParameterErr2);
      }

      //

      #region processing arguments and options 

      if (!string.IsNullOrWhiteSpace(sourceDirArgument.Value))
      {
        parameters.sourceDir = sourceDirArgument.Value;
      }

      if (!string.IsNullOrWhiteSpace(targetDirArgument.Value))
      {
        parameters.targetDir = targetDirArgument.Value;
      }

      if (string.IsNullOrWhiteSpace(parameters.sourceDir) || string.IsNullOrWhiteSpace(parameters.targetDir))
      {
        ShowErrorMessageAndUsage("The 'sourceDir' & 'targetDir' arguments are required!", (int)ExitCode.ParameterErr1);
      }

      if (methodOption.HasValue())
      {
        parameters.methodFullname = methodOption.Value();
      }
      else
      {
        parameters.method = MethodParameter.Singly;
      }

      parameters.checkArchiveBit      = archiveOption.HasValue();
      parameters.clearArchiveBit      = clearOption.HasValue();

      parameters.copySubdirectories   = subdirOption.HasValue();

      parameters.createRootDirectory  = createRootDirectoryOption.HasValue();
      parameters.existSubdirectories  = existSubdirectoriesOption.HasValue();

      parameters.includeFiles         = Parameters.SplitParamList(includeFilesOption.Values);
      parameters.excludeFiles         = Parameters.SplitParamList(excludeFilesOption.Values);

      parameters.silence              = silenceOption.HasValue();
      parameters.logEcho              = logEchoOption.HasValue();

      parameters.quickCheck           = quickCheckOption.HasValue();

      parameters.writeDebugText       = writeDebugTextOption.HasValue();

      CorrectingSourceDirParameter();

      parameters.prevVers.ProcessingParameterTextLines(prevVersionOption.Values);

      if (saveParamsOption.HasValue())
      {
        string filename = saveParamsOption.Value();

        if (filename == "*")
        {
          filename = null;                                                    // parameters.SaveTo() knows it.
        }

        parameters.Display("Saved parameter values:");

        Console.WriteLine(" The '{0}' parameter file created.\n", parameters.SaveTo(filename));

        ShowErrorMessageAndUsage("Parameter file created, didn't make a backup!", (int)ExitCode.NormalExit, false, true);
      }

      #endregion
    }

    //private static void SaveParameterFile(string filename, Parameters parameters)
    //{
    //  throw new NotImplementedException();

    //  Console.WriteLine("--- Command line parameters: ---");
    //  Console.WriteLine($"Source directory      [1st]:  {parameters.sourceDir}");
    //  Console.WriteLine($"Target directory      [2nd]:  {parameters.targetDir}");
    //  Console.WriteLine($"Method                [-m] :  {parameters.methodFullname}");
    //  Console.WriteLine($"Include files         [-i] :  {string.Join("|", parameters.includeFiles)}");
    //  Console.WriteLine($"Exclude files         [-e] :  {string.Join("|", parameters.excludeFiles)}");
    //  Console.WriteLine($"Copy subdirectories   [-s] :  {parameters.copySubdirectories}");
    //  Console.WriteLine($"Create root directory [-cr]:  {parameters.createRootDirectory}");
    //  Console.WriteLine($"Exist subdirectories  [-es]:  {parameters.existSubdirectories}");
    //  Console.WriteLine($"Check archive bit     [-q] :  {parameters.checkArchiveBit}");
    //  Console.WriteLine($"Clear archive bit     [-c] :  {parameters.clearArchiveBit}");
    //  Console.WriteLine($"Prev.Ver. Files Count [-pv]:  {parameters.prevVerFilesCount}");
    //  Console.WriteLine($"Prev.Ver. Files Size  [-pv]:  {parameters.prevVerFilesSize} Kbytes");
    //  Console.WriteLine($"Prev.Ver. Files Age   [-pv]:  {parameters.prevVerFilesAge} days");
    //  Console.WriteLine($"Silence/Supress displ.[-si]:  {parameters.silence}");
    //  Console.WriteLine($"Echo copied filename  [-le]:  {parameters.logEcho}");
    //  Console.WriteLine($"Quick check           [-qc]:  {parameters.quickCheck}");
    //  Console.WriteLine($"Backup of backup      [-bb]:  {parameters.backupOfBackup}");
    //}

    #region waste
    //private static void SaveParameterXML(string filename, Parameters parameters)
    //{
    //  if (parameters.prevVersParameters == null)
    //  {
    //    parameters.prevVersParameters = new List<PrevVersParameters>();
    //  }

    //  if (parameters.excludeFiles == null)
    //  {
    //    parameters.excludeFiles = new List<string>();
    //  }

    //  if (parameters.includeFiles == null)
    //  {
    //    parameters.includeFiles = new List<string>();
    //  }

    //  filename = GetParameterXMLfilename(filename);

    //  if (!parameters.silence)
    //  {
    //    Console.WriteLine($"Save parameters to {filename}");
    //  }

    //  XmlSerializer serializer = new XmlSerializer(parameters.GetType());

    //  using (StreamWriter file = new StreamWriter(filename))
    //  {
    //    serializer.Serialize(file, parameters);
    //  }
    //}

    //private static Parameters LoadParameterXML(string filename)
    //{
    //  filename = GetParameterXMLfilename(filename);

    //  if (!parameters.silence)
    //  {
    //    Console.WriteLine($"Load default parameter values from {filename}");
    //  }

    //  Parameters retPars;

    //  XmlSerializer serializer = new XmlSerializer(parameters.GetType());

    //  using (StreamReader file = new StreamReader(filename))
    //  {
    //    retPars = (Parameters)(serializer.Deserialize(file));
    //  }

    //  return retPars;
    //}

    //private static string GetParameterXMLfilename(string filename)
    //{
    //  if (Path.GetExtension(filename).ToLower() != ".xml")
    //  {
    //    filename += ".xml";
    //  }

    //  if (Path.GetFileName(filename).Length != filename.Length)
    //  {
    //    var dirname = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BackupperKISS");

    //    Directory.CreateDirectory(dirname);

    //    filename = Path.Combine(dirname, filename);
    //  }

    //  return filename;
    //}
    #endregion

    private static void CorrectingSourceDirParameter()
    {
      string path = Path.GetDirectoryName(parameters.sourceDir);

      if (path.Length < parameters.sourceDir.Length)
      {
        string wildcard = parameters.sourceDir.Substring(path.Length);

        if (wildcard.StartsWith('\\') || wildcard.StartsWith('/'))
        {
          wildcard = wildcard.Substring(1);
        }

        parameters.sourceDir = path;

        parameters.includeFiles.Add(wildcard);
      }
    }    

    public static void ShowErrorMessageAndUsage(string errorMessage = null, int exitCode = 0, bool showHelp = true, bool onlyWarning = false)
    {
      if (showHelp && !parameters.silence)
      {
        commandLineApp.ShowHelp();
      }

      if (!String.IsNullOrWhiteSpace(errorMessage))
      {
        var colBG = Console.BackgroundColor;
        var colFG = Console.ForegroundColor;

        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Red;

        Console.Error.WriteLine();
        if (!onlyWarning)
        {
          Console.Error.WriteLine("-------------------------------- !!! ERROR !!! --------------------------------");
        }

        Console.Error.WriteLine(errorMessage);
        Console.Error.WriteLine();

        Console.BackgroundColor = colBG;
        Console.ForegroundColor = colFG;
      }    

      #if DEBUG
      Console.ReadKey();
      #endif

      if (exitCode > 0)
      {
        Environment.Exit(exitCode);
      }
    }
  }

  public enum ExitCode
  {
    OK            = 0,
    NormalExit    = 1,
    ParameterErr1 = 11,
    ParameterErr2 = 12,
    Error         = 100,
    ZipError      = 101
  };
}
