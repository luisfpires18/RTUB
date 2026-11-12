# Password recovery, email confirmation, /Error, /share and Passkeys

| Route | Owner |
| --- | --- |
| `/forgot-password`, `/reset-password`, `/confirm-email` | React, public (`portal/src/ForgotPassword.tsx`, `ResetPassword.tsx`, `ConfirmEmail.tsx`, `accountApi.ts`) |
| `/Error` | Server-owned static page (`wwwroot/error.html`, `wwwroot/css/error.css`), on purpose |
| `/share` | React (`portal/src/Share.tsx`), public, Web Share Target |

Code: `Services/AccountRecoveryService.cs`, `Endpoints/AccountRecoveryEndpoints.cs`, `Program.cs` (page routes, `/Error`,
`/share`). Identity options, password rules and token providers are the framework's, unchanged.

## API (`/api/account`, public)

| Method | Path | Body | Answers |
| --- | --- | --- | --- |
| POST | `/forgot-password` | `{email}` | 200 `{succeeded, message}` (always the neutral sentence); 400 on a bad email |
| POST | `/reset-password` | `{email, code, password, confirmPassword}` | 200 `{succeeded, message}`; 400 with `code`, `email`, `password`, `confirmPassword` or `reset` |
| POST | `/confirm-email` | `{userId, code}` | 200 `{succeeded, message}` |

Every call needs `X-CSRF-TOKEN` (from `/api/public/antiforgery-token`), and every answer is `no-store`.
`GET /api/account/me` lives in `AccountController`.

## Behavior

- Forgot password: validates the email (required, well-formed). An unknown or unconfirmed address gets the neutral
  sentence and no email. A confirmed account gets a reset token (`GeneratePasswordResetTokenAsync`) and the link
  `{scheme}://{host}{pathBase}/reset-password?code={UrlEncode(token)}&email={UrlEncode(email)}`, rendered by
  `IEmailTemplateRenderer.RenderPasswordResetAsync` and sent with `IEmailSender` ("Recuperação de palavra-passe"). A
  failed send is logged without token or link and answered with the same neutral sentence, so it never reveals that an
  account exists.
- Reset password: needs `email` and `code` (else "Link de redefinição de palavra-passe inválido."); password required
  and 8-100 long, confirmation equal. An unknown email shows the success message (reveals nothing). Otherwise
  `ResetPasswordAsync`; Identity's error descriptions are joined by `; `. An invalid or expired token reads "Este link de
  redefinição é inválido ou já expirou. Pede um novo link.". It never signs the user in.
- Confirm email: the React page posts the link's `userId` and `code` once; the server applies a second
  `WebUtility.UrlDecode` to the code, then `ConfirmEmailAsync`. Messages: "Link de confirmação inválido.", "Utilizador
  não encontrado.", "Obrigado por confirmar o seu email.", "Erro ao confirmar o seu email.". Nobody is signed in.
  Nothing in the app generates confirmation links (no `GenerateEmailConfirmationTokenAsync` caller), so only old links
  reach it.
- `/Error`: target of `UseExceptionHandler("/Error")` in every non-Development environment. Fixed Portuguese text with
  a "Voltar ao início" link, no script, no request data, no stack trace, `Cache-Control: no-store`. Not React on
  purpose: the handler re-executes the failed request with its own method (POST, PUT, DELETE, ...), and an error page
  must render even when the portal bundle, the session or the database is what failed. The handler's 500 status is
  kept; a direct visit answers 200.
- `/share`: the installed app is a share target (manifest `share_target`: `action: "/share"`, `method: "GET"`, params
  `title`, `text`, `url`). `Share.tsx` reads the values from the query (left in the address bar so a reload keeps the
  content), shows them read-only (Título, Texto, URL when present); "Copiar" writes `title\n\ntext\n\nurl` to the
  clipboard (a refused clipboard is swallowed); "Voltar" goes home; with nothing shared: "Nenhum conteúdo para
  partilhar." Nothing is stored, logged or sent. Public, served as the ordinary React shell (GET/HEAD only, POST is
  405). The service worker has no share handling (a GET target needs none). Other manifest members:
  `docs/pwa-practices.md` → *PWABuilder report card*.

## Identity tokens: where they go

- They come only from the emailed link. `/forgot-password`, `/reset-password` and `/confirm-email` are served with
  `Cache-Control: no-store` and `Referrer-Policy: no-referrer`.
