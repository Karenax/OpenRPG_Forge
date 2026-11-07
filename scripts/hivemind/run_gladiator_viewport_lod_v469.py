"""Include the actual bootstrap dependency omitted by the first native bench."""
from pathlib import Path
parent=Path(__file__).with_name('run_gladiator_viewport_lod_v466.py')
code=parent.read_text('utf-8').replace('viewport-lod-cycle-v466','viewport-lod-cycle-v469').replace('viewport-lod-v466','viewport-lod-v469')
assert code.count("'context','lights'")==1
code=code.replace("'context','lights'","'context','bootstrap','lights'")
code=code.replace("files=[Path(__file__),O/'src/main.rs'", "files=[Path(__file__),parent,O/'src/main.rs'")
exec(compile(code,str(parent)+'#bootstrap-dependency-v469','exec'),globals())
