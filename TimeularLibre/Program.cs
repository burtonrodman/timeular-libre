using InTheHand.Bluetooth;
using System.Text.Json;
using System.Linq;

// WinRT APIs are referenced with fully-qualified names in the Windows-only code path

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

        // CLI: allow saving DeviceId to config when platform doesn't support the system picker
        if (args != null && args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--device-id" || args[i] == "--set-device-id")
                {
                    if (i + 1 < args.Length)
                    {
                        _config.DeviceId = args[i + 1];
                        SaveConfig(_config);
                        Console.WriteLine($"Saved DeviceId to config: {_config.DeviceId}");
                    }
                    break;
                }
            }
        }

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

    private static async Task<bool> ConnectAndListenWindowsAsync()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var orientationGuid = Guid.Parse(ORIENTATION_UUID);

            // Enumerate BLE devices via WinRT
            var selector = Windows.Devices.Bluetooth.BluetoothLEDevice.GetDeviceSelector();
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
            Windows.Devices.Enumeration.DeviceInformation? selectedInfo = null;

            if (_config?.DeviceId is not null)
            {
                selectedInfo = devices.FirstOrDefault(d => d.Id == _config.DeviceId || d.Name == _config.DeviceName);
                if (selectedInfo != null)
                    Console.WriteLine($"Using configured device: {selectedInfo.Name ?? selectedInfo.Id}");
            }

            // Auto-select if exactly one device with the expected Timeular name is present
            var winrtTrackers = devices.Where(d => string.Equals(d.Name, "Timeular Tracker", StringComparison.OrdinalIgnoreCase)).ToList();
            if (winrtTrackers.Count == 1 && selectedInfo == null)
            {
                selectedInfo = winrtTrackers[0];
                Console.WriteLine("Auto-selecting Timeular Tracker");
            }

            if (selectedInfo == null)
            {
                if (devices.Count == 0)
                {
                    Console.WriteLine("No BLE devices found. Pair in OS settings or add DeviceId to config.");
                    return false;
                }

                for (int i = 0; i < devices.Count; i++)
                    Console.WriteLine($"{i + 1}: {devices[i].Name ?? devices[i].Id} ({devices[i].Id})");

                Console.Write("Select device number: ");
                var line = Console.ReadLine();
                if (!int.TryParse(line, out var choice) || choice < 1 || choice > devices.Count)
                {
                    Console.WriteLine("Invalid selection.");
                    return false;
                }

                selectedInfo = devices[choice - 1];
            }

            var bleDevice = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromIdAsync(selectedInfo.Id);
            if (bleDevice == null)
            {
                Console.WriteLine("Failed to open device.");
                return true; // handled but failed to open
            }

            Console.WriteLine($"Connecting to device: {bleDevice.Name ?? "Unknown"}");
            _eventLogger?.LogEvent("Connected", bleDevice.Name ?? selectedInfo.Id);

            if (_config != null)
            {
                _config.DeviceId = selectedInfo.Id;
                _config.DeviceName = selectedInfo.Name;
                SaveConfig(_config);
            }

            var servicesResult = await bleDevice.GetGattServicesAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
            if (servicesResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
            {
                Console.WriteLine("Failed to enumerate GATT services.");
                bleDevice.Dispose();
                return true;
            }

            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic? orientationChar = null;
            foreach (var svc in servicesResult.Services)
            {
                var charsResult = await svc.GetCharacteristicsAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                if (charsResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success) continue;
                foreach (var ch in charsResult.Characteristics)
                {
                    if (ch.Uuid == orientationGuid)
                    {
                        orientationChar = ch;
                        break;
                    }
                }
                if (orientationChar != null) break;
            }

            if (orientationChar == null)
            {
                Console.WriteLine("Could not find orientation characteristic!");
                bleDevice.Dispose();
                return true;
            }

            Console.WriteLine("Found orientation characteristic. Starting notifications...");

            // Keep last known orientation (used by notifications + polling fallback)
            byte? lastOrientation = null;

            Windows.Foundation.TypedEventHandler<Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs> handler = (sender, args) =>
            {
                // Dump raw buffer for diagnostics
                var readerAll = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
                var rawBytes = new List<byte>();
                while (readerAll.UnconsumedBufferLength > 0)
                {
                    rawBytes.Add(readerAll.ReadByte());
                }

                var timestamp = DateTime.UtcNow;

                byte orientation = rawBytes.Count > 0 ? rawBytes[0] : (byte)0;
                lastOrientation = orientation;

                string side = orientation == 0 ? "No orientation" :
                    (_config?.SideLabels.ContainsKey(orientation) == true ? _config.SideLabels[orientation] : $"Side {orientation}");

                Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Flip detected: {side}");
                _eventLogger?.LogEvent("Flip", side, orientation);
            };

            orientationChar.ValueChanged += handler;

            // Choose Notify vs Indicate based on characteristic properties
            var props = orientationChar.CharacteristicProperties;
            var cccdValue = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None;
            if (props.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Notify))
                cccdValue = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify;
            else if (props.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Indicate))
                cccdValue = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Indicate;

            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus setResult = Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success;
            if (cccdValue != Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
            {
                setResult = await orientationChar.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
            }
            else
            {
                Console.WriteLine("Characteristic does not support Notify/Indicate; using polling fallback only.");
            }

            if (setResult != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success && cccdValue != Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
            {
                Console.WriteLine("Failed to start notifications/indications.");
                orientationChar.ValueChanged -= handler;
                bleDevice.Dispose();
                return true;
            }

            // Start a periodic read fallback in case the device doesn't push notifications
            _ = Task.Run(async () =>
            {
                while (_isRunning && bleDevice.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected)
                {
                    try
                    {
                        var readResult = await orientationChar.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                        if (readResult.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
                        {
                            var rdr = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                            byte val = 0;
                            if (rdr.UnconsumedBufferLength > 0) val = rdr.ReadByte();

                            if (lastOrientation == null || val != lastOrientation)
                            {
                                lastOrientation = val;
                                var ts = DateTime.UtcNow;
                                string side = val == 0 ? "No orientation" : (_config?.SideLabels.ContainsKey(val) == true ? _config.SideLabels[val] : $"Side {val}");
                                Console.WriteLine($"[{ts:yyyy-MM-dd HH:mm:ss}] Flip detected: {side}");
                                _eventLogger?.LogEvent("Flip", side, val);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Periodic read failed: {ex.Message}");
                    }

                    await Task.Delay(1000);
                }
            });

            Console.WriteLine("Listening for flips... Press Ctrl+C to exit.");

            while (_isRunning && bleDevice.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected)
            {
                await Task.Delay(1000);
            }

            Console.WriteLine("\nDisconnecting...");
            try
            {
                await orientationChar.WriteClientCharacteristicConfigurationDescriptorAsync(Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None);
                orientationChar.ValueChanged -= handler;
                _eventLogger?.LogEvent("Disconnected", bleDevice.Name ?? selectedInfo.Id);
                bleDevice.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnect: {ex.Message}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bluetooth path failed: {ex.Message}");
            _eventLogger?.LogEvent("Error", ex.Message);
            return false;
        }
    }

    private static async Task ConnectAndListenAsync()
    {
        // Platform-specific Bluetooth path: prefer WinRT on Windows
        if (OperatingSystem.IsWindows())
        {
            var handled = await ConnectAndListenWindowsAsync();
            if (handled) return;
        }

        // Request Bluetooth device
        Console.WriteLine("Scanning for Timeular device...");
        Console.WriteLine("Please select your Timeular device from the system dialog (if supported).");
        
        var options = new RequestDeviceOptions
        {
            AcceptAllDevices = true
        };
        
        // Add optional services
        options.OptionalServices.Add(BluetoothUuid.FromGuid(Guid.Parse(ORIENTATION_UUID)));

        // Try to use configured device (avoid system dialog when possible)
        BluetoothDevice? device = null;

        if (_config?.DeviceId is not null)
        {
            try
            {
                var paired = (await Bluetooth.GetPairedDevicesAsync()).ToList();
                device = paired.FirstOrDefault(d => d.Id == _config.DeviceId || d.Name == _config.DeviceName);
                if (device != null)
                {
                    Console.WriteLine($"Using configured device: {device.Name ?? device.Id}");
                }
            }
            catch
            {
                // ignore and fall back to picker/paired-list
            }
        }

        if (device == null)
        {
            try
            {
                // Try auto-select paired Timeular Tracker if it's the only matched paired device
                try
                {
                    var pairedAll = (await Bluetooth.GetPairedDevicesAsync()).ToList();
                    var trackers = pairedAll.Where(d => string.Equals(d.Name, "Timeular Tracker", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (trackers.Count == 1)
                    {
                        device = trackers[0];
                        Console.WriteLine("Auto-selecting paired Timeular Tracker (single match).");
                    }
                }
                catch
                {
                    // ignore paired-device enumeration errors
                }

                if (device == null)
                {
                    // Primary path: system device-picker (may throw on some console-only platforms)
                    device = await Bluetooth.RequestDeviceAsync(options);
                }
            }
            catch (PlatformNotSupportedException) { /* fallback below */ }
            catch (NotSupportedException) { /* fallback below */ }
            catch (InvalidOperationException) { /* fallback below */ }
        }

        // If picker not available or returned null, fall back to paired-device console selection
        if (device == null)
        {
            Console.WriteLine("System device-picker not available — listing paired BLE devices...");
            try
            {
                var paired = (await Bluetooth.GetPairedDevicesAsync()).ToList();

                // If exactly one paired device matches the Timeular name, auto-select it
                var trackers = paired.Where(d => string.Equals(d.Name, "Timeular Tracker", StringComparison.OrdinalIgnoreCase)).ToList();
                if (trackers.Count == 1)
                {
                    device = trackers[0];
                    Console.WriteLine("Only one paired device named 'Timeular Tracker' found — auto-selecting it.");
                }
                else
                {
                    if (paired.Count == 0)
                    {
                        Console.WriteLine("No paired BLE devices found. Please pair your Timeular device in OS settings or add its DeviceId to the config file.");
                        _isRunning = false;
                        return;
                    }

                    for (int i = 0; i < paired.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}: {paired[i].Name ?? paired[i].Id} ({paired[i].Id})");
                    }

                    Console.Write("Select device number: ");
                    var line = Console.ReadLine();
                    if (!int.TryParse(line, out var choice) || choice < 1 || choice > paired.Count)
                    {
                        Console.WriteLine("Invalid selection.");
                        _isRunning = false;
                        return;
                    }

                    device = paired[choice - 1];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list paired devices: {ex.Message}");

                // Interactive fallback for environments that don't support the system picker
                Console.WriteLine("Your platform doesn't support in-app Bluetooth device selection.");
                Console.Write("If you have the Timeular device ID, enter it now to save to the configuration (or press Enter to cancel): ");
                var manualId = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(manualId))
                {
                    if (_config == null) _config = new TimeularConfig();
                    _config.DeviceId = manualId.Trim();
                    Console.Write("Optional: Device name to store with the ID (or press Enter to skip): ");
                    var manualName = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(manualName)) _config.DeviceName = manualName.Trim();
                    SaveConfig(_config);
                    Console.WriteLine($"Saved DeviceId to config. Restart the app to use it: {ConfigFilePath}");
                }

                _isRunning = false;
                return;
            }
        }
        
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
                // Dump raw bytes for diagnostics
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

        // Poll fallback for InTheHand path (read characteristic periodically if notifications are silent)
        _ = Task.Run(async () =>
        {
            byte? last = null;
            while (_isRunning && gatt.IsConnected)
            {
                try
                {
                    var read = await orientationChar.ReadValueAsync();
                    if (read != null && read.Length > 0)
                    {
                        var val = read[0];
                        if (last == null || val != last)
                        {
                            last = val;
                            var ts = DateTime.UtcNow;
                            string side = val == 0 ? "No orientation" : (_config?.SideLabels.ContainsKey(val) == true ? _config.SideLabels[val] : $"Side {val}");
                            Console.WriteLine($"[{ts:yyyy-MM-dd HH:mm:ss}] Flip detected: {side}");
                            _eventLogger?.LogEvent("Flip", side, val);
                        }
                    }
                }
                catch
                {
                    // ignore poll errors
                }

                await Task.Delay(1000);
            }
        });

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
