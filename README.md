# Family PC Monitor

> **Project status: early development.** This is source code for a Windows WPF tray application, not a signed or production installer. Build it yourself and review the code before using it. The optional administrator-only Discord bot and slash commands are not implemented; only outgoing webhook notifications are supported. Windows session notifications and shutdown reporting have operating-system limitations described below.

Family PC Monitor is intended for transparent use on a computer you own or are authorized to administer. Tell people who use the computer that it is installed and what it records. This project is not designed for covert employee or partner surveillance.

A visible Windows tray application for recording basic computer and Windows session activity on the local computer. It records startup, login/logoff, lock/unlock, and best-effort shutdown locally in SQLite. When configured, it sends enabled events to a family administrator's Discord webhook.

It does not collect keystrokes, passwords, browsing history, private messages, camera or microphone data. The app displays a tray icon and clearly reports its monitoring state. Use only on a computer you own or are authorized to administer, and explain its use to the people who use it.

## Architecture

The project uses .NET 8 WPF for the visible settings UI and notification-area app, Windows Terminal Services session notifications for login/logoff/lock/unlock, `SystemEvents.SessionEnding` for best-effort shutdown, Microsoft.Data.Sqlite for local storage, Windows DPAPI for the webhook secret, and Discord's webhook HTTPS endpoint for notifications. These capabilities are grouped in one project under `Services/` to keep the beginner setup approachable. A service is not used because session state belongs to the interactive Windows account; the app runs visibly in that account with a normal HKCU Run startup entry. It does not require administrator rights.

## Requirements and fresh-PC setup

