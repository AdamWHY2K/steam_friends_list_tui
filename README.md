# Steam Friends CLI

View your Steam friends list from the command line.

## Features

- **Modern Terminal UI**: Interactive TUI built with Terminal.Gui
- **Real-time Updates**: Friends list updates automatically when friends change status or open/close games
- **Last Seen Tracking**: Displays when offline friends were last online
- **QR Code Authentication**: Easy login using Steam Mobile app
- **Cross-platform**: Works on Windows, macOS, and Linux

## Authentication

The application uses Steam's QR code authentication system. When you first run the app:

1. A QR code will be displayed in the terminal
2. Open the Steam Mobile app
3. Scan the QR code to authenticate
4. The GUI will start once authentication is complete

## Dependencies

- .NET 9.0
- SteamKit2 - Steam API library
- Terminal.Gui - Modern terminal UI framework
- QRCoder - QR code generation for authentication
