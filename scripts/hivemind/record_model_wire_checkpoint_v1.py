"""Expose transport preparation without claiming an installed Web renderer."""
from pathlib import Path
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def main():
    receipt = W / 'model-wire-inplace-encoding-v1/RESULT.v1.json'
    if not receipt.exists():
        return
    result = json.loads(receipt.read_text('utf-8'))
    assert result['tests_passed'] == 3 and result['encode_and_decode_retained_allocation']
    assert not result['canonical_installed'] and not result['web_runtime_qualified']
    block = '''<!-- MODEL-WIRE-CANDIDATE-BEGIN -->
## L01 — Transport : correctif préparé, installation restante

Trois tests réussis sur le candidat isolé du module produit. Le véritable GLB FirePit (36 796 448 octets) conserve son SHA et le même tampon lors de l'encodage et du décodage du paquet binaire ; aucune seconde allocation complète pour l'enveloppe lorsque la lecture réserve l'en-tête.

**Candidat sur F:, non installé** : l'audit d'intégrité PID 38100 épingle la version actuelle du transport. Les 11 entrées de cet audit ont été rehachées sans différence. Le correctif sera appliqué après la clôture de l'audit et vérification de sa préimage ; ne pas invalider le contrôle en cours.

**Suite L01.STUDIO.TRANSPORT_WEB** : réserver l'en-tête lors de la lecture native, qualifier un transport de grands modèles avec limites mémoire explicites, puis raccorder l'aperçu et le placement au renderer source_graph. Les plafonds actuels de 256/512 Mio et le rendu Web restent ouverts.

[Preuve du candidat et du paquet FirePit](../model-wire-inplace-encoding-v1/RESULT.v1.json).
<!-- MODEL-WIRE-CANDIDATE-END -->
'''
    large_receipt = W / 'model-native-large-carrier-v2/RESULT.v1.json'
    if large_receipt.exists():
        large = json.loads(large_receipt.read_text('utf-8'))
        assert large['ordinary_tests_passed'] == 45 and large['actual_rocks_test_passed'] == 1
        assert large['model_bytes'] == 709944920 and large['encode_decode_retained_body_allocation']
        block = '''<!-- MODEL-WIRE-CANDIDATE-BEGIN -->
## L01 — Transport des grands modèles : candidat natif vérifié

**45 tests de bibliothèque réussis**, plus la sonde explicite du véritable GLB Rocks de **709 944 920 octets**. Lecture native avec réserve d'en-tête, paquet de 709 945 176 octets, même tampon conservé pendant l'encodage et le décodage, SHA original inchangé. Le plafond candidat est borné à 1 Gio ; fichiers non réguliers, tailles excessives, SHA divergents et références historiques invalides restent refusés.

**Candidats sur F:, non installés** : préserver la version canonique épinglée par l'audit d'intégrité PID 38100. Les tests natifs sur le Vault réel ne sont pas présentés comme exécutés dans cette sonde ; l'audit dédié continue. Les budgets Web canoniques restent à 256/512 Mio.

**Suite L01.STUDIO.TRANSPORT_WEB** : sonde WASM de mémoire et d'intégrité, puis intégration de l'aperçu et du placement dans le renderer source_graph. Une réussite du transport ne qualifie ni les LOD ni le rendu GPU.

[Réservation d'en-tête et bibliothèque](../model-native-headroom-v1/RESULT.v1.json) · [Véritable paquet Rocks](../model-native-large-carrier-v2/RESULT.v1.json) · [État de la sonde WASM](../model-large-wasm-carrier-v1/RUN-RESULT.v1.json).
<!-- MODEL-WIRE-CANDIDATE-END -->
'''
    for name in ('AVANCEMENT_PAR_LOTS.md', 'LOTS_ACTIFS.md'):
        target = T / name
        text = target.read_text('utf-8-sig')
        text = re.sub(r'<!-- MODEL-WIRE-CANDIDATE-BEGIN -->.*?<!-- MODEL-WIRE-CANDIDATE-END -->\s*', '', text, flags=re.S)
        text = text.rstrip() + '\n\n' + block
        target.write_text(text, 'utf-8')
    state_path = T / 'ETAT.v1.json'
    state = json.loads(state_path.read_text('utf-8'))
    state['latest_model_wire_candidate'] = dict(status=result['status'], tests_passed=3,
                                               receipt=str(receipt), receipt_sha256=hashlib.sha256(receipt.read_bytes()).hexdigest(),
                                               canonical_installed=False, web_runtime_qualified=False)
    state['goal_complete'] = False
    if large_receipt.exists():
        state['latest_large_native_model_transport'] = dict(
            receipt=str(large_receipt), receipt_sha256=hashlib.sha256(large_receipt.read_bytes()).hexdigest(),
            ordinary_tests_passed=45, actual_rocks_test_passed=1, model_bytes=709944920,
            headroom_reader_verified=True, canonical_installed=False, web_runtime_qualified=False)
        state['active_sub_batch'] = 'L01.STUDIO.TRANSPORT_WEB.WASM'
    state_path.write_text(json.dumps(state, indent=2), 'utf-8')
    journal = T / 'JOURNAL.md'
    marker = 'L01.TRANSPORT.TAMPON.01 — candidat vérifié et isolé'
    if marker not in journal.read_text('utf-8-sig'):
        with journal.open('a', encoding='utf-8') as stream:
            stream.write(f'\n\n## {marker}\n\n{result["finished_utc"]} : trois tests et paquet réel FirePit de 36 796 448 octets ; SHA et allocation conservés. Candidat isolé pour préserver les entrées épinglées de l’audit vivant. Non installé, plafonds Web inchangés. [Reçu](../model-wire-inplace-encoding-v1/RESULT.v1.json).\n')
    if large_receipt.exists():
        marker = 'L01.TRANSPORT.GRAND_MODELE.02 — paquet Rocks vérifié'
        if marker not in journal.read_text('utf-8-sig'):
            with journal.open('a', encoding='utf-8') as stream:
                stream.write(f'\n\n## {marker}\n\n{large["finished_utc"]} : 45 tests de bibliothèque et sonde native réelle Rocks réussis. 709 944 920 octets, paquet 709 945 176 octets, SHA et tampon conservés. Réservation d’en-tête avant lecture, plafond candidat 1 Gio. Candidats isolés pour préserver l’audit vivant. Aucun verdict WASM/éditeur/GPU. [Reçu](../model-native-large-carrier-v2/RESULT.v1.json).\n')
    print('Transport candidate checkpoint recorded; Web delivery remains open')
    from record_closed_index_and_transport_v1 import main as record_terminal
    record_terminal()


if __name__ == '__main__':
    main()
