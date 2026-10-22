# Admin security review against Next.js guidance

Reviewed 2026-10-18. Installed Next.js: 16.2.10. Read its bundled authentication,
data-security, self-hosting, CSP and production-checklist guides; checked the
current official pages below as well. The public docs currently show a later
16.x version, so this is a review of shared security principles, not a claim
that every latest-version feature is available in the installed version.

## What the official guides recommend

- Check authorization at the server/data boundary for every sensitive read or
  mutation. Layouts, client redirects and Next Proxy are insufficient as the
  sole boundary. Use a maintained authentication implementation. Source:
  [Authentication](https://nextjs.org/docs/app/guides/authentication).
- Existing external REST backends remain a supported architecture. Treat calls
  as untrusted, enforce access at the backend, and return the minimum necessary
  data. Moving Identity or PostgreSQL into Next.js is not necessary here.
  Source: [Data security](https://nextjs.org/docs/app/guides/data-security).
- Protect session cookies with HttpOnly, Secure for HTTPS, an intentional
  SameSite policy, expiry and scope. Source:
  [Session cookies](https://nextjs.org/docs/app/guides/authentication#session-management).
- For self-hosting, put a reverse proxy in front of the Next server to handle
  hostile/oversized requests, connection limits and rate limiting. Source:
  [Self-hosting](https://nextjs.org/docs/app/guides/self-hosting#reverse-proxy).
- Keep secrets out of public environment variables and add a tested CSP.
  A nonce-based CSP requires per-request dynamic rendering; a static nonce
  would not provide that protection. Sources:
  [Production checklist](https://nextjs.org/docs/app/guides/production-checklist#security),
  [CSP](https://nextjs.org/docs/app/guides/content-security-policy).

## Verified project state

- ASP.NET Identity owns password verification/lockout and backend role checks.
  Dedicated admin sign-in refuses normal participant credentials. Admin setup
  accepts only trusted configured emails, assigns only Admin, and requires email
  verification. Public participant signup cannot create the selected admin
  identity. Existing selected accounts can still receive Admin at startup.
- Admin reads require server-side Admin authorization; only bounded summary
  DTOs are returned. Reads and admin sign-in are audited. The React route guard
  controls presentation and is not the authorization boundary.
- Refresh cookies are HttpOnly, SameSite=Lax, scoped to `/api/auth`, expiring and
  rotated; Secure is set outside Development or on HTTPS. Access tokens remain
  in browser memory. Development HTTP is not evidence of production TLS.
- Local direct responses for `/admin/login` and `/dashboard/admin` currently
  lack CSP, X-Frame-Options, X-Content-Type-Options and HSTS headers. External
  proxy headers have not been configured or verified.
- The local Next listener is `*:3000`, not loopback-only. This demonstrates
  all-interface binding, not proven Internet reachability; firewall/router
  behavior was not tested. The local API listens on loopback.
- No admin MFA enrollment/challenge implementation was found in the auth
  controllers/services or admin UI. Email confirmation is not MFA.
- No reverse-proxy/VPN deployment configuration was found in the repository.
  This review did not deploy a service or alter firewall, ports or cookie policy.

## Proposal for local administration of an online service

This is a project-specific recommendation inferred from the guidance, not a
Next.js requirement or a feature already deployed:

1. Keep the public customer application separate from the management surface.
   Make admin pages, admin authentication/setup and admin/reviewer data routes
   accessible through a private network or an identity-aware access gateway.
   Keep server role checks after that network gate. Cover both `/api/admin/*`
   and `/api/review/*`, as well as future admin file/chat endpoints.
2. Add MFA for administrators using the established Identity provider or an
   approved access gateway, with enrollment/recovery and revocation tests.
   Preserve the existing human-wallet signing boundary.
3. Use a production build behind HTTPS and a reverse proxy. Restrict origin
   access so the proxy cannot be bypassed. For a purely local development UI,
   bind the dev server to loopback explicitly.
4. Deploy and test CSP and related headers against Next hydration, styles,
   wallet integration and API calls. Keep personalized responses out of shared
   caches. Review cookie/CORS behavior for the chosen admin/API origins rather
   than disabling protections to make cross-origin access work.

Running only the admin UI on a laptop does not restrict a remotely exposed
admin API. A private network gate plus backend authorization addresses that
boundary. An online service still needs a reachable backend; a local panel
cannot manage it completely offline. The production host/origins and private
access mechanism have not been selected. Public deployment remains deferred.

## Acceptance before claiming this architecture is enabled

Verify that external clients cannot reach management endpoints even by knowing
their URLs; an authorized private connection still requires an Admin session;
participant credentials are refused; MFA and session expiry/revocation behave
correctly; private responses are not cached publicly; and the public application
continues to work. None of the proposed network/MFA/CSP work is marked complete
by this documentation review.
