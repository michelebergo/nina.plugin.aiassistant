---
title: Guiding is poor or loses the star
type: concept
created: 2026-08-15
updated: 2026-08-15
tags: [troubleshooting, guiding, phd2]
---

# Guiding is poor or loses the star

> High RMS, one-axis oscillation, dec spikes or lost stars: separate seeing
> problems (tune parameters) from mechanical ones (fix hardware) before touching
> algorithms.

## Checklist, in order of likelihood

1. **Recalibrate near the target** — calibration far from the current declination
   gives wrong step sizes; recalibrate after large slews or use dec compensation.
2. **One axis oscillates** — aggressiveness too high or min-move too low for the
   seeing: raise min-move first, halve aggressiveness second.
3. **Dec spikes on reversals** — declination backlash: PHD2 backlash compensation
   or uni-directional dec guiding. See [[backlash]].
4. **Smooth RA wave** — worm periodic error: predictive PEC (PPEC).
5. **Star lost** — exposure too short, star too faint/saturated, or clouds;
   multi-star guiding is more robust.
6. **Cable drag / balance** — simultaneous jumps on both axes are mechanical:
   snagging cables or a balance shift crossing the meridian.
7. **Wind** — correlated bursts; no software setting fixes it.

## When solved on this rig

Working calibration step, min-move, aggressiveness and algorithms go in the
mount's entity page with date and seeing conditions.

## Collegamenti
- See also: [[backlash]], [[meridian-flip]]
