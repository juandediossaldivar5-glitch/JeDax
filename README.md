# JeDax — Control de Inventario

Blazor Server + EF Core 8 + SQLite (dev) / PostgreSQL (prod)

## Estructura

```
JeDax/
├── Models/          # Entidades (Tenant, Usuario, Case, Vale, Producto, Movimiento)
├── Data/            # AppDbContext + TenantContext + DbSeeder
├── Security/        # SessionUser, Permisos, AuthService
├── Services/        # InventarioService, ValeService
├── Components/
│   ├── Pages/       # Login, Stock, Ingreso, Salidas, Logout
│   └── Shared/      # MainLayout, NavMenu
└── wwwroot/css/     # Tema oscuro
```

## Arrancar en desarrollo

```bash
dotnet restore
dotnet ef migrations add InitialCreate --project JeDax
dotnet run
```

El seed crea automáticamente el tenant `laredo` y los usuarios del sistema original.

## Usuarios iniciales

| Usuario     | Contraseña   | Rol    |
|-------------|-------------|--------|
| JUAN_ADM    | JD180794    | Admin  |
| OPE_LAREDO  | LAREDO2026  | Laredo |
| OPE_LNK     | LANDESK.2026| Lnk    |
| OPE_MKT     | MKT2026     | Mkt    |
| OPE_MLX     | Mexico.2026 | Mlx    |
| OPE_EPQ     | EPQ2026     | Epq    |

**Login:** ir a `/login`, slug = `laredo`

## Escaneo QR

El login acepta formato `usuario!contraseña` desde escáner (igual que el sistema Laredo).
Ingreso y Salidas hacen auto-focus al campo de scan tras cada registro.

## Migrar a PostgreSQL (Railway)

1. En `appsettings.Production.json` las variables de entorno de Railway se inyectan automáticamente.
2. Cambiar `"UsePostgres": true` o setear desde Railway env vars.
3. `dotnet ef migrations add InitialCreate` genera el schema compatible con ambos providers.

## Próximos módulos (Semana 2+)

- [ ] Página Vales (crear, listar, importar)
- [ ] Página Admin (gestión de usuarios)
- [ ] Generador de CASE (port de CaseGenerador.cs)
- [ ] Exportación a Excel (ClosedXML)
- [ ] Ubicación visual por rack
