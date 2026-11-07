"""Launch a pinned installed Studio on a separate workspace and browser profile."""
from pathlib import Path

recipe_path = Path(__file__).with_name('launch_installed_library_probe_v969.py')
code = recipe_path.read_text('utf-8').replace('installed-library-gui-probe-v969', 'installed-library-gui-probe-v971')
code = code.replace('copies = []', 'copies = []\nexcluded_locks = []')
anchor = "        digest = sha(source)"
assert code.count(anchor) == 1
code = code.replace(anchor, "        if source.name == 'authoring.lock':\n"
                    "            assert source.stat().st_size == 0\n"
                    "            excluded_locks.append(str(source))\n"
                    "            continue\n"+anchor)
code = code.replace("source_workspace=str(source_workspace),", "source_workspace=str(source_workspace), excluded_live_session_locks=excluded_locks,")
anchor = "assert sha(state_path) == state_hash and sha(manifest_path) == manifest_hash and sha(exe) == pin"
assert code.count(anchor) == 1
code = code.replace(anchor, "assert all(sha(c['source']) == sha(c['isolated_copy']) == c['sha256'] for c in copies)\n"+anchor)
code = code.replace("str(Path(__file__).resolve()):sha(Path(__file__))", "str(recipe_path):sha(recipe_path),str(Path(__file__).resolve()):sha(Path(__file__))")
exec(compile(code, str(recipe_path)+'#safe-isolated-installed-library-launch', 'exec'), globals())
