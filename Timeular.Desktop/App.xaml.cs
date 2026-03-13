using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using H.NotifyIcon;
using Timeular.Core;
using Timeular.Desktop.Helpers;

namespace Timeular.Desktop
{
    public partial class App : System.Windows.Application
    {
        private TaskbarIcon? _notifyIcon;
        private const int HOTKEY_ID = 9000;
        private ILogger<App>? _logger;
        internal ILogger<App>? Logger => _logger;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((ctx, services) =>
                {
                    var configPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TimeularLibre", "config.json");
                    services.AddSingleton<IConfigProvider>(
                        _ => new FileConfigProvider(configPath));
                    services.AddSingleton(sp =>
                        Task.Run(() => sp.GetRequiredService<IConfigProvider>().GetConfigAsync())
                            .GetAwaiter().GetResult());
                    services.AddSingleton<ICubeListener>(sp =>
                        new BluetoothCubeListener(
                            sp.GetRequiredService<TimeularConfig>(),
                            logger: sp.GetRequiredService<ILogger<BluetoothCubeListener>>()));
                })
                .Build();

            _logger = host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("[Desktop] starting");

            try
            {
                var listener = (BluetoothCubeListener)host.Services.GetRequiredService<ICubeListener>();
                listener.FlipOccurred += OnCubeFlip;

                Task.Run(async () =>
                {
                    try { await listener.StartAsync(CancellationToken.None); }
                    catch (Exception ex) { _logger.LogError(ex, "[Desktop] listener error"); }
                });

                var config = host.Services.GetRequiredService<IConfiguration>();
                var logApiUrl = config["services:log:https:0"]
                    ?? config["services:log:http:0"]
                    ?? "https://localhost:5001";
                _logger.LogInformation("[Desktop] log API: {Url}", logApiUrl);
                MainWindow = new MainWindow(logApiUrl);
                MainWindow.Hide();

                try { SetupNotifyIcon(); }
                catch (Exception ex) { _logger.LogError(ex, "[Desktop] tray icon setup failed"); }

                RegisterGlobalHotKey();
                _logger.LogInformation("[Desktop] ready");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Desktop] startup failed");
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
            var resource = TryFindResource("TrayIcon") as TaskbarIcon;
            if (resource != null)
            {
                _notifyIcon = resource;
            }
            else
            {
                _notifyIcon = new TaskbarIcon();
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                _notifyIcon.ToolTipText = "Timeular Desktop";
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

        private void OnCubeFlip(object? sender, FlipEventArgs e)
        {
            _logger?.LogInformation("[Desktop] flip: side={Side} label={Label}", e.Side, e.Label);
            Dispatcher.Invoke(ShowEntryWindow);
        }

        private void ShowSettings()
        {
            _logger?.LogInformation("[Desktop] settings (not yet implemented)");
        }

        private void RegisterGlobalHotKey()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(MainWindow);
            helper.EnsureHandle();
            var windowHandle = helper.Handle;
            RegisterHotKey(windowHandle, HOTKEY_ID, 0x0002 | 0x0004,
                (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.E));
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
