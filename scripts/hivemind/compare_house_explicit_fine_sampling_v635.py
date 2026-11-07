"""Test the alternative fine derivative choice against the same native source."""
from pathlib import Path
base_recipe=Path(__file__).with_name('compare_house_explicit_coarse_sampling_v634.py')
text=base_recipe.read_text('utf-8-sig').replace('house-explicit-coarse-sampling-comparison-v634','house-explicit-fine-sampling-comparison-v635').replace('openrpg-house-coarse-sampling-v634','openrpg-house-fine-sampling-v635').replace('dpdxCoarse','dpdxFine').replace('dpdyCoarse','dpdyFine').replace('coarse_candidate_matches_fixture','fine_candidate_matches_fixture').replace('explicit coarse derivatives','explicit fine derivatives').replace('for p in [Path(__file__),recipe,tp,mp,','for p in [Path(__file__),base_recipe,recipe,tp,mp,')
exec(compile(text,str(base_recipe)+'#fine-derivative-control','exec'),globals())
