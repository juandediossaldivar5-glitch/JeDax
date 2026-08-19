# Bitácora MELX — Standalone App Design Spec
Date: 2026-08-19

## Overview

New standalone .NET 10 SSR app (`~/Desktop/bitacora-melx`) that replaces the "BITACORA MELX.numbers" spreadsheet for scheduling and tracking truck access (loading/unloading) at MELX. Deployed as a separate Railway service pointing to the same shared PostgreSQL instance used by JeDax and kitting-web.

Auth is kitting-web-style: one shared password per role configured via environment variables. No per-user accounts table.

## Tech Stack

- .NET 10 Blazor SSR (same pattern as JeDax and kitting-web)
- PostgreSQL (shared Railway instance, table prefix `Melx`)
- EF Core 9 with raw SQL `CREATE TABLE IF NOT EXISTS` at startup (no migrations, same as JeDax Postgres path)
- Cookie auth via `melx_session` JSON cookie
- UI: dark/lime theme matching JeDax (`wwwroot/css/app.css`)

## Auth

Environment variables (Railway service settings):
```
MELX_PASS_ADM=...
MELX_PASS_MKT=...
MELX_PASS_OPE=...
MELX_PASS_MELX=...
```

Login form: free-text `usuario` + `contraseña`. The password determines the role — whichever env var matches. Username is stored in the session for display and audit (`CreadoPor`, `ActualizadoPor`).

### Roles

```csharp
public enum RolMelx { ADM, MKT, OPE, MELX }
```

### Permission matrix

| Action | ADM | MKT | OPE | MELX |
|---|---|---|---|---|
| `PuedeVer` | ✅ | ✅ | ✅ | ✅ |
| `PuedeRegistrar` | ✅ | ✅ | — | ✅ |
| `PuedeCompletarOperaciones` | ✅ | — | ✅ | — |

## Data Model

### Table: `MelxUnidades`

| Field | Type | Nullable | Notes |
|---|---|---|---|
| Id | int | no | PK, auto-increment |
| Fecha | date | no | Scheduled date |
| Horario | text | no | Free text, e.g. "08:00–10:00" |
| HoraRegistro | timestamptz | no | UTC timestamp when row was created |
| ResponsableMkt | text | no | Name of MKT/MELX person who registered |
| Origen | text | no | |
| Destino | text | no | |
| LineaTransportista | text | no | |
| NombreOperador | text | no | |
| Placas | text | no | |
| NumeroCaja | text | no | |
| TelefonoOperador | text | no | |
| TipoMovimiento | text | no | "Descarga" or "Carga" |
| Estatus | text | no | Default: "Programada" |
| PersonaAcceso | text | yes | Filled by OPE/ADM |
| HoraIngreso | time | yes | Filled by OPE/ADM |
| HoraSalida | time | yes | Filled by OPE/ADM |
| Comentario | text | yes | Filled by OPE/ADM |
| CreadoPor | text | no | Username from session |
| ActualizadoPor | text | yes | Username of last editor |
| ActualizadoEn | timestamptz | yes | UTC |

Enums stored as text (no Postgres enum types — simpler).

Valid Estatus values: `Programada`, `EnPlanta`, `Salida`, `Retenida`.
Valid TipoMovimiento values: `Descarga`, `Carga`.

## Pages & Endpoints

### GET `/bitacora`

- Default filter: today's date. User can change via date input.
- Secondary filter: Estatus dropdown (Todos / Programada / En planta / Salida / Retenida).
- Table columns: Horario, Hora Registro, Responsable, Origen, Destino, Transportista, Operador, Placas, Caja, Teléfono, Tipo, Estatus, Acceso, Ingreso, Salida, Comentario, [Actualizar].
- **HoraRegistro** shown as `dd/MM/yy HH:mm` (local time).
- Conflict indicator: rows sharing the same `Fecha + Horario` show ⚠ in the Horario cell.
- MKT/MELX/ADM: "Nueva unidad" button + inline registration form.
- OPE/ADM: "Actualizar" link per row → `?editar={id}` expands inline Operaciones form.
- Status badges: Programada (gray), EnPlanta (lime), Salida (blue), Retenida (red).
- Header: MELCO.GIS logo left of title "Control de Acceso de Unidades".

### POST `/api/crear`
MKT/MELX/ADM only.
- Validates all required fields.
- Sets HoraRegistro = DateTime.UtcNow, Estatus = "Programada", CreadoPor = session username.
- Before inserting: checks if another record with same Fecha + Horario exists.
  - If yes: redirects to `/bitacora?warn=conflicto` (warning, not a block — record still saved).
- On success: redirects to `/bitacora`.

### POST `/api/actualizar/{id}`
OPE/ADM only.
- Updates: Estatus, PersonaAcceso, HoraIngreso, HoraSalida, Comentario.
- Sets ActualizadoPor = session username, ActualizadoEn = now.
- On success: redirects to `/bitacora`.

### POST `/api/login` / POST `/api/logout`
Standard cookie-based session, same pattern as kitting-web.

## UI Details

### Conflict warning banner
When `?warn=conflicto` query param is present:
> "⚠ Ya existe otra unidad programada en el mismo horario. Se registró de todas formas."

### Logo
`wwwroot/melx-logo.png` — copy from `/private/tmp/bitacora_extract/Data/image1-23.png`.

### CSS badges
```css
.badge-programada { background: rgba(255,255,255,.08); color: var(--muted); }
.badge-enplanta   { background: rgba(46,204,113,.12); color: var(--green); }
.badge-salida     { background: rgba(77,158,255,.12); color: var(--blue); }
.badge-retenida   { background: rgba(255,77,77,.12);  color: var(--red); }
```

## Files

```
bitacora-melx/
├── bitacora-melx.csproj
├── Program.cs                    # startup, endpoints, table creation
├── appsettings.json
├── appsettings.Production.json
├── Models/
│   └── MelxUnidad.cs            # POCO + enums as constants
├── Security/
│   ├── RolMelx.cs
│   ├── SessionUser.cs
│   ├── AuthService.cs           # password→role lookup via env vars
│   ├── CurrentUser.cs           # scoped, reads cookie
│   └── Permisos.cs
├── Data/
│   └── AppDbContext.cs          # DbSet<MelxUnidad>, no query filters (single tenant)
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   ├── _Imports.razor
│   ├── Shared/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   └── Pages/
│       ├── Bitacora.razor       # main page
│       └── Login.razor
└── wwwroot/
    ├── melx-logo.png
    └── css/
        └── app.css
```

## JeDax Cleanup

Remove from JeDax (`feat/bitacora-unidades` already merged to main):
- `Components/Pages/Bitacora.razor`
- `Models/UnidadAcceso.cs`
- `Helpers/StringExtensions.cs`
- `Migrations/20260819155146_AddUnidadAcceso.cs` + Designer + Snapshot update
- `Security/Permisos.cs` — remove 3 Bitacora permission methods
- `Data/AppDbContext.cs` — remove `DbSet<UnidadAcceso>` and query filter
- `Components/Shared/NavMenu.razor` — remove Bitácora link
- `Program.cs` — remove 2 Bitacora endpoints + CREATE TABLE block
- `wwwroot/melx-logo.png`
- `wwwroot/css/app.css` — remove 4 badge classes if not used elsewhere

## Out of Scope

- Export to Excel/PDF
- Push notifications
- Per-user accounts (single password per role)
- Multi-tenant isolation (single tenant: MELX)
