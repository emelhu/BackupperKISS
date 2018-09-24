using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BackupperKISS
{
  public struct Parameters
  {
    public string           sourceDir;
    public string           targetDir;
    public string           targetExtension;                                    // default value is targetExtensionDefault
    public List<string>     includeFiles;                                       // wildcards (delault: includeFileWildcardDefault)
    public List<string>     excludeFiles;                                       // wildcards
    public bool             copySubdirectories;
    public bool             createRootDirectory;
    public bool             existSubdirectories;
    public MethodParameter  method;                                             // Singly|Group|Copy|Dont  - S|G|C|D
    public bool             checkArchiveBit;
    public bool             clearArchiveBit;
    public bool             silence;                                            // don't display informations, error text will wrote to standard error output stream.    

    public bool             logEcho;                                            // display filenames of copied files.
    public bool             quickCheck;                                         // only ZIP date-time check for quick mode
    public bool             backupOfBackup;                                     // Save backup files (with targetExtension) too
    public bool             writeDebugText;                                     // write debuf text to subdirectiries

    public PrevVersParamPack prevVers;

    public static string  targetExtensionDefault     = ".BK.zip";
    public static string  includeFileWildcardDefault = null;                    // you may change to "*.*" 

    public static string  parameterFileNameDefault = ".BackupperKISS.params";

    internal void Reviser()
    {
      if (string.IsNullOrWhiteSpace(targetExtension))
      {
        targetExtension = Parameters.targetExtensionDefault;
      }

      if (includeFiles == null)
      {
        includeFiles = new List<string>();
      }

      if (excludeFiles == null)
      {
        excludeFiles = new List<string>();
      }

      if ((includeFiles.Count < 1) && (!String.IsNullOrWhiteSpace(Parameters.includeFileWildcardDefault)))
      {
        includeFiles.AddRange(Parameters.SplitList(Parameters.includeFileWildcardDefault));
      }

      if (! backupOfBackup)
      {
        excludeFiles.Add("*" + targetExtension);
      }

      includeFiles = includeFiles.Distinct().ToList();
      excludeFiles = excludeFiles.Distinct().ToList();
    }

    public void Display (string headerText)
    {
      if (! silence)
      {
        Console.WriteLine(headerText);
        Console.WriteLine($"Source directory      [1st]:  {sourceDir}");
        Console.WriteLine($"Target directory      [2nd]:  {targetDir}");
        Console.WriteLine($"Method                [-m] :  {methodFullname}");
        Console.WriteLine($"Include files         [-i] :  {string.Join("|", includeFiles)}");
        Console.WriteLine($"Exclude files         [-e] :  {string.Join("|", excludeFiles)}");
        Console.WriteLine($"Copy subdirectories   [-s] :  {copySubdirectories}");
        Console.WriteLine($"Create root directory [-cr]:  {createRootDirectory}");
        Console.WriteLine($"Exist subdirectories  [-es]:  {existSubdirectories}");
        Console.WriteLine($"Check archive bit     [-q] :  {checkArchiveBit}");
        Console.WriteLine($"Clear archive bit     [-c] :  {clearArchiveBit}");

        prevVers.InitializeIf();

        foreach (var p in prevVers.pars)
        {
          string line = $"Prev.Ver. Files Size  [-pv]:  ext:{p.extension} count:{p.count}, size:{p.sizeText}, age:{p.ageText}";
          Console.WriteLine(line);
        }
               
        Console.WriteLine($"Silence/Supress displ.[-si]:  {silence}");
        Console.WriteLine($"Echo copied filename  [-le]:  {logEcho}");
        Console.WriteLine($"Quick check           [-qc]:  {quickCheck}");
        Console.WriteLine($"Backup of backup      [-bb]:  {backupOfBackup}");
        Console.WriteLine($"Write debug text      [-wd]:  {writeDebugText}");
        

        Console.WriteLine();
      }
    }

    private const string keepValuesText = "«KeepValues»";

    public string SaveTo(string filename = null)
    {
      if (String.IsNullOrWhiteSpace(filename) || (filename == "*"))
      {
        filename = Path.Combine(sourceDir, parameterFileNameDefault);
      }
      else if (String.IsNullOrWhiteSpace(Path.GetDirectoryName(filename)))
      {
        filename = Path.Combine(sourceDir, filename);
      }

      var lines  = new List<string>();
      var helper = new SaveToHelper();

      lines.Add("// .BackupperKISS file : parameter file of BackupperKISS utility for this directory and subdirectories [You can override in subdirectories]");
      lines.Add("");

      helper.Add($"m:  {methodFullname}"                                                    ,  "// Method                      [-m]  Singly|Group|Copy|Dont");
      helper.Add($"i:  {string.Join("|", includeFiles)}"                                    , $"// Include files               [-i] [optional {keepValuesText}]");
      helper.Add($"e:  {string.Join("|", excludeFiles)}"                                    , $"// Exclude files               [-e] [optional {keepValuesText}]");
      helper.Add($"s:  {copySubdirectories}"                                                ,  "// Copy subdirectories         [-s] ");
      helper.Add($"cr: {createRootDirectory}"                                               ,  "// Create root directory       [-cr]");
      helper.Add($"es: {existSubdirectories}"                                               ,  "// Exist subdirectories        [-es]");
      helper.Add($"a:  {checkArchiveBit}"                                                   ,  "// Check archive bit           [-q] ");
      helper.Add($"c:  {clearArchiveBit}"                                                   ,  "// Clear archive bit           [-c] ");

      {
        var extraHelpText = $"[optional {keepValuesText}]";                   // write only to first line

        foreach (var line in prevVers.textLines)
        {
          helper.Add($"pv: {line}", $"// Prev.Ver. Files Conditions  [-pv] {extraHelpText}");

          extraHelpText = String.Empty;
        }
      }                                                                                       
                                                                                               
      helper.Add($"si: {silence}"                                                           ,  "// Silence/Supress displ.      [-si]");
      helper.Add($"le: {logEcho}"                                                           ,  "// Echo copied filename        [-le]");
      helper.Add($"qc: {quickCheck}"                                                        ,  "// Quick check of ZIP by date  [-qc]");
      helper.Add($"bb: {backupOfBackup}"                                                    ,  "// Backup of backup            [-bb]");
      helper.Add($"wd: {writeDebugText}"                                                    ,  "// Write Debug Text            [-wd]");

      helper.AddToList(lines);


      File.WriteAllLines(filename,   lines);

      return filename;
    }

    public void LoadFrom(string filename)
    {
      if (File.Exists(filename))
      {
        bool includeFilesParameterDefined = false;
        bool excludeFilesParameterDefined = false;
        bool prevVersionsParameterDefined = false;

        var lines = File.ReadAllLines(filename);

        foreach (var line in lines)
        {
          if (! (String.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//")))
          {
            var lineData = RetrieveParameterLineData(line);

            Debug.Assert(!String.IsNullOrWhiteSpace(lineData.type));

            if (!String.IsNullOrWhiteSpace(lineData.value))                    // Skip not definied parameters, leave original (or default) value of this
            {
              switch (lineData.type)
              {
                case "m":
                  method = (MethodParameter)GetMethodParameterValue(lineData.value);
                  break;

                case "i":
                  bool keepValuesI = CheckKeepValuesFlag(ref lineData.value);                     // remove flag text too

                  if (!includeFilesParameterDefined)
                  {
                    includeFilesParameterDefined = true;

                    if (!keepValuesI)
                    {
                      includeFiles = new List<string>();
                    }
                  }

                  includeFiles.AddRange(SplitList(lineData.value));

                  break;

                case "e":
                  bool keepValuesE = CheckKeepValuesFlag(ref lineData.value);                     // remove flag text too

                  if (!excludeFilesParameterDefined)
                  {
                    excludeFilesParameterDefined = true;

                    if (!keepValuesE)
                    {
                      excludeFiles = new List<string>();
                    }
                  }

                  excludeFiles.AddRange(SplitList(lineData.value));

                  break;

                case "s":
                  copySubdirectories = GetBooleanValue(lineData.value);
                  break;

                case "cr":
                  createRootDirectory = GetBooleanValue(lineData.value);
                  break;

                case "es":
                  existSubdirectories = GetBooleanValue(lineData.value);
                  break;

                case "a":
                  checkArchiveBit = GetBooleanValue(lineData.value);
                  break;

                case "c":
                  clearArchiveBit = GetBooleanValue(lineData.value);
                  break;

                case "pv":
                  bool keepValuesPV = CheckKeepValuesFlag(ref lineData.value);                    // remove flag text too

                  if (!prevVersionsParameterDefined)
                  {
                    prevVersionsParameterDefined = true;

                    if (!keepValuesPV)
                    {
                      prevVers = new PrevVersParamPack();
                    }
                  }

                  prevVers.ProcessingParameterTextLines(ToEnumerable(lineData.value));

                  break;

                case "si":
                  silence = GetBooleanValue(lineData.value);
                  break;

                case "le":
                  logEcho = GetBooleanValue(lineData.value);
                  break;

                case "qc":
                  quickCheck = GetBooleanValue(lineData.value);
                  break;

                case "bb":
                  backupOfBackup = GetBooleanValue(lineData.value);
                  break;

                case "wd":
                  writeDebugText = GetBooleanValue(lineData.value);
                  break;

                default:
                  throw new FormatException($"Parameter file line parameter-type invalid: '{line}'");
              }
            }
          }
        }

        Reviser();
      }
      else
      {
        Debug.WriteLine($"Parameters.LoadFrom({filename}): filename not exists!");
      }
    }

    private bool CheckKeepValuesFlag(ref string value)
    { // check flag text and remove flag text too
      int pos = value.IndexOf(keepValuesText, StringComparison.OrdinalIgnoreCase);

      if (pos >= 0)
      {
        value = value.Remove(0, keepValuesText.Length);

        return true;
      }

      return false;
    }

    private IEnumerable<string> ToEnumerable(string text)
    {
      yield return text;
    }

    public static IEnumerable<string> SplitList(string value)
    {
      var result = value.Split(new[] { ':', '|', '/', '\\', ';' }, StringSplitOptions.RemoveEmptyEntries);

      foreach (var item in result)
      {
        yield return item.Trim();
      }
    }

    private bool GetBooleanValue(string text)
    {
      string trueText  = "true";
      string falseText = "false";

      text = text.ToLower();

      if (String.Compare(text, 0, trueText, 0, Math.Min(trueText.Length, text.Length), StringComparison.OrdinalIgnoreCase) == 0)
      {
        return true;
      }
      else if (String.Compare(text, 0, falseText, 0, Math.Min(falseText.Length, text.Length), StringComparison.OrdinalIgnoreCase) == 0)
      {
        return false;
      }
      
      throw new FormatException($"Parameter file line parameter-value (true/false) invalid: '{text}'");
    }

    private (string type, string value) RetrieveParameterLineData(string line)
    {
      var elements = line.Split(new string[] { ":", "//" }, StringSplitOptions.None);

      if (elements.Length < 2)
      {
        throw new FormatException($"Parameter file line format error: '{line}'");
      }

      string type  = elements[0].Trim().ToLower();
      string value = elements[1].Trim();

      if ((type.Length < 1) || (type.Length > 2))
      {
        throw new FormatException($"Parameter file line parameter-type error: '{line}'");
      }

      return (type, value); 
    }

    public string methodFullname
    {
      get
      {
        string name = Enum.GetName(typeof(MethodParameter), method);

        Debug.Assert(! String.IsNullOrWhiteSpace(name));

        return name;
      }

      set
      {
        Debug.Assert(!String.IsNullOrWhiteSpace(value));

        method = (MethodParameter)GetMethodParameterValue(value);
      }
    } 
    
    private char GetMethodParameterValue(string value)
    {
      if (! string.IsNullOrWhiteSpace(value))
      {
        foreach (var item in Enum.GetNames(typeof(MethodParameter)))
        {
          if (string.Compare(item, 0, value, 0, Math.Min(value.Length, item.Length), StringComparison.OrdinalIgnoreCase) == 0)
          {
            return item[0];
          }
        }
      }

      throw new Exception($"Parameters.methodFullname: Invalid name! [{value}]");
    }

    public static List<string> SplitParamList(IEnumerable<string> original)
    {
      var result = new List<string>();

      foreach (var item in original)
      {
        var temp = item.Split(new[] { ':', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item2 in temp)
        { // like result.AddRange(temp) with trim()
          result.Add(item2.Trim());
        }
      }

      return result;
    }

    public Parameters Clone()
    {
      var ret = this;

      ret.includeFiles = new List<string>();
      ret.excludeFiles = new List<string>();

      ret.includeFiles.AddRange(this.includeFiles);
      ret.excludeFiles.AddRange(this.excludeFiles);

      ret.prevVers = ret.prevVers.Clone();

      ret.Reviser();

      return ret;
    }
  }

  public class PrevVersParameters
  {
    private   int           _count;                                           // count of stored archive files
    private   long          _size;                                            // size in kilobytes
    private   int           _age;                                             // age in days


    public    string        extension;                                        // extension/EndsWith of filename to give your own retention time/count/size
    
    public    int           count                                             // count of stored archive files
    {
      get => (_count < 1) ? CountDefault : _count;
      set => _count = Math.Min(value, CountMaximum);
    }

    public    long          size                                              // size in kilobytes
    {
      get => (_size < 1) ? SizeDefault : _size;
      set => _size = Math.Min(value, SizeMaximum);
    }

    public    int           age                                               // age in days
    {
      get => (_age < 1) ? AgeDefault : _age;
      set => _age = Math.Min(value, AgeMaximum);
    }

    public string sizeText
    {
      get
      {
        long mega, giga;
        long kilo = size;

        giga = kilo / (1024 * 1024);
        kilo = kilo % (1024 * 1024);

        mega = kilo / 1024;
        kilo = kilo % 1024;

        return ((giga > 0) ? (giga.ToString() + "g") : String.Empty) +
               ((mega > 0) ? (mega.ToString() + "m") : String.Empty) +
               ((kilo > 0) ? (kilo.ToString() + "k") : String.Empty);
      }
    }

    public string ageText
    {
      get
      {
        int year, month, week;
        int day = age;

        year  = age / 365;
        day   = age % 365;

        month = day / 31;
        day   = day % 31;

        week = day / 7;
        day  = day % 7;

        return ((year > 0)  ? (year.ToString()  + "y") : String.Empty) +
               ((month > 0) ? (month.ToString() + "o") : String.Empty) +
               ((week > 0)  ? (week.ToString()  + "w") : String.Empty) +
               ((day > 0)   ? (day.ToString()   + "d") : String.Empty);
      }
    }


    internal  static string ExtensionDefault        = "*";
    public    static int    CountDefault            = 32;                     // default: keep 32 files
    public    static long   SizeDefault             = 1024 * 1024 * 32;       // default: 64 Gbytes in archive 
    public    static int    AgeDefault              = 365 * 8;                // default: keep 8 years

    public    static int    CountMaximum            = CountDefault * 32;       
    public    static long   SizeMaximum             = SizeDefault  * 32;       
    public    static int    AgeMaximum              = AgeDefault   * 16;                              

    public PrevVersParameters(string e = null, int c = 0, long s = 0, int a = 0)
    {
      if (String.IsNullOrWhiteSpace(e))
      {
        e = PrevVersParameters.ExtensionDefault;
      }

      e = e.Trim().ToLowerInvariant();

      if (e != PrevVersParameters.ExtensionDefault)
      {
        if (e[0] != '.')
        {
          e = '.' + e;
        }
      }

      //

      extension = e;
      count     = c;
      size      = s;
      age       = a;
    }

    public PrevVersParameters Clone()
    {
      return new PrevVersParameters(extension, count, size, age);
    }

    public void CollectSize(long addValue)  => _size = Math.Min(((_size < 1) ? 0 : _size) + addValue, SizeMaximum);
    public void CollectAge(int addValue)    => _age  = Math.Min(((_age  < 1) ? 0 : _age)  + addValue, AgeMaximum);
  }

  public struct PrevVersParamPack
  {
    internal List<PrevVersParameters> pars;

    public void InitializeIf()
    {
      if (pars == null)
      {
        pars = new List<PrevVersParameters>();

        pars.Add(new PrevVersParameters(null));
      }
    }

    public PrevVersParamPack Clone()
    {
      InitializeIf();

      var ret = this;                                                         // struct --> clone all variables

      for (int i = 0; i < ret.pars.Count; i++)
      {
        ret.pars[i] = ret.pars[i].Clone();                                    // class --> make new object instance
      }

      return ret;
    }

    internal PrevVersParameters GetPrevVersParameters(string sourceFilename, bool addIfNotFound = false)
    {
      InitializeIf();

      string ext = Path.GetExtension(sourceFilename).ToLowerInvariant();

      if (String.IsNullOrWhiteSpace(ext))
      {
        ext = PrevVersParameters.ExtensionDefault;
      }
      else
      {
        if (ext[0] != '.')
        {
          ext = '.' + ext;
        }
      }


      var par = pars.Find(a => a.extension == ext);

      if (par?.extension == null)
      { // Find() -> not found
        if (addIfNotFound)
        {
          par = new PrevVersParameters(ext);

          pars.Add(par);
        }
        else
        {
          par = pars.Find(a => a.extension == PrevVersParameters.ExtensionDefault);

          Debug.Assert((par?.extension != null), "PrevVersParameters.ExtensionDefault not found!");
        }
      }

      return par;
    }

    public IEnumerable<string> textLines
    {
      get
      {
        InitializeIf();

        var ret = new List<string>();

        foreach (var item in pars)
        {
          ret.Add($"{item.extension}={item.count} {item.sizeText} {item.ageText}");
        }

        return ret;
      }
    }   

    internal void ProcessingParameterTextLines(IEnumerable<string> paramLines)
    {
      var tempControl = new List<(char type, char code, int multiplier, int maxvalue)>();

      tempControl.Add(('s', 'k', 1,     1024 * 1024 * 1024));                 // size:kilobyte
      tempControl.Add(('s', 'm', 1024,  1024 * 1024));                        // size:megabyte
      tempControl.Add(('s', 'g', 1024 * 1024, 1024 * 1024));                  // size:gigabyte
      tempControl.Add(('t', 'd', 1,     100 * 365));                          // time:day
      tempControl.Add(('t', 'w', 7,     100 * 52));                           // time:week
      tempControl.Add(('t', 'o', 31,    100 * 12));                           // time:month / warning:'o'
      tempControl.Add(('t', 'y', 365,   100));                                // time:year
      tempControl.Add(('q', 'q', 1,     1000));                               // quantity
      tempControl.Add(('q', '#', 1,     1000));                               // quantity
      tempControl.Add(('q', 'p', 1,     1000));                               // quantity (piece)
      tempControl.Add(('q', 'f', 1,     1000));                               // quantity (file)

      //

      var optionValues = new List<(string ext, string value)>();

      {
        var regex = new Regex(@"\d+\D?", RegexOptions.IgnoreCase);    // split '124d54m24' format

        foreach (var optionItem in Parameters.SplitParamList(paramLines))
        {
          string ext;
          string par;

          {
            if (optionItem.Contains("="))
            {
              var temp = optionItem.Split('=');

              Debug.Assert((temp.Length == 2), "Parameter '=' content.");

              ext = temp[0].Trim();
              par = temp[1].Trim();
            }
            else
            {
              ext = PrevVersParameters.ExtensionDefault;
              par = optionItem.Trim();
            }
          }

          var matches = regex.Matches(par);

          foreach (Match match in matches)
          {
            if (!String.IsNullOrWhiteSpace(match.Value))
            {
              optionValues.Add((ext, match.Value.Trim()));
            }
          }
        }
      }

      //

      foreach (var item in optionValues)
      {
        var itemValue = item.value.Trim();

        if (itemValue.Length >= 1)
        {
          char lastChar = Char.ToLower(itemValue[itemValue.Length - 1]);

          if ((lastChar >= '0') && (lastChar <= '9'))
          { // without type signal character means quantity
            lastChar = '#';
          }
          else
          {
            itemValue = itemValue.Substring(0, itemValue.Length - 1);                           // cut last character
          }

          var found = tempControl.Find(x => (x.code == lastChar));

          if (found.code != lastChar)
          {
            Program.ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option type code of value! [{item}]", (int)ExitCode.ParameterErr1);
          }

          //

          int value;                                                            // value part of 'item' in prevVersionOption.Values

          {
            if (!int.TryParse(itemValue, out value))
            {
              value = 0;
            }

            if ((value < 1) || (value > found.maxvalue))
            {
              Program.ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option value! [{item}]", (int)ExitCode.ParameterErr1);
            }
          }


          var prevVerParam = GetPrevVersParameters(item.ext, true);

          value *= found.multiplier;

          switch (found.type)
          {
            case 's':                                                           // size (collect for example 1g + 20m)
              prevVerParam.CollectSize(value);
              break;
            case 't':                                                           // time (collect for example 1y + 10w)
              prevVerParam.CollectAge(value);
              break;
            case 'q':                                                           // quantity (overwrite)
              prevVerParam.count = value;
              break;
            default:
              Program.ShowErrorMessageAndUsage($"Error! Invalid 'prevVersion' option type code of value! [{item}] (internal error!)", 3);
              break;
          }
        }
      }
    }    
  }

  
  public enum MethodParameter
  {
    Singly = 'S',
    Groups = 'G',
    Copy   = 'C',
    Dont   = 'D'
  }
}
