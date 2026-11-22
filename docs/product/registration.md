# Local registration

CP-001 starts with the [server-owned placement result](placement.md). The development account endpoint requires the same fixture identity that completed the placement test. The browser sends the opaque test ID, an email address and a password. The starting level comes from the test row.

One PostgreSQL transaction claims an owned, current and completed test and inserts its account. The update locks the test row, which serializes concurrent registration and restart. A unique account reference to the test reinforces the one-account invariant. If the account insert fails, the transaction releases the claim. Accounts store the original email, a case-insensitive lookup key, the ASP.NET Core Identity password hash, the starting level and account timestamps. A new account starts with email confirmation unset.

The development API gives the same registration-conflict response for an unavailable result and a duplicate email. A public endpoint still needs a decision on the registration response and abuse protection, along with real authentication, credential recovery and transactional mail. This local endpoint returns the new account ID and starting level. The fixture identity remains separate from an account login.
