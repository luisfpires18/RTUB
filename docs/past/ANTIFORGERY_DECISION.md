# Antiforgery Configuration Decision

## Current State

The application configures antiforgery with a custom header name:
```csharp
services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
app.UseAntiforgery();
```

However, authentication endpoints (`/auth/login` and `/auth/logout`) explicitly disable antiforgery:
```csharp
app.MapPost("/auth/login", ...).DisableAntiforgery();
app.MapPost("/auth/logout", ...).DisableAntiforgery();
```

## Decision: Keep Antiforgery Disabled for Auth Endpoints

**Rationale:**
1. **Blazor Interactive Server Context**: The login/logout endpoints are called from Blazor Interactive Server components, which handle antiforgery differently than traditional form submissions.
2. **Cookie-based Authentication**: Since authentication uses cookies and the endpoints redirect after completion, the risk of CSRF is mitigated by:
   - POST-only operations (no GET requests that modify state)
   - Immediate redirects after authentication
   - Cookie SameSite attributes (configured via Identity options)
3. **User Experience**: Requiring antiforgery tokens for login/logout would complicate the Blazor component implementation without significant security benefit.
4. **Industry Practice**: Many applications disable antiforgery for authentication endpoints when using cookie-based auth with proper SameSite configuration.

## Security Mitigations

1. **SameSite Cookie Configuration**: Ensure Identity cookie options include `SameSite=Strict` or `SameSite=Lax` (default in ASP.NET Core Identity).
2. **POST-only**: Both endpoints only accept POST requests.
3. **Redirects**: Both endpoints redirect immediately after processing, preventing state leakage.
4. **Lockout Protection**: Login endpoint includes account lockout checks to prevent brute-force attacks.

## Recommendation

**Keep the current configuration** with antiforgery disabled for auth endpoints, but:
- Document this decision (this file)
- Ensure SameSite cookie configuration is properly set
- Monitor for any CSRF-related security advisories
- Consider enabling antiforgery if the application architecture changes (e.g., API-first approach)

## Future Considerations

If the application moves to:
- API-first architecture with JWT tokens
- Separate authentication service
- Stateless authentication

Then antiforgery should be re-evaluated and potentially enabled for auth endpoints.
