# Timeular Libre

A .NET application to use the Timeular device without a subscription. This app can detect when the device is flipped and maintain a local log of all events.

## Features

- **Flip Detection**: Detects when the Timeular device is flipped to any of its 8 sides
- **Event Logging**: Maintains a local JSON log of all flip events with timestamps
- **Configuration**: Customizable side labels stored in a configuration file
- **Auto-Reconnection**: Automatically attempts to reconnect if the connection is lost
- **Cross-Platform**: Built with .NET 8.0 for cross-platform support
- **Bluetooth LE**: Uses Bluetooth Low Energy for wireless communication

## Requirements

- .NET 8.0 SDK or later
- Timeular Tracker device
- Bluetooth 4.0+ adapter
- Windows, macOS, or Linux (with Bluetooth support)

## Installation

1. Clone the repository:
```bash
git clone https://github.com/burtonrodman/timeular-libre.git
cd timeular-libre
```

2. Build the application:
```bash
cd TimeularLibre
dotnet build
```

## Usage

Run the application:
```bash
dotnet run
```

The application will:
1. Prompt you to select your Timeular device via the system Bluetooth dialog
2. Connect to the device
3. Start listening for flip events
4. Log all events to a JSON file
5. Automatically reconnect if the connection is lost

To stop the application, press `Ctrl+C`.

### Event Log Location

Events are logged to:
- **Windows**: `%APPDATA%\TimeularLibre\events.json`
- **macOS/Linux**: `~/.config/TimeularLibre/events.json`

### Configuration Location

Configuration is stored at:
- **Windows**: `%APPDATA%\TimeularLibre\config.json`
- **macOS/Linux**: `~/.config/TimeularLibre/config.json`

### Event Log Format

Each event is logged as a JSON object:
```json
{
  "Timestamp": "2026-02-17T01:00:00.000",
  "EventType": "Flip",
  "Details": "Side 3",
  "Orientation": 3
}
```

### Configuration File Format

You can customize the labels for each side:
```json
{
  "DeviceId": "device-id",
  "DeviceName": "Timeular Tra",
  "SideLabels": {
    "1": "Meeting",
    "2": "Coding",
    "3": "Email",
    "4": "Break",
    "5": "Planning",
    "6": "Review",
    "7": "Documentation",
    "8": "Research"
  }
}
```

## Device Information

The Timeular device is a Bluetooth LE device shaped like a D8 dice with 8 sides. Each side can be used to track different activities. When flipped:
- Orientation values: 0 (no orientation/flat) or 1-8 (corresponding to each side)
- Events are detected in real-time via Bluetooth notifications

## Technical Details

### Bluetooth UUIDs Used

- **Orientation**: `c7e70012-c847-11e6-8175-8c89a55d403c`
- **Battery**: `00002a19-0000-1000-8000-00805f9b34fb`

### Implementation

The application uses the [InTheHand.BluetoothLE](https://github.com/inthehand/32feet) library for cross-platform Bluetooth LE support.

### Architecture

- **Program.cs**: Main entry point and connection logic
- **EventLogger.cs**: Event logging functionality with thread-safe file operations
- **TimeularConfig.cs**: Configuration model for device settings and side labels

## Future Enhancements

- Azure DevOps integration
- Activity tracking and reporting
- Battery level monitoring and alerts
- Desktop notifications for flip events
- Web dashboard for visualizing activity logs

## License

See LICENSE file for details.

## Credits

Reference implementation: [timeular-python-app](https://github.com/stoffl6781/timeular-python-app)
