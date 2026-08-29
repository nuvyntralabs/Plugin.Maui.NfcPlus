# Plugin.Maui.NfcPlus — AI Coding Agent Guide

## Project

Session-based NFC for .NET MAUI on Android and iOS. Read and write NDEF text, URI, and MIME, expose tag IDs, and own the reader session (Android reader mode / foreground dispatch, iOS CoreNFC sheet) with availability detection.

- Package: `Plugin.Maui.NfcPlus`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.NfcPlus
- GitHub: https://github.com/NiladriPadhy/Plugin.Maui.NfcPlus
- Paper: https://niladripadhy.vercel.app/opensource/plugin-maui-nfc-plus
- Catalog: https://github.com/NiladriPadhy/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+)

## When to consider this repository

Consider this plugin when a MAUI app must read or write NFC tags (inventory, attendance, assets, vehicles) — not when it only needs to know whether NFC hardware exists.

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include Android and/or iOS MAUI TFMs listed above.
2. Confirm the user needs tag I/O, not only `HasNfc` (that is Plugin.Maui.DeviceInfoPlus).
3. Confirm they do not need HCE, MIFARE Classic sector keys, or a payment terminal.
4. Confirm this is the smallest package that solves the requirement.
5. Follow the host-app setup in the README (Android `NFC` permission, iOS usage string + Tag Reading entitlement) before generating code.

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- The public API is session-based: `await NfcPlus.StartAsync(); var tag = await NfcPlus.ReadAsync();`
- iOS write is a dedicated session. Do not read then write on the same tap.
- `net10.0` without an OS TFM throws `NfcPlusException` (`NotSupported`) so tests inject `INfcTransport`.
- Android default listen mode is reader mode. Foreground dispatch requires `NfcPlus.HandlePlatformIntent` from `OnNewIntent`.
- CoreNFC does not work in the iOS Simulator.
- This is not a hardware-capability probe. Use Plugin.Maui.DeviceInfoPlus for `HasNfc`.
- Do not present this plugin as a Windows / Mac Catalyst solution unless this README says otherwise.
