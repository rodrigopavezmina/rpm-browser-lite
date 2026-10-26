# RPM Browser Lite

Navegador ligero para Windows pensado para mantener muchas páginas, cuentas y proyectos organizados sin dejar todos los WebView2 activos al mismo tiempo.

## Descargar

- [Instalador Windows x64 2.8.29](https://github.com/rodrigopavezmina/rpm-browser-lite/releases/download/v2.8.29/RPM-Browser-Lite-Setup-2.8.29-x64.exe)
- [Versión portable x64 2.8.29](https://github.com/rodrigopavezmina/rpm-browser-lite/releases/download/v2.8.29/RPM-Browser-Lite-Portable-2.8.29-x64.zip)
- [Notas de la versión 2.8.29](https://github.com/rodrigopavezmina/rpm-browser-lite/releases/tag/v2.8.29)

El instalador crea un acceso directo en el Escritorio y en el menú Inicio. Las actualizaciones verifican SHA-256, esperan el cierre de la versión anterior, escriben los archivos de forma atómica y abren el administrador al terminar.

## Cómo funciona

- El administrador se abre al iniciar sin restaurar automáticamente todas las ventanas guardadas.
- Cada ventana se puede abrir bajo demanda desde el administrador.
- **Active** mantiene la página cargada.
- **Sleeping** pausa temporalmente CPU y temporizadores.
- **Caffeine** mantiene una ventana activa aunque esté inactiva.
- **Hibernated** libera el WebView2 y conserva estado, historial y una vista previa local.
- Las sesiones y cookies se conservan en el perfil local de WebView2; la aplicación no solicita ni registra contraseñas ni claves API.
- Las ventanas admiten ID, título, emoji, color, notas RTF, marcadores, captura de página y búsqueda rápida.
- En TickTick aparecen acciones rápidas para aplazar la tarea seleccionada a mañana o una semana.

El modo portable guarda su información en una carpeta `Data` junto a `RPMBrowserLite.exe`. La instalación normal usa `%LOCALAPPDATA%\ChatGPT Lite` para conservar sesiones, ventanas y notas entre actualizaciones.

## Requisitos

- Windows 10/11 x64.
- .NET Framework 4.8.
- Microsoft Edge WebView2 Runtime Evergreen.

## Compilar desde el código

La compilación usa el compilador de .NET Framework y descarga automáticamente el paquete NuGet de WebView2:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1 -ArtifactLabel 2.8.29 -BuildFolder build-2.8.29
```

Los archivos generados quedan en `artifacts\` (instalador, portable y `SHA256SUMS`). Las carpetas `build-*`, `artifacts`, `.packages`, `bin` y `obj` están excluidas del repositorio para no subir binarios temporales ni dependencias descargadas.

## Actualizaciones

El archivo [`update-manifest.json`](update-manifest.json) publica la versión disponible, la URL HTTPS del instalador, las notas y su hash SHA-256. La aplicación usa el contenido de este archivo desde la rama `main` y solo acepta instaladores cuyo hash coincida.

## Privacidad y datos locales

No subas al repositorio `estado.json`, perfiles WebView2, cookies, capturas ni notas personales. Estos archivos viven fuera del proyecto, normalmente en `%LOCALAPPDATA%\ChatGPT Lite`. Protege esa carpeta en equipos compartidos porque contiene sesiones autenticadas.

## Estado del proyecto

La rama `main` contiene el código fuente reproducible de la versión publicada. Los instaladores se distribuyen mediante [GitHub Releases](https://github.com/rodrigopavezmina/rpm-browser-lite/releases), no dentro del historial Git.
