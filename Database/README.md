# Database Setup

Two supported ways to get schema + seed data into SQL Server. Pick one — don't
mix them on the same database without checking for duplicate inserts.

## Option A — EF Core Migrations (recommended)

The `InitialCreate` migration (covering Identity/RBAC, Society Setup, and the
Festival & Event Management Phase 1 tables) already exists under
`SocietyManagement.Infrastructure/Migrations/`. Just apply it:

```bash
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef database update -p SocietyManagement.Infrastructure -s SocietyManagement.API
```

(Run from the solution root. This sandbox generated the migration successfully
but has no reachable SQL Server/LocalDB instance to apply it against — do this
step on a machine where `(localdb)\MSSQLLocalDB` or your configured
`DefaultConnection` is reachable.)

Then just run the API (`dotnet run`) with `SeedDatabase` left at its default
(`true` in Development, in `Program.cs`) — `Infrastructure/Persistence/DbSeeder.cs`
seeds roles, the full permission matrix, and a bootstrap Admin account on
startup, idempotently. (The API also auto-applies pending migrations on
startup via this same path, so a plain `dotnet run` is enough after cloning —
`dotnet ef database update` above is only needed if you want the schema in
place before first run, e.g. to inspect it.)

## Option B — Raw SQL scripts

Run against an empty `SocietyManagementDb` database, in order:

1. `Schema/01_CreateSchema.sql`
2. `Seed/02_SeedRolesPermissions.sql`
3. `Seed/03_SeedSampleSociety.sql` (optional — local dev convenience data only)

This path does **not** create a login-ready Admin user (the password hash has
to come from the app's real `IPasswordHasher`, not a hand-typed value in SQL —
see the comment in `DbSeeder.cs`). Either let `DbSeeder` add the Admin account
on first run against this schema, or insert one yourself via the app once
it's up (e.g. temporarily relax `[HasPermission(Permissions.Users.Create)]` on
`POST /api/users`, or call it from a already-privileged account).

## Default bootstrap admin account

| Field | Value |
|---|---|
| Email | `admin@societymanagement.local` |
| Mobile | `9999999999` |
| Temporary password | `Admin@12345` |

`MustChangePassword` is `true` on this account — the API forces a password
change via `POST /api/auth/change-password` right after first login. Change
or disable this account before exposing any environment beyond your own
laptop.

See `ERD/ERD.md` for the entity-relationship diagram (Mermaid).
