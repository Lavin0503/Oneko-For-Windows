@echo off
rem Builds Oneko.exe with the C# compiler that ships with Windows (.NET Framework 4)
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /out:Oneko.exe /win32icon:assets\oneko.ico /resource:assets\sheet.png,sheet.png /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Oneko.cs
if %ERRORLEVEL%==0 (echo Built Oneko.exe) else (echo Build failed)
pause