- The page reads the query once (`takeLinkQuery`) and immediately calls `history.replaceState` to drop it from the
  address bar and history. The values live in a module variable, never in React state, `localStorage` or
  `sessionStorage`. The reset page forgets them once the reset succeeds; a reload asks for a new link.
- The token travels in the POST body only, never in a URL the app builds. No answer echoes it and nothing logs it (the
  only log line is a failed send, which names the account, not the link).

## Passkeys: Face ID / fingerprint sign-in

A member signs in once with the password, turns it on in `/profile` → *Face ID / impressão digital* ("Ativar neste
dispositivo"), and from then on `/login` → "Entrar com Face ID / impressão digital" signs in with the device's unlock
(Face ID, Touch ID, Android fingerprint, Windows Hello). The password keeps working.

- Implementation: ASP.NET Core Identity's own WebAuthn (no extra package). `IdentityOptions.Stores.SchemaVersion =
  Version3` adds the table `AspNetUserPasskeys` (migration `AddUserPasskeys`, additive; the N-1 app ignores it, so a
  rollback is a plain redeploy). Identity reads that option from the app's service provider when it builds the model: a
  context made without it (unit tests' InMemory contexts) has no passkey table, and `MigrationChainTests` builds its
  context with the host's `AddIdentityServices` for that reason. Version3 also configures `PhoneNumber` at 256;
  `ApplicationUserConfiguration` restates the entity's 80. Discoverable credentials: no username is asked.
- Host binding: a passkey belongs to the host it was made on (RP ID = request host, origin checked against the
  browser's `Origin` header). DEV and PROD keep separate passkeys. Needs HTTPS (or `localhost`).
- API (`Endpoints/PasskeyEndpoints.cs`; every POST/DELETE needs `X-CSRF-TOKEN`, every answer no-store):

  | Route | Who | What |
  | --- | --- | --- |
  | `POST /auth/passkey/options` | anyone | WebAuthn request options (challenge kept in Identity's cookie) |
  | `POST /auth/passkey/login` `{ credential, rememberMe, returnUrl }` | anyone, login rate limit | verify, same checks (unconfirmed, locked: `Locked`, expelled: `Expelled`) and the same tail as `/auth/login` (`LastLoginDate`, `LoginEvents`, cookie); `{ redirect }` or 401 `{ error }` |
  | `GET /api/me/passkeys` | member | own passkeys `[{ id (base64url), name, createdAt }]` |
  | `POST /api/me/passkeys/options` `{ currentPassword }` | member | checks the current password (wrong ones count towards the lockout), then creation options; 409 at 10 passkeys |
  | `POST /api/me/passkeys` `{ credential }` | member | verify + store, named from the User-Agent ("iPhone · Safari") |
  | `DELETE /api/me/passkeys/{id}` | member | own passkey only; anyone else's id is 404 |

- Security: enrollment needs the current password, so an open session alone (borrowed phone, stolen cookie) cannot plant
  a passkey; the challenge issued after that check is what the store call verifies against. A password reset through
  `/reset-password` removes every passkey of the account (`AccountRecoveryService.ResetAsync`).
- Shared sign-in: `Program.SignInBlockAsync` (unconfirmed / locked / expelled) and `Program.CompleteSignInAsync` (reset
  failed count, last login, `LoginEvents`, cookie, landing) serve both sign-ins, so the rules cannot drift. The
  signature counter is stored after each passkey sign-in.
- Client: `portal/src/passkey.ts` converts WebAuthn JSON natively (`parse*OptionsFromJSON`, `toJSON`) with a small
  base64url fallback for older Safari. The buttons show only where WebAuthn exists (secure context). A closed prompt is
  silent; a removed passkey answers "Este dispositivo não tem uma entrada ativa...".

## Tests

`AccountRecoveryApiTests` (also checks a reset clears passkeys), `PasskeyApiTests`, `IdentityPagesTests` in
`RTUB.Integration.Tests`; `IdentityAndShareSourceTests` in `RTUB.Web.Tests/ReactPortal`;
`PwaManifestTests` (manifest `share_target`).

## Follow-ups

- No rate limit on forgot-password and reset-password: anyone can trigger reset emails to an address, one per request.
  Consider the per-IP login limiter.
- `/confirm-email` has no link generator in the app. Decide: keep it for old links, or drop it.
- Passkeys: autofill (conditional mediation) on the username field; a real-device check (iPhone Face ID, Android
  fingerprint, the Play Store TWA) on DEV.
