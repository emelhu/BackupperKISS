@echo off

dotnet c:\utility\BackupperKISS\BackupperKISS.dll C:\DataDirectory\*.*  D:\BackupDirectory\DataDirectory\  -cr -s -pv=62d128m20 -le -qc 

pause

rem when was copied  ...\BackupperKISS\bin\Release\PublishOutput\*.* to c:\utility\BackupperKISS\*.*