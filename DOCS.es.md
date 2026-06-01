# FileFinder — Documentación

Una herramienta rápida de búsqueda de archivos por nombre para Windows
(C# / WPF, .NET 9) con un motor de búsqueda acelerado con SIMD. Esta guía cubre
desde el indexado hasta la sintaxis de búsqueda, la configuración y los atajos de
teclado.

- [Instalación y ejecución](#instalacion)
- [Indexar tus unidades](#indexar)
- [Buscar](#buscar)
- [Motores de búsqueda (JIT vs MASM)](#motores)
- [Columnas de resultados](#columnas)
- [Acciones del clic derecho](#clic-derecho)
- [Configuración](#configuracion)
- [Bandeja e instancia única](#bandeja)
- [Estadísticas y la caché del índice](#estadisticas)
- [Atajos de teclado](#atajos)
- [Preguntas frecuentes](#faq)

---

## Instalación y ejecución

Dos descargas en la página de [Releases](https://github.com/robertorenz/FileFinder/releases/latest):

- **`FileFinder-Setup-x.y.z.exe`** — instalador. Instala por usuario (sin
  administrador) o para todos los usuarios (con administrador); añade una entrada
  al menú Inicio y un icono de escritorio opcional.
- **`FileFinder.exe`** — archivo único portable. Sin instalación y **sin
  necesidad del runtime de .NET** — solo descárgalo y ejecútalo.

En el primer arranque, si no hay índice, FileFinder abre la **Configuración** para
que elijas unidades y crees uno.

---

## Indexar tus unidades

FileFinder busca archivos en un índice en memoria, así que primero indexa las
unidades que elijas. Elige el método automáticamente por unidad:

| Método | Cuándo | Velocidad |
|---|---|---|
| **Lectura de la MFT (NTFS)** | Ejecutando **como administrador** en una unidad **NTFS** | Indexa una unidad completa en segundos |
| **Exploración de carpetas** | En caso contrario (sin admin o no NTFS) | Primer escaneo más lento; sin admin |

- Elige unidades en **Configuración → Unidades a indexar** y pulsa **Crear índice**.
- Para la vía rápida, pulsa **Reiniciar como administrador** en la Configuración.
- El índice se **guarda en disco** y se recarga al instante en el siguiente inicio.
- **Borrar índice** (Configuración o el menú Archivo) elimina el índice y la
  caché; al borrarlo desde la Configuración, el cuadro permanece abierto para
  reconstruirlo.

> El índice se reconstruye a petición — no se actualiza solo cuando cambian los
> archivos.

---

## Buscar

Escribe en el cuadro de búsqueda; los resultados aparecen mientras escribes. Un
**cuadro vacío lista todos los archivos** indexados (hasta el límite de
visualización).

**Texto simple** es una coincidencia de subcadena sin distinción de
mayúsculas/minúsculas contra el **nombre del archivo y su extensión** (no la ruta
completa):

```
informe       → Informe Trimestral 2024.xlsx, informe.png, mi-informe.txt …
```

**Varias palabras** deben aparecer *todas*, en cualquier orden, en cualquier
parte del nombre:

```
icono auriculares png   → auriculares-icono.png, png_icono_auriculares.svg …
```

**Comodines** — una palabra con `*` o `?` se compara como patrón contra todo el
nombre:

| Patrón | Coincide con |
|---|---|
| `*.gif` | todos los archivos `.gif` |
| `informe*` | nombres que empiezan por "informe" |
| `IMG_????.jpg` | `IMG_0001.jpg`, `IMG_2024.jpg`, … |

Puedes combinarlos: `icono *.png` exige la palabra "icono" **y** un nombre `.png`.

La línea de resultados muestra el recuento, el tiempo y qué motor se usó
(JIT/MASM).

---

## Motores de búsqueda (JIT vs MASM)

La búsqueda de subcadena viene en dos implementaciones intercambiables:

- **JIT** — intrínsecos de hardware AVX2 de C#, compilados por el JIT a ensamblador
  vectorizado.
- **MASM** — una rutina en ensamblador x64 escrita a mano en `FileFinderAsm.dll`,
  invocada desde C# por P/Invoke.

Elige el predeterminado en **Configuración → Motor de búsqueda predeterminado**
(MASM donde esté disponible), o compáralos con **Ver → Comparar JIT vs MASM…**
(`Ctrl+B`) — ejecuta tu término actual en ambos motores 40 veces en todos los
núcleos e informa del mejor tiempo.

> Las búsquedas con varias palabras y comodines siempre usan la vía JIT; una sola
> palabra simple puede usar MASM.

---

## Columnas de resultados

Activa columnas en **Configuración → Columnas de resultados** (el nombre siempre
se muestra):

- **Carpeta**, **Tipo**, **Tamaño**, **Fecha de modificación**, **Atributos**

El tamaño, la fecha y los atributos se leen del disco **solo para las filas que
ves**, así que no cuestan nada cuando están ocultas. Haz clic en una cabecera para
**ordenar** — la cabecera de la columna activa está en **negrita** con una flecha
▲/▼. El tamaño ordena numéricamente y la fecha cronológicamente.

---

## Acciones del clic derecho

Haz clic derecho en cualquier resultado:

- **Abrir**, **Abrir con…**, **Ejecutar como administrador**
- **Abrir carpeta contenedora**, **Abrir en Terminal aquí**
- **Copiar ▸** Archivo · Ruta completa · Ruta de la carpeta · Nombre del archivo ·
  Nombre sin extensión · Tamaño · Fecha de modificación
- **Buscar otros archivos de este tipo** (pone la búsqueda en `*.ext`)
- **Propiedades** (el cuadro nativo de Windows)

Hacer doble clic en una fila abre el archivo.

---

## Configuración

**Archivo → Configuración…** (`Ctrl+,`) es el centro de control:

- Unidades a indexar, Crear / Borrar índice, Reiniciar como administrador
- Motor de búsqueda predeterminado + Comparativa
- Columnas de resultados
- Idioma (English / Español, cambia en vivo)

Todo se guarda en `settings.json` (ver abajo) y se aplica en cada inicio.

---

## Bandeja e instancia única

- Al cerrar la ventana, **FileFinder se minimiza a la bandeja del sistema** para
  que el índice en memoria siga listo.
- Al abrirlo de nuevo, **vuelve la ventana existente** (instancia única).
- **Clic derecho en el icono de la bandeja → Salir** (o **Archivo → Salir**) para
  cerrar del todo; doble clic en el icono para restaurar la ventana.

---

## Estadísticas y la caché del índice

**Ver → Estadísticas del índice…** (`Ctrl+I`) muestra recuentos de archivos y
carpetas, unidades, RAM en uso, la ubicación y el tamaño de la caché y los tipos
de archivo principales.

La caché del índice está en:

```
%LocalAppData%\FileFinder\index.ffix
```

`.ffix` es el índice binario sin comprimir propio de FileFinder (una imagen en
memoria de nombres y rutas — sin el contenido de los archivos). La configuración
vive junto a él en `settings.json`. Puedes borrar cualquiera de forma segura; la
app los reconstruye.

---

## Atajos de teclado

| Atajo | Acción |
|---|---|
| `F1` | Documentación |
| `Ctrl+,` | Configuración |
| `Ctrl+I` | Estadísticas del índice |
| `Ctrl+B` | Comparar JIT vs MASM |
| `Enter` / doble clic | Abrir el archivo seleccionado |

---

## Preguntas frecuentes

**¿Necesito permisos de administrador?** No — solo para la vía rápida de indexado
por MFT. Todo lo demás funciona como usuario estándar.

**¿Busca en el contenido de los archivos?** No, busca en los **nombres** (y la
extensión).

**¿Por qué es grande el archivo de caché?** Guarda los nombres dos veces
(visualización + en minúsculas para buscar) más las rutas de carpetas. Bórralo
cuando quieras desde la Configuración.

**¿Se actualiza cuando cambian los archivos?** No automáticamente — reconstruye el
índice para reflejar los cambios.

**¿CPU sin AVX2?** Se usa una vía escalar automáticamente; la búsqueda sigue
siendo rápida.
