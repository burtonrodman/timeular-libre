# Timeular Libre

A .NET-based system composed of a shared core library, a Windows background service that listens to the Timeular cube, and an optional cloud-hosted web interface. The service detects flips, logs events locally, and can launch a browser to the web UI with the action name as a parameter.

## Features

- **Flip Detection**: Detects when the Timeular device is flipped to any of its 8 sides
- **Event Logging**: Maintains a local JSON log of all flip events with timestamps
- **Configuration**: Customizable side labels stored in a configuration file (local or remote)
- **Auto-Reconnection**: Automatically attempts to reconnect if the connection is lost
- **Windows Service**: Runs as a background worker that launches a browser on flips
- **Config Refresh**: Periodically reloads configuration from a remote endpoint
- **Cross-Platform**: Core library supports any .NET 8.0 host (service, CLI, tests)
- **Bluetooth LE**: Uses Bluetooth Low Energy for wireless communication

## Architecture Overview

The solution now consists of several projects:

- `Timeular.Core` – shared library containing device logic, models, and interfaces
- `Timeular.Service` – Windows worker service that listens to the cube and launches actions
- `Timeular.Web` – optional cloud-hosted web UI for recording actions and integrating with Azure DevOps
- `Timeular.Core.Tests` – unit tests covering core components and the service worker

The service loads configuration locally or via HTTP, starts a cube listener, and responds to flips by logging events and opening the configured URL.

## Running the Service

To run the service for local debugging:

```bash
cd Timeular.Service
dotnet run
```

In production the project can be published and installed as a Windows service using `sc.exe` or `New-Service`. Configuration values (remote endpoint URL, logging paths, etc.) are provided via `appsettings.json` or environment variables (`Config__ConfigUrl`).

The web application includes a simple `/config` GET endpoint that returns a JSON representation of `TimeularConfig`, which the service can poll. Replace the sample implementation with your own logic (e.g. user-specific configurations stored in a database).

The service respects ◦ `IConfigProvider` implementations; by default it fetches `https://example.com/config` if configured, otherwise uses `%APPDATA%\TimeularLibre\config.json`.

## Testing

Unit tests live in `Timeular.Core.Tests`. They exercise the launcher logic and worker behavior.

Run them with:

```bash
dotnet test Timeular.Core.Tests/Timeular.Core.Tests.csproj
```

Any changes to core interfaces should be accompanied by new tests.

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
   - Note: On some console-only platforms the system picker may be unavailable. In that case the app will attempt to list paired devices or allow you to save a DeviceId manually (see "Manual device configuration" below).
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

Events are logged in JSON Lines format (one compact JSON object per line):
```json
{"Timestamp":"2026-02-17T01:00:00.000Z","EventType":"Flip","Details":"Side 3","Orientation":3}
{"Timestamp":"2026-02-17T01:01:15.234Z","EventType":"Flip","Details":"Side 5","Orientation":5}
```

Note: Timestamps are stored in UTC format for consistency across time zones.

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

#### Manual device configuration (console-only platforms)

If the OS/system Bluetooth picker is not available from a console app the program cannot show the device selection dialog. In that case you can:

- Pair the Timeular device in OS Bluetooth settings and run the app again; or
- Manually add the device ID to `config.json` under `DeviceId`; or
- Use the built-in CLI to save a device id:

  - Save device id: `dotnet run -- --device-id <device-id>`

After adding a `DeviceId` the app will prefer the configured device on startup.

## Device Information

The Timeular device is a Bluetooth LE device shaped like a D8 dice with 8 sides. Each side can be used to track different activities. When flipped:
- Orientation values: 0 (no orientation/flat) or 1-8 (corresponding to each side)
- Events are detected in real-time via Bluetooth notifications

## Architecture

The codebase is now divided into three projects:

1. **Timeular.Core** – a class library containing shared models, Bluetooth
   cube logic, configuration providers, event logging, and helper interfaces.
   Other projects reference this library.
2. **Timeular.Service** – a Windows background worker that keeps the cube
   connected, listens for flips, logs events, and launches the system browser
   to the configured web interface with the selected action as a query
   parameter. The service obtains its `WebInterfaceUrl` from configuration
   (either via HTTP from the web app’s `/config` endpoint or from a local
   `config.json` file) and passes that URL plus the action name to an
   `IActionLauncher`.
3. **Timeular.Web** – an ASP.NET Core web application hosted in the cloud.
   In development the app returns its own base URL from `/config`, so running
   the service against a locally‑hosted instance automatically causes flips
   to open a browser pointed at the correct host. The UI displays the action
   and provides links for recording it or attaching to Azure DevOps work items.

### Running the service

1. Build the solution: `dotnet build` from the root folder.
2. Configure the remote endpoint via `appsettings.json` or the
   `Config:ConfigUrl` environment variable. This URL should point at the
   web app’s `/config` endpoint and return a JSON payload matching
   `TimeularConfig` (in development the JSON itself will include the
   current host address). **For convenience the service will now automatically
   probe the two common localhost URLs (`http://localhost:5036/config` and
   `https://localhost:7031/config`) if it’s running in a Development
   environment and you haven’t supplied any `ConfigUrl` – so simply starting
   both projects side‑by‑side is sufficient for local debugging.**

   If you start the service with no working HTTP endpoint it falls back to a
   local file provider; `WebInterfaceUrl` will be left empty and the worker
   will log a warning to that effect.
3. Install the service using `sc.exe` or `New-Service` and start it, or
   run `dotnet run` inside `Timeular.Service` for console debugging.
5. When the cube is flipped the service logs the event and then invokes the
   `IActionLauncher`; with the default implementation this simply calls
   `Process.Start` to open the system browser to the configured URL
   (e.g. `http://localhost:5036/?action=Work`).

The legacy console application (`TimeularLibre`) remains for
local experimentation but has been simplified to delegate most work to
`Timeular.Core`.

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
