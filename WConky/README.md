# WConky 🖥️

Widget de escritorio para Windows inspirado en Conky (Linux). Muestra información del sistema en tiempo real, flotando transparente sobre el wallpaper como si fuera parte del escritorio.

---

## ¿Qué muestra?

- 🕐 **Hora y fecha** en tiempo real
- 🌤️ **Clima** de Santa Cruz de la Sierra (API open-meteo.com, actualiza cada 30 min)
- ⭕ **3 círculos animados** — CPU % + temperatura, RAM usada/total, Disco C:
- 🌐 **Red** — velocidad de subida y bajada
- 🐳 **Docker** — estado del servicio + contenedores en ejecución
- 🗄️ **Servicios de BD** — detecta automáticamente los instalados en Windows:
  - PostgreSQL, MySQL, Redis, SQL Server, MariaDB, MongoDB
- 🐧 **WSL2** — estado + distros instaladas

---

## Stack técnico

| Elemento | Tecnología |
|---|---|
| Lenguaje | C# |
| Framework UI | WPF (.NET 10) |
| Métricas hardware | LibreHardwareMonitorLib |
| Servicios Windows | System.ServiceProcess.ServiceController |
| Clima | open-meteo.com (gratis, sin API key) |
| Tray icon | System.Windows.Forms |
| Datos sistema | WMI (Win32_OperatingSystem) |

---

## Estructura del proyecto

```
WConky/
├── App.xaml                  ← Instancia única (Mutex)
├── App.xaml.cs               ← OnStartup / OnExit
├── MainWindow.xaml           ← UI / diseño XAML
├── MainWindow.xaml.cs        ← Toda la lógica
├── layer-minus.ico           ← Ícono del tray
└── WConky.csproj             ← Configuración del proyecto
```

---

## Dependencias NuGet

```xml
<PackageReference Include="LibreHardwareMonitorLib" Version="0.9.6" />
<PackageReference Include="System.ServiceProcess.ServiceController" Version="10.0.6" />
```

Y en el `.csproj`:
```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

---

## Cómo compilar

### Desde Visual Studio
1. Abrir `WConky.sln`
2. Presionar `F5` para debug o `F6` para compilar

### Publicar como un solo .exe
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

El exe queda en:
```
bin\Release\net10.0-windows\win-x64\publish\WConky.exe
```

---

## Cómo usar

1. Ejecutar `WConky.exe` (como Administrador recomendado para temperaturas)
2. El widget aparece en la esquina superior derecha del escritorio
3. Se queda **detrás de todas las ventanas** como parte del wallpaper
4. No aparece en el Alt+Tab ni en la barra de tareas
5. Para cerrarlo: click derecho en el ícono del **system tray** → **Salir**

### Si se abre dos veces
El programa detecta si ya está corriendo y muestra un aviso. Solo corre una instancia a la vez.

---

## Autostart con Windows

Ejecutar en terminal como administrador:
```bash
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v WConky /t REG_SZ /d "C:\ruta\a\WConky.exe" /f
```

Para quitarlo del autostart:
```bash
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v WConky /f
```

---

## Personalización

### Cambiar transparencia
En `MainWindow.xaml`, el `Border` principal:
```xml
Background="#18000000"  <!-- más bajo = más transparente -->
```

### Agregar servicios a monitorear
En `MainWindow.xaml.cs`, método `UpdateServices()`:
```csharp
var dbServices = new List<(string Label, string ServiceName)>
{
    ("🐘 PostgreSQL", "postgresql"),
    ("🐬 MySQL",      "MySQL"),
    // agregar aquí...
};
```

### Cambiar ciudad del clima
En `FetchWeather()`:
```csharp
var url = "https://api.open-meteo.com/v1/forecast" +
          "?latitude=TU_LAT&longitude=TU_LON&current_weather=true";
```

### Cambiar posición
En `PositionWindow()`:
```csharp
var area = SystemParameters.WorkArea;
Left = area.Right - Width - 10;  // esquina derecha
Top  = area.Top + 10;             // arriba
```

---

## API del clima

Usa [open-meteo.com](https://open-meteo.com) — **gratis, sin API key**, hasta 10.000 llamadas diarias.

Coordenadas actuales: Santa Cruz de la Sierra, Bolivia (`-17.4, -63.8333`)

---

## Notas

- Las temperaturas de CPU requieren correr como **Administrador**
- Docker debe estar instalado y en el PATH para detectar contenedores
- WSL2 se detecta via el servicio `WslService` de Windows
