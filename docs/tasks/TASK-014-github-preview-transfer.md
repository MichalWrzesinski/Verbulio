# TASK-014 GitHub preview transfer

Status: DONE

Deliver preview files to the existing VPS exclusively from GitHub Actions. SSH uses the dedicated SFTP account and pinned host key. A transfer validates the selected successful CI run from main, checks the package revision and publishes the checksum after the completed archive. This task adds the transfer path. VPS key provisioning and service activation are separate operational steps.

Verification covers workflow parsing, shell syntax and package selection from a successful integration run. Live SFTP requires provisioning the dedicated server key authorization.
