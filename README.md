# Timeular Libre

A .NET application to use the Timeular device without a subscription. This app can detect when the device is flipped and maintain a local log of all events.

## Features

- **Flip Detection**: Detects when the Timeular device is flipped to any of its 8 sides
- **Event Logging**: Maintains a local JSON log of all flip events with timestamps
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

### Event Log Location

Events are logged to:
- **Windows**: `%APPDATA%\TimeularLibre\events.json`
- **macOS/Linux**: `~/.config/TimeularLibre/events.json`

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

## Future Enhancements

- Azure DevOps integration
- Activity tracking and reporting
- Configuration for custom side labels
- Battery level monitoring
- Automatic reconnection

## License

See LICENSE file for details.

## Credits

Reference implementation: [timeular-python-app](https://github.com/stoffl6781/timeular-python-app)
