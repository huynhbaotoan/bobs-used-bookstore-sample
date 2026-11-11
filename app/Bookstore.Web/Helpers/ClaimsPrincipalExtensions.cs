using System.Security.Claims;

namespace Bookstore.Web.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetSub(this ClaimsPrincipal claimsPrincipal)
        {
            // With MapInboundClaims = false the raw "sub" claim type is preserved. The
            // ClaimTypes.NameIdentifier fallback keeps this working if inbound claim mapping
            // is ever re-enabled (the JWT handlers map "sub" to NameIdentifier in that case).
            // This method is only called for authenticated users whose tokens always carry a sub
            // claim, so null-coalescing to empty string is a safe belt-and-suspenders guard.
            return claimsPrincipal?.FindFirst("sub")?.Value
                ?? claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? string.Empty;
        }
    }
}
