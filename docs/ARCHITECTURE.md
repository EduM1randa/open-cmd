# Arquitectura — Open CMD

Documento técnico del diseño de la aplicación. Orientado a quien lea el código o quiera extenderlo.

## 1. Visión

Open CMD es una aplicación **WPF de proceso único** (sin backend y sin base de datos). Su trabajo es:

1. Elegir o recordar una carpeta padre de proyecto
2. Descubrir subcarpetas que representen un “proyecto”
3. Permitir al usuario configurar selección, orden, comandos y shell
4. Lanzar procesos externos: **Windows Terminal** y, de forma opcional, **Cursor**

No ejecuta los servidores de desarrollo por sí misma: solo abre consolas (y, si aplica, el editor) en las rutas correctas.

## 2. Diagrama de capas

```text
┌─────────────────────────────────────────────────────────┐
│  Presentación                                           │
│  App.xaml / MainWindow.xaml + MainWindow.xaml.cs        │
│  (eventos de UI, diálogos, drag & drop, MessageBox)     │
└───────────────────────────┬─────────────────────────────┘
                            │ DataContext / llamadas
┌───────────────────────────▼─────────────────────────────┐
│  ViewModel                                              │
│  MainViewModel                                          │
│  (estado, recientes, persistencia, orquestación)        │
└───────────────────────────┬─────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌───────────────┐  ┌────────────────┐  ┌────────────────┐
│ ProjectScanner│  │TerminalLauncher│  │ CursorLauncher │
└───────────────┘  └────────────────┘  └────────────────┘
        │                   │                   │
        │                   ▼                   ▼
        │              wt.exe               cursor.cmd
        ▼
   DetectedProject[]
                            │
                    ┌───────▼────────┐
                    │ SettingsStore  │
                    │ %APPDATA%\…    │
                    └────────────────┘
```

### Responsabilidades

| Pieza | Responsabilidad | No hace |
|-------|-----------------|---------|
| `MainWindow` | Binding, clics, OpenFolderDialog, errores al usuario | Lógica de escaneo o de `wt` |
| `MainViewModel` | Estado observable, recientes, guardar/cargar configuración, lanzar | Interpretar la CLI de Terminal |
| `ProjectScanner` | Heurísticas de detección y orden | UI / lanzamiento |
| `TerminalLauncher` | Armar argumentos de `wt` y `Process.Start` | Decidir qué carpetas abrir |
| `CursorLauncher` | `cursor.cmd . --classic` en `WorkingDirectory` | Validar la UI |
| `SettingsStore` | Serializar JSON en AppData | Conocer WPF |

## 3. Flujo principal

### 3.1 Inicio (sin proyecto cargado)

1. `App` crea `MainWindow` → `MainViewModel.Load()`
2. `SettingsStore.Load()` lee `%APPDATA%\OpenCmd\settings.json`
3. Se hidratan `Recents` (máximo 3) y el shell seleccionado
4. La interfaz muestra la zona “Elegir carpeta” y las tarjetas de recientes

Acciones por reciente:

- **Abrir** → `OpenSavedProject` (LoadRoot + LaunchTerminal)
- **Editar** → `LoadRoot` (entra a la vista del proyecto sin lanzar la terminal)
- **Cursor** → `CursorLauncher.Open(path)`

### 3.2 Dentro de un proyecto

1. `LoadRoot(path)` normaliza la ruta y llama a `ProjectScanner.Scan`
2. Se aplican `Order`, `Unchecked` y `Commands` guardados para esa raíz
3. El usuario marca carpetas, reordena y escribe comandos
4. **Abrir en N paneles** → `RememberCurrentProjects` + `TouchRecent` + `TerminalLauncher.Launch`
5. Icono de inicio (header) → `GoHome` (persiste y limpia `RootPath`)

Los recientes **solo se actualizan al lanzar la terminal**, no al navegar o editar únicamente.

## 4. Detección de proyectos (`ProjectScanner`)

Orden de la estrategia:

1. **Workspaces** npm/yarn (`package.json` / `pnpm-workspace`) → expandir globs
2. **Hijos inmediatos** que parezcan un proyecto (marcadores de stack)
3. Si hay menos de 2: bajar un nivel en contenedores (`apps`, `packages`, `services`, `projects`) o en carpetas que a su vez tengan 2 o más proyectos (por ejemplo `src/backend` + `src/frontend`)
4. **Fallback**: listar hijos que no sean carpetas auxiliares (docs, tests, assets, etc.)

### Marcadores (ejemplos)

`package.json`, `pyproject.toml` / `requirements.txt` / `Pipfile` / `manage.py`, `go.mod`, `Cargo.toml`, `pom.xml` / Gradle, `*.csproj` / `*.fsproj` / `*.sln`, `composer.json`, `Gemfile`, `pubspec.yaml`, `mix.exs`, `deno.json`, `angular.json`.

### Roles y orden

Nombres como `backend`, `frontend`, `api`, `web`, `server`, `client` (y variantes en español) influyen en el ordenamiento: primero las carpetas tipo backend y después las tipo frontend.

