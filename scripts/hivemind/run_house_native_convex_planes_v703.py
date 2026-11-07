"""Expand native acquisition and retain a separately rotated validation set."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_native_convex_planes_v701.py')
recipe=parent.read_text('utf-8-sig').replace('v701','v703').replace('V701','V703')
recipe=recipe.replace('house-native-component-state-v700/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v701/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==49","len(audit['helpers'])==50")
recipe=recipe.replace('for(int s=0;s<1024;s++)','for(int s=0;s<16384;s++)').replace('/1024.0','/16384.0')
recipe=recipe.replace('"fixed_fibonacci_direction"','"training_fibonacci_direction"')
needle='return rows.ToArray();'
assert recipe.count(needle)==1
extra=r'''
 var rotation=Quaternion.Euler(17,31,53);
 for(int s=0;s<1024;s++){
  double y=1.0-2.0*(s+0.25)/1024.0;double r=Math.Sqrt(Math.Max(0,1-y*y));double angle=(s+0.375)*Math.PI*(3-Math.Sqrt(5));
  var normal=rotation*new Vector3((float)(r*Math.Cos(angle)),(float)y,(float)(r*Math.Sin(angle))).normalized;
  probe("independent_rotated_fibonacci",s,center,normal.normalized);
 }
'''
recipe=recipe.replace(needle,extra+needle)
exec(compile(recipe,str(parent)+'#dense-acquisition-and-independent-validation-v703','exec'),globals())
