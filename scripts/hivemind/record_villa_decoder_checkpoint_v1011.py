"""Document the measured converter defect and next faithful derivative lot."""
from pathlib import Path
from datetime import datetime, timezone
import json

from compare_villa_builtin_normal_gpu_v1006 import read, sha

W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-decoder-checkpoint-v1011'

def main():
    old_path = W / 'villa-builtin-normal-gpu-comparison-v1009/RESULT.v1.json'
    fixed_path = W / 'villa-builtin-decoder-repair-plan-v1010/RESULT.v1.json'
    old, fixed = read(old_path), read(fixed_path)
    assert old['status'] == 'EXACT_VILLA_DECODER_GPU_PARITY_REFUSED'
    assert fixed['status'] == 'MEASURED_DECODER_REPAIR_CANDIDATE_GPU_PASS'
    assert fixed['mode2_max_abs_errors'] == [0., 0., 0., 0.] and fixed['other_modes_bitwise_preserved']
    assert len(fixed['source_rows']) == 19
    O.mkdir(exist_ok=False)
    text = '''## Conversion Villa : défaut de décodeur mesuré — v1011

Reprise des preuves récentes : 207 scènes Villa avaient déjà été assemblées et admises par le lecteur natif en onze lots le 7 octobre ; ne pas les reconvertir. Leur admission historique ne prouve pas le rendu, le lecteur courant ou la disponibilité dans Studio. Dix-neuf autres scènes restent différées ; elles constituent le prochain lot de réparation fidèle.

Diagnostic GPU v1009 : 19 GLB entièrement relus par SHA (environ 5,2 Go lus par le job), 38 programmes embarqués vérifiés et une paire de fonctions de décodage commune extraite sans modification. Le helper embarqué ignore le mode RG-or-AG. Sur DX12 réel, trois des quatre constantes diffèrent de la référence Unity D3D11, dont la constante blanche ; une simple valeur de mode ne résout pas ce défaut. Pas de GLB modifié ni de nouvelle admission. Les erreurs de l'outil de diagnostic v1006–v1008 restent conservées ; v1009 corrige uniquement son API wgpu et le nom réservé du point d'entrée.

Candidat v1010 : remplacement de la fonction de décodage par celle d'un helper antérieurement mesuré et épinglé ; quatre comparaisons GPU avec Unity passent, écarts nuls. Les huit sorties des autres modes sont identiques octet pour octet. Cela qualifie uniquement ce calcul sur ces constantes ; aucun GLB dérivé n'a encore été créé, aucune texture, géométrie, source, licence, métadonnée ou registre modifié. Le binding effectif du matériau et son rendu complet restent à qualifier.

Prochain lot L04.VILLA.DECODER.19 : lier explicitement le slot de décodeur builtin avec sa provenance de défaut source, construire des dérivés conservant géométrie, textures, métadonnées et anciens octets shader, rouvrir indépendamment, vérifier le lecteur canonique courant, puis dessiner les matériaux et qualifier placement/runtime. Ne pas transplanter les anciens profils GPU : leurs identités de mesh ne correspondent pas aux scènes actuelles.

Images : compteurs inchangés depuis v1005, 426 fiches source sans image, dont 92 Pirate. Aucun nouvel asset entièrement converti ou qualifié runtime dans ce checkpoint. Preuves sous F:/OpenRPG_ImportWork : villa-builtin-normal-gpu-comparison-v1009/RESULT.v1.json ; villa-builtin-decoder-repair-plan-v1010/RESULT.v1.json.

'''
    for name in ('TABLEAU_DE_BORD_LOTS.md', 'SUIVI_LISIBLE.md', 'POINT_DE_REPRISE.md'):
        p = W / 'SUIVI_LOTS_HIVEMIND' / name
        before = p.read_bytes()
        (O / (name + '.before')).write_bytes(before)
        pending = p.with_name(name + '.v1011.pending')
        with pending.open('xb') as stream:
            stream.write(text.encode('utf-8') + before)
        assert p.read_bytes() == before, 'Concurrent tracking edit'
        pending.replace(p)
    result = dict(status='VILLA_DECODER_DEFECT_AND_MEASURED_REPAIR_DOCUMENTED',
                  finished_utc=datetime.now(timezone.utc).isoformat(), affected_scenes=19,
                  candidate_gpu_verified=True, new_fully_converted_assets=0,
                  input_pins={str(old_path): sha(old_path), str(fixed_path): sha(fixed_path)}, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(result))

if __name__ == '__main__':
    main()
