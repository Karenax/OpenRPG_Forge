"""Expand measured coverage by framing the selected original submesh only.

All original geometry and material slots remain intact; only the diagnostic
camera framing changes. Preserve the v8 success and its one-pixel limitation.
"""
from pathlib import Path

HERE = Path(__file__).resolve().parent

def main():
    original = HERE / 'queue_source_null_mask_surface_v8.py'
    text = original.read_text('utf-8-sig')
    text = text.replace('unity-source-null-mask-surface-v8', 'unity-source-null-mask-surface-v9')
    text = text.replace('SurfaceV8', 'SurfaceV9')
    namespace = dict(__file__=str(Path(__file__).resolve()), __name__='configured_probe')
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), namespace)
    inherited = namespace['surface_helper']
    once = namespace['once']
    def focused(text):
        text = inherited(text)
        text = once(text, 'public int mesh_submeshes,material_slots;',
            'public int mesh_submeshes,material_slots,targeted_submesh_indices; public Vector3 targeted_submesh_world_center;public float targeted_submesh_world_radius;')
        text = once(text, 'var center=renderer.bounds.center;float radius=Math.Max(renderer.bounds.extents.magnitude,.01f);',
            '''if(mesh.subMeshCount!=originalSlots.Length)throw new Exception("Native slot/submesh mapping is not one-to-one");
  var selectedIndices=mesh.GetIndices(request.slot,true);var sourceVertices=mesh.vertices;
  if(selectedIndices.Length==0)throw new Exception("Selected original submesh is empty");
  var selectedBounds=new Bounds(fixture.transform.TransformPoint(sourceVertices[selectedIndices[0]]),Vector3.zero);
  foreach(int index in selectedIndices)selectedBounds.Encapsulate(fixture.transform.TransformPoint(sourceVertices[index]));
  var center=selectedBounds.center;float radius=Math.Max(selectedBounds.extents.magnitude,.01f);''')
        text = once(text, 'var report=new Report{debug_material_settings_restored=true,',
            'var report=new Report{targeted_submesh_indices=selectedIndices.Length,targeted_submesh_world_center=center,targeted_submesh_world_radius=radius,debug_material_settings_restored=true,')
        return text
    namespace['surface_helper'] = focused
    namespace['main']()

if __name__ == '__main__':
    main()
