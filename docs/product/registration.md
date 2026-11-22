# Local registration

CP-001 starts with the [server-owned placement result](placement.md). The development account endpoint requires the same fixture identity that completed the placement test. The browser sends the opaque test ID, an email address and a password. The starting level comes from the test row.

One PostgreSQL transaction claims an owned, current and completed test and inserts its account. The update locks the test row, which serializes concurrent registration and restart. A unique account reference to the test reinforces the one-account invariant. If the account insert fails, the transaction releases the claim. Accounts store the original email, a case-insensitive lookup key, the ASP.NET Core Identity password hash, the starting level and account timestamps. A new account starts with email confirmation unset. The database ignores legacy restart writes on claimed tests, so the preceding API version can run after migration and return its usual unavailable response.

The development API gives the same registration-conflict response for an unavailable result and a duplicate email. A public endpoint still needs a decision on the registration response and abuse protection, along with real authentication, credential recovery and transactional mail. This local endpoint returns the new account ID and starting level. The fixture identity remains separate from an account login.

The local outcome read returns the account ID and saved level only to the same fixture guest. It supports browser refresh and checking an uncertain registration response. An unclaimed test and a foreign test use the same not-found response.

## Local account sign-in

The account cookie and guest cookie use distinct ASP.NET Core schemes. Login checks the Identity password hash against the normalized address. Unknown addresses also incur password verification. Both cases return the same invalid-credentials message for a failed login. The development API limits attempts by address and source IP.

The account cookie carries a protected session identifier. PostgreSQL holds its owner, eight-hour expiry and last activity. The account scheme resolves the owner and refreshes the 30-minute idle deadline on authenticated requests. Idle activity cannot extend the absolute expiry. A successful login consumes the current browser session once and logout removes it. A conflicting login leaves the cookie unchanged. The local reset action clears a stale cookie after the browser checks the session and leaves a valid cookie intact. A browser copy of a revoked identifier cannot authenticate. The local account route requires HTTPS on loopback and uses a Secure cookie. Account learning, real delivery, password recovery and deployment hardening are separate increments.
