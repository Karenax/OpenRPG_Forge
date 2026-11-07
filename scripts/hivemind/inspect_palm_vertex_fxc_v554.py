"""Inspect controlled FXC lowering of the complete preserved vertex function."""
from pathlib import Path
parent=Path(__file__).with_name('inspect_palm_fxc_assembly_v551.py')
code=parent.read_text('utf-8').replace('v551','v554').replace('pirate-palm-wind-native-reference-v547','pirate-palm-wind-native-reference-v541')
exec(compile(code,str(parent)+'#complete-vertex','exec'),globals())
