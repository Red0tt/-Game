"""Compatibility entry point; the water prototype is now Unit 02."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('check_unit02.py')),run_name='__main__')
