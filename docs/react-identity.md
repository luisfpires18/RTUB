# Password recovery, email confirmation, /Error and /share (task 036)

Task 036 handled the last five routes the app still served as Blazor pages:

| Route | Before | Now |
| --- | --- | --- |
| `/forgot-password` | Blazor (static SSR form) | **React**, public |
| `/reset-password` | Blazor (static SSR form) | **React**, public |
| `/confirm-email` | Blazor (static SSR) | **React**, public |
| `/Error` | Blazor (`Error.razor`, English text) | **Server-owned static page** (`wwwroot/error.html`), on purpose |
| `/share` | Blazor (Web Share Target page) | **Removed** in 036 (404); **React again since 053** (`portal/src/Share.tsx`, manifest `share_target`) |

**No Blazor page is left** (`@page` count: 0). The Blazor host itself (`App.razor`, `MainLayout`,
`MapRazorComponents`, `/_blazor`, `RTUB.Shared`) was retired by task 037. There was no schema change and no migration. Identity options, password rules and token providers are unchanged.

Code:
- `src/RTUB.Application/Services/AccountRecoveryService.cs`
- `src/RTUB.Web/Endpoints/AccountRecoveryEndpoints.cs`
- `portal/src/ForgotPassword.tsx`, `ResetPassword.tsx`, `ConfirmEmail.tsx` and `accountApi.ts`; `Share.tsx` (053)
- `Program.cs` (page routes, `/Error`, `/share`)
- `wwwroot/error.html` and `wwwroot/css/error.css`

## Audit (old pages) → now

### /forgot-password
- **Before:** anonymous. A static SSR `EditForm` with a framework antiforgery token checked the email as required and
  well-formed (DataAnnotations). It then looked up the account by email. An address with no account, or an
  unconfirmed one, got the neutral sentence. A confirmed account got a reset token
  (`GeneratePasswordResetTokenAsync`), and the link
  `{base}/reset-password?code={UrlEncode(token)}&email={UrlEncode(email)}` was rendered with
  `IEmailTemplateRenderer.RenderPasswordResetAsync` and sent with `IEmailSender` ("Recuperação de palavra-passe"). The
  page then showed the same sentence. If the send failed, the exception went to the error page, so a failure could
  only happen for a real account.
- **Now:** `POST /api/account/forgot-password {email}` runs the same checks, token, link (`{scheme}://{host}{pathBase}`),
  template, sender and neutral sentence. **Changed on purpose:** a failed send is logged without the token or link
  and answered with the same neutral sentence, so it no longer reveals that an account exists.

