# CLAUDE.md — WConky Project Rules

Este archivo define el contexto y reglas para continuar el desarrollo de WConky en futuras sesiones.

---

## ¿Qué es WConky?

Widget de escritorio para Windows, inspirado en Conky de Linux. Desarrollado en C# + WPF (.NET 10). Muestra métricas del sistema en tiempo real flotando transparente sobre el wallpaper.

---

## Stack

- **Lenguaje:** C#
- **UI:** WPF (.NET 10, `net10.0-windows`)
- **IDE:** Visual Studio Community 2026
- **NuGet:** `LibreHardwareMonitorLib`, `System.ServiceProcess.ServiceController`
- **csproj flags:** `<UseWPF>true</UseWPF>` + `<UseWindowsForms>true</UseWindowsForms>`

---

## Estructura de archivos

```
WConky/
├── App.xaml
├── App.xaml.cs          ← Mutex instancia única + error handling
├── MainWindow.xaml      ← UI transparente, sin bordes, sin taskbar
├── MainWindow.xaml.cs   ← Toda la lógica
├── layer-minus.ico      ← Ícono tray (Build Action: Content, Copy if newer)
└── WConky.csproj
```

---

## Reglas del proyecto

### UI / XAML
- Ventana transparente: `AllowsTransparency="True"`, `Background="Transparent"`, `WindowStyle="None"`
- Sin taskbar: `ShowInTaskbar="False"`, `Topmost="False"`
- Fondo glass: `Background="#18000000"`, `BorderBrush="#33FFFFFF"`, `BorderThickness="0.5"`
- No se puede mover (DragMove eliminado)
- Posición fija: esquina superior derecha del WorkArea
- Fijado detrás de ventanas via `SetWindowPos` con `HWND_BOTTOM`
- Sin Alt+Tab: `SetWindowLong` con `WS_EX_TOOLWINDOW`

### C# / Lógica
- Timer principal: cada 2 segundos
- Timer clima: cada 30 minutos
- Una sola instancia via `Mutex("WConky_SingleInstance")`
- Temperaturas via `LibreHardwareMonitor` (requiere Admin)
- RAM via WMI `Win32_OperatingSystem`
- Red via `PerformanceCounter("Network Interface")`
- Disco via `DriveInfo("C")`

### Servicios monitoreados
- Docker: servicio `com.docker.service` + contenedores via `docker ps`
- BD: detecta automáticamente los instalados (postgresql, MySQL, Redis, MSSQLSERVER, MariaDB, MongoDB)
- WSL2: servicio `WslService` + distros via `wsl --list --verbose`

### Clima
- API: `https://api.open-meteo.com/v1/forecast`
- Parámetro: `current_weather=true`
- Coordenadas: Santa Cruz de la Sierra `-17.4, -63.8333`
- Campo temperatura: `current_weather.temperature`
- Campo código clima: `current_weather.weathercode`

### Conflictos de namespace conocidos
- `Application` → siempre usar `System.Windows.Application` (conflicto con WinForms)
- `Color` → siempre usar `System.Windows.Media.Color`
- `Path` → siempre usar `System.Windows.Shapes.Path`
- `MessageBox` → siempre usar `System.Windows.MessageBox`

### Publish
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Resultado: un solo `WConky.exe` en `bin\Release\net10.0-windows\win-x64\publish\`

---

## Pendientes / Ideas futuras

- [ ] Autostart con Windows via registry
- [ ] Ícono personalizado en tray (`layer-minus.ico`)
- [ ] Configuración via `appsettings.json` (servicios, posición, transparencia)
- [ ] Animación suave en los arcos de CPU/RAM/Disco
- [ ] Mostrar GPU % y temperatura
- [ ] Notificación cuando CPU > 90% o RAM > 90%
- [ ] Modo claro/oscuro según wallpaper
- [ ] Click derecho en widget para ajustar transparencia

---

## Historial de decisiones

| Decisión | Razón |
|---|---|
| WPF sobre Electron | Más liviano, nativo Windows |
| WPF sobre Python+PyQt | Mejor integración con Win32 APIs |
| open-meteo.com | Gratis, sin API key, confiable |
| `current_weather=true` en vez de `hourly` | Endpoint correcto para datos actuales |
| `SetWindowPos(HWND_BOTTOM)` | Pone widget detrás de ventanas sin usar WorkerW |
| Mutex para instancia única | Evitar doble ejecución accidental |
| `UseWindowsForms=true` | Necesario para `NotifyIcon` del tray |

---

## Cómo continuar en una nueva sesión

1. Compartir este archivo `CLAUDE.md` al inicio
2. Indicar qué feature se quiere agregar
3. Claude tendrá todo el contexto para continuar sin repetir lo ya hecho
