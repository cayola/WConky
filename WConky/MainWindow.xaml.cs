using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace WConky
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _weatherTimer;
        private Computer _computer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _netSent;
        private PerformanceCounter _netRecv;
        private static readonly HttpClient _http = new();
        private NotifyIcon _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupTray();
            InitHardware();
            InitCounters();
            StartTimer();
            StartWeatherTimer();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            PositionWindow();
            SendToDesktop();
        }

        void SetupTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Salir", null, (s, e) =>
            {
                _trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "WConky",
                ContextMenuStrip = menu
            };
        }

        void PositionWindow()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 10;
            Top = area.Top + 10;
        }

        void SendToDesktop()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // Quitar de Alt+Tab
            int exStyle = GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, exStyle | 0x00000080);

            // Poner detrás de todas las ventanas permanentemente
            SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0,
                0x0001 | 0x0002 | 0x0010);
        }

        void InitHardware()
        {
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
        }

        void InitCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                var cat = new PerformanceCounterCategory("Network Interface");
                var inst = cat.GetInstanceNames().FirstOrDefault();
                if (inst != null)
                {
                    _netSent = new PerformanceCounter("Network Interface", "Bytes Sent/sec", inst);
                    _netRecv = new PerformanceCounter("Network Interface", "Bytes Received/sec", inst);
                }
            }
            catch { }
        }

        void StartTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => Update();
            _timer.Start();
            Update();
        }

        void Update()
        {
            UpdateClock();
            UpdateCpu();
            UpdateRam();
            UpdateDisk();
            UpdateNetwork();
            UpdateServices();
        }

        void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm");
            DateText.Text = DateTime.Now.ToString("dddd · dd MMM yyyy").ToUpper();
        }

        void UpdateCpu()
        {
            float pct = 0;
            try { pct = _cpuCounter?.NextValue() ?? 0; } catch { }

            float temp = 0;
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    foreach (var s in hw.Sensors)
                        if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            temp = s.Value.Value;
                }
            }
            catch { }

            CpuPct.Text = $"{(int)pct}%";
            CpuTemp.Text = temp > 0 ? $"{(int)temp}°C" : "--";
            DrawArc(CpuArc, pct / 100f);
        }

        void UpdateRam()
        {
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    ulong totalKB = (ulong)obj["TotalVisibleMemorySize"];
                    ulong freeKB = (ulong)obj["FreePhysicalMemory"];
                    double totalGB = totalKB / 1024.0 / 1024.0;
                    double usedGB = (totalKB - freeKB) / 1024.0 / 1024.0;
                    float pct = (float)(usedGB / totalGB);
                    RamPct.Text = $"{(int)(pct * 100)}%";
                    RamVal.Text = $"{usedGB:F1}/{totalGB:F0} GB";
                    DrawArc(RamArc, pct);
                    return;
                }
            }
            catch { }
        }

        void UpdateDisk()
        {
            try
            {
                var drive = new System.IO.DriveInfo("C");
                double total = drive.TotalSize / 1073741824.0;
                double free = drive.TotalFreeSpace / 1073741824.0;
                double used = total - free;
                float pct = (float)(used / total);
                DiskPct.Text = $"{(int)(pct * 100)}%";
                DiskVal.Text = $"{(int)used}/{(int)total} GB";
                DrawArc(DiskArc, pct);
            }
            catch { }
        }

        void UpdateNetwork()
        {
            try
            {
                float sent = _netSent?.NextValue() ?? 0;
                float recv = _netRecv?.NextValue() ?? 0;
                NetUp.Text = FormatBytes(sent);
                NetDown.Text = FormatBytes(recv);
            }
            catch { }
        }

        string FormatBytes(float b)
        {
            if (b >= 1_000_000) return $"{b / 1_000_000:F1} MB/s";
            if (b >= 1_000) return $"{b / 1_000:F1} KB/s";
            return $"{(int)b} B/s";
        }

        void UpdateServices()
        {
            ServicesPanel.Children.Clear();

            // Docker
            bool dockerRunning = IsServiceRunning("com.docker.service");
            AddServiceRow("🐳 Docker", dockerRunning);

            if (dockerRunning)
            {
                var containers = GetDockerContainersWithStatus();
                if (containers.Count == 0)
                    AddSubRow("  ↳ sin contenedores", false);
                else
                    foreach (var (name, running) in containers)
                        AddSubRow($"  ↳ {name}", running);
            }

            // Servicios de BD instalados en Windows
            var dbServices = new List<(string Label, string ServiceName)>
    {
        ("🐘 PostgreSQL", "postgresql"),
        ("🐬 MySQL",      "MySQL"),
        ("🔴 Redis",      "Redis"),
        ("🗄️ SQL Server", "MSSQLSERVER"),
        ("🦭 MariaDB",    "MariaDB"),
        ("🍃 MongoDB",    "MongoDB"),
    };

            foreach (var (label, svcName) in dbServices)
            {
                if (ServiceExists(svcName))
                    AddServiceRow(label, IsServiceRunning(svcName));
            }

            // WSL2
            if (ServiceExists("WslService"))
            {
                bool wslRunning = IsServiceRunning("WslService");
                AddServiceRow("🐧 WSL2", wslRunning);

                if (wslRunning)
                {
                    var distros = GetWslDistros();
                    foreach (var (name, running) in distros)
                        AddSubRow($"  ↳ {name}", running);
                }
            }

        }

        bool IsServiceRunning(string name)
        {
            try { return new ServiceController(name).Status == ServiceControllerStatus.Running; }
            catch { return false; }
        }

        bool ServiceExists(string name)
        {
            try
            {
                var svc = new ServiceController(name);
                var status = svc.Status;
                return true;
            }
            catch { return false; }
        }

        List<(string Name, bool Running)> GetDockerContainersWithStatus()
        {
            var list = new List<(string, bool)>();
            try
            {
                // Todos los contenedores con su estado
                var psi = new ProcessStartInfo("docker", "ps --format {{.Names}}|{{.State}}")

                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2)
                    {
                        string name = parts[0].Trim();
                        bool running = parts[1].Trim() == "running";
                        list.Add((name, running));
                    }
                }
            }
            catch { }
            return list;
        }

        List<(string Name, bool Running)> GetWslDistros()
        {
            var list = new List<(string, bool)>();
            try
            {
                var psi = new ProcessStartInfo("wsl", "--list --verbose")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.Unicode
                };
                var p = Process.Start(psi);
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
                {
                    var parts = line.Trim().TrimStart('*').Trim()
                                   .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string name = parts[0];
                        bool running = parts[1].ToLower() == "running";
                        list.Add((name, running));
                    }
                }
            }
            catch { }
            return list;
        }

        void AddServiceRow(string label, bool running)
        {
            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = running
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)),
                Margin = new Thickness(0, 4, 8, 0)
            });
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = running ? "running" : "stopped",
                Foreground = running
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)),
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            ServicesPanel.Children.Add(panel);
        }

        void AddSubRow(string label, bool running)
        {
            ServicesPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = label,
                Foreground = running
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(16, 2, 0, 0)
            });
        }

        void DrawArc(System.Windows.Shapes.Path path, float pct)
        {
            pct = Math.Clamp(pct, 0.001f, 0.999f);
            double cx = 36, cy = 36, r = 32;
            double angle = pct * 360 - 90;
            double rad = angle * Math.PI / 180;
            double x = cx + r * Math.Cos(rad);
            double y = cy + r * Math.Sin(rad);
            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new System.Windows.Point(cx, cy - r) };
            fig.Segments.Add(new ArcSegment(new System.Windows.Point(x, y),
                new System.Windows.Size(r, r), 0, pct > 0.5,
                SweepDirection.Clockwise, true));
            geo.Figures.Add(fig);
            path.Data = geo;
        }

        void StartWeatherTimer()
        {
            FetchWeather();
            _weatherTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _weatherTimer.Tick += (s, e) => FetchWeather();
            _weatherTimer.Start();
        }

        async void FetchWeather()
        {
            try
            {
                var url = "https://api.open-meteo.com/v1/forecast" +
                          "?latitude=-17.4&longitude=-63.8333&current_weather=true";
                var json = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                var curr = doc.RootElement.GetProperty("current_weather");
                double temp = curr.GetProperty("temperature").GetDouble();
                int code = curr.GetProperty("weathercode").GetInt32();
                Dispatcher.Invoke(() =>
                {
                    WeatherIcon.Text = GetWeatherIcon(code);
                    WeatherTemp.Text = $"{temp:F0}°C";
                    WeatherDesc.Text = GetWeatherDesc(code);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => WeatherDesc.Text = ex.Message);
            }
        }

        string GetWeatherIcon(int code) => code switch
        {
            0 => "☀️",
            1 => "🌤️",
            2 => "⛅",
            3 => "☁️",
            45 or 48 => "🌫️",
            51 or 53 or 55 => "🌦️",
            61 or 63 or 65 => "🌧️",
            71 or 73 or 75 => "❄️",
            80 or 81 or 82 => "🌧️",
            95 => "⛈️",
            96 or 99 => "⛈️",
            _ => "🌡️"
        };

        string GetWeatherDesc(int code) => code switch
        {
            0 => "Despejado",
            1 => "Mayormente despejado",
            2 => "Parcialmente nublado",
            3 => "Nublado",
            45 or 48 => "Neblina",
            51 or 53 or 55 => "Llovizna",
            61 or 63 or 65 => "Lluvia",
            71 or 73 or 75 => "Nieve",
            80 or 81 or 82 => "Chubascos",
            95 => "Tormenta",
            96 or 99 => "Tormenta con granizo",
            _ => "Desconocido"
        };

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            _timer?.Stop();
            _computer?.Close();
            base.OnClosed(e);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
    }
}