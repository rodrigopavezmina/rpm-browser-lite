# RPM IA Job Apply Browser Lite Turbo

Edición independiente de RPM Browser Lite para Windows, enfocada en preparar y revisar postulaciones laborales desde un perfil local. Mantiene el navegador base ligero, pero incorpora asistencia de llenado para formularios de empleo.

## Instalación independiente

- Instalador x64: `RPM-IA-Job-Apply-Browser-Lite-Turbo-Setup-1.0.1-x64.exe`
- Versión portable x64: `RPM-IA-Job-Apply-Browser-Lite-Turbo-Portable-1.0.1-x64.zip`

Se instala en `%LOCALAPPDATA%\Programs\RPM IA Job Apply Browser Lite Turbo`, sin tocar la instalación ni los accesos directos de RPM Browser Lite. También puede ejecutarse en paralelo con el navegador original.

## Cómo funciona

- El administrador se abre al iniciar sin restaurar automáticamente todas las ventanas guardadas.
- Cada ventana se puede abrir bajo demanda desde el administrador.
- **Active** mantiene la página cargada.
- **Sleeping** pausa temporalmente CPU y temporizadores.
- **Caffeine** mantiene una ventana activa aunque esté inactiva.
- **Hibernated** libera el WebView2 y conserva estado, historial y una vista previa local.
- Las sesiones y cookies se conservan en el perfil local de WebView2; la aplicación no solicita ni registra contraseñas ni claves API.
- Las ventanas admiten ID, título, emoji, color, notas RTF, marcadores, captura de página y búsqueda rápida.
- Desde Options → Edge extensions se pueden cargar carpetas locales desempaquetadas de Microsoft Edge/Chromium que contengan `manifest.json`; los cambios se aplican al reiniciar.

El modo portable guarda su información en una carpeta `Data` junto a `RPMIAJobApplyBrowserLiteTurbo.exe`. La instalación normal usa `%LOCALAPPDATA%\RPM IA Job Apply Browser Lite Turbo`; por diseño no lee cookies, sesiones ni ventanas del navegador original.

## Requisitos

- Windows 10/11 x64.
- .NET Framework 4.8.
- Microsoft Edge WebView2 Runtime Evergreen.

## Compilar desde el código

La compilación usa el compilador de .NET Framework y descarga automáticamente el paquete NuGet de WebView2:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1 -ArtifactLabel 1.0.1 -BuildFolder build-job-apply-turbo
```

Los archivos generados quedan en `artifacts\` (instalador, portable y `SHA256SUMS`). Las carpetas `build-*`, `artifacts`, `.packages`, `bin` y `obj` están excluidas del repositorio para no subir binarios temporales ni dependencias descargadas.

## Actualizaciones

Esta edición inicia sin canal automático para no consumir ni instalar actualizaciones de RPM Browser Lite. Cuando exista un canal propio, se puede configurar manualmente una dirección HTTPS de manifiesto en Options.

## Privacidad y datos locales

No subas al repositorio `estado.json`, `perfil-postulacion.dat`, perfiles WebView2, cookies, capturas ni notas personales. Estos archivos viven fuera del proyecto, normalmente en `%LOCALAPPDATA%\RPM IA Job Apply Browser Lite Turbo`. Protege esa carpeta en equipos compartidos porque contiene sesiones autenticadas.

## Postulacion rapida

RPM IA Job Apply Browser Lite Turbo incluye un asistente local para formularios de empleo. En el menu de opciones o en el menu de la bandeja puedes abrir **Perfil de postulación** y guardar una sola vez tus datos, experiencias, estudios, preferencias y el archivo de CV. El perfil se cifra con DPAPI de Windows y queda separado del estado general del navegador.

Cuando una vacante este abierta, pulsa **⚡ Postular** (tambien funciona `Ctrl+Shift+A`). El asistente relaciona etiquetas en espanol e ingles, atributos `autocomplete`, nombres de campos y tipos de control; completa inputs, selects, fechas y formularios React/Angular/Vue, y marca en amarillo los campos que requieren revision. Si configuraste un CV compatible, intenta adjuntarlo al campo de curriculum mediante WebView2.

El flujo nunca envia la postulacion. No completa contrasenas, CAPTCHA, firmas, consentimientos, declaraciones legales ni datos demograficos. En formularios de varias paginas, mientras permanezcas en el mismo portal la siguiente pagina se prepara automaticamente durante 45 minutos; el boton **Enviar** siempre debe pulsarlo la persona.

Si un portal no acepta eventos sinteticos, usa el resumen del asistente para completar manualmente el campo marcado. El motor es generico y no depende de que el ATS interprete correctamente el PDF o LaTeX del CV: la fuente de verdad es el perfil revisado por el usuario.

Las fechas laborales conservan su precision original: `AAAA-MM` no se convierte silenciosamente en un dia. Los formularios con `AAAA-MM-DD`, mes, ano, dia separado o selects de meses se completan cuando hay evidencia suficiente; si un campo exige un dia que no existe en el perfil, se muestra una fecha derivada para confirmacion y se deja el campo sin escribir.

## Estado del proyecto

La rama `browser-lite-original` (igual que `main`) conserva RPM Browser Lite 2.8.33. Esta rama, `job-apply-turbo`, contiene únicamente la edición Turbo y sus artefactos usan nombres propios.
