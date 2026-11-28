#!/usr/bin/env python3
"""Small, read-only Unity/Git completeness check. Run from anywhere in checkout."""
from pathlib import Path
import re, subprocess, sys
root=Path(__file__).resolve().parents[1]
paths=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode().split('\0')
files={p for p in paths if p and (root/p).is_file()}
errors=[];guids={}
for path in sorted(files):
    if not path.startswith('Assets/'):continue
    p=root/path
    if path.endswith('.meta'):
        if not Path(str(p)[:-5]).exists():errors.append('orphan meta: '+path)
        match=re.search(r'^guid: ([a-f0-9]{32})$',p.read_text(errors='replace'),re.M)
        if match:
            guid=match[1]
            if guid in guids:errors.append('duplicate GUID: '+path+' / '+guids[guid])
            guids[guid]=path
    elif path+'.meta' not in files:errors.append('missing/excluded meta: '+path)
# Ignore Unity built-in GUIDs; package references are owned by Packages, not Assets.
for path in sorted(files):
    if not path.startswith('Assets/') or not path.endswith(('.unity','.prefab','.asset','.mat')):continue
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})',(root/path).read_text(errors='replace'))):
        if guid.startswith('0000000000000000'):continue
        if guid not in guids:errors.append('unresolved asset GUID: '+path+' -> '+guid)
settings=(root/'ProjectSettings/EditorBuildSettings.asset').read_text()
for path in re.findall(r'^\s+path: (.+)$',settings,re.M):
    if path not in files:errors.append('missing/excluded build scene: '+path)
for folder in ('Assets','Packages','ProjectSettings'):
    if not (root/folder).is_dir():errors.append('missing project folder: '+folder)
print('\n'.join(errors) if errors else f'UNITY_SYNC_OK: {len(files)} source files; {len(guids)} asset GUIDs; build scenes resolved.')
print('Local completeness does not verify the remote; commit/push is required before another computer can receive this version.')
sys.exit(bool(errors))
