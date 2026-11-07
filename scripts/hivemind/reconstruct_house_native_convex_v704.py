"""Use dense training rays and a distinct rotated validation family; retain v702 refusal."""
from pathlib import Path
parent=Path(__file__).with_name('reconstruct_house_native_convex_v702.py')
recipe=parent.read_text('utf-8-sig').replace('v702','v704').replace('state-v701','state-v703')
recipe=recipe.replace("r['family']=='source_triangle_face' or r['sample']%2==0",
                      "r['family'] in ['source_triangle_face','training_fibonacci_direction']")
recipe=recipe.replace("r['family']=='fixed_fibonacci_direction' and r['sample']%2==1",
                      "r['family']=='independent_rotated_fibonacci'")
recipe=recipe.replace('len(holdout)==512','len(holdout)==1024').replace('8704','17408')
exec(compile(recipe,str(parent)+'#distinct-native-validation-family-v704','exec'),globals())
