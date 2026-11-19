# xContextGuard

A native Windows context-menu and Win+X menu manager built with C# and WPF.

## Purpose

Inspect, edit, and safely manage Windows context menus and the **Win+X (Power User) menu**:

- **Win+X Menu Editing & Creation**: Add new shortcuts, edit existing targets, names, arguments, group assignments (Group 1, 2, 3), and "Run as administrator" flags.
- **Win+X Integrity Hashing**: Automatically calculates and injects the required `PKEY_WinX_Hash` property store signature so Windows accepts user-modified and custom Win+X shortcuts.
- **Enable**: Re-enable supported disabled context-menu registrations and Win+X shortcuts.
- **Disable**: Reversibly disable context-menu entries and Win+X shortcuts without losing configuration.
- **Delete**: Permanently delete registrations or shortcuts, with an automatic backup snapshot saved first.
- **Backup & Restore**: Safely back up and restore captured registry subtrees and Win+X shortcut files.
- **Restart Explorer**: Convenient one-click option to restart Windows Explorer and reload menu changes immediately.
- **Search & Filter**: Search entries by name, command, or path; filter by menu location (including dedicated Win+X filtering).
- **Modern Theme**: Dark and light modes with adaptive title-bar rendering and responsive layout.

## Implementation details

- **Context Menus**: Scans global registrations for file, folder, folder-background, drive, and desktop-background menus, covering static shell verbs and `ContextMenuHandlers` keys. Disabling static verbs uses the `LegacyDisable` marker, while shell extensions use the reversible `xContextGuard.Disabled:` CLSID marker.
- **Win+X Menu**: Scans shortcuts across `%LOCALAPPDATA%\Microsoft\Windows\WinX` groups (`Group1`, `Group2`, `Group3`). Disabling toggles the `.disabled` extension to preserve the original shortcut binaries, and editing/adding recalculates the hash against Windows' internal salt to prevent Windows from discarding customized links.
- **Backups**: Every change automatically creates a timestamped JSON snapshot before modification. Restoring reinstates the captured state.

## Development

Requirements:
- Windows 10 or Windows 11 x64
- .NET 10 SDK
- Visual Studio 2022 or a compatible editor with the .NET desktop workload

Build and run:

```powershell
dotnet restore .\src\xContextGuard\xContextGuard.csproj
dotnet build .\src\xContextGuard\xContextGuard.csproj -c Release
dotnet run --project .\src\xContextGuard\xContextGuard.csproj
```

GitHub Actions builds the WPF project on Windows for pushes and pull requests.

## Safety

Registry registrations and shortcut properties vary by Windows version and installed applications. Automated backups are created before changes are applied, but always review each path and keep backups. Access to machine-wide entries may require launching xContextGuard as administrator.
