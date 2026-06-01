namespace FileFinder.Localization;

/// <summary>Static string tables. Keys are shared; one dictionary per language.</summary>
internal static class Strings
{
    public static readonly Dictionary<string, string> En = new()
    {
        // Window / brand
        ["WindowTitle"] = "FileFinder — Fast Disk Search",
        ["BrandSubtitle"] = "SIMD-accelerated disk search",

        // Menu
        ["MenuFile"] = "_File",
        ["MenuOpenCacheFolder"] = "Open Cache Folder",
        ["MenuPreferences"] = "Preferences…",
        ["MenuExit"] = "E_xit",
        ["MenuView"] = "_View",
        ["MenuStatistics"] = "Index Statistics…",
        ["MenuBenchmark"] = "Benchmark JIT vs MASM…",
        ["MenuHelp"] = "_Help",
        ["MenuAbout"] = "About FileFinder",

        // Sidebar
        ["AdminElevated"] = "Administrator — fast MFT indexing available",
        ["AdminStandard"] = "Standard user — using portable folder scan",
        ["RestartAsAdmin"] = "Restart as Administrator",
        ["DrivesToIndex"] = "DRIVES TO INDEX",
        ["SearchEngineHeader"] = "SEARCH ENGINE",
        ["EngineJit"] = "JIT — C# AVX2 intrinsics",
        ["EngineMasm"] = "MASM — hand-written .asm",
        ["EngineReady"] = "MASM engine ready (FileFinderAsm.dll)",
        ["EngineUnavailable"] = "MASM engine unavailable — JIT only",
        ["BenchmarkButton"] = "Benchmark JIT vs MASM",
        ["BuildIndex"] = "Build Index",
        ["CancelIndexing"] = "Cancel Indexing",
        ["ClearIndex"] = "Clear Index",

        // Search / results
        ["SearchPlaceholder"] = "Search file names…  (try *.gif, report*, or any text)",
        ["ColName"] = "Name",
        ["ColFolder"] = "Folder",
        ["ColType"] = "Type",
        ["ColSize"] = "Size",
        ["ColModified"] = "Date modified",
        ["ColAttributes"] = "Attributes",
        ["CtxOpenFile"] = "Open file",
        ["CtxOpenFolder"] = "Open containing folder",

        // Status bar / summaries
        ["Ready"] = "Ready.",
        ["IndexReady"] = "Index ready. Start typing to search.",
        ["NoIndexYet"] = "No index yet — select drives and click Build Index.",
        ["NoIndexShort"] = "No index — select drives and click Build Index.",
        ["LoadingIndex"] = "Loading saved index…",
        ["Benchmarking"] = "Benchmarking engines…",
        ["IndexingCancelled"] = "Indexing cancelled.",
        ["IndexingCancelledSummary"] = "Indexing cancelled — no index built.",
        ["IndexingFailed"] = "Indexing failed.",
        ["IndexCleared"] = "Index cleared.",
        ["BuildFirst"] = "Build an index first.",
        ["MethodMftFast"] = "MFT (fast)",
        ["MethodScanning"] = "Scanning",

        // Composite (string.Format) — keep placeholders intact
        ["StatusIndexing"] = "{0} · {1} · {2} files · {3}",
        ["ResultCapped"] = "{0} matches (showing first {1}) · {2} ms · {3}",
        ["ResultMany"] = "{0} matches · {1} ms · {2}",
        ["ResultOne"] = "{0} match · {1} ms · {2}",
        ["LoadedFromCache"] = "Loaded {0} files from cache ({1}) · built {2}",
        ["IndexedSummary"] = "{0} files indexed across {1} in {2}s",

        // Modal dialogs
        ["OK"] = "OK",
        ["Cancel"] = "Cancel",
        ["Close"] = "Close",
        ["Save"] = "Save",
        ["NoDrivesTitle"] = "No drives selected",
        ["NoDrivesMsg"] = "Select at least one drive to index.",
        ["ClearTitle"] = "Clear index",
        ["ClearMsg"] = "Remove the in-memory index and delete the saved cache file?",
        ["ClearConfirm"] = "Clear",
        ["CannotOpenFile"] = "Cannot open file",
        ["CannotOpenFolder"] = "Cannot open folder",
        ["CannotOpenCache"] = "Cannot open cache folder",
        ["IndexingFailedTitle"] = "Indexing failed",
        ["RestartAdminFailTitle"] = "Could not restart as Administrator",

        // About
        ["AboutTitle"] = "About FileFinder",
        ["AboutBody"] = "FileFinder {0}\n\nSIMD-accelerated disk search for Windows (C# / WPF, .NET 9).\n\nAVX2 hardware search: {1}.",
        ["AvxEnabled"] = "enabled",
        ["AvxScalar"] = "scalar fallback",

        // Benchmark
        ["BenchTitle"] = "Engine Benchmark",
        ["BenchNoIndex"] = "Build or load an index first, then run the benchmark.",
        ["BenchTypeFirst"] = "Type a search term first, then run the benchmark.\n\nTip: use plain text (no * or ?) — the MASM engine compares against the JIT engine on substring searches.",
        ["BenchWildcard"] = "Wildcard queries always use the JIT engine, so there's nothing to compare.\n\nUse a plain text term (no * or ?) to race the JIT and MASM engines.",
        ["BenchTerm"] = "Search term:  \"{0}\"",
        ["BenchScanned"] = "Names scanned:  {0}",
        ["BenchMatches"] = "Matches:  {0}",
        ["BenchJit"] = "JIT  (C# AVX2 intrinsics):     {0:0.000} ms",
        ["BenchMasm"] = "MASM (hand-written .asm DLL):  {0:0.000} ms",
        ["BenchMasmFaster"] = "→ MASM is {0:0.0}× faster",
        ["BenchJitFaster"] = "→ JIT is {0:0.0}× faster",
        ["BenchMasmUnavailable"] = "MASM: unavailable (FileFinderAsm.dll not loaded)",
        ["BenchFooter"] = "(best of 40 runs, all CPU cores)",

        // Statistics dialog
        ["StatsTitle"] = "Index Statistics",
        ["StatsNoIndexSubtitle"] = "No index is currently loaded.",
        ["StatsBuilt"] = "Built {0}",
        ["StatsStatus"] = "Status",
        ["StatsNoIndexRow"] = "No index built yet — use Build Index first.",
        ["StatsFiles"] = "Files indexed",
        ["StatsFolders"] = "Folders",
        ["StatsDrives"] = "Drives",
        ["StatsMemory"] = "Memory in use (app)",
        ["StatsSimd"] = "SIMD acceleration",
        ["SimdAvx2"] = "AVX2 (hardware)",
        ["SimdScalar"] = "Scalar fallback",
        ["StatsCacheFile"] = "Cache file",
        ["StatsCacheSize"] = "Cache size on disk",
        ["StatsNotSaved"] = "Not saved yet",
        ["StatsTopTypes"] = "TOP FILE TYPES",
        ["StatsNoExt"] = "(no extension)",

        // Preferences dialog
        ["PrefTitle"] = "Preferences",
        ["PrefSubtitle"] = "Choose your default search engine and language.",
        ["PrefDefaultEngine"] = "DEFAULT SEARCH ENGINE",
        ["PrefEngineMasm"] = "MASM — hand-written assembly (fastest)",
        ["PrefEngineJit"] = "JIT — C# AVX2 intrinsics (works everywhere)",
        ["PrefEngineNote"] = "MASM needs FileFinderAsm.dll; falls back to JIT if unavailable.",
        ["PrefLanguage"] = "LANGUAGE",
        ["PrefColumns"] = "RESULT COLUMNS",
        ["PrefColumnsNote"] = "Name is always shown. Size, Date modified and Attributes are read from disk for the results you see.",
    };

