# Arquitectura — Open CMD

Documento técnico del diseño de la app. Pensado para quien lea el código o quiera extenderlo.

## 1. Visión

Open CMD es una app **WPF de proceso único** (sin backend, sin base de datos). Su trabajo es:

1. Elegir / recordar una carpeta padre de proyecto
2. Descubrir subcarpetas “proyecto”
3. Dejar al usuario configurar selección, orden, comandos y shell
4. Lanzar procesos externos: **Windows Terminal** y, opcionalmente, **Cursor**

No ejecuta los servidores de desarrollo ella misma: solo abre consolas (y opcionalmente el editor) en las rutas correctas.

## 2. Diagrama de capas

```text
┌─────────────────────────────────────────────────────────┐
│  Presentación                                           │
│  App.xaml / MainWindow.xaml + MainWindow.xaml.cs        │
│  (eventos UI, diálogos, drag & drop, MessageBox)        │
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
| `MainWindow` | Binding, clicks, OpenFolderDialog, errores al usuario | Lógica de scan o de `wt` |
| `MainViewModel` | Estado observable, recientes, Save/Load settings, Launch | Parsear CLI de Terminal |
| `ProjectScanner` | Heurísticas de detección y orden | UI / launch |
| `TerminalLauncher` | Armar args de `wt` y `Process.Start` | Elegir qué carpetas |
| `CursorLauncher` | `cursor.cmd . --classic` en `WorkingDirectory` | Validar UI |
| `SettingsStore` | Serializar JSON en AppData | Conocer WPF |

## 3. Flujo principal

### 3.1 Inicio (sin proyecto cargado)

1. `App` crea `MainWindow` → `MainViewModel.Load()`
2. `SettingsStore.Load()` lee `%APPDATA%\OpenCmd\settings.json`
3. Se hidratan `Recents` (máx. 3) y el shell seleccionado
4. UI muestra zona “Elegir carpeta” + tarjetas de recientes

Acciones por reciente:

- **Abrir** → `OpenSavedProject` (LoadRoot + LaunchTerminal)
- **Editar** → `LoadRoot` (entra a la vista de proyecto sin lanzar)
- **Cursor** → `CursorLauncher.Open(path)`

### 3.2 Dentro de un proyecto

1. `LoadRoot(path)` normaliza la ruta y llama a `ProjectScanner.Scan`
2. Se aplican `Order`, `Unchecked` y `Commands` guardados para esa raíz
3. El usuario marca carpetas, reordena, escribe comandos
4. **Abrir en N paneles** → `RememberCurrentProjects` + `TouchRecent` + `TerminalLauncher.Launch`
5. Casita (header) → `GoHome` (persiste y limpia `RootPath`)

Los recientes **solo se actualizan al lanzar la terminal**, no al solo navegar/editar.

## 4. Detección de proyectos (`ProjectScanner`)

Orden de estrategia:

1. **Workspaces** npm/yarn (`package.json` / `pnpm-workspace`) → expandir globs
2. **Hijos inmediatos** que parezcan proyecto (marcadores de stack)
3. Si hay menos de 2: bajar un nivel en contenedores (`apps`, `packages`, `services`, `projects`) o en carpetas que a su vez tengan ≥2 proyectos (p. ej. `src/backend` + `src/frontend`)
4. **Fallback**: listar hijos no “basura” (docs, tests, assets, etc.)

### Marcadores (ejemplos)

`package.json`, `pyproject.toml` / `requirements.txt` / `Pipfile` / `manage.py`, `go.mod`, `Cargo.toml`, `pom.xml` / Gradle, `*.csproj` / `*.fsproj` / `*.sln`, `composer.json`, `Gemfile`, `pubspec.yaml`, `mix.exs`, `deno.json`, `angular.json`.

### Roles y orden

Nombres como `backend`, `frontend`, `api`, `web`, `server`, `client` (y variantes ES) influyen en el sort: backend-like antes que frontend-like.

Carpetas ignoradas: `node_modules`, `.git`, `dist`, `bin`, `obj`, venvs, etc.

## 5. Lanzamiento de Windows Terminal (`TerminalLauncher`)

### Por qué no un string de PowerShell suelto

`wt.exe` interpreta `;` como separador de sus propios comandos. Un `-Command` con `;` se parte mal. Por eso:

- PowerShell / pwsh: el script del panel se pasa con **`-EncodedCommand`** (UTF-16LE Base64)
- CMD: se genera un `.bat` temporal en `%TEMP%\OpenCmd` (limpieza de archivos viejos)

### Layouts

| Paneles | Estrategia |
|---------|------------|
| 1 | Un tab |
| 2 | `split-pane -V` |
| 3 | Vertical + horizontal en el derecho |
| 4 | Vertical, horizontal del derecho, `move-focus left`, horizontal del izquierdo (grilla A\|B / C\|D) |
| 5+ | Columnas verticales con tamaños relativos |

Siempre: `-w new` (ventana nueva).

Cada panel:

1. `Set-Location` (o `cd /d`) a la carpeta
2. Título de ventana / banner
3. Comando de usuario opcional

## 6. Cursor (`CursorLauncher`)

```text
FileName        = cursor.cmd
Arguments       = . --classic
WorkingDirectory = <carpeta elegida>
UseShellExecute = true   # necesario para .cmd
```

Equivale a abrir esa carpeta en Cursor en modo classic. Si `cursor` no está en PATH, se muestra un error amigable.

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

- Claves normalizadas (`PathUtil.Normalize`), comparación case-insensitive
- **Máximo 3** entradas en `recent`
- Contiene **rutas absolutas de la máquina del usuario** → nunca versionar este archivo

## 8. Modelos

| Tipo | Uso |
|------|-----|
| `DetectedProject` | Nombre, path, kind, comando, selección, placeholder |
| `RecentEntry` | Path + summary legible para la home |
| `ShellOption` | Label + executable (`powershell.exe` / `pwsh.exe` / `cmd.exe`) |
| `AppSettings` | DTO del JSON |

`ObservableModel` es el helper mínimo de `INotifyPropertyChanged`.

## 9. UI / UX

- **Home**: drop zone + últimos proyectos (sin casita)
- **Proyecto**: header con logo/título + casita a la derecha; card de raíz; lista de hijos; chips de shell; CTA principal
- Tema oscuro (`#101114`, acento mint `#6EE7B7`), title bar oscuro vía DWM (`WindowTheme`)
- Idioma de UI: español (voseo)

