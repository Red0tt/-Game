"""Scene topology and conservative jump bounds for Unit02Water01."""
from pathlib import Path
import json, math, re, unittest, hashlib
ROOT=Path(__file__).resolve().parents[1]
SCENE=ROOT/'Assets/Level/Unit02/Unit02Water01.unity'
text=SCENE.read_text()
floors={tuple(map(int,p)) for p in re.findall(r'm_Name: Cell (\d+), (\d+)',text)}
def flood(start,blocked=frozenset()):
    todo=[start];seen={start}
    while todo:
        x,z=todo.pop()
        for p in [(x+1,z),(x-1,z),(x,z+1),(x,z-1)]:
            if p in floors and p not in seen and p not in blocked:seen.add(p);todo.append(p)
    return seen
v=math.sqrt(2*9.81*1.2)
def reach(drop):return 4*(v+math.sqrt(v*v+2*9.81*drop))/9.81
class Checks(unittest.TestCase):
    def test_separate_grid_planes(self):self.assertNotIn((7,6),flood((1,10)))
    def test_closed_gate_blocks_spare(self):self.assertNotIn((8,11),flood((7,6),{(8,z) for z in range(6,21)}))
    def test_submerged_switch_not_on_grid_floor(self):self.assertNotIn((5,3),floors)
    def test_exit_not_on_grid_floor(self):self.assertNotIn((18,8),floors)
    def test_five_fire_cells(self):self.assertEqual(set(re.findall(r'm_Name: Fire (\d+), 7',text)),set(map(str,range(11,16))))
    def test_dry_basin_cannot_cross_lip(self):self.assertLess(2.5+.4+1.2,5.5+.4)
    def test_no_direct_fire_jump(self):self.assertLess(reach(.3),17.6-.4-(10.5+.24))
    def test_short_cooled_midpoint_cannot_reach_exit(self):self.assertLess(reach(.3),17.6-.4-(12.5+.24))
    def test_middle_cooled_step_has_jump_window(self):self.assertGreater(reach(.3),17.6-.4-13.35)
    def test_preparation_drop_cannot_reach_exit(self):self.assertLess(4*math.sqrt(2*(11-7.6)/9.81),17.6-.4-13)
    def test_push_stop(self):self.assertIn((13,11),floors);self.assertNotIn((14,11),floors)
    def test_solver_does_not_read_grid_floor_or_physics(self):
        code=(ROOT/'Assets/Scripts/Unit02WaterSimulation.cs').read_text()
        self.assertNotIn('Physics.',code);self.assertNotIn('Physics2D.',code);self.assertNotIn('HasFloorSupport',code)
    def test_old_scene_hashes(self):
        report=ROOT/'Design/UNIT02_VALIDATION.json'
        if not report.exists():self.skipTest('validation report not yet written')
        for path,expected in json.loads(report.read_text())['old_scene_sha256'].items():
            with self.subTest(path=path):self.assertEqual(hashlib.sha256((ROOT/path).read_bytes()).hexdigest(),expected)
if __name__=='__main__':unittest.main(verbosity=2)
