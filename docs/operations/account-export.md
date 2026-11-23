# Account data export

This command creates a readable JSON copy for an operator handling a verified account request. First verify the requester's identity with the approved account-support procedure and establish the internal account UUID. A claimed email address or a UUID supplied by the requester is insufficient verification. Run the command only against the authorized environment using the operator's database connection in `ConnectionStrings__Learning`. Never place credentials on the command line.

From the directory containing the published API, use an absolute path in a private directory on Linux or macOS. The destination filesystem must support hard links.

```sh
dotnet Verbulio.Api.dll --export-account <account-uuid> /private/operator-directory/verbulio-export.json
```

The versioned JSON contains registered contact and account preferences, linked placement answers, word progress, practice sessions and credited days. The file is created with mode 0600 or stricter and an existing destination is preserved. The command prints status only. Exit codes are 0 for success, 1 for an execution failure, 2 for invalid usage and 3 for an unknown account.

Use an approved private delivery channel after checking the recipient. The current preview verification uses synthetic accounts. Retention and deletion of generated files and the final operator identity-verification and delivery procedure need approval before public handling. See the [sensitive data inventory](../architecture.md#sensitive-data).