### /reset-password
- **Before:** anonymous. The page read `email` and `code` from the query (decoded once, never twice). With either one
  missing it showed "Link de redefinição de palavra-passe inválido.". The form checked the password as required and
  8–100 long, and the confirmation as required and equal to it. An unknown email showed "Redefinição de palavra-passe
  concluída." as a success, so it revealed nothing. Otherwise the page called `ResetPasswordAsync(user, code,
  password)` and on failure showed Identity's error descriptions joined by `; `. On success it showed a message and a
  login link. It never signed the user in.
- **Now:** `POST /api/account/reset-password {email, code, password, confirmPassword}` applies the same rules, messages
  and Identity call. **Changed on purpose:** an invalid or expired token now reads "Este link de redefinição é inválido
  ou já expirou. Pede um novo link." instead of Identity's English "Invalid token.". Other Identity errors still show
  as before.

### /confirm-email
- **Before:** anonymous. The page confirmed on opening (SSR GET) with `userId` and `code` from the query. With either
  missing it said "Link de confirmação inválido."; with an unknown id, "Utilizador não encontrado.". Otherwise it
  called `ConfirmEmailAsync(user, WebUtility.UrlDecode(code))`, a second decode on top of the query's, and answered
  "Obrigado por confirmar o seu email." or "Erro ao confirmar o seu email.". Nobody was signed in. **Nothing in the
  app generates confirmation links today** (no `GenerateEmailConfirmationTokenAsync` caller), so only old links reach
  it.
- **Now:** the React page posts the link's values once to `POST /api/account/confirm-email {userId, code}`. The server
  applies the same second decode, the same messages and the same Identity call. Nobody is signed in.

### /Error
- **Before:** `Error.razor` was the target of `UseExceptionHandler("/Error")` (every non-Development environment). It
  showed a fixed English sentence with no details, inside the Blazor layout.
- **Now:** `app.Map("/Error", …)` serves `wwwroot/error.html`. It is fixed Portuguese text with a "Voltar ao início"
  link, no script, no request data and no stack trace, sent with `Cache-Control: no-store`. **Why not React:** the
  exception handler re-executes the failed request with its own method (POST, PUT, DELETE, …), and an error page must
  render even when the portal bundle, the session or the database is the thing that failed. A fixed static page
  answers every method and depends on none of them. The handler's 500 status is kept; a direct visit answers 200.

### /share
- **Before:** `Share.razor` was the Web Share Target (`manifest.webmanifest` → `share_target`, GET `/share?title&text&url`).
  It showed the shared values read-only with a "Copiar" button. Nothing linked to it; only the installed app received
  shares there.
- **036:** removed with no replacement. `share_target` left the manifest, and `/share` was an explicit 404 for GET,
  HEAD and POST. The service worker had no share handling, so nothing changed there.
- **Now (053, owner's decision while filling the PWABuilder report card):** the installed app is a share target again,
  with the pre-036 contract: `share_target` → `action: "/share"`, `method: "GET"`, params `title`, `text`, `url`. The
  React `portal/src/Share.tsx` reads the three values from the query (kept in the address bar, so a reload keeps the
  content), shows them read-only (Título, Texto, URL when present), "Copiar" writes `title\n\ntext\n\nurl` to the
  clipboard (a refused clipboard is swallowed, as before), "Voltar" goes home; with nothing shared it says "Nenhum
  conteúdo para partilhar." with a link home. Nothing is stored, logged or sent anywhere. Public like the Blazor page
  (no `[Authorize]`), served as the ordinary React shell (`Program.cs` public route list, GET/HEAD only, so POST → 405).
  The service worker still has no share handling (a GET target needs none). Other install, offline, shortcut and push
  behaviour is untouched; the other manifest members added in 053 are in `docs/pwa-practices.md` → *PWABuilder report
  card (053)*.

## API (`/api/account`, public)

| Method | Path | Body | Answers |
| --- | --- | --- | --- |
| POST | `/forgot-password` | `{email}` | 200 `{succeeded, message}` (always the neutral sentence); 400 on a bad email |
| POST | `/reset-password` | `{email, code, password, confirmPassword}` | 200 `{succeeded, message}`; 400 with `code`, `email`, `password`, `confirmPassword` or `reset` |
| POST | `/confirm-email` | `{userId, code}` | 200 `{succeeded, message}` |

Every call needs `X-CSRF-TOKEN`, from the token at `/api/public/antiforgery-token`, as on every other React form, and
every answer is `no-store`. `GET /api/account/me` is unchanged and lives in `AccountController`.

## Identity tokens: where they go

- They come only from the emailed link. The pages `/forgot-password`, `/reset-password` and `/confirm-email` are served
  with `Cache-Control: no-store` and `Referrer-Policy: no-referrer`.
- The page reads the query once (`takeLinkQuery`) and immediately calls `history.replaceState` to drop it from the
  address bar and history. The values live in a module variable, never in React state, `localStorage` or
  `sessionStorage`. The reset page forgets them once the reset succeeds, and a reload asks for a new link.
- The token travels in the POST body only, never in a URL the app builds. No answer echoes it, and nothing logs it: the
  only log line is a failed send, which names the account, not the link.
- No flow signs anyone in, and token generation and validation are Identity's own, unchanged.

## Passkeys: Face ID / fingerprint sign-in (task 057)

A member signs in once with the password, turns it on in `/profile` → *Face ID / impressão digital* ("Ativar neste
dispositivo"), and from then on `/login` → "Entrar com Face ID / impressão digital" signs in with the device's unlock
(Face ID, Touch ID, Android fingerprint, Windows Hello). The password keeps working; nothing about it changes.

- **Implementation:** ASP.NET Core Identity's own WebAuthn (no extra package). `IdentityOptions.Stores.SchemaVersion =
  Version3` adds the table `AspNetUserPasskeys` (migration `AddUserPasskeys`, additive; the N-1 app ignores it, so a
  rollback is a plain redeploy). Identity reads that option from the app's service provider when it builds the model:
  a context made without it (unit tests' InMemory contexts) has no passkey table, and `MigrationChainTests` builds its
  context with the host's `AddIdentityServices` for that reason. Version3 also configures `PhoneNumber` at 256;
  `ApplicationUserConfiguration` restates the entity's 80. Discoverable credentials: no username is asked.
- **Host binding:** a passkey belongs to the host it was made on (RP ID = request host, origin checked against the
  browser's `Origin` header). DEV and PROD keep separate passkeys; a passkey made on DEV does nothing on PROD. Needs
  HTTPS (or `localhost`).
- **API** (`src/RTUB.Web/Endpoints/PasskeyEndpoints.cs`; every POST/DELETE needs `X-CSRF-TOKEN`, every answer no-store):

  | Route | Who | What |
  | --- | --- | --- |
  | `POST /auth/passkey/options` | anyone | WebAuthn request options (challenge kept in Identity's cookie) |
  | `POST /auth/passkey/login` `{ credential, rememberMe, returnUrl }` | anyone, login rate limit | verify → same checks (unconfirmed, locked → `Locked`, expelled → `Expelled`) and the same tail as `/auth/login` (`LastLoginDate`, `LoginEvents`, cookie); `{ redirect }` or 401 `{ error }` |
  | `GET /api/me/passkeys` | member | own passkeys `[{ id (base64url), name, createdAt }]` |
  | `POST /api/me/passkeys/options` `{ currentPassword }` | member | the current password (wrong ones count towards the lockout), then creation options; 409 at 10 passkeys |
  | `POST /api/me/passkeys` `{ credential }` | member | verify + store, named from the User-Agent ("iPhone · Safari") |
  | `DELETE /api/me/passkeys/{id}` | member | own passkey only; anyone else's id is 404 |

- **Security rules:** enrolment needs the current password, so an open session alone (a borrowed phone, a stolen
  cookie) cannot plant a passkey; the challenge issued after that check is what the store call verifies against. A
  password reset through `/reset-password` removes every passkey of the account (`AccountRecoveryService.ResetAsync`),
  so taking an account back also evicts passkeys made before.
- **Shared sign-in:** `Program.SignInBlockAsync` (unconfirmed / locked / expelled) and `Program.CompleteSignInAsync`
  (reset failed count, last login, `LoginEvents`, cookie, landing) serve both sign-ins, so the rules cannot drift.
  The signature counter is stored after each passkey sign-in.
- **Client:** `portal/src/passkey.ts` converts WebAuthn JSON natively (`parse*OptionsFromJSON`, `toJSON`) with a
  small base64url fallback for older Safari. The buttons show only where WebAuthn exists (secure context). A closed
  prompt is silent; a removed passkey answers "Este dispositivo não tem uma entrada ativa…".
- **Tests:** `RTUB.Integration.Tests/Api/PasskeyApiTests.cs`: visitors manage nothing and need the token; members see
  and remove only their own; a software ES256 "device" signs the server's real challenge and signs in without a
  password (counter stored), and the same passkey is refused with `Expelled` once the member is expelled; enrolment
  without the right current password is a `currentPassword` error. `AccountRecoveryApiTests` checks a reset clears the
  account's passkeys.
- **Not done:** autofill (conditional mediation) on the username field; a real-device check (iPhone Face ID,
  Android fingerprint, the Play Store TWA) on DEV.

## Tests

- `RTUB.Integration.Tests/Api/AccountRecoveryApiTests.cs`, through the real host and real tokens with a recording
  email sender:
  - forgot-password: validation; an identical answer for a confirmed address, an unconfirmed one and an unknown one;
    an email only to the confirmed account; no token in the answer; a failed send stays neutral.
  - reset-password: following the emailed link changes the password, signs nobody in and burns the link; validation of
    the missing code, short password, mismatch, bad token and unknown address.
  - confirm-email: a valid token (with the second decode) confirms the address and signs nobody in; missing, unknown
    and bad links are handled.
  - All three require antiforgery.
  - `/Error` is the fixed page for GET, POST and PUT with no details; `/share` is the public React shell for GET (with
    or without a query, never echoed) and 405 for POST; the manifest's `share_target` is GET `/share`; the service worker
    has no share handling (053).
- `RTUB.Web.Tests/ReactPortal/IdentityAndShareSourceTests.cs`: no `@page` is left; route ownership (`/share` in the
  public React list, the 036 explicit 404 gone); POST only, with antiforgery and no-store; no storage, no logs, the query
  cleared (the share page keeps its query and sends nothing); the share target contract; no move wording; no removed
  module.
- `RTUB.Integration.Tests/Pages/PwaManifestTests.Manifest_DeclaresTheInstallExtras_Task053`: the 053 manifest members,
  `related_applications` against `assetlinks.json`, the skipped members absent.
- Updated:
  - `IdentityPagesTests` and `ReactLoginTests`: the pages are React, no-store and no-referrer.
  - `PortalRouteTests`.
  - `AuthAntiforgeryTests`: the logout token and the session check use what the React shell uses.
  - `Retired*RoutesTests` and `MessagesRollbackSafetyTests`: the signed-in React page and `/api/account/me` replace
    `/share`.

## Deferred (recorded, not fixed)

- (resolved in 037) The Blazor host is retired (`BlazorHostRetirementTests`); the system announcement is a React
  listener (`react-portal-pilot.md` → *System announcement*). `/Error` stays the static page; `/share` is React since 053.
- No rate limit on forgot-password and reset-password (as before): anyone can trigger reset emails to an address, one
  per request. Consider the per-IP login limiter.
- `/confirm-email` has no link generator in the app. Decide: keep it for old links, or drop it in the host cleanup.
