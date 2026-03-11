using InTheHand.Bluetooth;

namespace Timeular.Core;

public class BluetoothCubeListener : ICubeListener
{
    private const string ORIENTATION_UUID = "c7e70012-c847-11e6-8175-8c89a55d403c";
    public TimeularConfig Config { get; set; }
    private readonly EventLogger? _logger;
    private CancellationTokenSource? _cts;

    public event EventHandler<FlipEventArgs>? FlipOccurred;

    public BluetoothCubeListener(TimeularConfig config, EventLogger? logger = null)
    {
        Config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(token);
            }
            catch (Exception ex)
            {
                _logger?.LogEvent("Error", ex.Message);
                await Task.Delay(5000, token);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task ConnectAndListenAsync(CancellationToken token)
    {
#if WINDOWS
        // prefer Windows-specific path if available
        if (OperatingSystem.IsWindows())
        {
            var handled = await ConnectAndListenWindowsAsync(token);
            if (handled) return;
        }
#endif
        // fallback to InTheHand implementation
        await ConnectAndListenCrossPlatformAsync(token);
    }

#if WINDOWS
    private async Task<bool> ConnectAndListenWindowsAsync(CancellationToken token)
    {
        try
        {
            var orientationGuid = Guid.Parse(ORIENTATION_UUID);
            var selector = Windows.Devices.Bluetooth.BluetoothLEDevice.GetDeviceSelector();
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector).AsTask(token);
            Windows.Devices.Enumeration.DeviceInformation? selectedInfo = null;

            if (!string.IsNullOrEmpty(Config.DeviceId))
            {
                selectedInfo = devices.FirstOrDefault(d => d.Id == Config.DeviceId || d.Name == Config.DeviceName);
                if (selectedInfo != null)
                    _logger?.LogEvent("Info", $"Using configured device: {selectedInfo.Name ?? selectedInfo.Id}");
            }

            var trackers = devices.Where(d => string.Equals(d.Name, "Timeular Tracker", StringComparison.OrdinalIgnoreCase)).ToList();
            if (trackers.Count == 1 && selectedInfo == null)
                selectedInfo = trackers[0];

            if (selectedInfo == null)
            {
                if (devices.Count == 0)
                {
                    _logger?.LogEvent("Info", "No BLE devices found.");
                    return false;
                }
                // pick first
                selectedInfo = devices[0];
            }

            var bleDevice = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromIdAsync(selectedInfo.Id).AsTask(token);
            if (bleDevice == null)
                return true;

            _logger?.LogEvent("Connected", bleDevice.Name ?? selectedInfo.Id);
            if (Config != null)
            {
                Config.DeviceId = selectedInfo.Id;
                Config.DeviceName = selectedInfo.Name;
            }

            var servicesResult = await bleDevice.GetGattServicesAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(token);
            if (servicesResult.Status != Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
            {
                bleDevice.Dispose();
                return true;
            }
            Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic? orientationChar = null;
            foreach (var svc in servicesResult.Services)
            {
                var charsResult = await svc.GetCharacteristicsAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(token);
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
                bleDevice.Dispose();
                return true;
            }

            byte? lastOrientation = null;
            Windows.Foundation.TypedEventHandler<Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, Windows.Devices.Bluetooth.GenericAttributeProfile.GattValueChangedEventArgs> handler = (sender, args) =>
            {
                var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
                byte orientation = reader.UnconsumedBufferLength > 0 ? reader.ReadByte() : (byte)0;
                lastOrientation = orientation;
                var side = orientation == 0 ? "No orientation" : (Config.SideLabels.TryGetValue(orientation, out var lab) ? lab : $"Side {orientation}");
                _logger?.LogEvent("Flip", side, orientation);
                FlipOccurred?.Invoke(this, new FlipEventArgs(orientation, side));
            };

            orientationChar.ValueChanged += handler;
            var props = orientationChar.CharacteristicProperties;
            var cccd = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None;
            if (props.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Notify))
                cccd = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Notify;
            else if (props.HasFlag(Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicProperties.Indicate))
                cccd = Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.Indicate;

            if (cccd != Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None)
                await orientationChar.WriteClientCharacteristicConfigurationDescriptorAsync(cccd).AsTask(token);

            // polling fallback
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && bleDevice.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected)
                {
                    try
                    {
                        var readResult = await orientationChar.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached).AsTask(token);
                        if (readResult.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
                        {
                            var rdr = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                            byte val = rdr.UnconsumedBufferLength > 0 ? rdr.ReadByte() : (byte)0;
                            if (lastOrientation == null || val != lastOrientation)
                            {
                                lastOrientation = val;
                                var side = val == 0 ? "No orientation" : (Config.SideLabels.TryGetValue(val, out var lab) ? lab : $"Side {val}");
                                _logger?.LogEvent("Flip", side, val);
                                FlipOccurred?.Invoke(this, new FlipEventArgs(val, side));
                            }
                        }
                    }
                    catch { }
                    await Task.Delay(1000, token);
                }
            });

            while (!token.IsCancellationRequested && bleDevice.ConnectionStatus == Windows.Devices.Bluetooth.BluetoothConnectionStatus.Connected)
            {
                await Task.Delay(1000, token);
            }

            try
            {
                await orientationChar.WriteClientCharacteristicConfigurationDescriptorAsync(Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue.None).AsTask(token);
                orientationChar.ValueChanged -= handler;
                bleDevice.Dispose();
            }
            catch { }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogEvent("Error", ex.Message);
            return false;
        }
    }