## 10. Build y empaquetado

| Modo | Comando típico | Salida |
|------|----------------|--------|
| Debug/run | `dotnet run` | `bin\` |
| Publish | `dotnet publish … -o dist` | `dist\OpenCmd.exe` single-file |

- Target: `net9.0-windows`
- `PublishSingleFile=true`, `self-contained false` → requiere runtime .NET 9 Desktop en el PC destino
- `ApplicationIcon` + Resource WPF: `Assets/OpenCmd.ico`

`bin/`, `obj/` y `dist/` están en `.gitignore`.

## 11. Extender la app

Ideas de puntos de extensión limpios:

| Objetivo | Dónde tocar |
|----------|-------------|
| Nuevo stack / marcador | `ProjectScanner.IsProject` / kind |
| Nuevo layout de paneles | `TerminalLauncher.BuildArguments` |
| Otro editor (VS Code, etc.) | Nuevo launcher al estilo `CursorLauncher` |
| Más de 3 recientes | `MainViewModel.MaxRecent` + UI |
| Temas | recursos en `MainWindow.xaml` |

Mantener la regla: **servicios sin WPF**, **ventana sin lógica de wt**.

## 12. Seguridad / límites

- No hay red ni telemetría
- No se ejecutan comandos remotes: solo lo que el usuario escribió en cada card + shells locales
- Rutas con espacios se entrecomillan; trailing `\` se normaliza (excepto raíz de unidad `C:\`)
- No incluir en el repo: settings de AppData, builds, `.env`, capturas de prueba con rutas personales
