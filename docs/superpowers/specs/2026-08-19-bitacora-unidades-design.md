# Bitácora de Unidades MELX — Design Spec
Date: 2026-08-19

## Overview

New module inside JeDax (Railway + PostgreSQL) that replaces the "BITACORA MELX.numbers" spreadsheet used to schedule and track truck access for loading/unloading at MELX. Two roles participate: Mkt creates the record, Mlx completes the Operaciones fields.

## Data Model

### New enum: `TipoMovimientoUnidad`
```
Descarga | Carga
```
(Separate from existing `TipoMovimiento` to avoid naming conflict.)

### New enum: `EstadoUnidad`
```
Programada | EnPlanta | Salida | Retenida
```
Default on creation: `Programada`.

### New model: `UnidadAcceso`

| Field | Type | Nullable | Notes |
|---|---|---|---|
| Id | int | no | PK |
| TenantId | int | no | Row-level isolation |
| Fecha | DateOnly | no | Scheduled date |
| Horario | string | no | Free text, e.g. "08:00–10:00" |
| ResponsableMkt | string | no | |
| Origen | string | no | |
| Destino | string | no | |
| LineaTransportista | string | no | |
| NombreOperador | string | no | |
| Placas | string | no | |
| NumeroCaja | string | no | |
| TelefonoOperador | string | no | |
| TipoMovimiento | TipoMovimientoUnidad | no | |
| Estatus | EstadoUnidad | no | Default: Programada |
| PersonaAcceso | string? | yes | Filled by Mlx |
| HoraIngreso | TimeOnly? | yes | Filled by Mlx |
| HoraSalida | TimeOnly? | yes | Filled by Mlx |
| Comentario | string? | yes | Filled by Mlx |
| CreadoPor | string | no | Username |
| CreadoEn | DateTime | no | UTC |
| ActualizadoPor | string? | yes | Username of last editor |
| ActualizadoEn | DateTime? | yes | UTC |

Query filter on TenantId (same pattern as Case, Vale, etc.).

## Permissions

Add to `Security/Permisos.cs`:

```csharp
PuedeCrearBitacora(rol) → Admin, Mkt
PuedeCompletarBitacora(rol) → Admin, Mlx
PuedeVerBitacora(rol) → true (all authenticated)
```

NavMenu link visible for Admin, Mkt, Mlx.

## Pages & Endpoints

### GET `/bitacora`
- Shows a table of `UnidadAcceso` records.
- Default filter: today's date. User can change date via date input.
- Secondary filter: Estatus (all / Programada / EnPlanta / Salida / Retenida).
- Conflict indicator: rows that share the same `Fecha + Horario` as another row display a ⚠ icon in the Horario cell.
- Mkt/Admin: "Nueva unidad" button.
- Mlx/Admin: each row has an "Actualizar" button that reveals an inline form for Operaciones fields.
- Status badges: Programada (gray), EnPlanta (lime/green), Salida (blue), Retenida (red).

### POST `/api/bitacora/crear`
Mkt/Admin only.
- Validates required MKT fields.
- Before inserting, checks if another record with same TenantId + Fecha + Horario already exists.
  - If yes: redirects back to `/bitacora` with `?warn=conflicto` query param so the page shows a yellow warning banner.
  - The record is still saved — it's a warning, not a block.
- Sets Estatus = Programada, CreadoPor = current user, CreadoEn = now.
- On success: redirects to `/bitacora`.

### POST `/api/bitacora/actualizar/{id}`
Mlx/Admin only.
- Updates Operaciones fields: Estatus, PersonaAcceso, HoraIngreso, HoraSalida, Comentario.
- Sets ActualizadoPor = current user, ActualizadoEn = now.
- On success: redirects to `/bitacora`.

## UI Details

### Header
- MELCO.GIS logo (`wwwroot/melx-logo.png`, extracted from BITACORA MELX.numbers) displayed left of the page title.
- Title: "Control de Acceso de Unidades".

### Logo extraction
Copy `/tmp/bitacora_extract/Data/image1-23.png` → `wwwroot/melx-logo.png` at migration time (done once manually or in setup script).

### Conflict warning banner
When query param `?warn=conflicto` is present, show a yellow banner:
> "⚠ Ya existe otra unidad programada en el mismo horario. Se registró de todas formas."

Auto-dismisses on next navigation.

### Inline Operaciones form
Clicking "Actualizar" on a row expands a small form below it (or replaces the row) with fields for Estatus (select), PersonaAcceso, HoraIngreso, HoraSalida, Comentario. Submit POSTs to `/api/bitacora/actualizar/{id}`.

## Files Touched

| File | Change |
|---|---|
| `Models/UnidadAcceso.cs` | New model + enums |
| `Data/AppDbContext.cs` | Add `DbSet<UnidadAcceso>`, query filter, index |
| `Migrations/` | New EF Core migration |
| `Security/Permisos.cs` | 3 new permission methods |
| `Components/Pages/Bitacora.razor` | New page |
| `Program.cs` | 2 new POST endpoints |
| `Components/Shared/NavMenu.razor` | Add Bitácora link |
| `wwwroot/melx-logo.png` | Copy logo from Numbers archive |

## Out of Scope

- Editing MKT fields after creation (not in the original Excel workflow).
- Export to Excel/PDF.
- Push notifications for conflicts.
- History/audit log beyond ActualizadoPor.
