# Steam Friends CLI

View your Steam friends list from the command line.

## Features

- **Rich Console Interface**: Beautiful terminal UI built with Spectre.Console
- **Real-time Updates**: Friends list updates automatically when friends change status or open/close games
- **Last Seen Tracking**: Displays when offline friends were last online
- **QR Code Authentication**: Easy login using Steam Mobile app
- **Cross-platform**: Works on Windows, macOS, and Linux

## Authentication

The application uses Steam's QR code authentication system. When you first run the app:

1. A QR code will be displayed in the terminal
2. Open the Steam Mobile app
3. Scan the QR code to authenticate
4. The console interface will start once authentication is complete

## Dependencies

- .NET 9.0
- SteamKit2 - Steam API library
- Spectre.Console - Rich terminal interface framework
- QRCoder - QR code generation for authentication
