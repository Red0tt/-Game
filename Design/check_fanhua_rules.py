"""Check the two-cell geometry and the experiment's approach routes."""
from pathlib import Path
import re
import unittest

ANCHOR = (-2, -1)
SCENE = (Path(__file__).resolve().parents[1] / "Assets/Scenes/FanhuaLab.unity").read_text()
GROUND = {
    (int(x), int(z)) for x, z in re.findall(
        r"m_Name: Cell (-?\d+), (-?\d+)\n(?:(?!^--- ).)*?m_IsActive: 1", SCENE, re.M | re.S
    )
}


def footprint(end):
    ax, az = ANCHOR
    x, z = end
    return {(col, row)
            for col in range(min(ax, x), max(ax + 1, x) + 1)
            for row in range(min(az, z), max(az + 1, z) + 1)}


class DesignChecks(unittest.TestCase):
    def test_folded_footprint(self):
        self.assertEqual(footprint(ANCHOR), {(-2, -1), (-1, -1), (-2, 0), (-1, 0)})

    def test_first_step_adds_two_cells_in_every_direction(self):
        for end, added in {
            (0, -1): {(0, -1), (0, 0)},
            (-3, -1): {(-3, -1), (-3, 0)},
            (-2, 1): {(-2, 1), (-1, 1)},
            (-2, -2): {(-2, -2), (-1, -2)},
        }.items():
            self.assertEqual(footprint(end) - footprint(ANCHOR), added)

    def test_lab_has_walking_support_for_all_four_pulls(self):
        for approach, destination in (
            ((0, -1), (1, -1)), ((-3, 0), (-4, 0)),
            ((-2, 1), (-2, 2)), ((-2, -2), (-2, -3)),
        ):
            self.assertLessEqual({approach, destination}, GROUND)


if __name__ == "__main__":
    unittest.main(verbosity=2)
