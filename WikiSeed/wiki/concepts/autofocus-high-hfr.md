---
title: Autofocus produces high or unstable HFR
type: concept
created: 2026-08-15
updated: 2026-08-15
tags: [troubleshooting, autofocus, hfr]
---

# Autofocus produces high or unstable HFR

> Flat or noisy V-curve, HFR that stays high or drifts within the hour: work the
> checklist top-down — temperature first, mechanics last.

## Checklist, in order of likelihood

1. **Temperature drop** — focus shifts with tube temperature. Add an autofocus
   trigger on temperature change (1-2 °C) or HFR increase (5-10%).
2. **Step size wrong** — the V-curve needs clearly out-of-focus points on both
   sides. Flat curve: increase step size. Curve missing its minimum: decrease it.
3. **Exposure too short** — few detected stars make HFR noisy. 4-8 s unfiltered
   is a common range; narrowband needs more.
4. **Backlash** — good focus from one approach direction only: set focuser
   backlash compensation (consistent overshoot direction). See [[backlash]].
5. **Clouds or wind during the run** — a run with collapsing star counts is
   invalid, not a focuser problem.
6. **Mechanical slip** — heavy trains slip on Crayford focusers; check the
   tension screw before blaming software.

## When solved on this rig

Write the working trigger settings and step size into the focuser's entity page
(`wiki/entities/<focuser>.md`) with the date.

## Collegamenti
- See also: [[backlash]], [[guiding-issues]]
