# Open CMD

Aplicación de escritorio para Windows que abre **Windows Terminal** ya dividida: elegís la carpeta padre de un proyecto (por ejemplo monorepo con `backend` y `frontend`) y obtenés un panel por cada parte, listo para trabajar.

Sin abrir PowerShell a mano, sin dividir paneles, sin `cd` en cada uno.

---

## Qué resuelve

En el día a día de desarrollo suele pasar esto:

1. Abrir Windows Terminal
2. Dividir paneles
3. Navegar a `backend`, `frontend`, etc.
4. Recién ahí correr `npm run dev`, `dotnet watch`, etc.

**Open CMD** hace eso en un clic: detecta las carpetas del proyecto, te deja marcar cuáles abrir, opcionalmente setear un comando de arranque por carpeta, y lanza la terminal partida.

También podés abrir la carpeta (o cada subcarpeta) en **Cursor** con `cursor . --classic`.

---

## Requisitos

| Requisito | Notas |
|-----------|--------|
| Windows 10/11 | App WPF |
| [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) | Framework-dependent |
| [Windows Terminal](https://aka.ms/terminal) | `wt.exe` (Microsoft Store o winget) |
| [Cursor](https://cursor.com/) (opcional) | Solo para el botón “Abrir en Cursor” |

---

## Características

- **Detección automática** de subproyectos (Node, Python, Go, Rust, .NET, Java, PHP, Ruby, Dart, Elixir, Deno, Angular, workspaces npm/yarn, carpetas `apps`/`packages`/`services`, nombres tipo `backend`/`frontend`)
- **Layouts de paneles** según la cantidad marcada (2 vertical, 3 en L, 4 en grilla 2×2, 5+ en columnas)
- **Comando opcional** por carpeta (si lo dejás vacío, solo abre la consola ahí)
- **Shell**: PowerShell, PowerShell 7 (`pwsh`) o CMD
- **Últimos 3 proyectos** en la pantalla de inicio (Abrir / Editar / Cursor)
- **Abrir en Cursor** la carpeta raíz o cada hija
- Tema oscuro e icono propio

---

## Capturas / flujo

1. **Inicio** — elegí carpeta o reabrí uno de los últimos 3
2. **Dentro del proyecto** — marcá carpetas, ordená, comandos opcionales, shell
3. **Abrir** — Windows Terminal con un panel por carpeta seleccionada

---

## Uso rápido

### Opción A — ejecutar el `.exe` publicado

Si ya tenés un build en `dist\` (local, no se versiona):

```powershell
.\dist\OpenCmd.exe
```

### Opción B — compilar y correr

```powershell
dotnet run -c Release
```

### Opción C — publicar un ejecutable

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

El resultado queda en `dist\OpenCmd.exe` (carpeta ignorada por git).

También podés pasar una carpeta como argumento:

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

Ese archivo **no forma parte del repositorio**: contiene rutas locales de tu máquina.

Más detalle en [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## Estructura del repo

```text
open-cmd/
├── Assets/                 # Icono de la app
├── Converters/             # Conversores WPF
├── Models/                 # DetectedProject, RecentEntry, ShellOption
├── Services/
│   ├── ProjectScanner.cs   # Detección de subproyectos
│   ├── TerminalLauncher.cs # Construcción y launch de wt.exe
│   ├── CursorLauncher.cs   # Abrir carpeta en Cursor
│   └── SettingsStore.cs    # Persistencia en %APPDATA%
├── ViewModels/
│   └── MainViewModel.cs    # Estado de la UI
├── tools/                  # Scripts auxiliares (p. ej. generar icono)
├── docs/
│   └── ARCHITECTURE.md
├── App.xaml / MainWindow.xaml
├── OpenCmd.csproj
└── README.md
```

---

## Privacidad y qué no se sube

Este repo está pensado para código fuente, no para artefactos ni datos personales.

| Qué | Dónde vive | ¿En git? |
|-----|------------|----------|
| Código fuente | este repo | sí |
| `bin/`, `obj/`, `dist/` | build local | no (`.gitignore`) |
| `settings.json` | `%APPDATA%\OpenCmd\` | no |
| Rutas de tus proyectos | settings locales | no |
| `.env`, secretos | N/A | no (ignorados) |

Antes del primer push conviene revisar:

```powershell
git status
git check-ignore -v bin obj dist
```

---

## Stack

- C# / **.NET 9**
- **WPF** (`net9.0-windows`)
- Windows Terminal (`wt.exe`)
- System.Text.Json para settings

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

Podés agregar la licencia que prefieras al publicar (MIT, Apache-2.0, etc.). Mientras no haya archivo `LICENSE`, el uso queda a criterio del autor del repo.

---

## Roadmap / ideas

- Atajos de teclado para Abrir / Inicio
- Perfiles de Windows Terminal por proyecto
- Soporte para más layouts personalizados
- Instalador / winget

---

Hecho para acortar el ritual diario de abrir consolas en monorepos.
