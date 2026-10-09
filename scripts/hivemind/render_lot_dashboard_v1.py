"""Small delivery-oriented dashboard, derived from the existing live tracker."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'

def render():
    source = T / 'SUIVI_SIMPLE.md'
    state_path = T / 'SUIVI_SIMPLE.v1.json'
    state = json.loads(state_path.read_text(encoding='utf-8'))
    text = source.read_text(encoding='utf-8')
    table = text.split('## Découpage principal', 1)[1].split('## Ordre des prochaines tranches', 1)[0].strip()
    if sum(line.startswith('| L0') for line in table.splitlines()) != 10:
        raise RuntimeError('Refused: expected ten principal lots')
    audit_path = W / 'firepit-receiver-cascade-selection-audit-v1/RESULT.v1.json'
    audit = json.loads(audit_path.read_text(encoding='utf-8')) if audit_path.exists() else None
    controls_path = W / 'firepit-receiver-shadow-controls-reopen-v1/RESULT.v1.json'
    controls = json.loads(controls_path.read_text(encoding='utf-8')) if controls_path.exists() else None
    point_path = W / 'firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json'
    point = json.loads(point_path.read_text(encoding='utf-8')) if point_path.exists() else None
    bias_path = W / 'firepit-point-world-bias-control-reopen-v1/RESULT.v1.json'
    bias = json.loads(bias_path.read_text(encoding='utf-8')) if bias_path.exists() else None
    raster_path = W / 'firepit-source-point-raster-reopen-v1/RESULT.v1.json'
    raster = json.loads(raster_path.read_text(encoding='utf-8')) if raster_path.exists() else None
    atlas_path = W / 'source-punctual-atlas-contract-v1/RESULT.v1.json'
    atlas = json.loads(atlas_path.read_text(encoding='utf-8')) if atlas_path.exists() else None
    caster_path = W / 'firepit-punctual-d16-caster-reopen-v1/RESULT.v1.json'
    caster = json.loads(caster_path.read_text(encoding='utf-8')) if caster_path.exists() else None
    mapping_path = W / 'source-punctual-atlas-mapped-reopen-v1/RESULT.v1.json'
    mapping = json.loads(mapping_path.read_text(encoding='utf-8')) if mapping_path.exists() else None
    mapping_close_path = W / 'source-punctual-atlas-mapped-install-v1/RESULT.v1.json'
    mapping_close = json.loads(mapping_close_path.read_text(encoding='utf-8')) if mapping_close_path.exists() else None
    depth_isolation_path = W / 'firepit-punctual-depth-isolation-reopen-v1/RESULT.v1.json'
    depth_isolation = json.loads(depth_isolation_path.read_text(encoding='utf-8')) if depth_isolation_path.exists() else None
    projection_clip_path = W / 'firepit-punctual-projection-clipping-reopen-v1/RESULT.v1.json'
    projection_clip = json.loads(projection_clip_path.read_text(encoding='utf-8')) if projection_clip_path.exists() else None
    now = datetime.now(timezone.utc).isoformat()
    output = f'''# Hivemind — lots, livrables et avancement

Actualisé : {now}. État de référence : {state['updated_utc']}.

**Tranche active : {state['active_sub_lot']}.** {state.get('current_work_state', state['state'])}.

Fichiers définitifs : `F:\\OpenRPG_AssetVault`. Conversions, caches, reçus et essais : `F:\\OpenRPG_ImportWork`.

## Lots principaux

{table}

## Critère de fin d’une livraison

Pour chaque ID logique : sources et licence conservées → conversion adaptée au type → dérivé réouvert et contrôlé → comportement et rendu vérifiés → image et métadonnées indexées → recherche, aperçu, placement ou usage, sauvegarde/réouverture et runtime vérifiés.

Une géométrie, une compilation ou un test réussi ne compte pas comme un asset livré. Un échec reste attaché à son ID et à sa preuve. Le nombre total de fichiers ou de fiches ne constitue pas le total des objets livrés.

## Tranches de travail

| Tranche | Livrable attendu | Critère de fermeture |
|---|---|---|
| L01 — rendu commun | Ombres, sampling, éclairage et LOD raccordés au moteur | Contrôles ciblés + comparaison GPU dans les seuils ; installation Studio vérifiée |
| L03.HOUSE — assemblage | 48 géométries existantes assemblées avec matériaux, par groupes de 16 | Chaque prefab complet réouvert ; identité, slots et rendu vérifiés |
| L03.TOWNSMITH — bindings | Qualification des 37 bindings / 8 meshes dans les huit prefabs | Sources exactes et aucune substitution silencieuse |
| L03.VILLA — scènes | 19 scènes restantes, par groupes de 5 au maximum | Hiérarchie, dépendances, transforms et rendu de chaque scène vérifiés |
| L02/L04 — matériaux | Dungeon, feuillage et autres groupes de shaders | Alpha, normales, ombres, déformations et LOD qualifiés sur les IDs concernés |
| L05 — animation | Rigs et clips par groupe de squelette compatible | Poids, poses, clips et déformation runtime vérifiés |
| L06 — effets | FirePit (2 effets), puis Blood et Dust par groupes de graphes | Simulation dans le temps, rendu et répétabilité comparés aux références |
| L07 — autres formats | Sprites, terrains, audio et dépendances | Recette propre au type ; formats impossibles conservés avec refus explicite |
| L08 — bibliothèque | Images et fiches recherchables ; tranches de 8 fiches pour la revue des images | Image lisible, filtres et métadonnées cohérents ; usage réel dans l’éditeur vérifié |
| L09 — rapprochement | Chaque ID des 48 packs/variantes classé livré ou refusé | Aucune absence masquée et preuves finales liées à chaque ID |

Cet ordre indique les priorités après le rendu commun ; une tranche indépendante peut avancer si elle ne dépend pas du blocage actif. Les lots déjà validés sont réutilisés.

## Dernière avancée mesurée

Orientation des ombres : 10 tests techniques, transport GPU et compilation Web vérifiés ; image sans ombre inchangée. La comparaison visuelle reste refusée : IoU des ombres 0,392872 pour un seuil de 0,95. **Aucun asset complet supplémentaire n’est déclaré livré par ces contrôles.**

'''
    if audit:
        output += '''Sélection des cascades : audit terminé. Unity emploie des sphères ; le récepteur courant utilise encore la profondeur de vue. Cette différence de code est prouvée ; son rôle dans l’écart de pixels reste à mesurer. [Reçu de l’audit](../firepit-receiver-cascade-selection-audit-v1/RESULT.v1.json).

'''
    if controls:
        output += '''**Dernière tranche terminée : deux contrôles du récepteur, deux vues GPU chacun, 956 épingles réouvertes par capture.** Géométrie, prépasses et quatre buffers de profondeur conservés. La sélection sphérique et la formule des bordures donnent une IoU de 0,389967 : fidélité toujours refusée. La suppression du rejet de profondeur donne deux images strictement identiques au contrôle précédent ; cette piste est écartée pour ce cas. Le premier essai avec 22 750 pixels non finis par vue reste archivé comme refus. [Preuve indépendante](../firepit-receiver-shadow-controls-reopen-v1/RESULT.v1.json).

La formule de bordure est contrôlée en replay privé ; le dither temporel source et l’installation générale restent non qualifiés. Aucun asset complet supplémentaire.

'''
    if point:
        output += '''**L01 — données ponctuelles GPU vérifiées : 2 records, 12 vues, 576 scalaires de matrices comparés bit à bit.** Couleur et portée concordent avec la source ; cinq faces ont des bins de casters dans la vue avec ombres, aucune dans la vue sans ombres. Ce sont des bins de phases, pas des compteurs de dessins. Deux images identiques après instrumentation. Projection, résolution et biais ne sont pas encore équivalents à la source. [Preuve](../firepit-point-shadow-diagnostics-reopen-v1/RESULT.v1.json).

'''
    if bias:
        output += '''**Dernier contrôle ponctuel terminé : suppression privée du déplacement natif de 8 cm.** IoU des ombres **0,389967 → 0,423939**, seuil conservé à **0,95**. 1 016 épingles vérifiées ; records ponctuels, matrices des faces et profondeurs directionnelles identiques. Ce contrôle améliore la couverture mais ne qualifie pas le rendu ; projection et filtre ponctuels restent à reproduire. Aucun asset complet supplémentaire. [Contrôle réouvert](../firepit-point-world-bias-control-reopen-v1/RESULT.v1.json).

'''
    if raster:
        output += '''**Dernier lot source ponctuel vérifié : atlas D16_UNorm de 4 096 × 4 096, 16 777 216 profondeurs actives.** Six requêtes raster, pente effective 2,5 et clipping activé ; 576 valeurs de matrices finies réouvertes. 24 dépendances, images PNG et buffers HDR originaux inchangés. La vue inactive lie une texture de repli 1 × 1 ; les pixels conservés dans l’allocation précédente ne représentent pas une nouvelle ombre. Les deux refus initiaux D32 sont conservés. [Preuve source](../firepit-source-point-raster-reopen-v1/RESULT.v1.json).

Le chemin ponctuel natif utilise encore D32 et un cube de 1 024 pixels par face. Le prochain lot doit produire réellement l’atlas D16 et son échantillonnage ; recopier les pixels Unity dans le rendu Forge ne serait pas une conversion autonome. Aucune nouvelle qualification de rendu ni livraison complète.

'''
    if atlas:
        output += '''### Détail du lot actif

| Sous-lot | État | Fin attendue |
|---|---|---|
| L01.D16.1 — allocation et effacement | Technique vérifié | 5 tests, compilation native, profondeurs GPU réouvertes |
| L01.D16.2 — couverture et géométries source | En cours ; bord refusé | Couverture expliquée et ombres des meshes existants comparées |
| L01.D16.3 — récepteur ponctuel PCF5 | À faire | Projection, biais et filtre source raccordés ; comparaison dans les seuils |
| L01.D16.4 — usage Studio et Web | À faire | Compilation Web et emploi réel dans le parcours éditeur vérifiés |

**Nouveau sous-lot L01 — infrastructure D16 commune : 5 tests et compilation native vérifiés.** Deux rendus de contrôle réouverts : **33 554 432 profondeurs** ; intérieur D16 et effacement de l'atlas inactif conformes. Les six viewports source gardent leurs valeurs exactes. Le contrôle strict de couverture reste **refusé sur 4 096 pixels de bord** ; son échec initial est conservé. [Preuve indépendante](../source-punctual-atlas-contract-v1/RESULT.v1.json).

Il s'agit de primitives numériques de contrôle GPU, pas de nouveaux modèles 3D. La qualification du rendu des géométries achetées, le récepteur PCF5, la compilation Web et la vérification Studio restent à réaliser. Aucun asset complet supplémentaire.

'''
    if caster:
        output += '''**Dernier sous-lot mesuré : géométrie FirePit originale rendue dans l'atlas D16 commun.** 3 381 sommets, 2 819 triangles et six faces ponctuelles. L'IoU des supports de profondeur est **0,713568** ; écart maximal de profondeur **0,359106**. Ce replay de positions et indices ne qualifie pas le shader source complet ni le rendu produit. [Preuve indépendante](../firepit-punctual-d16-caster-reopen-v1/RESULT.v1.json).

**Le défaut de bord est confirmé face à Unity/D3D11 : 4 096 pixels différents**, colonne x=2 730. L'ajout d'un fragment shader ne le résout pas. Les deux atlas inactifs sont entièrement effacés ; le premier contrôle Unity avec convention de clear incorrecte est conservé et exclu de la comparaison. Le prochain sous-lot traite couverture, conventions Y et winding. Aucun asset complet supplémentaire.

'''
    if mapping:
        output = output.replace('En cours ; bord refusé','Couverture numérique corrigée ; profondeurs mesh refusées')
        output += '''**Dernière correction commune : viewports fractionnaires conservés dans le shader.** Les six régions Unity/D3D11 sont reproduites **bit à bit sur 33 554 432 profondeurs**, états actif et inactif. Le défaut de colonne historique est résolu dans ce chemin ; le chemin matériel précédent reste inchangé. 5 tests et compilation native moteur vérifiés. [Preuve de la révision](../source-punctual-atlas-mapped-reopen-v1/RESULT.v1.json).

**FirePit : couverture des profondeurs 0,713568 → 0,998328 avec Y conservé et faces antihoraires, puis 0,999321 avec mapping fractionnaire.** Il reste 1 823 pixels de support différents et 1 630 835 profondeurs communes au-delà du seuil déclaré de deux pas D16. Fidélité de profondeur, shader source complet et récepteur PCF5 restent refusés ou non qualifiés. Ces conventions sont mesurées pour les positions/indices source bruts ; aucune orientation GLB générale n'est déduite du nom de l'asset.

Les contrôles précédents restent liés à leur révision sauvegardée. La compilation Web actuelle et l'usage Studio doivent être vérifiés séparément ; aucun asset complet supplémentaire.

'''
    if mapping_close:
        output += '''**Sous-lot couverture fractionnaire — clôture technique native et Web.** 5 tests, 33 554 432 profondeurs numériques bit-exactes ; moteur et Studio compilent aussi pour WASM, avec 1 054 épingles source inchangées. [Clôture technique](../source-punctual-atlas-mapped-install-v1/RESULT.v1.json).

Le rendu WebGPU et l'usage réel Studio restent non vérifiés. Le prochain sous-lot porte sur les profondeurs asset, la passe shader source et le récepteur PCF5. Fidélité de profondeur toujours refusée ; aucun asset complet supplémentaire.

'''
    if depth_isolation:
        output += '''**Dernier sous-lot profondeur : passe simple Unity et passe HDRP identiques sur 16 777 216 profondeurs.** La géométrie LOD0 et ses six matrices donnent exactement la référence source ; aucun traitement shader manquant n'explique l'écart de ce cas. La qualification reste limitée à cet état de référence. [Réouverture indépendante](../firepit-punctual-depth-isolation-reopen-v1/RESULT.v1.json).

Le contrôle d'invariance vertex ne change aucun pixel. Sans biais raster dans les deux backends, l'écart Forge persiste : IoU des supports 0,999321, profondeur maximale en écart 0,303258. Le biais n'est donc pas une explication suffisante. Une rampe de profondeur conserve les supports et diffère au maximum de deux pas entiers D16 ; 819 pixels dépassent néanmoins le seuil flottant strict, conservé tel quel.

Le contrôle de clipping géométrique s'arrête explicitement : feature GPU `CLIP_DISTANCES` indisponible sur l'adapter D3D12 testé (code 101), aucun rendu ni fallback silencieux. Le prochain travail contrôle les positions clip GPU et prépare un clipping sans cette feature optionnelle. Aucun asset complet supplémentaire ; fidélité de profondeur toujours refusée.

'''
    if projection_clip:
        output += '''**Dernier contrôle de projection : 81 144 scalaires GPU Unity et Forge identiques bit à bit**, sur 3 381 sommets et six faces. Il s'agit de la même expression exécutée en compute ; aucun feedback direct de l'étage vertex n'est revendiqué. La nouvelle capture Unity conserve exactement la profondeur HDRP de référence. [Preuve indépendante](../firepit-punctual-projection-clipping-reopen-v1/RESULT.v1.json).

**Découpe des triangles existants aux limites des faces : essai exécuté sans feature GPU optionnelle.** Cinq faces non vides dessinées, une face vide correctement sautée, atlas inactif effacé. L'échec initial de buffer vide est conservé. Les coordonnées utilisées pour rendre sont calculées par Forge ; aucun pixel ni buffer de coordonnées Unity n'est injecté dans le rendu.

La découpe ne résout pas le refus : IoU des supports 0,999321, erreur maximale de profondeur 0,299062 et 1 630 762 pixels communs hors seuil. Elle reste un diagnostic privé et n'est pas installée comme correction moteur. Prochain sous-lot : transformation de viewport et récepteur PCF5 pour la comparaison du rendu final, avec refus de profondeur conservé. Aucun asset complet supplémentaire.

'''
    tent5_path=W/'source-shadow-tent5-source-reopen-v1/RESULT.v1.json'
    if tent5_path.exists():
        output += '**L01.PCF5.1 termine : reference du filtre ponctuel capturee sur GPU Unity et reouverte.** 25 phases, neuf taps, deux captures : 450 observations poids/UV, identiques entre les vues. Sources et dependances conservees. Le calcul Forge, le raccordement de l atlas et le rendu final restent a verifier ; refus de profondeur conserve, zero asset complet ajoute. [Preuve](../source-shadow-tent5-source-reopen-v1/RESULT.v1.json). [Plan court par lots](PLAN_EXECUTION_LOTS_20261009.md).\n\n'
    tent5_math_path=W/'source-shadow-tent5-gpu-reopen-v1/RESULT.v1.json'
    tent5_atlas_path=W/'source-shadow-tent5-atlas-reopen-v1/RESULT.v1.json'
    tent5_audit_path=W/'source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json'
    output += '**L01.PCF5.2 verifie : poids et UV calcules sur GPU Forge identiques a Unity**, 25 phases, neuf taps, 450 comparaisons des deux captures source. Zero ecart, images HDR originales inchangees. [Preuve](../source-shadow-tent5-gpu-reopen-v1/RESULT.v1.json).\n\n'
    output += '**L01.PCF5.3 execute sur les atlas autonomes : 1 458 controles actifs et 1 458 inactifs.** Etat inactif exact ; etat actif : un cas refuse, ecart maximal 0,001898 pour le seuil fixe de 0,0000002. Il concerne la face 4 et le texel (410,2864) : profondeur Unity 0,222278, Forge 0. Les 127 cas ombres correspondent dans chaque capture, mais cela ne ferme pas la fidelite. [Comparaison](../source-shadow-tent5-atlas-reopen-v1/RESULT.v1.json) · [Localisation](../source-shadow-tent5-atlas-difference-audit-v1/RESULT.v1.json).\n\n'
    output += 'Les deux essais source precedents avaient un kernel invalide (type Texture2D incompatible) ; leurs sorties ne sont pas des mesures du sampler. Les refus sont archives. Kernel corrige execute, atlas source original et atlas natif precedent inchanges ; aucun pixel Unity utilise pour rendre Forge. Prochaine tranche : neuf lectures du cas isole, puis raccordement a la scene et usage Studio. Zero asset complet ajoute.\n\n'
    tent5_taps_path=W/'source-shadow-tent5-taps-reopen-v1/RESULT.v1.json'
    tent5_bridge_path=W/'source-shadow-tent5-single-texel-bridge-v1/RESULT.v1.json'
    town_scope_path=W/'townsmith-current-draw-binding-scope-v1/RESULT.v1.json'
    output += '**Derniere tranche PCF5 : 26 244 taps par backend reouverts.** Poids et UV identiques partout ; un seul tap actif differe. Ses quatre texels et ses fractions de comparaison reproduisent exactement les deux lectures GPU ; le texel absent (410,2864) explique l ecart pondere a moins de 0,00000003. Cette preuve concerne ce point seulement et ne qualifie ni raster complet, ni scene ni Studio. [Taps](../source-shadow-tent5-taps-reopen-v1/RESULT.v1.json) · [Cause locale](../source-shadow-tent5-single-texel-bridge-v1/RESULT.v1.json).\n\n'
    output += '**L03 Townsmith : perimetre corrige apres rapprochement des huit prefabs.** 37 bindings attendent une qualification de leurs dessins, repartis sur huit meshes : trois LOD de porte (15 instances), quatre LOD de lit (20) et un LOD d etagere (2). L ancien compteur de six ne couvrait que les cinq premiers prefabs. Geometries et slots source conserves ; aucune nouvelle affectation inventee ni livraison complete. [Inventaire actuel](../townsmith-current-draw-binding-scope-v1/RESULT.v1.json).\n\n'
    town_context_path=W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'
    if town_context_path.exists():
        output += '**L03.TOWNSMITH.DESSINS.CONTEXTE_HDRP : huit configurations source reouvertes.** 144 passes compilees ; huit images identiques entre deux executions. Les sept copies privees de materiaux ont ete reserialisees par HDRP, avec proprietes de rendu preservees ; les originaux et licences restent conserves. Le FrameDebugger est indisponible en batch : aucun dessin natif observe ne sera revendique. Les 37 bindings, le rendu Forge et l usage Studio restent a qualifier. [Preuve](../townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json). Zero asset complet ajoute.\\n\\n'
    town_explicit_path=W/'townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json'
    if town_explicit_path.exists():
        output += '**L03 Townsmith : 16 comparaisons GPU executees et reouvertes.** Les dix vues de lit et etagere sont bit-exactes. Pour les trois portes, affecter le materiau a tous les sous-maillages change plus de 4 500 pixels ; dessiner selon les slots laisse 2 a 4 pixels differents. Ces ecarts restent ouverts, sans seuil assoupli. Les materiaux identiques repetes ne permettent pas de deduire le nombre de passes a partir des images. Affectations Forge et usage editeur non qualifies. [Preuve](../townsmith-explicit-slot-controls-reopen-v1/RESULT.v1.json). Zero asset complet ajoute.\n\n'
    town_descriptors_path=W/'townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json'
    if town_descriptors_path.exists():
        output += '**L03 Townsmith : indices et attributs originaux verifies.** 16 nouvelles captures, 44 descripteurs ; positions, normales, tangentes et UV conserves. Les ecarts des portes restent 3/2/4 pixels. La reconstruction des indices ne les explique pas. Premier essai refuse pour troncature du buffer, conserve ; controle corrige execute. Prochain diagnostic : clone complet et passes d ombres source. Aucun asset complet ajoute. [Preuve](../townsmith-descriptor-slot-controls-reopen-v2/DESCRIPTOR-RESULT.v1.json).\n\n'
    town_pass_path=W/'townsmith-door-pass-controls-reopen-v1/RESULT.v1.json'
    town_shadow_path=W/'townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json'
    if town_pass_path.exists() and town_shadow_path.exists():
        output += '**L03 Townsmith : quinze nouvelles captures bit-exactes.** Clone complet et contexte enfant conservent la reference ; separer renderer visible et shadow caster supprime les neuf pixels residuels dans ces captures. Toutefois, les ombres explicites completes et limitees au sous-maillage visible donnent toutes deux zero ecart : les triangles caches ne sont pas qualifies comme cause. Prochain controle : mode shadow casting et etat reel de la lumiere. Conversion Forge, scenes completes et usage Studio restent ouverts. [Renderer](../townsmith-door-pass-controls-reopen-v1/RESULT.v1.json) / [Ombres explicites](../townsmith-explicit-shadow-controls-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    town_modes_path=W/'townsmith-shadow-mode-controls-reopen-v1/RESULT.v1.json'
    town_recipes_path=W/'townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json'
    if town_recipes_path.exists():
        output += '**L03 Townsmith : quinze affectations de couleur converties en recettes verifiees, pour trois meshes.** Tous les sous-maillages, slots et identifiants source sont conserves ; 39 epingles de conversion verifiees. Les nouvelles captures relevent les lumieres : anciennes vues sans ombres ; ombres Soft actives modifient environ 19 000 pixels par LOD, et la geometrie visible donne la meme image source. Les trois comparaisons source-shadowed avec elles-memes ne comptent pas comme validation independante. Les ecarts des anciens controles restent archives ; aucune cause geometrique globale revendiquee. Shader Forge, politique complete des ombres, transformations des prefabs et usage Studio restent ouverts. [Modes](../townsmith-shadow-mode-controls-reopen-v1/RESULT.v1.json) / [Recettes converties](../townsmith-verified-door-draw-recipes-v1/DRAW_BINDINGS.v1.json). Zero asset complet ajoute.\n\n'
    town_paint_path=W/'townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json'
    if town_paint_path.exists():
        output += '**L03 Townsmith : S_Paint compile en programme intermediaire et reouvert contre les sources.** 134 operations, 255 ports, sept branches et deux instances de sous-graphe ; 18 references de textures conservees. Le code genere par Unity est exporte et verifie. Ses entrees vertex implicites utilisent position, normale et tangente originales : les zeros serialises de ces ports ne doivent pas devenir des constantes. L essai initial d export avec une signature obsolete est conserve ; signature active utilisee avec succes. La traduction WGSL, les types/precisions et la comparaison GPU Forge restent a faire. [Preuve source](../townsmith-s-paint-source-program-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    town_wgsl_path=W/'townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json'
    if town_wgsl_path.exists():
        output += '**L03 Townsmith : les 134 operations S_Paint sont generees en WGSL et leurs types fragment valides puis reouverts.** Dix textures, dont trois normal maps independantes ; UV, alpha des normales, modes de decodage et bent-normal implicite conserves. Les types sont releves dans le code Unity ; les sept aliases initiaux elides sont resolus, audits partiels conserves. Le contrat commun Forge accepte actuellement sept textures : une extension generique a dix est requise sans collision avec les ressources de scene. Les profils importes, mips, sampler, comparaison GPU source/Forge et usage Studio restent a qualifier. Le fragment de validation n a pas ete execute ; ses constantes ne sont pas des defaults runtime. [Preuve](../townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    layout_probe_path=W/'source-texture-layout-admission-v2/RUN-RESULT.v1.json'
    if layout_probe_path.exists():
        output += '**Sous-lot dix textures :** '+state.get('current_work_state',state['state'])+'. [Controle courant](../source-texture-layout-admission-v2/RUN-RESULT.v1.json). Le premier echec de compilation est conserve. Aucun asset complet ajoute.\n\n'
    texture_sources_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json'
    pipeline_probe_path=W/'source-texture-layout-admission-v3/RUN-RESULT.v1.json'
    if texture_sources_path.exists():
        output += '**Dix textures S_Paint reouvertes :** dix fichiers et dix meta originaux verifies par SHA, copies privees identiques ; trois profils de normales distincts aux indices texture 6/7/8. Formats GPU, mips et sampler effectifs restent a capturer. [Inventaire](../townsmith-s-paint-texture-inventory-v1/RESULT.v1.json). Le controle V2 a cree les layouts natifs (18 textures / 17 samplers proposes), pas un rendu ni une preuve Web. [Quatre groupes en controle](../source-texture-layout-admission-v3/RUN-RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    native_texture_reopen_path=W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json'
    layout_reopen_path=W/'source-texture-layout-admission-reopen-v3/RESULT.v1.json'
    output += '**Controle historique avant extraction BC :** dix profils Unity verifies ; deux textures UNORM completes (24 mips, 44 739 240 octets). Huit textures compressees BC1/BC3 ne sont pas lisibles par AsyncGPUReadback : refus retenus et recette isolee a executer. [Preuve textures](../townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json). Six layouts Bevy natifs reouverts ; limites Web de base depassees, aucun shader de rendu ni navigateur qualifie. [Preuve layouts](../source-texture-layout-admission-reopen-v3/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    compressed_run_path=W/'townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json'
    compressed_close_path=W/'townsmith-s-paint-compressed-storage-reopen-v1/RESULT.v1.json'
    output += '**Extraction BC isolee :** '+state.get('current_work_state',state['state'])+'. [Execution](../townsmith-s-paint-compressed-storage-v1/RUN-RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    ten_texture_wire_path=W/'source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json'
    ten_texture_engine_path=W/'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json'
    output += '**Contrat commun dix textures applique :** schema 6, 25 tests wire passes ; anciens schemas limites a sept, bindings de scene et uniformes preserves. La reservation commune de slots augmente le budget GPU meme des anciens programmes : compatibilite device a verifier. [Tests wire](../source-graph-ten-texture-contract-check-v1/RUN-RESULT.v1.json) / [Tests moteur](../source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json). Aucun rendu ni asset complet qualifie.\n\n'
    resource_progress_paths=[W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json',W/'townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json']
    output += '**Dernier lot technique termine :** dix ressources natives reouvertes, 120 mips et 78 293 832 octets exacts ; les 864 mesures des huit textures compressees sont bit-exactes et leurs reglages restaures. [Extraction V2](../townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json) / [Assemblage reouvert](../townsmith-s-paint-native-resources-reopen-v1/RESULT.v1.json). Les anciens refus sont conserves ; admission Forge, normales, rendu et Studio restent ouverts. Aucun nouvel asset complet livre.\n\n'
    candidate_close_path=W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json'
    output += '**Admission preparee et testee en isolation :** 8 tests passes ; 10 ressources reelles acceptees, 40 mutations refusees. Liaison GUID/meta, installation commune et sampling GPU restants. [Preuve limitee](../native-texture-capture-contract-reopen-v1/RESULT.v1.json).\n\n'
    native_identity_proof_path=W/'native-texture-identity-contract-reopen-v1/RESULT.v1.json'
    output += '**Contrat parent teste en isolation :** 29 tests passes, dix ressources originales avec PNG/meta acceptees ; 40 mutations de capture et 30 mutations des GUID metadata refusees. Installation dans le workspace, GPU et Studio restent ouverts. [Preuve](../native-texture-identity-contract-reopen-v1/RESULT.v1.json).\n\n'
    output += 'Prochaine action enregistrée : '+state['next_action']+'.\n\n'
    output += '''## Comment suivre

Après chaque tranche : ID du lot, vérifié/attendu pour le contrôle réalisé, résultat, refus précis, lien du reçu et prochaine action. Les livraisons complètes sont comptées séparément des contrôles techniques.

[Détails et reçus](SUIVI_SIMPLE.md) · [Journal des étapes](JOURNAL.md) · [Inventaire par pack](INVENTAIRE_PAR_PACK.csv).
'''
    native_identity_v2_path=W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json'
    native_normal_source_path=W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json'
    output += '**Contrat generique V2 :** 30 tests passent, admission par ressource independante du statut et de la taille du lot ; 80 mutations de ressources et deux protocoles inconnus refuses. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json). Installation commune restante.\n\n'
    output += '**Reference normales source terminee :** trois profils, 324 decodages GPU Unity ; 324 texels encodes et alphas bit-exacts avec la capture anterieure. Mode RG/AG observe pour chaque profil ; comparaison Forge GPU et rendu restants. [Preuve](../townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    native_texture_gpu_proof_path=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v1/RESULT.v1.json'
    output += '**Lectures GPU natives du prototype verifiees :** dix textures / 120 mips, 1 080 lectures et 324 decodages normaux sans ecart ; 864 references directes GPU Unity et 216 references des bytes natifs UNORM. Sampling filtre, ABI commun des normales, rendu materiau, Web et Studio restent ouverts. [Preuve](../townsmith-s-paint-native-texture-gpu-control-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    native_texture_gpu_proof_path=W/'townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json'
    output += '**Lectures GPU natives du prototype verifiees :** dix textures / 120 mips, 1 080 lectures et 324 decodages normaux sans ecart avec modes communs (source RG/AG 1 vers ABI 2) ; 864 references directes GPU Unity et 216 references des bytes natifs UNORM. Sampling filtre, binding effectif des uniforms, rendu materiau, Web et Studio restent ouverts. [Preuve](../townsmith-s-paint-native-texture-gpu-control-reopen-v2/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    filtered_texture_progress_paths=[W/'townsmith-s-paint-native-filtered-gpu-reopen-v1/RESULT.v1.json',W/'townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json',W/'townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json']
    output += '**Filtrage a LOD explicite verifie :** dix textures, 1 080 valeurs RGBA et 324 normales filtrees sans ecart. La sonde Unity melange les deux mips adjacents ; ce comportement est reproduit dans le controle GPU, metadata Bilinear conservees. [Refus V1](../townsmith-s-paint-native-filtered-gpu-reopen-v1/RESULT.v1.json) / [Diagnostic](../townsmith-s-paint-filtered-mip-blend-diagnostic-v1/RESULT.v1.json) / [Controle V2](../townsmith-s-paint-native-filtered-gpu-reopen-v2/RESULT.v1.json). Politique complete du sampler, gradients implicites, moteur et Studio restent ouverts. Aucun asset complet ajoute.\n\n'
    common_regression_path=W/'native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json'
    common_install_prepared_path=W/'native-texture-capture-common-install-v1/PREPARED.v1.json'
    output += '**Installation commune en chaine :** regressions permanentes preparees, 33 tests candidat passent ; installation attend le succes du test moteur deja actif, puis tests workspace assets et moteur sequentiels. Les sources historiques sont snapshottees avant remplacement ; aucune installation complete annoncee avant ses recus. [Regressions](../native-texture-capture-common-regressions-reopen-v1/RESULT.v1.json) / [Etat du travail](../native-texture-capture-common-install-v1/RUN-RESULT.v1.json).\n\n'
    texture_packet_proof_path=W/'townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json'
    output += '**Contrats de ressources exportes et reouverts :** dix textures avec leurs captures, PNG et meta ; 40 references de buffers verifiees, 34 tests des modules candidats passent. Ces ressources attendent assemblage du materiau et admission moteur ; aucun objet placable ni livraison complete ajoute. [Preuve](../townsmith-native-texture-contract-packets-seal-v1/RESULT.v1.json).\n\n'
    texture_set_proof_path=W/'townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json'
    output += '**Jeu de textures compose et reouvert :** dix slots originaux, 32 vues partagees et bindings canoniques ; 134 operations arithmetiques conservees. 35 tests passent avec les modules candidats. Passes de rendu completes, admission moteur et Studio restent ouverts. [Preuve](../townsmith-s-paint-texture-set-reopen-v1/RESULT.v1.json). Aucun nouvel asset complet.\n\n'
    canonical_wgsl_proof_path=W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json'
    output += '**Composition du shader arithmetique :** deux controles Bevy/Naga passent avec les bindings canoniques. Sonde de types composee, non executee ; passes completes et rendu Studio restent ouverts. [Preuve](../townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    host_adapter_proof_path=W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json'
    output += '**Adaptateur du materiau et reprise moteur :** binding uniform 100, trois profils de normales et transforms explicites raccordes ; deux compositions passent. Le test moteur v1 a refuse un constructeur non eclaire incomplet ; trois slots optionnels initialises sans changer sa capacite de sept textures. Reprise sequentielle v2, refus v1 conserve. UV0 complet dans les varyings, passes completes et Studio restent ouverts. [Adaptateur](../townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json), [reprise](../townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json). Aucun asset complet ajoute.\n\n'
    path = T / 'LOTS_ET_AVANCEMENT.md'
    temp = path.with_suffix('.md.tmp')
    temp.write_text(output, encoding='utf-8')
    temp.replace(path)
    manifest = {
        'updated_utc': now, 'active_sub_lot': state['active_sub_lot'],
        'full_import': False, 'complete_assets_delivered_increment': 0,
        'dashboard': str(path), 'input_pins': {
            str(p): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in [source, state_path, Path(__file__)] + ([audit_path] if audit else []) + ([controls_path] if controls else []) + ([point_path] if point else []) + ([bias_path] if bias else []) + ([raster_path] if raster else []) + ([atlas_path] if atlas else []) + ([caster_path] if caster else []) + ([mapping_path] if mapping else []) + ([mapping_close_path] if mapping_close else []) + ([depth_isolation_path] if depth_isolation else []) + ([projection_clip_path] if projection_clip else []) + ([tent5_path] if tent5_path.exists() else []) + [tent5_math_path,tent5_atlas_path,tent5_audit_path] + [tent5_taps_path,tent5_bridge_path,town_scope_path] + ([town_context_path] if town_context_path.exists() else [])
        },
    }
    if town_explicit_path.exists():
        manifest['input_pins'][str(town_explicit_path)] = hashlib.sha256(town_explicit_path.read_bytes()).hexdigest()
    if town_descriptors_path.exists():
        manifest['input_pins'][str(town_descriptors_path)] = hashlib.sha256(town_descriptors_path.read_bytes()).hexdigest()
    for p in [town_pass_path,town_shadow_path]:
        if p.exists():
            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [town_modes_path,town_recipes_path]:
        if p.exists():
            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    if town_paint_path.exists():
        manifest['input_pins'][str(town_paint_path)] = hashlib.sha256(town_paint_path.read_bytes()).hexdigest()
    if town_wgsl_path.exists():
        manifest['input_pins'][str(town_wgsl_path)] = hashlib.sha256(town_wgsl_path.read_bytes()).hexdigest()
    if layout_probe_path.exists():
        manifest['input_pins'][str(layout_probe_path)] = hashlib.sha256(layout_probe_path.read_bytes()).hexdigest()
    for p in [texture_sources_path,pipeline_probe_path]:
        if p.exists():
            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [native_texture_reopen_path,layout_reopen_path]:
        manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [compressed_run_path,compressed_close_path]:
        if p.exists():
            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [ten_texture_wire_path,ten_texture_engine_path]:
        if p.exists():
            manifest['input_pins'][str(p)] = hashlib.sha256(p.read_bytes()).hexdigest()
    for p in resource_progress_paths:
        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    manifest['input_pins'][str(candidate_close_path)]=hashlib.sha256(candidate_close_path.read_bytes()).hexdigest()
    manifest['input_pins'][str(native_identity_proof_path)]=hashlib.sha256(native_identity_proof_path.read_bytes()).hexdigest()
    for p in [native_identity_v2_path,native_normal_source_path]:
        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    manifest['input_pins'][str(native_texture_gpu_proof_path)]=hashlib.sha256(native_texture_gpu_proof_path.read_bytes()).hexdigest()
    manifest['input_pins'][str(native_texture_gpu_proof_path)]=hashlib.sha256(native_texture_gpu_proof_path.read_bytes()).hexdigest()
    for p in filtered_texture_progress_paths:
        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    for p in [common_regression_path,common_install_prepared_path]:
        manifest['input_pins'][str(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    manifest['input_pins'][str(texture_packet_proof_path)]=hashlib.sha256(texture_packet_proof_path.read_bytes()).hexdigest()
    manifest['input_pins'][str(texture_set_proof_path)]=hashlib.sha256(texture_set_proof_path.read_bytes()).hexdigest()
    manifest['input_pins'][str(canonical_wgsl_proof_path)]=hashlib.sha256(canonical_wgsl_proof_path.read_bytes()).hexdigest()
    manifest['input_pins'][str(host_adapter_proof_path)]=hashlib.sha256(host_adapter_proof_path.read_bytes()).hexdigest()
    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'
    temp = meta.with_suffix('.json.tmp')
    temp.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temp.replace(meta)
    return path

if __name__ == '__main__':
    print(render())
