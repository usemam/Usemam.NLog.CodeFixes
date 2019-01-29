# Usemam.NLog.CodeFixes
Roslyn-based code fixes for warnings related to using deprecated [NLog](https://github.com/NLog/NLog) API

## Build & Installation
1. Clone this repo.
2. Install [.NET Compiler Platform SDK](https://marketplace.visualstudio.com/items?itemName=VisualStudioProductTeam.NETCompilerPlatformSDK)
3. Open Usemam.NLog.CodeFixes.sln in Visual Studio 2017.
4. Build Usemam.NLog.CodeFixes.Vsix project.
5. After successful build, open project binaries folder (bin\Debug or bin\Release).
6. Close all running Visual Studio instances.
7. Run Usemam.NLog.CodeFixes.vsix file.

## Supported code fixes
Below are deprecated [ILogger](https://github.com/NLog/NLog/blob/dev/src/NLog/ILogger.cs) methods and their new substitutions

Deprecated API | New API
-------------- | -------
```void TraceException(string message, Exception exception)``` | ```void Trace(Exception exception, string message)```
```void DebugException(string message, Exception exception)``` | ```void Debug(Exception exception, string message)```
```void InfoException(string message, Exception exception)``` | ```void Info(Exception exception, string message)```
```void WarnException(string message, Exception exception)``` | ```void Warn(Exception exception, string message)```
```void ErrorException(string message, Exception exception)``` | ```void Error(Exception exception, string message)```
```void FatalException(string message, Exception exception)``` | ```void Fatal(Exception exception, string message)```
```void Trace(string message, Exception exception)``` | ```void Trace(Exception exception, string message)```
```void Debug(string message, Exception exception)``` | ```void Debug(Exception exception, string message)```
```void Info(string message, Exception exception)``` | ```void Info(Exception exception, string message)```
```void Warn(string message, Exception exception)``` | ```void Warn(Exception exception, string message)```
```void Error(string message, Exception exception)``` | ```void Error(Exception exception, string message)```
```void Fatal(string message, Exception exception)``` | ```void Fatal(Exception exception, string message)```

### If 'message' parameter spans multiple lines, code fix extracts it into variable

Before code fix applied
```
public static void Main(string[] args)
{
    _logger.Trace(
        string.Format(
            ""Important message. Parameters: [ID1={0}]"",
            1234567),
        new Exception());
}
```
After code fix applied
```
public static void Main(string[] args)
{
    string traceLogMessage = string.Format(
            ""Important message. Parameters: [ID1={0}]"",
            1234567);
    _logger.Trace(
        new Exception(), traceLogMessage);
}
```
