"""Seal repeated measurements and shader dependencies, without asserting a fix."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-vertex-diagnostic-review-v926';S=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    run_path=W/'mountain-native-vertex-diagnostic-v925/RUN-RESULT.v1.json';run=read(run_path)
    assert run['status']=='native_speedtree_vertices_and_material_properties_measured' and run['unity_exit_code']==0 and not run['input_changes']
    prior=W/'mountain-native-vertex-diagnostic-v924/NATIVE-RESULT.v1.json';current=run_path.parent/'NATIVE-RESULT.v1.json'
    assert sha(current)==run['native_result_sha256'] and read(prior)==read(current),'Diagnostic repeat differs'
    pins={str(p):sha(p) for p in [run_path,prior,current]}
    for p,d in run['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    observations=[]
    for card in read(current)['cards']:
        assert card['authored_objects_unchanged']
        for renderer in card['renderers']:
            for slot in renderer['slots']:
                if 'SpeedTree' not in slot['material']['shader']:continue
                values={p['name']:p for p in slot['material']['properties']}
                assert slot['color_count']==0 and values['_ExtraTex']['value']=='null'
                observations.append(dict(id=card['id'],mesh_guid=renderer['mesh_guid'],mesh_file_id=renderer['mesh_file_id'],mesh_path=renderer['mesh_path'],submesh=slot['submesh'],vertices=slot['unique_vertices'],vertex_color_buffer_absent=True,material=slot['material']['path'],extra_texture_bound=False,extra_texture_feature_value=values['EFFECT_EXTRA_TEX']['value']))
    assert len(observations)==3
    shader=W/'mountain-hdrp-capture-stage-v869/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material/Nature/SpeedTree8.shadergraph'
    decoder=json.JSONDecoder();text=shader.read_text('utf-8-sig');objects=[]
    while text.strip():text=text.lstrip();obj,end=decoder.raw_decode(text);objects.append(obj);text=text[end:]
    by_id={o.get('m_ObjectId'):o for o in objects};graph=objects[0]
    target=next(o for o in objects if o.get('m_SerializedDescriptor')=='SurfaceDescription.Occlusion')
    seen=set();todo=[target['m_ObjectId']];upstream=[]
    while todo:
        node=todo.pop()
        if node in seen:continue
        seen.add(node);obj=by_id[node];upstream.append(dict(id=node,type=obj['m_Type'],name=obj.get('m_Name')))
        for edge in graph['m_Edges']:
            if edge['m_InputSlot']['m_Node']['m_Id']==node:todo.append(edge['m_OutputSlot']['m_Node']['m_Id'])
    assert any(n['type'].endswith('.VertexColorNode') for n in upstream);pins[str(shader)]=sha(shader)
    O.mkdir(exist_ok=False)
    result=dict(status='three_held_tree_native_measurements_repeated_identically_clean_exit',finished_utc=datetime.now(timezone.utc).isoformat(),native_exit_code=0,repeated_report_equal=True,prior_shutdown_crash_preserved=True,observations=observations,occlusion_upstream_nodes=upstream,input_pins=pins,root_cause_qualified=False,repair_applied=False,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    p=S/'POINT_DE_REPRISE.md';old=p.read_bytes();(O/'before-POINT_DE_REPRISE.md').write_bytes(old)
    note='''# Diagnostic arbres Mountain — v926

Les trois arbres reserves : mesures natives repetees a l'identique avec sortie Unity 0 (v925). Le premier run v924 a produit le meme rapport mais crashe a la fermeture ; echec conserve. Aucun fichier surveille ni objet auteur modifie.

Les trois troncs SpeedTree8 : aucun buffer de couleurs de sommets, _ExtraTex non lie. Le graphe d'occlusion du shader SDK actuel depend d'un VertexColorNode. Le toggle EFFECT_EXTRA_TEX differe : 1 pour Master01, 0 pour Branch04. Ceci constitue un diagnostic mesure, pas une cause prouvee ni une autorisation de reconstruire les couleurs/textures. Ne pas blanchir les sommets ni remapper MRTA automatiquement. Prochaine action : verifier les attributs FBX originaux et mesurer le chemin shader/default effectivement responsable, avec temoins sur des clones uniquement ; garder les trois images en reserve.

44 fiches Mountain / 88 vues deja livrees v922. Manquantes : 518. Ne pas refaire les lots valides. Aucun nouvel asset full-import.

---

'''
    p.write_bytes(note.encode('utf-8')+old)
    for name in ['TABLEAU_DE_BORD_LOTS.md','SUIVI_LISIBLE.md']:
        p=S/name;old=p.read_bytes();(O/('before-'+name)).write_bytes(old);lines=old.decode('utf-8-sig').splitlines()
        for i,line in enumerate(lines):
            if line.startswith('| Images : Mountain |'):
                lines[i]='| Images : Mountain | 44 fiches / 88 vues livrees v922. Les trois arbres reserves ont des mesures natives repetees identiques, sortie Unity 0, sources intactes v926. | Absence de couleurs de sommets et ExtraTex non lie mesures ; graphe SDK depend de VertexColor. Verifier attributs FBX et chemin shader responsable avant toute reparation. |';break
        else:raise AssertionError('Missing Mountain row')
        p.write_text('\n'.join(lines)+'\n','utf-8')
    print(json.dumps(dict(status=result['status'],held_cards=3,root_cause_qualified=False)))
if __name__=='__main__':main()
