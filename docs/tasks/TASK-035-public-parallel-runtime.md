# TASK-035 Parallel public runtimes

Status: DONE

Allow the public application to run on either loopback port 5444 or 5445 via `PublicSite__Port`. The existing deployment keeps its default on 5444. Expose the packaged revision on the health endpoint so an operator can verify which instance answered. Existing HTTPS forwarding, host and account API restrictions apply to both instances.

Exercise the two instances concurrently in CI and confirm each reports its own revision. This increment does not change Nginx routing or activate a release.
