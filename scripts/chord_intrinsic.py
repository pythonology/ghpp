"""
Enumerate every chord permutation across the 5 frets (G, R, Y, B, O) and
compute its intrinsic difficulty.

The cost of a chord is the sum of four physical contributions:

  fret_cost   per held fret, priced by its role in the chord shape:
              FRET_LONE if the fret below it isn't held,
              FRET_RUN  if it extends a consecutive run.

  gap_cost    per interior unheld fret, weighted by position. The center
              fret (Y) is hardest to skip because it requires the most
              hand opening; G and O are endpoints and cannot be gaps.

  barre_cost  flat addition when all 5 frets are held, since only 4
              fingers are physically available and the chord must be
              played with a barre or the thumb.

  outer_pair  flat reduction when only the bottom and top frets are held
              with at least one gap between them. Such "outer-pair" chords
              are pure stretch — no interior fingers are doing coordination
              work, only the hand opening matters.
"""

import csv
from pathlib import Path

FRET_NAMES = ["G", "R", "Y", "B", "O"]  # bits 0..4

FRET_LONE = 1.0   # held fret with no held neighbor immediately below
FRET_RUN = 0.5    # held fret extending a consecutive run

# Per-position interior gap costs. G (0) and O (4) are always endpoints
# when a gap exists, so their entries are unused (kept 0.0 for clarity).
GAP_COST = [0.0, 0.5, 1.0, 0.5, 0.0]

BARRE_COST = 1.0    # 5-fret chord buff
OUTER_PAIR_REDUCTION = 0.5  # only the bottom + top fret held with a gap between: pure stretch, no interior coordination


def chord_label(mask: int) -> str:
    if mask == 0:
        return "(open)"
    return "".join(name for i, name in enumerate(FRET_NAMES) if mask & (1 << i))


def held_positions(mask: int) -> list[int]:
    return [i for i in range(len(FRET_NAMES)) if mask & (1 << i)]


def fret_cost(mask: int, held: list[int]) -> float:
    return sum(
        FRET_RUN if (i > 0 and (mask & (1 << (i - 1)))) else FRET_LONE
        for i in held
    )


def gap_cost(mask: int, held: list[int]) -> float:
    bottom, top = held[0], held[-1]
    return sum(
        GAP_COST[i]
        for i in range(bottom + 1, top)
        if not (mask & (1 << i))
    )


def chord_intrinsic(mask: int) -> tuple[int, int, float, float, float]:
    held = held_positions(mask)
    if not held:
        return 0, 0, 0.0, 0.0, 0.0

    fingers = len(held)
    span = held[-1] - held[0] + 1
    frets = fret_cost(mask, held)
    gaps = gap_cost(mask, held)

    cost = frets + gaps
    if fingers == 5:
        cost += BARRE_COST
    if fingers == 2 and gaps > 0:
        cost -= OUTER_PAIR_REDUCTION
    return fingers, span, frets, gaps, cost


def main() -> None:
    out_path = Path(__file__).parent / "chord_intrinsic.csv"
    rows = []
    for mask in range(1 << len(FRET_NAMES)):
        fingers, span, frets, gaps, cost = chord_intrinsic(mask)
        rows.append({
            "mask": mask,
            "chord": chord_label(mask),
            "fingers": fingers,
            "span": span,
            "fret_cost": round(frets, 4),
            "gap_cost": round(gaps, 4),
            "intrinsic": round(cost, 4),
        })

    rows.sort(key=lambda r: (r["intrinsic"], r["fingers"], r["mask"]))

    with out_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)

    print(f"Wrote {len(rows)} rows to {out_path}")


if __name__ == "__main__":
    main()
