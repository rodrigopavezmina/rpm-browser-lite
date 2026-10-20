# RPM Browser Lite para Windows

RPM Browser Lite es un navegador Windows ligero pensado para mantener muchas páginas, cuentas y proyectos abiertos sin dejar que cada ventana consuma memoria permanentemente. Usa un perfil WebView2 compartido: al iniciar sesión en una página, la sesión se conserva entre sus ventanas.

## Estados de ahorro

- ● Active en verde: la página está cargada y disponible.
- ● Sleeping en amarillo: se pausan CPU y temporizadores, conservando el navegador en memoria.
- ● Caffeine en ámbar: la ventana se mantiene despierta y queda así incluso después de reiniciar la aplicación.
- ● Hibernated en rojo: se libera por completo el WebView2 de esa ventana. La barra superior muestra el estado y el botón Restore la reconstruye.

Al hibernar se guarda una vista previa JPEG reducida de la última página. Ocupa disco local, no mantiene un proceso WebView2 ni una captura de tamaño completo en memoria. La vista previa se puede desactivar en Options. El modo Keep permanently hibernated evita que la ventana se reactive al seleccionarla.

El botón Use low-memory preset propone 20 segundos para dormir, 5 minutos para hibernar y 256 MB de caché. Las ventanas con cafeína se excluyen de Free RAM now.

Al volver a abrir RPM Browser Lite, si hay ventanas guardadas de la sesion anterior aparece una pregunta de restauracion. La opcion No abre solamente el administrador y deja todo en disco, sin crear ventanas ni procesos web; la opcion Si restaura las ventanas de forma explicita para ese arranque. La eleccion no se aplica automaticamente a los siguientes arranques.

## Marcadores y capturas

El gestor abre tres pestañas: ventanas, páginas recientes y marcadores. Desde Bookmarks puedes:

- Guardar la ventana seleccionada.
- Importar exportaciones HTML de Chrome y otros navegadores.
- Importar el archivo JSON Bookmarks de un perfil Chrome.
- Exportar en HTML compatible con Chrome.

El botón 📷 Capture hace una captura PNG de página completa cuando el sitio lo permite y la abre desde la carpeta local Capturas.

El botón ★ Bookmarks abre la barra lateral dentro de cada ventana. Las carpetas importadas se muestran como un árbol con líneas y doble clic. En el gestor, las ventanas seleccionadas pueden moverse a la papelera, restaurarse durante 30 días o eliminarse definitivamente.

## Actualizaciones

En Options puedes configurar un manifiesto HTTPS y activar la instalación automática. El manifiesto debe incluir version, downloadUrl HTTPS, notes y sha256 del instalador. RPM Browser Lite descarga el instalador, verifica el hash y espera a que la aplicación cierre antes de actualizarse.

## Notas y personalización

Cada ventana puede tener ID opcional TD-D01 a TD-D50, título, emoji monocromático y color propio. El color cubre la barra superior y da un tono claro a la nota lateral.

Las notas son RTF locales: admiten negrita, cursiva, color, Markdown escrito e imágenes insertadas en cualquier punto del texto mediante arrastrar y soltar.

## Actualizar desde ChatGPT Lite

Ejecuta el instalador RPM-Browser-Lite-Setup más reciente con la aplicación cerrada. Si existe la instalación anterior, el instalador usa su misma carpeta y conserva el perfil local en %LOCALAPPDATA%\ChatGPT Lite; por eso se mantienen cookies, caché, sesiones, ventanas y notas. Solo cambia el nombre visible a RPM Browser Lite.

La versión portable usa una carpeta Data junto a RPMBrowserLite.exe. Para actualizar una instalación portable, reemplaza los archivos de la aplicación, abre RPMBrowserLite.exe y conserva esa carpeta Data.

## Privacidad

Las contraseñas se escriben y procesan en cada sitio web dentro de WebView2; RPM Browser Lite no pide ni registra claves, y no usa una clave API. Como cualquier navegador, su perfil local contiene cookies de sesión, caché, vistas previas y capturas: protege la carpeta de datos, especialmente en equipos compartidos.

WebView2 y los sitios web mantienen sus propias políticas de autenticación, permisos y apariencia. El modo oscuro de RPM Browser Lite corresponde solo a sus controles nativos; una captura hibernada muestra fielmente el aspecto que tenía la página al momento de hibernar.
