# TASK-041 Interactive public staging

Status: DONE

Deploy the available account and learning features at the public staging URL. Use the existing HTTPS ingress, trusted forwarded headers, a separate app identity and persistent PostgreSQL storage. Initialize the schema and logical pre-deployment backup before replacement. The deletion journal uses its own database and must be preserved when restoring the application database. The private preview remains separate.

This is staging with synthetic test accounts. Placement, registration, login, learning, categories, profile, settings, and password-confirmed account deletion are available. Email delivery and requesting reset links require remaining integration work. The operator export remains CLI-only.