#endif
    private async Task ConnectAndListenCrossPlatformAsync(CancellationToken token)
    {
        var options = new RequestDeviceOptions { AcceptAllDevices = true };
        options.OptionalServices.Add(BluetoothUuid.FromGuid(Guid.Parse(ORIENTATION_UUID)));

        BluetoothDevice? device = null;
        try
        {
            var paired = (await Bluetooth.GetPairedDevicesAsync()).ToList();
            var trackers = paired.Where(d => string.Equals(d.Name, "Timeular Tracker", StringComparison.OrdinalIgnoreCase)).ToList();
            if (trackers.Count == 1)
            {
                device = trackers[0];
                _logger?.LogEvent("Info", "Auto-selecting paired Timeular Tracker (single match).");
            }
        }
        catch { }

        if (device == null)
        {
            try
            {
                device = await Bluetooth.RequestDeviceAsync(options);
            }
            catch { }
        }

        if (device == null)
        {
            _logger?.LogEvent("Info", "No device available");
            return;
        }

        await device.Gatt.ConnectAsync();
        _logger?.LogEvent("Connected", device.Name ?? "Unknown");

        if (Config != null)
        {
            Config.DeviceId = device.Id;
            Config.DeviceName = device.Name;
        }

        var services = await device.Gatt.GetPrimaryServicesAsync();
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
            device.Gatt.Disconnect();
            return;
        }

        orientationChar.CharacteristicValueChanged += (sender, args) =>
        {
            var value = args.Value;
            if (value != null && value.Length > 0)
            {
                var orientation = value[0];
                var side = orientation == 0 ? "No orientation" : (Config.SideLabels.TryGetValue(orientation, out var lab) ? lab : $"Side {orientation}");
                _logger?.LogEvent("Flip", side, orientation);
                FlipOccurred?.Invoke(this, new FlipEventArgs(orientation, side));
            }
        };

        await orientationChar.StartNotificationsAsync();

        _ = Task.Run(async () =>
        {
            byte? last = null;
            while (!token.IsCancellationRequested && device.Gatt.IsConnected)
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
                            var side = val == 0 ? "No orientation" : (Config.SideLabels.TryGetValue(val, out var lab) ? lab : $"Side {val}");
                            _logger?.LogEvent("Flip", side, val);
                            FlipOccurred?.Invoke(this, new FlipEventArgs(val, side));
                        }
                    }
                }
                catch { }
                await Task.Delay(1000, token);
            }
        });

        while (!token.IsCancellationRequested && device.Gatt.IsConnected)
            await Task.Delay(1000, token);

        try
        {
            await orientationChar.StopNotificationsAsync();
            device.Gatt.Disconnect();
        }
        catch { }
    }
}
