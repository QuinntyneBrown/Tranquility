# Security Policy

## Supported versions

Tranquility is under active development. Security fixes are applied to the
latest commit on the `main` branch. Older commits, forks, custom builds, and
deployments are not supported by the maintainers.

## Reporting a vulnerability

Do not disclose a suspected vulnerability in a public issue, discussion, pull
request, packet capture, or social channel.

Use [GitHub private vulnerability reporting](https://github.com/QuinntyneBrown/Tranquility/security/advisories/new)
and include:

- A clear description of the vulnerability and potential impact
- The affected endpoint, protocol path, parser, dependency, or configuration
- Reproduction steps or a minimal synthetic proof of concept
- Affected versions or commit identifiers
- Any suggested mitigation, if known

Do not include real mission data, spacecraft identifiers, credentials, signing
keys, connection strings, private interface documents, or unredacted logs.

Maintainers will acknowledge reports as soon as practical, investigate them,
and coordinate remediation and disclosure with the reporter. Response times
are best effort because this is a volunteer-maintained project.

## Security considerations

Tranquility has not completed an independent production, mission-safety,
security, or privacy assessment.

- A missing JWT signing key causes an ephemeral key to be generated at startup;
  configure a stable secret through a secure deployment provider.
- A bare local launch has no seeded users. Test users, passwords, and
  certificates under `tests` are synthetic fixtures and must never be reused.
- Production deployments must configure TLS, identity, authorization, secret
  storage, data directories, audit retention, backups, and network boundaries.
- Protocol parsers process untrusted binary input and should remain covered by
  deterministic, malformed-input, and resource-boundary tests.
- Commanding, link control, lifecycle mutation, file transfer, and IAM paths
  require special review because mistakes can affect operational systems.

Deployers are responsible for validating Tranquility against their mission
assurance, regulatory, safety, and operational requirements.
