using System;
using System.Threading;
using System.IO;

namespace WConky
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            // Capturar errores no manejados
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                File.WriteAllText("error.log", ex.ExceptionObject.ToString());
                System.Windows.MessageBox.Show(ex.ExceptionObject.ToString(), "Error");
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                File.WriteAllText("error.log", ex.Exception.ToString());
                System.Windows.MessageBox.Show(ex.Exception.ToString(), "Error");
                ex.Handled = true;
            };

            _mutex = new Mutex(true, "WConky_SingleInstance", out bool created);
            if (!created)
            {
                System.Windows.MessageBox.Show("WConky ya está corriendo.",
                    "WConky", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                _mutex.Dispose();
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            try { _mutex?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}