# VAHINI: Enterprise DataOps Orchestrator

VAHINI is a high-performance, multi-tenant DataOps orchestrator designed to coordinate complex data annotation pipelines (e.g., RLHF, image labeling, medical audits) at scale. Built with **.NET 8** and **PostgreSQL**, VAHINI prioritizes absolute data isolation, zero-overhead concurrency, and deep integration with the ASP.NET Core Identity pipeline.

## Engineering Decisions

### 1. Zero-Overhead Concurrency (`PostgreSQL xmin`)
Chosen **PostgreSQL xmin** over `byte[]` to leverage native engine-level concurrency.
- **The Problem:** In generic applications, claiming work concurrently results in race conditions or locking overhead. 
- **The Solution:** By mapping `public uint Version { get; set; }` to `xmin`, VAHINI achieves lock-free, atomic queue processing. If two annotators claim the same batch simultaneously, the second attempt gracefully fails with a `DbUpdateConcurrencyException`.

### 2. Global Query Filter Isolation
Implemented **Global Query Filters** to prevent the "Insecure Direct Object Reference" (IDOR) class of vulnerabilities.
- Every `DataBatch` is inextricably bound to an `OrganizationId`.
- The `ApplicationDbContext` intercepts the user's claims and applies a **Global Query Filter**: `.HasQueryFilter(b => b.OrganizationId == _currentOrgId && _isUserApproved);`
- **Result:** It is mathematically impossible for developers to accidentally leak data across organizations or to unapproved users.

### 3. High-Performance Claims-Based Auth
Utilized **Claims-based Tenant ID** to eliminate redundant database joins on every request.
- VAHINI implements a bespoke `VahiniClaimsPrincipalFactory`.
- Instead of constantly hitting the database to check if a user is an Org Admin or Approved, these flags (`IsApproved`, `IsOrgAdmin`, `OrganizationId`) are baked directly into the encrypted HTTP-only session cookie.
- UI state and API authorization gates execute in **O(1) time** strictly from memory.

### 4. The Bouncer (Approval Gate)
- **Admins** can dynamically spin up new Workspaces. They are immediately granted `IsOrgAdmin` status and an `AccessCode`.
- **Members** must provide an `AccessCode` to join a Workspace. They are placed in a quarantined, `IsApproved = false` state.
- Unapproved members are blocked by the DbContext Shield until an Admin explicitly clicks **Approve** from the Command Center.

## Default Credentials
The system automatically seeds an initial "Vahini HQ" organization and 5 sample batches on startup.
- **Email:** `admin@vahini.com`
- **Password:** `Admin@123!`

## Deployment
VAHINI is optimized for containerized deployment on platforms like **Railway**.
- Uses multi-stage Alpine Linux builds (`.net 8-alpine`) for maximum performance and minimal attack surface.
- Simply link your PostgreSQL database via the `DefaultConnection` environment variable.

```bash
docker build -t vahini-app .
docker run -p 8080:80 vahini-app
```
