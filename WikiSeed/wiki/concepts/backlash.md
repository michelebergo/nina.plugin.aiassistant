---
title: Backlash (dead travel)
type: concept
created: 2026-08-15
updated: 2026-08-15
tags: [mechanics, backlash, focuser, guiding]
---

# Backlash (dead travel)

> Free play crossed on every direction change: commanded motion is consumed
> re-engaging gears before anything moves. Measure it per direction; and if the
> measured value is huge or inconsistent, suspect flex, not gears.

## Where it bites

- **Focuser**: alternating approach directions land on two different positions.
  Fix: always approach from the same direction (overshoot and return) or set
  driver backlash compensation. See [[autofocus-high-hfr]].
- **Declination guiding**: reversals eat small corrections → dec spikes or a
  dead zone. Fix: PHD2 compensation, uni-directional dec guiding, or mechanics.
  See [[guiding-issues]].
- **Motorized adjusters** (polar-alignment rigs, rotators): each reversal falls
  short by the play; on gravity-loaded axes the two directions can legitimately
  differ several-fold — measure both.

## Measuring it honestly

Move well past the play in one direction, mark, reverse by a known amount,
measure what actually moved: the shortfall is the backlash for THAT transition.
Repeat both ways; asymmetry is real, not an error.

## What compensation cannot fix

Elastic wind-up (flex, belt stretch, plastic parts) measures like backlash but
does not behave like dead travel: compensation tuned for real play overshoots on
an elastic drivetrain. Huge, inconsistent "backlash" readings mean flex.

## Collegamenti
- See also: [[autofocus-high-hfr]], [[guiding-issues]]
