---
title: Plate solving fails or is slow
type: concept
created: 2026-08-15
updated: 2026-08-15
tags: [troubleshooting, platesolving]
---

# Plate solving fails or is slow

> Repeated failed solves are almost always a wrong scale hint (focal length),
> too few stars, or missing index files — in that order.

## Checklist, in order of likelihood

1. **Wrong focal length or pixel size** — the solver's scale hint comes from the
   profile; a forgotten reducer/barlow is the most common cause. Verify Options >
   Equipment against the actual optical train.
2. **Exposure too short / too few stars** — 3-6 s unfiltered to start; more under
   narrowband or moonlight.
3. **Search radius too small** — after a big slew or cold start the first solve
   may be far off; 30° is a safe initial radius.
4. **Missing index files** — ASTAP: D50 for most setups, D80 wide field, H18/H17
   long focal lengths.
5. **Focus far off** — bloated stars defeat detection; rough-focus first.
6. **Clouds** — look at the image before the settings.

## When solved on this rig

Note the working exposure/binning/solver in the site or camera entity page.

## Collegamenti
- See also: [[autofocus-high-hfr]]