See [LICENSE](LICENSE) for the MIT license, [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidance, and [SECURITY.md](SECURITY.md) for vulnerability reports. Do not commit real webhook URLs, bot tokens, settings files, database files, or exported CSV logs.

## Source and contributions

The source is available at [github.com/jamalskid/FamilyPCMonitor](https://github.com/jamalskid/FamilyPCMonitor). Clone it with:

```powershell
git clone https://github.com/jamalskid/FamilyPCMonitor.git
cd FamilyPCMonitor
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidance. `.gitignore` excludes build output, local settings, SQLite files, CSV exports, and installer output. Never commit real webhook URLs, bot tokens, settings files, database files, or exported logs. If a real webhook URL or token is exposed, revoke it in Discord immediately; deleting it from the latest version does not remove it from Git history. GitHub Actions builds the solution on Windows for pushes and pull requests.

1. Install the .NET 8 SDK from [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0). Choose the Windows x64 SDK installer and accept its defaults.
2. Install Visual Studio Code from [code.visualstudio.com](https://code.visualstudio.com/).
3. In VS Code Extensions (`Ctrl+Shift+X`), install **C# Dev Kit** by Microsoft and **C#** by Microsoft.
4. Open PowerShell and verify the SDK: `dotnet --info`.
5. Open this folder in VS Code (`File > Open Folder`). In the integrated terminal, run:

```powershell
dotnet restore .\FamilyPCMonitor.sln
dotnet build .\FamilyPCMonitor.sln
dotnet run --project .\src\FamilyPCMonitor.App\FamilyPCMonitor.App.csproj
```

This workspace contains the complete source files. On first launch, the app creates `%LOCALAPPDATA%\FamilyPCMonitor\events.db` and `settings.json`. Build output is under `src\FamilyPCMonitor.App\bin\Debug\net8.0-windows`.

## Run and debug

Use the `dotnet run` command above, or open the folder in VS Code and press `F5`, selecting the C# debugger if prompted. The app stays in the notification area when its window is minimized or closed; select its green information icon to reopen it. Use the tray menu's **Exit** item to quit. Its visible dashboard shows monitoring status, computer, account, latest event, database path, and whether a webhook has been saved.

## Settings and event behavior

The Monitoring tab controls whether events are recorded and whether the app starts when the current user signs in. The startup option creates a normal, visible per-user Windows Run entry; turn it off in the app to remove the entry. The Windows administrator can also inspect it in Task Manager > Startup apps. The monitor observes the current interactive user session. It does not install a hidden service or start before a user signs in.

The event database has `Events(Id, EventType, TimestampUtc, LocalTimestamp, ComputerName, WindowsUser, SentToDiscord, CreatedAt)`. UTC is used for retention and event ordering; local timestamps are displayed. History shows up to 2,000 newest records, supports text search, and exports CSV. Retention can be set to 7, 30, 90 days, or forever. **Delete all locally stored logs** permanently clears the event table.

Windows may not give an application a chance to run when power is suddenly removed, the system crashes, or a forced reset occurs. In those cases the final shutdown event cannot be guaranteed. Startup and session events are recorded once this visible app launches and Windows sends the session notification. A restart is represented by the shutdown boundary followed by the next startup; Windows does not provide a reliable distinction for every restart path to this user-mode app.

## Discord webhook setup

In your Discord server, open the destination text channel's settings, choose **Integrations > Webhooks > New Webhook**, select the channel, and copy the webhook URL. In the app's Discord tab, paste it into the password field and save. The app validates HTTPS Discord webhook URLs and protects the saved secret using Windows DPAPI scoped to the current Windows user. It never displays the saved URL. Select desired event checkboxes and **Save notification settings**, then use **Test Discord Notification**.

Webhook notifications contain event name, device name, Windows account name, and local event time. Failed deliveries remain pending in SQLite and are retried on later activity/app startup with bounded exponential delay. The `SentToDiscord` field avoids duplicate sends after a successful HTTP response. Discord can accept a message while a response is lost, so exactly-once delivery cannot be guaranteed across network failure. Credentials are not included in app log messages; the app currently reports operational errors in the UI and does not write a verbose log file.

## Discord bot

This release sends notifications through a webhook. It does not include an interactive Discord bot or slash-command endpoint. The optional `/status`, `/devices`, `/logs`, `/today`, `/history`, `/settings`, and `/test` bot requested in the original specification needs a separately hosted Discord application, authorization policy, and secure token lifecycle; it must not be treated as implemented by the webhook integration. Do not paste a bot token into this app.

For a bot, use the official [Discord Developer Portal](https://discord.com/developers/applications): create an application, add a Bot user, enable only required intents, and invite it to a private family server with `applications.commands` and the minimum channel access needed. Protect the bot token as a secret and rotate it if exposed. Enable Developer Mode in Discord settings to copy server/channel/user IDs. Create an administrator role and configure an allowlist of role or user IDs in the bot. This app does not read bot tokens or verify Discord command permissions.

## Test plan

The app has no separate automated test project yet. Test on a computer you administer, using a non-critical account:

1. Launch the app: dashboard should show monitoring enabled; the database should contain `Startup` with UTC and local time, computer, and Windows username.
2. Sign out and back in while start-with-Windows is enabled: the app should launch and record its app startup. Windows may report session logon only if the session notification is active at the time.
3. Press `Win+L`: expect `Lock`; unlock: expect `Unlock`.
4. Sign out: expect a `Logout` event if Windows delivers the session change before process exit.
5. Restart and perform a normal shutdown: expect a best-effort `Shutdown` near the session ending and a `Startup` next run. A restart is not reliably labeled separately by this user-mode listener.
6. Configure Discord and use the test button: expect a test message. Then disconnect the network or use an invalid webhook and create an event; it stays pending locally and retries when connectivity returns or the app resumes sending.
7. To simulate a database failure, close the app and temporarily rename `events.db`, launch it, and observe a database error. Close the app and restore the original filename. Do not do this while important queued events are pending.
8. Exit and reopen from the Start menu or run command: earlier records remain. Repeated successful delivery should have `SentToDiscord=1`; queued failed events stay `0`.

Inspect history in the app and database at `%LOCALAPPDATA%\FamilyPCMonitor\events.db` using a SQLite viewer if desired. No notification can be delivered during a power loss or without network access.

## Publish and install

For a simple local build:

```powershell
dotnet publish .\src\FamilyPCMonitor.App\FamilyPCMonitor.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published application is under `src\FamilyPCMonitor.App\bin\Release\net8.0-windows\win-x64\publish`. A practical installer option is [Inno Setup](https://jrsoftware.org/isinfo.php). Install Inno Setup, create a new script, and point its application executable to the published `FamilyPCMonitor.exe`; include the publish directory contents and create a Start-menu shortcut. Do not package `settings.json` or a webhook secret. The app creates per-user configuration and database on first run. The HKCU Run entry is added only when the user turns on **Start with Windows** and is removed by turning it off. Inno Setup uninstall removes installed program files and shortcuts; user event/configuration data in `%LOCALAPPDATA%\FamilyPCMonitor` is intentionally preserved. Remove that folder manually only if you want to erase it. MSIX and WiX are also options for managed deployment, but Inno Setup is simpler for a first local Windows installer.

## Security and privacy

Run as a standard Windows user. The app writes only to that user's LocalAppData folder and per-user Run key. Webhook URL validation rejects non-HTTPS and non-Discord hosts. DPAPI encryption is tied to the Windows account that saved it; another account cannot reuse the protected value. Back up event data separately if needed. The app sends only enabled event fields to the webhook destination. Keep the Discord channel private and periodically review who can access it. No bot token, user ID, or role ID is needed for webhook operation.

## Troubleshooting

- `dotnet` not recognized: install the .NET 8 SDK, close and reopen the terminal, then run `dotnet --info`.
- Restore/build fails: verify internet access for the NuGet package restore, then rerun `dotnet restore`.
- No session events: ensure the app is running in the signed-in interactive session. Check that monitoring is enabled.
- No Discord message: check the channel webhook, network connection, notification toggles, and whether `SentToDiscord` is still false; save a fresh webhook if needed.
- The app cannot decrypt a webhook after changing Windows accounts: enter and save the webhook again from the account running the monitor.
- Shutdown event missing: Windows can terminate without delivering a user-mode shutdown callback; this is an operating-system limitation.
