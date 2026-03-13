using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Hardcodet.Wpf.TaskbarNotification;
using Timeular.Desktop.Helpers;
using System.Windows.Controls;

namespace Timeular.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private TaskbarIcon? _notifyIcon;
        private const int HOTKEY_ID = 9000;
        private ILogger<App>? _logger;

        public void Log(string message)
        {
            // temporary helper still writes to file for early startup diagnostics
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            _logger?.LogInformation(message);
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("[Desktop] OnStartup");
            base.OnStartup(e);

            IHost host;
            try
            {
                // build host for DI and services (default logging is fine)
                host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((ctx, services) =>
                    {
                    // configuration provider (reuse same approach as service project)
                    services.AddSingleton<Timeular.Core.IConfigProvider>(sp =>
                    {
                        // use file config in appdata for desktop
                        var configPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "TimeularLibre",
                            "config.json");
                        return new Timeular.Core.FileConfigProvider(configPath);
                    });

                    // load the config once so we can pass into listener
                    // use ConfigureAwait(false) to avoid capturing the UI context and deadlocking
                    services.AddSingleton(sp =>
                    {
                        var cfgProv = sp.GetRequiredService<Timeular.Core.IConfigProvider>();
                        return cfgProv.GetConfigAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    });

                    services.AddSingleton<Timeular.Core.ICubeListener>(sp =>
                    {
                        // log from factory - note this runs during GetRequiredService call later
                        this.Log("[Desktop] ICubeListener factory invoked");
                        var cfg = sp.GetRequiredService<Timeular.Core.TimeularConfig>();
                        this.Log("[Desktop] config obtained for listener");
                        return new Timeular.Core.BluetoothCubeListener(cfg);
                    });

                    // action launcher and event logger could be registered similarly
                })
                .Build();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Desktop] host build failed");
                throw;
            }

            // resolve logger from the host and keep for later
            _logger = host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("[Desktop] host built successfully");
            try
            {
                _logger.LogInformation("[Desktop] resolving cube listener service");
                // bypass DI entirely: read config directly so we don't trigger provider locks
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TimeularLibre",
                    "config.json");
                Log("[Desktop] instantiating FileConfigProvider with path: " + configPath);
                var cfgProv = new Timeular.Core.FileConfigProvider(configPath);
                _logger?.LogInformation("[Desktop] about to load config manually");
                Log("[Desktop] about to load config manually");
                // load config synchronously to avoid sync-over-async deadlock
                Timeular.Core.TimeularConfig cfg;
                try
                {
                    if (File.Exists(configPath))
                    {
                        _logger?.LogInformation("[Desktop] about to read config file");
                        Log("[Desktop] about to read config file");
                        string json;
                        try
                        {
                            json = File.ReadAllText(configPath);
                            _logger?.LogInformation("[Desktop] read config file, length={Length}", json.Length);
                            Log("[Desktop] read config file, length=" + json.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[Desktop] error reading config file");
                            Log("[Desktop] error reading config file: " + ex);
                            throw;
                        }

                        try
                        {
                            cfg = System.Text.Json.JsonSerializer.Deserialize<Timeular.Core.TimeularConfig>(json) ?? new Timeular.Core.TimeularConfig();
                            _logger?.LogInformation("[Desktop] deserialized config");
                            Log("[Desktop] deserialized config");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[Desktop] error deserializing config");
                            Log("[Desktop] error deserializing config: " + ex);
                            throw;
                        }
                    }
                    else
                    {
                        cfg = new Timeular.Core.TimeularConfig();
                        _logger?.LogInformation("[Desktop] config file missing, using defaults");
                        Log("[Desktop] config file missing, using defaults");
                    }
                    _logger?.LogInformation("[Desktop] config loaded manually");
                    Log("[Desktop] config loaded manually");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[Desktop] failed to load config");
                    Log("[Desktop] failed to load config: " + ex);
                    throw;
                }

                Log("[Desktop] constructing listener instance");
                var listener = new Timeular.Core.BluetoothCubeListener(cfg);
                _logger?.LogInformation("[Desktop] cube listener constructed");
                Log("[Desktop] cube listener constructed");

                listener.FlipOccurred += OnCubeFlip;

                Log("[Desktop] starting cube listener task");
                // run StartAsync on a threadpool thread so any synchronous work doesn't block UI
                Task.Run(async () =>
                {
                    try
                    {
                        await listener.StartAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[Desktop] listener.StartAsync threw inside Task.Run");
                        Log("[Desktop] listener.StartAsync threw inside Task.Run: " + ex);
                    }
                });
                _logger?.LogInformation("[Desktop] cube listener task started");
                Log("[Desktop] cube listener task started");

                MainWindow = new MainWindow();
                _logger?.LogInformation("[Desktop] created main window instance");
                Log("[Desktop] created main window instance");
                MainWindow.Hide();
                _logger?.LogInformation("[Desktop] hid main window");
                Log("[Desktop] hid main window");

                try
                {
                    SetupNotifyIcon();
                    _logger?.LogInformation("[Desktop] SetupNotifyIcon returned");
                Log("[Desktop] SetupNotifyIcon returned");
                }
                catch (Exception ex)
                {
                    var msg = "[Desktop] SetupNotifyIcon failed: " + ex;
                    _logger?.LogError(ex, msg);
                    Console.WriteLine(msg);
                }

                RegisterGlobalHotKey();
                _logger?.LogInformation("[Desktop] registered hotkey");
                Log("[Desktop] registered hotkey");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Desktop] OnStartup post-host error");
                Log("[Desktop] startup error: " + ex);
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            UnregisterGlobalHotKey();
            base.OnExit(e);
        }

        private void SetupNotifyIcon()
        {
            Log("[Desktop] initializing tray icon");
            // retrieve the icon declared in App.xaml
            var resource = TryFindResource("TrayIcon") as TaskbarIcon;
            if (resource != null)
            {
                Log("[Desktop] found TrayIcon resource");
                _notifyIcon = resource;
            }
            else
            {
                Log("[Desktop] TrayIcon resource missing, creating programmatically");
                _notifyIcon = new TaskbarIcon();
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                _notifyIcon.ToolTipText = "Timeular Desktop";
            }
            if (_notifyIcon == null)
            {
                Log("[Desktop] notify icon is still null after initialization");
                return;
            }
            var menu = new System.Windows.Controls.ContextMenu();
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Open entry",
                Command = new RelayCommand(_ => ShowEntryWindow())
            });
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Settings",
                Command = new RelayCommand(_ => ShowSettings())
            });
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Exit",
                Command = new RelayCommand(_ => Shutdown())
            });
            _notifyIcon.ContextMenu = menu;
        }

        private void ShowEntryWindow()
        {
            if (MainWindow is MainWindow mw)
            {
                mw.Show();
                mw.Activate();
            }
        }

        private void OnCubeFlip(object? sender, Timeular.Core.FlipEventArgs e)
        {
            Log($"[Desktop] cube flipped: side={e.Side},label={e.Label}");
            // show entry window when cube flipped
            Dispatcher.Invoke(ShowEntryWindow);
        }

        private void ShowSettings()
        {
            Log("[Desktop] ShowSettings invoked (placeholder)");
        }

        private void RegisterGlobalHotKey()
        {
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(MainWindow).Handle;
            // ctrl+alt+e
            RegisterHotKey(windowHandle, HOTKEY_ID, 0x0002 | 0x0004, (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.E));
            var source = System.Windows.Interop.HwndSource.FromHwnd(windowHandle);
            source.AddHook(HwndHook);
        }

        private void UnregisterGlobalHotKey()
        {
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(MainWindow).Handle;
            UnregisterHotKey(windowHandle, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ShowEntryWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}

