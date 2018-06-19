using System;
using System.Collections.Generic;
using System.Text;

namespace BackupperKISS
{
  public struct Parameters
  {
    public string       sourceDir;
    public string       targetDir;
    public string       targetExtension;                                        // default value is targetExtensionDefault
    public List<string> includeFiles;                                           // wildcards (delault: includeFileWildcardDefault)
    public List<string> excludeFiles;                                           // wildcards
    public bool         copySubdirectories;
    public bool         createRootDirectory;
    public bool         existSubdirectories;
    public bool         checkArchiveBit;
    public bool         clearArchiveBit;
    public int          prevVerFilesCount;                                      // count of stored archive files
    public int          prevVerFilesSize;                                       // size in kilobytes
    public int          prevVerFilesAge;                                        // age in days
    public bool         silence;                                                // don't display informations, error text will wrote to standard error output stream.
    public bool         logEcho;                                                // display filenames of copied files.
    public bool         quickCheck;                                             // only ZIP date-time check for quick mode
    public bool         backupOfBackup;                                         // Save backup files (with targetExtension) too

    public List<PrevVersParameters> prevVersParameters;

    public static string targetExtensionDefault     = ".BK.zip";
    public static string includeFileWildcardDefault = null;                     // you may change to "*.*"

    public static int   prevVerFilesCountDefault = 32;                          // default: keep 32 files
    public static int   prevVerFilesSizeDefault  = 1024*1024*64;                // default: 64 Gbytes in archive 
    public static int   prevVerFilesAgeDefault   = 365 * 32;                    // default: keep 32 years
  }

  public struct PrevVersParameters
  {
    public string       prevVerFilesExtension;                                  // extension/EndsWith of filename to give your own retention time/count/size
    public int          prevVerFilesCount;                                      // count of stored archive files
    public int          prevVerFilesSize;                                       // size in kilobytes
    public int          prevVerFilesAge;                                        // age in days
  }
}