    public static readonly Dictionary<string, string> Es = new()
    {
        // Window / brand
        ["WindowTitle"] = "FileFinder — Búsqueda rápida en disco",
        ["BrandSubtitle"] = "Búsqueda en disco acelerada con SIMD",

        // Menu
        ["MenuFile"] = "_Archivo",
        ["MenuOpenCacheFolder"] = "Abrir carpeta de caché",
        ["MenuPreferences"] = "Preferencias…",
        ["MenuExit"] = "_Salir",
        ["MenuView"] = "_Ver",
        ["MenuStatistics"] = "Estadísticas del índice…",
        ["MenuBenchmark"] = "Comparar JIT vs MASM…",
        ["MenuHelp"] = "A_yuda",
        ["MenuAbout"] = "Acerca de FileFinder",

        // Sidebar
        ["AdminElevated"] = "Administrador — indexado rápido por MFT disponible",
        ["AdminStandard"] = "Usuario estándar — usando exploración de carpetas",
        ["RestartAsAdmin"] = "Reiniciar como administrador",
        ["DrivesToIndex"] = "UNIDADES A INDEXAR",
        ["SearchEngineHeader"] = "MOTOR DE BÚSQUEDA",
        ["EngineJit"] = "JIT — intrínsecos AVX2 de C#",
        ["EngineMasm"] = "MASM — ensamblador propio",
        ["EngineReady"] = "Motor MASM listo (FileFinderAsm.dll)",
        ["EngineUnavailable"] = "Motor MASM no disponible — solo JIT",
        ["BenchmarkButton"] = "Comparar JIT vs MASM",
        ["BuildIndex"] = "Crear índice",
        ["CancelIndexing"] = "Cancelar indexado",
        ["ClearIndex"] = "Borrar índice",

        // Search / results
        ["SearchPlaceholder"] = "Buscar nombres de archivo…  (prueba *.gif, informe*, o cualquier texto)",
        ["ColName"] = "Nombre",
        ["ColFolder"] = "Carpeta",
        ["ColType"] = "Tipo",
        ["ColSize"] = "Tamaño",
        ["ColModified"] = "Fecha de modificación",
        ["ColAttributes"] = "Atributos",
        ["CtxOpenFile"] = "Abrir archivo",
        ["CtxOpenFolder"] = "Abrir carpeta contenedora",

        // Status bar / summaries
        ["Ready"] = "Listo.",
        ["IndexReady"] = "Índice listo. Empieza a escribir para buscar.",
        ["NoIndexYet"] = "Aún no hay índice — selecciona unidades y pulsa Crear índice.",
        ["NoIndexShort"] = "Sin índice — selecciona unidades y pulsa Crear índice.",
        ["LoadingIndex"] = "Cargando índice guardado…",
        ["Benchmarking"] = "Comparando motores…",
        ["IndexingCancelled"] = "Indexado cancelado.",
        ["IndexingCancelledSummary"] = "Indexado cancelado — no se creó ningún índice.",
        ["IndexingFailed"] = "El indexado falló.",
        ["IndexCleared"] = "Índice borrado.",
        ["BuildFirst"] = "Crea un índice primero.",
        ["MethodMftFast"] = "MFT (rápido)",
        ["MethodScanning"] = "Explorando",

        // Composite
        ["StatusIndexing"] = "{0} · {1} · {2} archivos · {3}",
        ["ResultCapped"] = "{0} coincidencias (mostrando las primeras {1}) · {2} ms · {3}",
        ["ResultMany"] = "{0} coincidencias · {1} ms · {2}",
        ["ResultOne"] = "{0} coincidencia · {1} ms · {2}",
        ["LoadedFromCache"] = "Se cargaron {0} archivos desde la caché ({1}) · creado {2}",
        ["IndexedSummary"] = "{0} archivos indexados en {1} en {2}s",

        // Modal dialogs
        ["OK"] = "Aceptar",
        ["Cancel"] = "Cancelar",
        ["Close"] = "Cerrar",
        ["Save"] = "Guardar",
        ["NoDrivesTitle"] = "Ninguna unidad seleccionada",
        ["NoDrivesMsg"] = "Selecciona al menos una unidad para indexar.",
        ["ClearTitle"] = "Borrar índice",
        ["ClearMsg"] = "¿Quitar el índice en memoria y eliminar el archivo de caché guardado?",
        ["ClearConfirm"] = "Borrar",
        ["CannotOpenFile"] = "No se puede abrir el archivo",
        ["CannotOpenFolder"] = "No se puede abrir la carpeta",
        ["CannotOpenCache"] = "No se puede abrir la carpeta de caché",
        ["IndexingFailedTitle"] = "El indexado falló",
        ["RestartAdminFailTitle"] = "No se pudo reiniciar como administrador",

        // About
        ["AboutTitle"] = "Acerca de FileFinder",
        ["AboutBody"] = "FileFinder {0}\n\nBúsqueda en disco acelerada con SIMD para Windows (C# / WPF, .NET 9).\n\nBúsqueda por hardware AVX2: {1}.",
        ["AvxEnabled"] = "activada",
        ["AvxScalar"] = "modo escalar",

        // Benchmark
        ["BenchTitle"] = "Comparativa de motores",
        ["BenchNoIndex"] = "Crea o carga un índice primero y luego ejecuta la comparativa.",
        ["BenchTypeFirst"] = "Escribe un término de búsqueda primero y luego ejecuta la comparativa.\n\nConsejo: usa texto sin comodines (sin * ni ?) — el motor MASM se compara con el JIT en búsquedas de subcadena.",
        ["BenchWildcard"] = "Las búsquedas con comodines siempre usan el motor JIT, así que no hay nada que comparar.\n\nUsa un término de texto sin comodines (sin * ni ?) para enfrentar los motores JIT y MASM.",
        ["BenchTerm"] = "Término de búsqueda:  \"{0}\"",
        ["BenchScanned"] = "Nombres analizados:  {0}",
        ["BenchMatches"] = "Coincidencias:  {0}",
        ["BenchJit"] = "JIT  (intrínsecos AVX2 de C#):  {0:0.000} ms",
        ["BenchMasm"] = "MASM (DLL en ensamblador):      {0:0.000} ms",
        ["BenchMasmFaster"] = "→ MASM es {0:0.0}× más rápido",
        ["BenchJitFaster"] = "→ JIT es {0:0.0}× más rápido",
        ["BenchMasmUnavailable"] = "MASM: no disponible (FileFinderAsm.dll no cargado)",
        ["BenchFooter"] = "(mejor de 40 ejecuciones, todos los núcleos)",

        // Statistics dialog
        ["StatsTitle"] = "Estadísticas del índice",
        ["StatsNoIndexSubtitle"] = "No hay ningún índice cargado.",
        ["StatsBuilt"] = "Creado {0}",
        ["StatsStatus"] = "Estado",
        ["StatsNoIndexRow"] = "Aún no hay índice — usa Crear índice primero.",
        ["StatsFiles"] = "Archivos indexados",
        ["StatsFolders"] = "Carpetas",
        ["StatsDrives"] = "Unidades",
        ["StatsMemory"] = "Memoria en uso (app)",
        ["StatsSimd"] = "Aceleración SIMD",
        ["SimdAvx2"] = "AVX2 (hardware)",
        ["SimdScalar"] = "Modo escalar",
        ["StatsCacheFile"] = "Archivo de caché",
        ["StatsCacheSize"] = "Tamaño de caché en disco",
        ["StatsNotSaved"] = "Aún no guardado",
        ["StatsTopTypes"] = "TIPOS DE ARCHIVO PRINCIPALES",
        ["StatsNoExt"] = "(sin extensión)",

        // Preferences dialog
        ["PrefTitle"] = "Preferencias",
        ["PrefSubtitle"] = "Elige tu motor de búsqueda e idioma predeterminados.",
        ["PrefDefaultEngine"] = "MOTOR DE BÚSQUEDA PREDETERMINADO",
        ["PrefEngineMasm"] = "MASM — ensamblador propio (el más rápido)",
        ["PrefEngineJit"] = "JIT — intrínsecos AVX2 de C# (funciona en todos)",
        ["PrefEngineNote"] = "MASM necesita FileFinderAsm.dll; usa JIT si no está disponible.",
        ["PrefLanguage"] = "IDIOMA",
        ["PrefColumns"] = "COLUMNAS DE RESULTADOS",
        ["PrefColumnsNote"] = "El nombre siempre se muestra. El tamaño, la fecha de modificación y los atributos se leen del disco para los resultados visibles.",
    };
}
