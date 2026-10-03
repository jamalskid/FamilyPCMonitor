# Contributing

Thanks for helping improve Family PC Monitor. Please keep changes transparent to the Windows user and limited to the stated family-monitoring purpose.

## Before opening an issue or pull request

- Search existing issues to avoid duplicates.
- Never include real webhook URLs, Discord bot tokens, private computer names, account names, or database files in an issue, screenshot, or commit.
- For a bug report, include Windows version, .NET SDK version (`dotnet --info`), reproduction steps, and sanitized error output.
- For a pull request, explain the user-visible behavior and privacy impact.

## Build locally

Install the .NET 8 SDK on Windows, then run:

```powershell
dotnet restore .\FamilyPCMonitor.sln
dotnet build .\FamilyPCMonitor.sln --configuration Release
```

Please build successfully before submitting. There is currently no automated unit-test project; add focused tests when introducing logic that can be tested independently of Windows session APIs.

## Privacy and security expectations

Contributions must not add stealth behavior, keylogging, credential collection, browser-history collection, recording, or security bypasses. Keep collection minimal, keep monitoring status visible, validate outbound destinations, and never print credentials in logs or diagnostic output. Report suspected vulnerabilities privately using the contact method listed in `SECURITY.md` rather than publishing exploit details in an issue.