Carpetas ignoradas: `node_modules`, `.git`, `dist`, `bin`, `obj`, entornos virtuales, etc.

## 5. Lanzamiento de Windows Terminal (`TerminalLauncher`)

### Por qué no un comando de PowerShell suelto

`wt.exe` interpreta `;` como separador de sus propios comandos. Un `-Command` con `;` se parte de forma incorrecta. Por eso:

- PowerShell / pwsh: el script del panel se pasa con **`-EncodedCommand`** (UTF-16LE Base64)
- CMD: se genera un `.bat` temporal en `%TEMP%\OpenCmd` (con limpieza de archivos antiguos)

### Disposiciones

| Paneles | Estrategia |
|---------|------------|
| 1 | Una pestaña |
| 2 | `split-pane -V` |
| 3 | Vertical + horizontal en el derecho |
| 4 | Vertical, horizontal del derecho, `move-focus left`, horizontal del izquierdo (cuadrícula A\|B / C\|D) |
| 5 o más | Columnas verticales con tamaños relativos |

Siempre se usa `-w new` (ventana nueva).

Cada panel:

1. `Set-Location` (o `cd /d`) a la carpeta
2. Título de ventana / banner
3. Comando de usuario opcional

## 6. Cursor (`CursorLauncher`)

```text
FileName         = cursor.cmd
Arguments        = . --classic
WorkingDirectory = <carpeta elegida>
UseShellExecute  = true   # necesario para .cmd
```

Equivale a abrir esa carpeta en Cursor en modo classic. Si `cursor` no está en el PATH, se muestra un error comprensible.

## 7. Persistencia (`SettingsStore`)

### Ubicación

```text
%APPDATA%\OpenCmd\settings.json
```

### Forma del JSON (conceptual)

```json
{
  "shell": "pwsh.exe",
  "recent": [ "C:\\ruta\\proyecto-a", "C:\\ruta\\proyecto-b" ],
  "commands": {
    "C:\\ruta\\proyecto-a\\backend": "npm run dev"
  },
  "unchecked": {
    "C:\\ruta\\proyecto-a": [ "C:\\ruta\\proyecto-a\\contracts" ]
  },
  "order": {
    "C:\\ruta\\proyecto-a": [
      "C:\\ruta\\proyecto-a\\backend",
      "C:\\ruta\\proyecto-a\\frontend"
    ]
  }
}
```

- Claves normalizadas (`PathUtil.Normalize`), comparación sin distinguir mayúsculas
- **Máximo 3** entradas en `recent`
- Contiene **rutas absolutas de la máquina del usuario** → este archivo no debe versionarse

## 8. Modelos

| Tipo | Uso |
|------|-----|
| `DetectedProject` | Nombre, ruta, tipo, comando, selección, placeholder |
| `RecentEntry` | Ruta + resumen legible para la pantalla de inicio |
| `ShellOption` | Etiqueta + ejecutable (`powershell.exe` / `pwsh.exe` / `cmd.exe`) |
| `AppSettings` | DTO del JSON |

`ObservableModel` es el helper mínimo de `INotifyPropertyChanged`.

## 9. Interfaz

- **Inicio**: zona para soltar o elegir carpeta + últimos proyectos (sin icono de casita)
- **Proyecto**: encabezado con logo/título e icono de inicio a la derecha; tarjeta de la raíz; lista de hijos; opciones de shell; botón principal de acción
- Tema oscuro (`#101114`, acento mint `#6EE7B7`), barra de título oscura mediante DWM (`WindowTheme`)
- Idioma de la interfaz: español

## 10. Compilación y empaquetado

| Modo | Comando típico | Salida |
|------|----------------|--------|
| Depuración / ejecución | `dotnet run` | `bin\` |
| Publicación | `dotnet publish … -o dist` | `dist\OpenCmd.exe` (single-file) |

- Destino: `net9.0-windows`
- `PublishSingleFile=true`, `self-contained false` → el equipo de destino necesita el runtime .NET 9 Desktop
- `ApplicationIcon` + recurso WPF: `Assets/OpenCmd.ico`

`bin/`, `obj/` y `dist/` están en `.gitignore`.

## 11. Extender la aplicación

Puntos de extensión recomendados:

| Objetivo | Dónde intervenir |
|----------|------------------|
| Nuevo stack / marcador | `ProjectScanner.IsProject` / kind |
| Nueva disposición de paneles | `TerminalLauncher.BuildArguments` |
| Otro editor (VS Code, etc.) | Nuevo launcher al estilo de `CursorLauncher` |
| Más de 3 recientes | `MainViewModel.MaxRecent` + interfaz |
| Temas | recursos en `MainWindow.xaml` |

Regla a mantener: **servicios sin WPF** y **ventana sin lógica de `wt`**.

## 12. Seguridad y límites

- No hay red ni telemetría
- No se ejecutan comandos remotos: solo lo que el usuario escribió en cada tarjeta y shells locales
- Las rutas con espacios se entrecomillan; la barra final `\` se normaliza (excepto la raíz de unidad `C:\`)
- No incluir en el repositorio: configuración de AppData, builds, `.env` ni capturas de prueba con rutas personales
