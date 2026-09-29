# Open CMD

Aplicación de escritorio para Windows que abre **Windows Terminal** ya dividida: eliges la carpeta padre de un proyecto (por ejemplo un monorepo con `backend` y `frontend`) y obtienes un panel por cada parte, listo para trabajar.

Sin abrir PowerShell a mano, sin dividir paneles y sin hacer `cd` en cada uno.

---

## Qué resuelve

En el día a día del desarrollo suele pasar esto:

1. Abrir Windows Terminal
2. Dividir paneles
3. Navegar a `backend`, `frontend`, etc.
4. Recién entonces ejecutar `npm run dev`, `dotnet watch`, etc.

**Open CMD** hace eso en un clic: detecta las carpetas del proyecto, permite marcar cuáles abrir, configurar de forma opcional un comando de arranque por carpeta y lanza la terminal dividida.

También puedes abrir la carpeta (o cada subcarpeta) en **Cursor** con `cursor . --classic`.

---

## Requisitos

| Requisito                                                                  | Notas                                |
| -------------------------------------------------------------------------- | ------------------------------------ |
| Windows 10/11                                                              | Aplicación WPF                       |
| [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) | Framework-dependent                  |
| [Windows Terminal](https://aka.ms/terminal)                                | `wt.exe` (Microsoft Store o winget)  |
| [Cursor](https://cursor.com/) (opcional)                                   | Solo para el botón “Abrir en Cursor” |

---

## Características

- **Detección automática** de subproyectos (Node, Python, Go, Rust, .NET, Java, PHP, Ruby, Dart, Elixir, Deno, Angular, workspaces npm/yarn, carpetas `apps`/`packages`/`services`, nombres como `backend`/`frontend`)
- **Disposición de paneles** según la cantidad marcada (2 verticales, 3 en L, 4 en cuadrícula 2×2, 5 o más en columnas)
- **Comando opcional** por carpeta (si queda vacío, solo se abre la consola en esa ruta)
- **Shell**: PowerShell, PowerShell 7 (`pwsh`) o CMD
- **Últimos 3 proyectos** en la pantalla de inicio (Abrir / Editar / Cursor)
- **Abrir en Cursor** la carpeta raíz o cada subcarpeta
- Tema oscuro e icono propio

---

## Flujo de uso

1. **Inicio** — elige una carpeta o reabre uno de los últimos 3 proyectos
2. **Dentro del proyecto** — marca carpetas, ordena, define comandos opcionales y el shell
3. **Abrir** — Windows Terminal con un panel por cada carpeta seleccionada

---

## Uso rápido

### Opción A — ejecutar el `.exe` publicado

Si ya tienes un build en `dist\` (local; no se versiona en git):

```powershell
.\dist\OpenCmd.exe
```

### Opción B — compilar y ejecutar

```powershell
dotnet run -c Release
```

### Opción C — publicar un ejecutable

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

El resultado queda en `dist\OpenCmd.exe` (carpeta ignorada por git).

También puedes pasar una carpeta como argumento:

```powershell
.\OpenCmd.exe "C:\ruta\a\tu\proyecto"
```

---

## Cómo funciona (resumen)

```
Elegir carpeta padre
        │
        ▼
 ProjectScanner ──► lista de DetectedProject
        │
        ▼
 UI (marcar / ordenar / comandos / shell)
        │
        ├──► TerminalLauncher ──► wt.exe (-w new + split-pane)
        └──► CursorLauncher   ──► cursor.cmd . --classic
```

La configuración de usuario (recientes, comandos, orden, carpetas desmarcadas, shell) se guarda en:

```text
%APPDATA%\OpenCmd\settings.json
```

Ese archivo **no forma parte del repositorio**: contiene rutas locales de la máquina.

Más detalle en [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## Estructura del repositorio

```text
open-cmd/
├── Assets/                 # Icono de la aplicación
├── Converters/             # Conversores WPF
├── Models/                 # DetectedProject, RecentEntry, ShellOption
├── Services/
│   ├── ProjectScanner.cs   # Detección de subproyectos
│   ├── TerminalLauncher.cs # Construcción y lanzamiento de wt.exe
│   ├── CursorLauncher.cs   # Abrir carpeta en Cursor
│   └── SettingsStore.cs    # Persistencia en %APPDATA%
├── ViewModels/
│   └── MainViewModel.cs    # Estado de la interfaz
├── tools/                  # Scripts auxiliares (por ejemplo, generar el icono)
├── docs/
│   └── ARCHITECTURE.md
├── App.xaml / MainWindow.xaml
├── OpenCmd.csproj
└── README.md
```

---

## Stack

- C# / **.NET 9**
- **WPF** (`net9.0-windows`)
- Windows Terminal (`wt.exe`)
- System.Text.Json para la configuración

Sin dependencias NuGet externas: solo el SDK de escritorio de .NET.

---

## Desarrollo

```powershell
# Restaurar y compilar
dotnet build

# Ejecutar
dotnet run

# Publicar single-file (framework-dependent)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

Regenerar el icono (opcional):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\make-icon.ps1
```

---

## Licencia

Este proyecto se distribuye bajo la licencia [MIT](LICENSE).

---

## Roadmap / ideas

- Atajos de teclado para Abrir / Inicio
- Perfiles de Windows Terminal por proyecto
- Soporte para más disposiciones personalizadas
- Instalador / winget

---

Hecho para acortar el ritual diario de abrir consolas en monorepos.
