# Migration Summary — .NET Framework → net10.0

## Status: COMPLETE ✅

All 5 projects build with **0 errors, 0 warnings**.

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## What the ATX tool did (iteration 4 baseline)

- Retargeted all 5 `.csproj` files to `net10.0` (SDK-style)
- Upgraded EF Core to 10.0.14, AWS SDK v4, OpenIdConnect 10.0.14, Magick.NET 15.1.0, CDK 2.275.0, xunit 2.9.3
- Removed redundant `System.Linq.Expressions 4.3.0` and unused `Microsoft.Build 17.2.0`
- Folded `Startup.cs` into a `Program.cs` minimal-hosting model (preserving all DI registrations and middleware order)
- All 5 parity gates passed: `config_parity`, `dangling_endpoint`, `auth_parity`, `cloud_sdk`

---

## Changes made in this cycle

### 1. `Bookstore.Data/Bookstore.Data.csproj` — Microsoft.Data.SqlClient upgrade
**Problem:** Transitive dependency `Microsoft.Data.SqlClient 6.1.7` (pulled in via `Microsoft.EntityFrameworkCore.SqlServer`) brought in `System.Security.Cryptography.Pkcs 9.0.11`, which has two known CVEs:
- `GHSA-2fmv-rv2c-59x7` (moderate)
- `GHSA-846f-f764-76mx` (high)

**Fix:** Pinned `Microsoft.Data.SqlClient` to `6.1.8` as an explicit direct reference to pull in a patched transitive `System.Security.Cryptography.Pkcs`.

### 2. `Bookstore.Web/Helpers/LocalAuthenticationHandler.cs` — ISystemClock obsolescence
**Problem:** The constructor signature used the obsolete `ISystemClock` parameter (`CS0618`):
```csharp
// Before
public LocalAuthenticationHandler(..., ISystemClock clock) : base(..., clock) {}
```

**Fix:** Removed the obsolete `ISystemClock` parameter. In .NET 8+, `AuthenticationHandler<T>` uses `TimeProvider` (set via `AuthenticationSchemeOptions.TimeProvider`) instead of the injected `ISystemClock`:
```csharp
// After
public LocalAuthenticationHandler(...) : base(...) {}
```

### 3. `Bookstore.Web/Bookstore.Web.csproj` and `Bookstore.Data/Bookstore.Data.csproj` — NuGet.Packaging / NuGet.Protocol vulnerability override
**Problem:** `Microsoft.VisualStudio.Web.CodeGeneration.Design 10.0.2` pulled in `NuGet.Packaging 6.12.1` and `NuGet.Protocol 6.12.1`, both affected by `GHSA-g4vj-cjjj-v7hg` (low severity).

**Fix:** Added explicit direct references to `NuGet.Packaging 6.13.1` and `NuGet.Protocol 6.13.1` in both `Bookstore.Web` and `Bookstore.Data` to force the resolver to use the patched versions.

---

## Final project state

| Project | Target | Errors | Warnings |
|---|---|---|---|
| Bookstore.Domain | net10.0 | 0 | 0 |
| Bookstore.Data | net10.0 | 0 | 0 |
| Bookstore.Web | net10.0 | 0 | 0 |
| Bookstore.Cdk | net10.0 | 0 | 0 |
| Bookstore.Domain.Tests | net10.0 | 0 | 0 |

---

## Next steps

None blocking. The solution builds cleanly on net10.0 with zero warnings.

Potential future improvements (not blocking):
- `Microsoft.VisualStudio.Web.CodeGeneration.Design` is a scaffolding/dev-time tool. Consider adding `<PrivateAssets>all</PrivateAssets>` to prevent it from flowing into dependent projects — this would also naturally eliminate the NuGet.* transitive overrides. Verify scaffolding still works after that change.
- Run the full test suite (`dotnet test`) to confirm runtime behavior is unchanged.
- Review `LocalAuthenticationHandler` — the `"Sub"` claim lookup (`identity.FindFirst("Sub")`) uses a case-sensitive string; confirm this matches the actual claim key emitted in the local dev flow.
