# sftui // Steam Friends List TUI

A beautiful, real-time command-line interface for viewing* your Steam friends list. Monitor your friends' online status, current games, and last seen times directly from your terminal.

*yes, read only for now™.. I am working on interactiveness, see [issues](https://github.com/AdamWHY2K/steam_friends_list_tui/issues).

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)](https://github.com/AdamWHY2K/steam_friends_list_tui)

<img width="1062" height="1007" alt="sftui_combined" src="https://github.com/user-attachments/assets/b965b39b-c3b2-4139-9776-00765496b559" />
The first terminal is terminator with transparent background, and disabled title/scroll bars. Second is konsole, again with disabled bars. Third is default gnome-terminal.

## Features

*tldr: it's the steam friends list with less features and more bugs but in the terminal...* :)

✨ **Rich Console Interface** - Beautiful terminal UI powered by Spectre.Console with colors and interactive elements

🔄 **Real-time Updates** - Instantly see changes as your friends come online, go offline, or start/stop playing games

👁️ **Last Seen Tracking** - View when offline friends were last online

🔐 **Secure Authentication** - QR code authentication with encrypted token storage - authenticate once, use repeatedly

🌐 **Cross-platform** - Works seamlessly (I hope) on Windows, macOS, and Linux

⚡ **Lightweight** - Minimal resource usage with efficient real-time Steam API integration

## Quick Start

### Prerequisites

- Steam Account: Steam Mobile app installed on phone (for initial authentication)
- Operating System: Windows 10+, macOS 10.15+, or Linux (most distributions)
- Network: Internet connection for Steam API access
- Terminal: Modern terminal with Unicode support (Windows Terminal, iTerm2, gnome-terminal, etc.)
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) or later _*_

_* only if you plan to build from source, as the releases are self contained_

### Installation

#### Option 1: Download Release (Recommended)
1. Download the latest release for your platform from the [Releases page](https://github.com/AdamWHY2K/steam_friends_list_tui/releases/latest)
   - **Windows**: `win-x64.zip`
   - **macOS**: `osx-x64.zip` 
   - **Linux**: `linux-x64.gz`
2. Extract the sftui executable to your desired location
3. Run `sftui`

#### Option 2: Build from Source (Cross-Platform)
```bash
# Clone the repository
git clone https://github.com/AdamWHY2K/steam_friends_list_tui.git
cd steam_friends_list_tui

# Build for your current platform
dotnet build -c Release
```

### Usage

```bash
# Windows (PowerShell/Command Prompt)
sftui.exe
sftui.exe --debug

# macOS/Linux
./sftui
./sftui --debug

# Using dotnet run (from source)
dotnet run
dotnet run -- --debug
```

## Authentication

The application uses Steam's official QR code authentication system for secure, convenient access:

### First-time Setup
1. Launch the application - a QR code will be displayed in your terminal
2. Open the **Steam Mobile app** on your phone
3. Navigate to the Steam Guard screen
4. Scan the QR code displayed in the terminal
5. Your authentication tokens will be encrypted and saved for future use

### Subsequent Usage
- The app automatically uses your saved authentication tokens
- No QR code scanning required unless tokens expire or become invalid
- Automatic re-authentication prompts when needed

### Security Features
- 🔒 Tokens are automatically encrypted using machine-specific keys
- 📁 Stored in secure application data directory with restricted permissions
- 🚫 Tokens cannot be transferred between devices
- 🗑️ Manual token clearing: delete `auth_tokens.json` in the sftui data folder

## Keyboard Controls

| Key | Action |
|-----|--------|
| `↑/↓/j/k` | Navigate friends list |
| `Page Up/Down` | Fast scroll |
| `Home/End` | Jump to top/bottom |
| `Ctrl+C/ESC/Q` | Quit application |

## Privacy & Data

- **No data collection**: This application does not collect, store, or transmit any personal data
- **Local authentication**: All authentication tokens are stored locally and encrypted
- **Steam API only**: Uses official Steam APIs - no third-party services involved
- **Open source**: Full source code available for audit and verification

## Technical Details

### Built With
- **[.NET 9.0](https://dotnet.microsoft.com/)** - Modern, cross-platform runtime
- **[SteamKit2](https://github.com/SteamRE/SteamKit)** - Robust Steam API library
- **[Spectre.Console](https://spectreconsole.net/)** - Rich terminal UI framework
- **[QRCoder](https://github.com/codebude/QRCoder)** - QR code generation for authentication
- **[DotNetEnv](https://github.com/bolorundurowb/dotNetEnv)** - Environment configuration

### Architecture
- Clean, modular C# codebase with separation of concerns
- Event-driven Steam API integration for real-time updates
- Secure token management with encryption and proper file permissions
- Responsive terminal UI with efficient rendering

## Support the Project

If you find this project useful, please consider:
- 🥺 **[Buying me a coffee](https://www.ko-fi.com/adamwhy2k)** to support ongoing development
- ⭐ **Starring the repository**
- 🐛 **Reporting bugs** or suggesting improvements
- 🔀 **Contributing code** or documentation
- 💬 **Sharing** with friends who might find it useful

---

*Made with ❤️ for the Steam nerds* <sup><sup><sup><sup><sup><sup><sup><sup>it's me, i am  the steam nerd</sup></sup></sup></sup></sup></sup></sup></sup>
