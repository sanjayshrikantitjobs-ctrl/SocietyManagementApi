# Society Management System

Enterprise Society Management Software — Clean Architecture ASP.NET Core (.NET
10) backend with CQRS/MediatR, Angular 22 standalone/signals frontend, SQL
Server database. Built incrementally, module by module, per the project spec.

## What's in this delivery: the Foundation phase

Everything below is real, hand-written, wired-together code — not a demo/mock.
It's the base every later module (Member Management, Maintenance, Festivals,
Notices, Complaints, ...) builds on:

- **Authentication & security**: JWT access + rotating refresh tokens, login by
  email *or* mobile number, forgot-password via OTP, self-service and
  admin-triggered password reset, account lockout after repeated failed
  logins, BCrypt password hashing, rate limiting, security headers, CORS.
- **Dynamic role & permission system**: Admin/Member system roles, a full
  `Permissions` table covering every module in the spec, a `RolePermissions`
  join table, a Role Management UI with a live permission-matrix editor, and a
  `[HasPermission("...")]` authorization attribute on every protected API
  endpoint — new roles and permission combinations can be created from the UI
  with no code change or redeploy.
- **User Management**: create user (temp password emailed), assign role,
  lock/unlock, admin password reset, soft delete, searchable/paginated list.
- **Society Setup** (Module 1 in full): Society → Building → Wing → Floor →
  Flat hierarchy, plus Parking Slot allocation, complete CRUD end to end
  (Domain entity → EF Core config → CQRS command/query → API controller →
  Angular service → Angular UI).
- **Admin & Member dashboards**: stat cards and Chart.js charts wired to real
  data for everything this phase has (flat occupancy, parking utilization,
  user counts); every metric that depends on a not-yet-built module
  (Maintenance, Festivals, Complaints, Visitors, ...) is present in the layout
  now with a clear "coming soon" state, so no dashboard rework is needed later.
- **Real-time channel**: SignalR hub + Angular client wired end to end and
  ready for `NewNotice` / `ComplaintUpdate` / `PaymentSuccess` /
  `FestivalReminder` events, per spec.
- **Cross-cutting**: standard `{success, message, data, errors}` API envelope,
  global exception handling, Serilog structured logging, Swagger with JWT
  bearer auth, audit logging, soft delete + full audit columns on every table,
  reusable Angular shared components (data table, confirm dialog, stat card,
  page header/breadcrumb, skeleton loader, empty state), light/dark theme.
- **Deployment support**: Dockerfiles for API and Angular (nginx), a
  `docker-compose.yml` wiring API + SQL Server + web together, IIS
  `web.config`, environment-based configuration throughout.

## Important: this code has not been compiled

The sandbox this was built in has no outbound access to NuGet, npm, or the
.NET install script (org network policy — see the conversation this was
delivered in), so **`dotnet build` and `ng build` could not be run here.**
Every file was written carefully by hand to be syntactically and logically
correct against .NET 10 / EF Core 10 / Angular 22 / Angular Material 22 APIs,
but it has not been through a real compiler. Before you rely on this:

1. Run `dotnet restore && dotnet build` from the solution root.
2. Run `npm install && npm run build` inside `SocietyManagement.Web`.
3. Fix whatever the compiler surfaces (expect this to be a short list of
   typos/import issues, not architectural problems) — this is meant to save
   you the 90% "boilerplate and wiring" effort, not to be a zero-touch drop-in.

If you'd like, come back with the compiler output and it'll get fixed fast —
it's much cheaper to fix a build error you paste in than to guess at what a
network-restricted sandbox can't verify.

## Project structure

```
SocietyManagement/
 ├── SocietyManagement.API              # ASP.NET Core Web API, controllers, Program.cs, Swagger
 ├── SocietyManagement.Application      # CQRS (MediatR), FluentValidation, AutoMapper, interfaces
 ├── SocietyManagement.Domain           # Entities, enums, domain events — zero external deps
 ├── SocietyManagement.Infrastructure   # EF Core, repositories, JWT/identity, SignalR, Serilog
 ├── SocietyManagement.Shared           # ApiResponse envelope, exceptions, constants (both layers use this)
 ├── SocietyManagement.Web              # Angular 22 standalone app
 └── Database                           # Raw SQL schema/seed + ERD, as an alternative to EF migrations
```

## Getting started

### Prerequisites
.NET 10 SDK, Node.js 20+, SQL Server (local, Docker, or Azure SQL), Angular
CLI (`npm i -g @angular/cli`).

### 1. Database
See `Database/README.md` for two options (EF Core migrations, or the raw SQL
scripts in `Database/Schema` and `Database/Seed`). Either way you end up with
a bootstrap Admin account — details also in that README.

### 2. Backend
```bash
cd SocietyManagement.API
dotnet restore
dotnet user-secrets set "JwtSettings:Secret" "$(openssl rand -base64 48)"   # or edit appsettings.Development.json directly
dotnet run
```
Swagger UI: `https://localhost:<port>/swagger`.

### 3. Frontend
```bash
cd SocietyManagement.Web
npm install
ng serve
```
Open `http://localhost:4200`. Update `src/environments/environment.ts` if
your API isn't on `https://localhost:7001`.

### 4. Or: everything via Docker
```bash
docker compose up --build
```
Web on `http://localhost:8080`, API on `http://localhost:5000`, SQL Server on
`1433`. Set a real `JwtSettings__Secret` in `docker-compose.yml` before using
this beyond your own machine.

## Roadmap — remaining modules

Built module by module, in the order the spec lists them, each building on
this Foundation phase's entities/patterns without breaking what's already
here:

2. Member Management (Owner/Tenant/Family, documents, vehicles) — wires up the
   `Users.MemberId` and `Flats.OwnerMemberId` forward references already in
   the schema.
3. Maintenance Management (bill generation, late fees, receipts, outstanding
   dashboard) — lights up the dashboard's Maintenance cards/charts.
4. Festival Management (flagship module — budgets, contributions, expenses,
   volunteers, festival dashboard).
5. Event Management (RSVP, QR check-in, attendance, gallery).
6. Notice Board, Complaint Management, Visitor Management, Parking
   enhancements, Staff Management, Document Management, Polls & Voting,
   Meeting Management, Finance/Reports module, then the "Advanced Features"
   list (digital notice board TV mode, society calendar, digital receipts,
   lost & found, marketplace, booking system, emergency SOS, vendor
   directory, utility tracking).

Each module follows the same recipe already established here: Domain entity →
EF Core configuration → CQRS command/query/validator → API controller →
Angular service → Angular feature UI → SQL schema addition, verified with a
real `dotnet build` / `ng build` before moving to the next one.
