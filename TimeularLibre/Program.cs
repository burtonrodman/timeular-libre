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

    static async Task Main(string[] args)
    {
        Console.WriteLine("Timeular Libre - Flip Detection and Event Logger");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Ensure log directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

        try
        {
            // Request Bluetooth device with optional services
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
                Console.WriteLine("No device selected. Exiting...");
                return;
            }

            Console.WriteLine($"Connecting to device: {device.Name ?? "Unknown"}");
            
            var gatt = device.Gatt;
            await gatt.ConnectAsync();
            
            Console.WriteLine("Connected successfully!");
            LogEvent("Connected", device.Name ?? "Unknown", null);

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
                    var timestamp = DateTime.Now;
                    
                    string side = orientation == 0 ? "No orientation" : $"Side {orientation}";
                    Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Flip detected: {side}");
                    
                    LogEvent("Flip", side, orientation);
                }
            };

            await orientationChar.StartNotificationsAsync();
            
            Console.WriteLine("Listening for flips... Press Ctrl+C to exit.");
            Console.WriteLine($"Events are being logged to: {LogFilePath}");
            Console.WriteLine();

            // Keep the application running
            var tcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                tcs.SetResult(true);
            };

            await tcs.Task;

            Console.WriteLine("\nDisconnecting...");
            await orientationChar.StopNotificationsAsync();
            gatt.Disconnect();
            LogEvent("Disconnected", device.Name ?? "Unknown", null);
            Console.WriteLine("Disconnected. Goodbye!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            LogEvent("Error", ex.Message, null);
        }
    }

    private static void LogEvent(string eventType, string details, int? orientation)
    {
        try
        {
            var logEntry = new
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Details = details,
                Orientation = orientation
            };

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            // Append to log file
            File.AppendAllText(LogFilePath, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log event: {ex.Message}");
        }
    }
}
