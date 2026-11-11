# .NET Framework → net10.0 Migration Summary

## Status
✅ **COMPLETE** — All 5 projects build with **0 errors and 0 warnings**.

## Projects Migrated
| Project | Target Framework | Errors | Warnings |
|---|---|---|---|
| Bookstore.Domain | net10.0 | 0 | 0 |
| Bookstore.Data | net10.0 | 0 | 0 |
| Bookstore.Web | net10.0 | 0 | 0 |
| Bookstore.Domain.Tests | net10.0 | 0 | 0 |
| Bookstore.Cdk | net10.0 | 0 | 0 |

## Changes Made

### Project Files
- **Bookstore.Web.csproj**: Added `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<NuGetAuditMode>direct</NuGetAuditMode>`. Marked `Microsoft.VisualStudio.Web.CodeGeneration.Design` as `PrivateAssets=all`.
- **Bookstore.Data.csproj**: Removed erroneously added `Microsoft.Build 18.11.0` (unused, .NET Framework only), removed `System.Linq.Expressions` (built-in to .NET), and removed `Microsoft.VisualStudio.Web.CodeGeneration.Design` (not needed in Data layer).

### Domain Entity Nullable Fixes (CS8618)
Added `= null!` initializers to EF Core navigation properties and byte array properties across Domain entities — the standard pattern for navigation properties populated at runtime by EF Core:
- `Entity.cs`: `RowVersion`
- `Address.cs`: `AddressLine1`, `City`, `State`, `Country`, `ZipCode`, `Customer` navigation property; removed `#pragma warning disable CS8618`
- `Books/Book.cs`: `Publisher`, `BookType`, `Genre`, `Condition` navigation properties
- `Carts/ShoppingCartItem.cs`: `ShoppingCart`, `Book` navigation properties
- `Customers/Customer.cs`: `Sub`
- `Offers/Offer.cs`: `Genre`, `Condition`, `Publisher`, `BookType`, `Customer` navigation properties
- `Orders/Order.cs`: `Customer`, `Address` navigation properties
- `Orders/OrderItem.cs`: `Order`, `Book` navigation properties
- `ReferenceData/ReferenceDataItem.cs`: `Text`

### Domain DTO Updates
- **Books/BookDtos.cs**: Made `Summary`, `CoverImage`, and `CoverImageFileName` nullable (`string?`, `Stream?`, `string?`) — these are genuinely optional fields (books may have no image or summary).
- **Books/BookService.cs**: Updated `SaveAsync` signature to `string? coverImageFileName`.

### Web Layer — Nullable Fixes
**Controllers:**
- `AuthenticationController.cs`: `Login(string? redirectUri = null)` parameter; added `?? "/"` fallback for `Url.Action`.
- `AddressController.cs`: Used `?? "/Address"` fallback for nullable `model.ReturnUrl` in `Redirect()`.
- `ResaleController.cs`: Used `!` operator for `BookName!`, `Author!`, `ISBN!` in `CreateOfferDto` construction (guaranteed non-null after `ModelState.IsValid`).
- `Areas/Admin/Controllers/InventoryController.cs`: `result.ErrorMessage ?? string.Empty` for `ModelState.AddModelError`; `model.CoverImage` is now `IFormFile?`.

**Helpers:**
- `ClaimsPrincipalExtensions.cs`: Changed `GetSub()` to return `string` (not `string?`) with `?? string.Empty` guard — prevents nullable propagation throughout the codebase while remaining safe.
- `ImageTypesAttribute.cs`, `MaxFileSizeAttribute.cs`: Updated `IsValid(object? value)` to match base class signature.

**Startup:**
- `ServicesSetup.cs`: `KnownNetworks.Clear()` → `KnownIPNetworks.Clear()` (fixes ASPDEPR005 deprecation).
- `AuthenticationSetup.cs`: `_cognitoDomain`, `_cognitoClientId`, `_cognitoAppSignOutUrl` declared as `string?`; `GetClientId()` return type `string?`; used `?.` for nullable `principal`; `sub ?? string.Empty` and `userName ?? string.Empty` when constructing `CreateOrUpdateCustomerDto`.

**ViewModels (Customer):** Added `= null!` or `string?` to all string properties across:
`WishlistIndexViewModel`, `ShoppingCartIndexViewModel`, `SearchIndexViewModel` + `SearchIndexItemViewModel`, `SearchDetailsViewModel`, `ResaleIndexViewModel`, `ResaleCreateViewModel`, `OrderIndexViewModel`, `OrderDetailsViewModel`, `HomeIndexViewModel`, `ErrorViewModel`, `CheckoutIndexViewModel` + `CheckoutAddressViewModel` + `CheckoutItemViewModel`, `CheckoutFinishedViewModel`, `AddressCreateUpdateViewModel`, `AddressIndexViewModel`.

Properties from optional nullable sources (`CoverImageUrl`, `Summary`) remain `string?`; required display fields use `= null!`.

**ViewModels (Admin):** Same treatment across:
`Areas/Admin/Models/ErrorViewModel`, `InventoryCreateUpdateViewModel`, `InventoryDetailsViewModel`, `InventoryIndexListItemViewModel`, `OfferIndexItemViewModel`, `Admin/OrderDetailsViewModel`, `OrderIndexListItemViewModel`, `ReferenceDataCreateViewModel`, `ReferenceDataIndexListItemViewModel`.

`OfferFilters Filters` and `OrderFilters Filters` initialized to `new()`.

`IFormFile CoverImage` → `IFormFile?` (optional upload).

**Razor Views:**
- `Views/ShoppingCart/Index.cshtml`: `User.Identity.IsAuthenticated` → `User.Identity?.IsAuthenticated != true`
- `Views/Shared/_LoginPartial.cshtml`: `User.Identity.IsAuthenticated` → `User.Identity?.IsAuthenticated == true`; `User.Identity.Name` → `User.Identity?.Name`
- `Views/Shared/_NavBarPartial.cshtml`: Both `User.Identity.IsAuthenticated` → `User.Identity?.IsAuthenticated == true`
- `Areas/Admin/Views/Shared/_LoginPartial.cshtml`: `User.Identity.Name` → `User.Identity?.Name`

### CDK — CloudFront API Migration (CS0618/CS0612)
`Bookstore.Cdk/CoreStack.cs`: Replaced deprecated `CloudFrontWebDistribution` with modern `Distribution` + `S3BucketOrigin.WithOriginAccessIdentity()` from `Amazon.CDK.AWS.CloudFront.Origins`. The new API automatically grants read access and sets the bucket policy, eliminating the manual `CanonicalUserPrincipal` policy statement. Removed unused `Amazon.CDK.AWS.IAM` using.

## Next Steps
- The `NuGetAuditMode=direct` setting in Bookstore.Web suppresses transitive NU1901 low-severity vulnerability advisories for `NuGet.Packaging`/`NuGet.Protocol` 6.12.1 that come through `Microsoft.VisualStudio.Web.CodeGeneration.Design`. Once a newer version of that tool (> 10.0.3) ships with updated NuGet dependencies, this workaround can be removed.
- Consider adding EF Core migrations if any database schema changes are needed post-upgrade.
- Review Razor views for any remaining nullable-reference patterns when upgrading to future .NET versions.
