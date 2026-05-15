# Privacy Policy

DispHub does not include custom telemetry, analytics beacons, or user account tracking.

## Data handling

- DispHub stores app settings and profiles locally on your device.
- DispHub does not transmit display profiles or personal data to the developer.
- If you install through Microsoft Store, Microsoft may collect standard platform analytics and crash diagnostics according to the Microsoft Privacy Statement.

## Third-party services

- DispHub does not call third-party marketing or tracking services.
- Optional distribution channels (GitHub Releases, Microsoft Store) may process download and reliability data under their own policies.

## Security

- DispHub does not require creating an online account.
- Sensitive operations (for example package signing) are handled in repository secrets and local owner workflows, not in public source.

## Network capabilities

- The packaged app manifest currently declares the `internetClient` capability for compatibility with packaged desktop app behavior.
- The current codebase does not contain outbound API/telemetry client calls.
- If future releases remove all networking needs, maintainers should reassess whether `internetClient` can be dropped.

## Changes

This policy may be updated as release and distribution models evolve. Changes are tracked in this repository history.
