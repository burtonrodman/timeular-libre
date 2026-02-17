using InTheHand.Bluetooth;
using System.Text.Json;

namespace TimeularLibre;

class Program
{
    // Timeular BLE UUIDs
    private const string ORIENTATION_UUID = "c7e70012-c847-11e6-8175-8c89a55d403c";
    private const string BATTERY_UUID = "00002a19-0000-1000-8000-00805f9b34fb";
    
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimeularLibre",
        "events.json"
    );

    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimeularLibre",
        "config.json"
    );

    private static EventLogger? _eventLogger;
    private static TimeularConfig? _config;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Timeular Libre - Flip Detection and Event Logger");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Initialize logger and config
        _eventLogger = new EventLogger(LogFilePath);
        _config = LoadConfig();

        Console.WriteLine($"Events will be logged to: {LogFilePath}");
        Console.WriteLine($"Configuration file: {ConfigFilePath}");
        Console.WriteLine();

        // Setup Ctrl+C handler
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _isRunning = false;
        };

        while (_isRunning)
        {
            try
            {
                await ConnectAndListenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                _eventLogger?.LogEvent("Error", ex.Message);
                
                if (_isRunning)
                {
                    Console.WriteLine("Will attempt to reconnect in 5 seconds...");
                    await Task.Delay(5000);
                }
            }
        }

        Console.WriteLine("\nShutting down. Goodbye!");
    }

    private static async Task ConnectAndListenAsync()
    {
        // Request Bluetooth device
        Console.WriteLine("Scanning for Timeular device...");
        Console.WriteLine("Please select your Timeular device from the system dialog.");
        
        var options = new RequestDeviceOptions
        {
            AcceptAllDevices = true
        };
        
        // Add optional services
        options.OptionalServices.Add(BluetoothUuid.FromGuid(Guid.Parse(ORIENTATION_UUID)));

        var device = await Bluetooth.RequestDeviceAsync(options);
        
        if (device == null)
        {
            Console.WriteLine("No device selected.");
            _isRunning = false;
            return;
        }

        Console.WriteLine($"Connecting to device: {device.Name ?? "Unknown"}");
        
        var gatt = device.Gatt;
        await gatt.ConnectAsync();
        
        Console.WriteLine("Connected successfully!");
        _eventLogger?.LogEvent("Connected", device.Name ?? "Unknown");

        // Save device info to config
        if (_config != null)
        {
            _config.DeviceId = device.Id;
            _config.DeviceName = device.Name;
            SaveConfig(_config);
        }

        // Get the service containing orientation characteristic
        var services = await gatt.GetPrimaryServicesAsync();
        GattCharacteristic? orientationChar = null;

        foreach (var service in services)
        {
            var characteristics = await service.GetCharacteristicsAsync();
            foreach (var characteristic in characteristics)
            {
                if (characteristic.Uuid == BluetoothUuid.FromGuid(Guid.Parse(ORIENTATION_UUID)))
                {
                    orientationChar = characteristic;
                    break;
                }
            }
            if (orientationChar != null) break;
        }

        if (orientationChar == null)
        {
            Console.WriteLine("Could not find orientation characteristic!");
            gatt.Disconnect();
            return;
        }

        Console.WriteLine("Found orientation characteristic. Starting notifications...");

        // Subscribe to orientation changes
        orientationChar.CharacteristicValueChanged += (sender, args) =>
        {
            var value = args.Value;
            if (value != null && value.Length > 0)
            {
                var orientation = value[0];
                var timestamp = DateTime.UtcNow;
                
                string side = orientation == 0 ? "No orientation" :
                    (_config?.SideLabels.ContainsKey(orientation) == true ? 
                        _config.SideLabels[orientation] : 
                        $"Side {orientation}");
                        
                Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Flip detected: {side}");
                
                _eventLogger?.LogEvent("Flip", side, orientation);
            }
        };

        await orientationChar.StartNotificationsAsync();
        
        Console.WriteLine("Listening for flips... Press Ctrl+C to exit.");
        Console.WriteLine();

        // Keep the application running until cancelled
        while (_isRunning && gatt.IsConnected)
        {
            await Task.Delay(1000);
        }

        Console.WriteLine("\nDisconnecting...");
        try
        {
            await orientationChar.StopNotificationsAsync();
            gatt.Disconnect();
            _eventLogger?.LogEvent("Disconnected", device.Name ?? "Unknown");
            Console.WriteLine("Disconnected successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disconnect: {ex.Message}");
        }
    }

    private static TimeularConfig LoadConfig()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<TimeularConfig>(json);
                if (config != null)
                {
                    Console.WriteLine("Configuration loaded.");
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }

        // Return default config
        return new TimeularConfig();
    }

    private static void SaveConfig(TimeularConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
