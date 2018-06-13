using System;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Extensions.CommandLineUtils;
using System.Text.RegularExpressions;
using System.IO;


// Install-Package Microsoft.Extensions.CommandLineUtils

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
        Console.WriteLine("*** BackupperKISS - Copy files/directories to ZIP archive and keeps previous versions ***");
        Console.WriteLine("                                                                  \"Keep it simple, stupid\"");
        Console.WriteLine("                                                            FreeWare by eMeL, www.emel.hu\n");
      }

      GetParams(args);

      ShowParameterValues();

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
        ShowErrorMessageAndUsage(e.Message, 10);
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

      var archiveOption = commandLineApp.Option("-a|--archive", "Check archive bit for select files to copy.", CommandOptionType.NoValue);      

      var clearOption   = commandLineApp.Option("-c|--clear",   "Clear archive bit of files after copy.",      CommandOptionType.NoValue);
       
      var subdirOption  = commandLineApp.Option("-s|--subdir",  "Copies directories and subdirectories.",      CommandOptionType.NoValue);

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

      try
      {
        commandLineApp.Execute(args);
      }
      catch (Exception e)
      {
        ShowErrorMessageAndUsage(e.Message, 3);
      }

      //
      
      if (string.IsNullOrWhiteSpace(sourceDirArgument.Value) || string.IsNullOrWhiteSpace(targetDirArgument.Value))
      {
        ShowErrorMessageAndUsage("The 'sourceDir' & 'targetDir' arguments are required!", 2);
      }

      #region processing arguments and options 

      parameters.sourceDir            = sourceDirArgument.Value;
      parameters.targetDir            = targetDirArgument.Value;

      parameters.checkArchiveBit      = archiveOption.HasValue();
      parameters.clearArchiveBit      = clearOption.HasValue();

      parameters.copySubdirectories   = subdirOption.HasValue();

      parameters.createRootDirectory  = createRootDirectoryOption.HasValue();
      parameters.existSubdirectories  = existSubdirectoriesOption.HasValue();

      parameters.includeFiles         = SplitParamList(includeFilesOption.Values);
      parameters.excludeFiles         = SplitParamList(excludeFilesOption.Values);

      parameters.silence              = silenceOption.HasValue();
      parameters.logEcho              = logEchoOption.HasValue();

      parameters.quickCheck           = quickCheckOption.HasValue();

      CorrectingSourceDirParameter();

      ProcessingPrevVersionOption(prevVersionOption);
      
      #endregion
    }

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

    private static void ProcessingPrevVersionOption(CommandOption prevVersionOption)
    {
      var tempControl = new List<(char type, char code, int multiplier, int maxvalue)>();

      tempControl.Add(('s', 'k', 1,           1024 * 1024 * 1024));             // size:kilobyte
      tempControl.Add(('s', 'm', 1024,        1024 * 1024));                    // size:megabyte
      tempControl.Add(('s', 'g', 1024 * 1024, 1024));                           // size:gigabyte
      tempControl.Add(('t', 'd', 1,           100 * 365));                      // time:day
      tempControl.Add(('t', 'w', 7,           100 * 52));                       // time:week
      tempControl.Add(('t', 'o', 31,          100 * 12));                       // time:month / warning:'o'
      tempControl.Add(('t', 'y', 365,         100));                            // time:year
      tempControl.Add(('q', 'q', 1,           1000));                           // quantity
      tempControl.Add(('q', '#', 1,           1000));                           // quantity

      //

      var optionValues = new List<string>();

      {
        var regex = new Regex(@"\d+\D?", RegexOptions.IgnoreCase);    // split '124d54m24' format

        foreach (var optionItem in SplitParamList(prevVersionOption.Values))
        {
          var matches = regex.Matches(optionItem.Trim());

          foreach (Match match in matches)
          {
            if (!String.IsNullOrWhiteSpace(match.Value))
            {
              optionValues.Add(match.Value.Trim());
            }
          }
        }
      }

      //

      foreach (var item in optionValues)
      {
        var optionItem = item.Trim();

        if (optionItem.Length >= 1)
        {         
          char lastChar = Char.ToLower(optionItem[optionItem.Length - 1]);

          if ((lastChar >= '0') && (lastChar <= '9'))
          { // without type signal character means quantity
            lastChar = '#';
          }
          else
          {
            optionItem = optionItem.Substring(0, optionItem.Length - 1);                           // cut last character
          }

          var found = tempControl.Find(x => (x.code == lastChar));

          if (found.code != lastChar)
          {
            ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option type code of value! [{item}]", 3);
          }

          //

          int value;                                                            // value part of 'item' in prevVersionOption.Values

          {
            if (!int.TryParse(optionItem, out value))
            {
              value = 0;
            }

            if ((value < 1) || (value > found.maxvalue))
            {
              ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option value number (min:1, max: {found.maxvalue})! [{item}]", 3);
            }
          }

          value *= found.multiplier;

          switch (found.type)
          {
            case 's':                                                           // size
              parameters.prevVerFilesSize   += value;              
              break;
            case 't':                                                           // time
              parameters.prevVerFilesAge    += value;
              break;
            case 'q':                                                           // quantity
              parameters.prevVerFilesCount  += value;
              break;                                                                                
            default:
              ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option type code of value! [{item}] (internal error!)", 3);
              break;
          }
        }
      }
    }

    private static List<string> SplitParamList(List<string> original)
    {
      var result = new List<string>();

      foreach (var item in original)
      {
        var temp = item.Split(new[] {':', '|', '/'}, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item2 in temp)
        { // like result.AddRange(temp) with trim()
          result.Add(item2.Trim());
        }
      }

      return result;
    }

    private static void ShowErrorMessageAndUsage(string errorMessage = null, int exitCode = 0)
    {
      if (!String.IsNullOrWhiteSpace(errorMessage))
      {
        Console.Error.WriteLine(errorMessage);
      }

      if (!parameters.silence)
      {
        commandLineApp.ShowHelp();
      }

      #if DEBUG
      Console.ReadKey();
      #endif

      if (exitCode > 0)
      {
        Environment.Exit(exitCode);
      }
    }

    private static void ShowParameterValues()
    {
      if (!parameters.silence)
      {
        Console.WriteLine("--- Command line parameters: ---");
        Console.WriteLine($"Source directory:       {parameters.sourceDir}");
        Console.WriteLine($"Target directory:       {parameters.targetDir}");
        Console.WriteLine($"Include files:          {string.Join("|", parameters.includeFiles)}");
        Console.WriteLine($"Exclude files:          {string.Join("|", parameters.excludeFiles)}");
        Console.WriteLine($"Copy subdirectories:    {parameters.copySubdirectories}");
        Console.WriteLine($"Create root directory:  {parameters.createRootDirectory}");
        Console.WriteLine($"Exist subdirectories:   {parameters.existSubdirectories}");
        Console.WriteLine($"Check archive bit:      {parameters.checkArchiveBit}");
        Console.WriteLine($"Clear archive bit:      {parameters.clearArchiveBit}");
        Console.WriteLine($"Prev.Ver. Files Count:  {parameters.prevVerFilesCount}");
        Console.WriteLine($"Prev.Ver. Files Size:   {parameters.prevVerFilesSize} Kbytes");
        Console.WriteLine($"Prev.Ver. Files Age:    {parameters.prevVerFilesAge} days");
        Console.WriteLine($"Silence/Supress displ.: {parameters.silence}");
        Console.WriteLine($"Echo copied filename:   {parameters.logEcho}");
        Console.WriteLine($"Quick check:            {parameters.quickCheck}");

        Console.WriteLine();
      }
    }
  }
}
