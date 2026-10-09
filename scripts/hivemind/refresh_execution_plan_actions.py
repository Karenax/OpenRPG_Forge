"""Refresh stale next actions without changing the full collection or completion criteria."""
import datetime
import hashlib
import json
import os
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
ROOT = W / 'execution-plan-current-actions-v1'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    assert not ROOT.exists(), 'Existing plan refresh preserved'
    json_path, md_path = T / 'DECOUPAGE_OPERATIONNEL.v1.json', T / 'DECOUPAGE_OPERATIONNEL.md'
    original_json, original_md = json_path.read_bytes(), md_path.read_bytes()
    plan = json.loads(original_json.decode('utf-8-sig'))
    followup = (T / 'PROCHAINS_SOUS_LOTS.md').read_bytes()
    state_bytes = (T / 'ETAT.v1.json').read_bytes()
    state = json.loads(state_bytes.decode('utf-8-sig'))['sequential_batch_followup_oct07']
    assert plan['pack_rows'] == len(plan['packs']) == 48 and not plan['complete']
    assert state['current_studio_wasm_check']['status'] == 'actual_studio_wasm_frontend_compile_check_passed'
    assert state['villa_batch_tracking_reconciliation']['historical_derivative_ids_reconciled'] == 207
    actions = {
        'L00': 'Poursuivre le contrôle complet du Vault sur F ; conserver les sources, licences et reçus. État courant dans PROCHAINS_SOUS_LOTS.md.',
        'L01': 'Contrôle WASM actuel réussi ; terminer le frontend puis le candidat natif en file. Raccordement aperçu/placement, rendu et recette restent requis.',
        'L02': 'Attendre les résultats des 42 captures HDRP et leur réouverture indépendante ; terminer les matériaux et le rendu avant livraison.',
        'L03': 'Suivre les sous-lots actuels dans PROCHAINS_SOUS_LOTS.md ; les géométries Townsmith déjà terminées ne doivent pas être reconverties. Résoudre les six bindings de dessin puis les matériaux et le rendu.',
        'L04': 'Onze sous-lots physiques Villa / 207 IDs réconciliés ; rendu restant. Traiter séparément les 19 scènes différées, les captures de matériaux en file et le rendu des textures. Villa URP : six captures et dix valeurs par défaut en file.',
        'L05': 'Medieval : dérivé riggé et contrôles géométriques déjà conservés ; terminer les matériaux, la déformation GPU et la recette, puis les autres rigs et clips.',
        'L06': 'Un sous-lot Blood puis un sous-lot Dust ; emission, trajectoires, durée, transparence et comparaison temporelle propres à chaque effet restent requis.',
        'L07': 'Sprites puis terrains ; terminer dépendances, conversion et usage du produit pour chaque type. Les intermédiaires restent non qualifiés.',
        'L08': 'Terminer audit et candidat Studio ; traiter les 948 images historiques. Les 71 fiches sans PNG sont réparties en six sous-lots dans PROCHAINS_SOUS_LOTS.md. Vérifier recherche, filtres, aperçu, placement, édition et réouverture.',
        'L09': 'Réconcilier tous les IDs des 48 packs avec les livraisons et refus ; fermer seulement après les preuves complètes du Vault, de Studio et du runtime.'}
    previous = []
    for lot in plan['lots']:
        previous.append(dict(id=lot['id'], previous_next=lot['next']))
        lot['next'] = actions[lot['id']]
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    plan['actions_refreshed_utc'] = now
    plan['current_sub_batches'] = str(T / 'PROCHAINS_SOUS_LOTS.md')
    plan['action_refresh_receipt'] = str(ROOT / 'RESULT.v1.json')
    text = original_md.decode('utf-8-sig')
    start = text.index('| Lot | Travail |')
    end = text.index('## Découpage d’une livraison', start)
    table = '| Lot | Travail | Critère de fin | Action suivante |\n|---|---|---|---|\n'
    table += '\n'.join(f"| {lot['id']} | {lot['scope']} | {lot['completion']} | {lot['next']} |" for lot in plan['lots'])
    text = text[:start] + table + '\n\n' + text[end:]
    text += f'\n\nActions actualisées le {now}. Les états détaillés et reçus sont dans [PROCHAINS_SOUS_LOTS.md](PROCHAINS_SOUS_LOTS.md). Cette actualisation ne qualifie aucun nouvel asset.\n'
    assert json_path.read_bytes() == original_json and md_path.read_bytes() == original_md
    assert (T / 'ETAT.v1.json').read_bytes() == state_bytes and (T / 'PROCHAINS_SOUS_LOTS.md').read_bytes() == followup
    ROOT.mkdir()
    (ROOT / 'PREVIOUS-DECOUPAGE_OPERATIONNEL.v1.json').write_bytes(original_json)
    (ROOT / 'PREVIOUS-DECOUPAGE_OPERATIONNEL.md').write_bytes(original_md)
    (ROOT / 'FOLLOWUP-SNAPSHOT.md').write_bytes(followup)
    result = dict(status='execution_plan_next_actions_refreshed_without_scope_reduction', pack_rows=48,
                  original_plan_sha256=sha(original_json), original_markdown_sha256=sha(original_md),
                  followup_snapshot_sha256=sha(followup), previous_actions=previous,
                  new_assets_validated=0, full_import=False, recorded_utc=now)
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), 'utf-8')
    for path, data in [(json_path, json.dumps(plan, indent=2, ensure_ascii=False).encode('utf-8')),
                       (md_path, text.encode('utf-8'))]:
        temporary = path.with_suffix(path.suffix + '.refresh.tmp')
        temporary.write_bytes(data)
        os.replace(temporary, path)
    print(json.dumps(dict(status=result['status'], lots=10, packs=48, new_assets_validated=0)))


if __name__ == '__main__':
    main()
