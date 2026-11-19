# Enviro
[![Build](https://github.com/regularentropy/enviro/actions/workflows/build.yml/badge.svg)](https://github.com/regularentropy/enviro/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/regularentropy/enviro)](https://github.com/regularentropy/enviro/releases)
[![.NET](https://img.shields.io/badge/.NET-10-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)]()
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

A lightweight environment variable manager for Windows with a classic interface.

![Screenshot](docs/scr.png)

## Why does this exist?

Windows has a built-in environment variable editor, but its interface is outdated and error-prone: values are edited as opaque strings, there is no change tracking to show what was modified, deleted or created, and mistakes are hard to notice and undo.

Enviro addresses this with a classic Windows interface, change tracking for every user and system variable, and a staged apply — nothing is written until you confirm it.

## Features

- 📋 **Change Tracking**: See exactly what was modified, deleted, or created.
- ✅ **Path Validation**: Detects variables pointing to invalid paths.
- 💾 **Import/Export**: Back up and restore your variables to a JSON or .reg file.
- ≠ **Drift Detection**: Flags variables changed outside Enviro (setx, installers, GPO).
- ☑️ **Multi-Select**: Bulk delete, copy, and drift actions on selected rows.
- 🪟 **Registry Jump**: "Open in Registry Editor" jumps straight to the variable's key in regedit.
- 🌓 **Theme Support**: Follows the Windows light/dark mode.
- ⚡ **Instant Apply**: Changes take effect immediately without restart.

## Requirements

- Windows 10 or later
- .NET 10
- Administrator privileges (only for system variable modifications)

## Usage

1. Launch Enviro
2. Edit user variables, or provide admin credentials for system variables
3. Review changes before applying — the view will show all modifications
4. Apply changes

## Keyboard shortcuts

Main window:

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | Add variable |
| `F2` / `Ctrl+E` | Edit selected variable |
| `Delete` | Delete selected variable(s) |
| `Ctrl+C` | Copy value (or `NAME=value` lines for a multi-selection) |
| `Ctrl+Shift+C` | Copy name (or bare names for a multi-selection) |
| `Ctrl+A` | Select all visible rows |
| `Ctrl+F` | Focus the filter box (`Esc` clears it) |
| `F5` | Refresh |
| `Ctrl+S` | Apply pending changes |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo |

Variable editor (PATH-style values):

| Shortcut | Action |
| --- | --- |
| `Ctrl+Up` / `Ctrl+Down` | Move segment up / down (drag also works) |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo segment changes |
| `Enter` / `Esc` | Confirm / Cancel |

Import/export review window:

| Shortcut | Action |
| --- | --- |
| `Space` | Toggle checkbox of the selected row(s) |
| `Delete` | Uncheck selected row(s) |
| `Esc` | Clear the filter |

## Installation

### Windows

1. Download the latest release from the [Releases](https://github.com/regularentropy/enviro/releases) page
2. Extract the archive and run `Enviro.exe`
3. No additional dependencies required

For system-wide variable editing, run as administrator or allow elevation when prompted.

## Contributing
Pull requests are welcome. For major changes, open an issue first to discuss what you want to change.

Build requirements:
- Visual Studio 2026 or later
- .NET 10 SDK

From source:

```shell
git clone https://github.com/regularentropy/enviro.git
cd enviro
dotnet run --project src/enviro/enviro.csproj
```

Self-contained single-file build:

```shell
dotnet publish src/enviro/enviro.csproj --configuration Release --output ./publish/win-x64 --runtime win-x64 --self-contained true /p:PublishSingleFile=true
```

## Used Assets
- Logo: https://www.svgrepo.com/svg/532995/pen-square

## License

[GPLv3](LICENSE) ©2026 regularentropy