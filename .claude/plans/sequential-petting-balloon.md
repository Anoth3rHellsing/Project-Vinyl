# Plan: Fix Verification Findings — Project Vinyl

## Context
Tras verificar el proyecto Project Vinyl (runtime smoke test + code review), se identificaron tres hallazgos accionables:
1. **VinylTheme.axaml huérfano** — existe pero no está referenciado en App.axaml; es un tema legacy "AeroGlass" (colores claros) vs el tema activo "AeroNight" (oscuro).
2. **NAudio source folder ruido** — ~1300 archivos .cs de NAudio-main dentro del proyecto, excluidos correctamente por .csproj pero ensucian búsquedas e indexación.
3. **Position polling verificado** — el loop de tracking usa CancellationToken correctamente y se detiene en Stop/Pause/Dispose. ✅ No requiere cambios.

Este plan cubre solo los items 1 y 2 que requieren acción.

## Changes

### 1. Eliminar VinylTheme.axaml
- **File:** `project-vinyl/Assets/Styles/VinylTheme.axaml`
- **Action:** Delete file
- **Rationale:** Tema legacy no referenciado. App.axaml solo importa AeroNightTheme.axaml. Confirmado que VinylTheme usa colores incompatibles (#07405E foreground claro vs #FFFFFF oscuro).

### 2. Mover NAudio-main fuera del árbol del proyecto
- **Source:** `project-vinyl/NAudio-main/`
- **Destination:** `../NAudio-main-reference/` (sibling al proyecto, fuera del scope de compilación)
- **Action:** `mv project-vinyl/NAudio-main ../NAudio-main-reference`
- **Rationale:** El .csproj ya excluye esta carpeta via `<Compile Remove="NAudio-main\**" />`. Moverla elimina ruido en glob/grep/IDE sin afectar la build. La referencia NuGet `NAudio 3.1.0` sigue siendo la fuente real de la librería.
- **Note:** Si el usuario prefiere eliminarla completamente (ya que NuGet provee todo), se puede hacer `rm -rf` en su lugar.

## Files to Modify
- `project-vinyl/Assets/Styles/VinylTheme.axaml` — DELETE
- `project-vinyl/NAudio-main/` — MOVE to `../NAudio-main-reference/`

## Verification
1. Confirmar que `VinylTheme.axaml` ya no existe
2. Confirmar que `NAudio-main/` ya no está dentro de `project-vinyl/`
3. Confirmar que `App.axaml` sigue referenciando solo `AeroNightTheme.axaml` (sin cambios necesarios)
4. Ejecutar `Executable/ProjectVinyl.exe` y capturar screenshot para confirmar que la UI sigue funcionando correctamente sin regresiones visuales