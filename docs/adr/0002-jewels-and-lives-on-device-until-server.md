# Jewels and lives stored on the phone until a server exists

The spec (section 11) says jewel balances and life regeneration must live on a server, because jewels can be bought and wagered. For the first releases we deliberately keep them on the phone behind a single wallet interface, with life regeneration on the phone's clock, and ship QR payment only as a placeholder that credits nothing. This lets stage mode and Bluetooth battles ship before any server or account work; the wallet interface is the seam where a server-backed implementation replaces the local one.

## Consequences

- Balances can be edited and lives refilled by changing the phone clock; acceptable only while no real money is involved.
- Real-money QR payments must not go live until the server-backed wallet replaces the local one.
- Uninstalling the app loses progress and jewels.
