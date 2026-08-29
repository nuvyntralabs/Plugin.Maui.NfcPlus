# Changelog

## 1.0.0

- Session-based NFC for .NET MAUI on Android and iOS
- `NfcPlus.StartAsync` / `NfcPlus.ReadAsync` / `NfcPlus.WriteAsync` with typed NDEF text, URI, and MIME
- Tag ID, technologies, writable / capacity, and `MakeReadOnlyAsync`
- Availability detection (`Available`, `Disabled`, `Restricted`, `Unsupported`)
- Android reader mode (default) and optional foreground dispatch
- iOS `NFCNdefReaderSession` system sheet
- Sample app (retail, attendance, asset, vehicle) and unit tests
