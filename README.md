# Steam Friends List TUI

View your Steam friends list from the command line.

## Features

- **Rich Console Interface**: Beautiful terminal UI built with Spectre.Console
- **Real-time Updates**: Friends list updates automatically when friends change status or open/close games
- **Last Seen Tracking**: Displays when offline friends were last online
- **Persistent Authentication**: QR code authentication with automatic token storage - authenticate once, use repeatedly
- **Cross-platform**: Works on Windows, macOS, and Linux

## Authentication

The application uses Steam's QR code authentication system with persistent token storage for convenience:

**First-time setup:**
1. A QR code will be displayed in the terminal
2. Open the Steam Mobile app
3. Scan the QR code to authenticate
4. Your authentication tokens will be saved securely for future use

**Subsequent runs:**
- The app will automatically use your saved authentication tokens
- No QR code scanning required unless tokens expire (after 30 days) or become invalid
- If re-authentication is needed, a new QR code will be automatically displayed

**Token storage:**
- Tokens are encrypted and stored securely in your system's application data directory
- File and directory permissions are set to owner-only access
- Tokens use machine-specific encryption to prevent theft if copied to another device
- Tokens automatically expire after 30 days for security
- You can manually clear tokens by deleting the auth_tokens.json file in the sftui folder

## Dependencies

- .NET 9.0
- SteamKit2 - Steam API library
- Spectre.Console - Rich terminal interface framework
- QRCoder - QR code generation for authentication
