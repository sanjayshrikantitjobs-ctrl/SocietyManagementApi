# Architecture Notes

## Layering (Clean Architecture)

```
SocietyManagement.Domain            <- no dependencies on anything else
        ^
SocietyManagement.Application       <- depends on Domain + Shared only
        ^
SocietyManagement.Infrastructure    <- depends on Application (implements its interfaces)
        ^
SocietyManagement.API               <- depends on Application + Infrastructure + Shared
```

`Shared` (ApiResponse envelope, exceptions, constants) is referenced by both
`Application` and `API`/`Infrastructure` — it has no dependencies of its own.

The dependency rule that matters: **Application never references
Infrastructure or API.** Application defines interfaces
(`IApplicationDbContext`, `IJwtService`, `IEmailService`, `IRepository<T>`,
...) in `Common/Interfaces`; Infrastructure implements them. This is what
makes the Application layer (all the actual business logic) unit-testable
without a database, an SMTP server, or ASP.NET Core running.

## Request flow (CQRS via MediatR)

```
Angular  →  Controller (API)  →  Mediator.Send(Command/Query)
                                        ↓
                     ValidationBehaviour (FluentValidation)
                                        ↓
                     LoggingBehaviour (Serilog, per-request)
                                        ↓
                     Handler (Application/Features/<Module>/...)
                                        ↓
                IApplicationDbContext / IRepository<T> (Infrastructure)
                                        ↓
                              SQL Server (EF Core)
```

Every write path (`Command`) goes through `AuditableEntitySaveChangesInterceptor`
(stamps `CreatedAt/CreatedBy/ModifiedAt/ModifiedBy`, turns a hard delete into a
soft delete) and `DispatchDomainEventsInterceptor` (publishes any
`BaseEvent`s raised on tracked entities, after the save actually commits).

## Adding a new module (the pattern every future module follows)

1. **Domain**: entity in `Entities/`, extending `BaseAuditableEntity`; enums in
   `Enums/` if needed.
2. **Infrastructure**: `IEntityTypeConfiguration<T>` in `Persistence/Configurations/`,
   add the `DbSet<T>` to `ApplicationDbContext` and `IApplicationDbContext`.
3. **Application**: `Features/<Module>/` — Commands (with inline
   FluentValidation validator + handler, see `Features/Buildings/BuildingFeature.cs`
   for the compact single-file pattern used for straightforward CRUD, or
   `Features/Auth/Commands/Login/LoginCommand.cs` for the one-file-per-command
   pattern used where a feature is complex enough to deserve its own file),
   Queries, and a DTO.
4. **API**: a controller under `Controllers/`, actions decorated with
   `[HasPermission(Permissions.<Module>.<Action>)]`.
5. **Shared**: add the module's permission codes to `Constants.Permissions`
   and seed them in `DbSeeder.cs` / `Database/Seed/02_SeedRolesPermissions.sql`.
6. **Angular**: a `<module>.service.ts` calling the new endpoints, a
   `<module>.routes.ts` lazy-loaded from `app.routes.ts`, and feature
   components built from the shared `app-data-table` / `app-page-header` /
   `app-confirm-dialog` / `app-stat-card` building blocks.
7. **Database**: extend `Database/Schema/01_CreateSchema.sql` (or a new
   numbered script) and `Database/ERD/ERD.md`.
8. Verify with `dotnet build` and `ng build` before starting the next module.

## Why permissions live in the JWT, not a per-request DB call

`Infrastructure/Services/JwtService.cs` bakes every permission code the user's
role currently has into the access token as `perm` claims at login/refresh
time. `API/Authorization/PermissionAuthorizationHandler.cs` then checks those
claims directly — no database round-trip on every authorized request. The
tradeoff: a permission change via Role Management takes effect for a given
user on their *next* login or token refresh (≤15 minutes, the access token
lifetime), not instantly. That's an intentional, documented tradeoff for
request-path performance; if instant revocation is a hard requirement for a
specific permission, check `Application.Common.Interfaces.ICurrentUserService`
against a live DB query in that one handler instead of relying on the JWT claim.
