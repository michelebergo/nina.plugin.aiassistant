---
title: Meridian flip settings and failures
type: concept
created: 2026-08-15
updated: 2026-08-15
tags: [troubleshooting, meridian-flip, mount]
---

# Meridian flip settings and failures

> One orchestrator only: NINA drives the flip, the mount driver's own auto-flip
> must be off, and the driver's track-past-meridian limit must cover NINA's
> "minutes after meridian".

## Checklist

1. **Disable the driver's auto-flip** — a driver-initiated flip mid-exposure
   ruins the frame and confuses the sequencer.
2. **Limits must agree** — if the mount stops tracking at the meridian but NINA
   flips 5 minutes later, the target drifts: driver limit >= NINA setting.
3. **"Use side of pier"** on only if the driver reports it reliably; verify the
   report before trusting flip logic to it.
4. **Recenter after flip** ON (platesolve + center) — the mechanical flip alone
   is not accurate.
5. **Guiding** — PHD2 "reverse dec output after flip" must be consistent with
   how calibration is handled.
6. **Cables** — daylight dry-run of a full flip watching cable slack.

## When solved on this rig

Driver limits and NINA flip settings are mount-specific: record them in the
mount's entity page.

## Collegamenti
- See also: [[guiding-issues]]
