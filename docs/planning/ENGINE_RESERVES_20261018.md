# Réserves d’architecture — reprise du 18 octobre 2026

Ce bilan décrit la première passe du 18 octobre. La décision ultérieure de
[séparation stricte](../ADR/ADR-0026-strict-engine-map-separation.md) retire les
ports et données de carte du moteur et de ses dépendances de domaine. Les API
de ports décrites ici résident désormais dans les composants facultatifs de
carte. Les compteurs et preuves ci-dessous conservent leur état d'acquisition ;
ils ne constituent pas l'inventaire actuel du moteur.

Demande propriétaire : traiter les deux réserves du bilan du 17 octobre.
Checkout partagé `Rust`, base `3c3a5efeba2473574597871f223d74aa800cf99a`, modifié.
Aucune publication Git ni nouvelle installation n’est implicite.

## Studio WebGPU

Les 80 diagnostics initiaux concernaient 34 fichiers. Ils ont été corrigés sans
suppression de lint : imports explicites, état UI regroupé derrière des handles,
conversions JSON vers des scalaires GPU refusant les débordements, requêtes
explicites Set/Clear, indirection des grandes variantes, fonctions séparées par
responsabilité et corrections de clonage, motifs, documentation et closures.

Le passage strict `studio-wasm-pass7-20261018.log` est PASS, ainsi que les rejeux
après les extractions, le contrat de profondeur et l'ordre final du flare
(`studio-wasm-flare-final-20261018.log`). La réserve des 80 diagnostics est levée.
Le relevé du 17 octobre reste un historique daté, pas le verdict actuel.

## Généricité : implémentation et frontières

- L’animation reçoit des horloges explicites. La capture de référence et ses
  contrôles de provenance appartiennent à l’adaptateur de contenu.
- Les modules communs de vent, pendules et locomotion ont remplacé leurs anciens
  noms. Le contrôleur et les échelles exposent des API sans indicatif de carte.
- Un profil de locomotion validé contrôle vitesses, gravité, immersion et échelles.
  L’initialisation du runtime conserve ce profil ; les constantes par défaut sont
  maintenues, y compris l’ordre d’évaluation de la hauteur d’œil en nage.
- Grain, AO et flare possèdent leurs modules et shaders communs. Sept noyaux WGSL
  sont conservés à l’octet près après normalisation LF. Les UUID restent stables.
- Le flare reçoit la lumière par `SceneSun` ou par les lumières Bevy. Le relay des
  matériaux importés est explicite, situé dans leur adaptateur et installé par
  les deux consommateurs existants. Le plugin optique n’exige plus ces assets.
- L’observation du milieu et le set de haze LDR sont des contrats communs.

Voir les [API et limites](../../crates/forge_engine/COMMON_SCENE_INPUTS.md) et
[ADR-0025](../ADR/ADR-0025-common-optical-and-animation-inputs.md).

Le probe commun natif/WebGPU utilise deux scènes procédurales différentes,
construites sans paquet importé. Pour chacune, il compare une capture de référence
avec le grain, le flare et l’AO activés séparément. Il vérifie d’abord une seconde capture sans effet, identique à l’octet près,
puis refuse les erreurs de pipeline, les readbacks absents et un effet qui ne
modifie pas assez de pixels. Le dithering de tonemapping est désactivé dans le
probe afin de ne pas attribuer son bruit à un effet.
Le lancement natif final est PASS (Vulkan, Radeon RX 7800 XT) : dix PNG ont été
acquis et inspectés, les deux témoins sont identiques et les six comparaisons
modifient chacune plus de 10 000 pixels. Les [captures et le reçu natifs](../evidence/architecture/generic-optical-native-final-20261018/proof.json)
sont conservés séparément des preuves historiques. Deux lancements navigateur
**BrowserWebGpu PASS** et dix PNG sont également conservés dans le
[reçu WebGPU](../evidence/architecture/generic-optical-webgpu-20261018/proof-replay.json).
Le navigateur ne révèle pas l'identité de son adaptateur matériel.
Ces vues utilisent des cibles sRGB LDR et n'activent pas le resolve temporel HDR.
Le probe ne vérifie donc ni cette branche HDR de l'AO, ni le relay des matériaux
importés dans une session Tidewater complète.

## Réserve globale encore distincte

L’inventaire statique courant compte 1 066 occurrences incluant la provenance,
contre 1 307 après la première passe. Un compte de texte n’est pas un critère de
clôture. Les autres familles et leurs preuves demeurent celles du
[plan d’architecture](ENGINE_ARCHITECTURE_DEBT_20261017.md) : recettes et données,
atmosphère, eau, objets et matériaux, ainsi que leurs intégrations et usages
indépendants natifs/WebGPU. Les extractions ci-dessus ne soldent pas ces familles.
L'AO conserve notamment un import vers `tidewater_temporal` pour l'ordonnancement
et les observations HDR. Le contrôleur amphibie conserve les dimensions de
capsule historiques. Ces deux frontières ne sont pas déclarées découplées par
les tests des autres modules communs.

## Validation et acquisitions intermédiaires

Les journaux locaux conservent les acquisitions initiales en échec séparément.
Le premier build natif a lu une ancienne définition du profil et un test modifié
pendant ce build : ce passage est incohérent et ne qualifie aucun état final.
Les déclarations ajoutées après des modules de test ont été déplacées avant
ceux-ci pour satisfaire Clippy. Un inventaire des entrées Rust et shaders fige
les sources des contrôles suivants ; toute correction ultérieure le renouvelle.

Le premier rejeu workspace a terminé la suite moteur avec 959 PASS, 2 échecs et
31 ignorés. Les deux échecs portent sur la readiness de sortie native et son
délai mural d’une seconde ; le même binaire rejoué isolément et séquentiellement
passe ces deux assertions. Le délai, les assertions et le code produit ne sont
pas modifiés pour cela. Le runner borne désormais aussi les threads du harness
de tests ; le rejeu workspace séquentiel est PASS : **2 229 réussis, 0 échec,
56 ignorés, 114 suites** (`workspace-tests-serial-20261018.log`, sortie 0).
Il couvre `--workspace --lib --tests`, sans les exécutables d'exemples. Le rejeu
final après le contrat de profondeur et l'ordre du flare passe **2 230 tests,
0 échec et 56 ignorés, dans 114 suites**. Le nouveau test de profondeur passe.
Les doctests s'exécutent aussi sans échec (aucun exemple exécutable présent).

Le runner de reprise complet passe ses dix étapes, avec entrées inchangées
entre ses snapshots et payload WASM identique à celui exécuté dans le navigateur.
Clippy WASM est aussi présent dans le script local standard et ajouté au job CI ;
la nouvelle commande CI passe localement, sans exécution distante revendiquée.

### Dépendance de profondeur découverte par le probe

Le premier lancement GPU indépendant est RED : AO et flare tentaient de lire
la profondeur principale d’une caméra Bevy standard sans usage `TEXTURE_BINDING`.
Le réglage était auparavant fourni par les scènes importées. Le contrat commun
`view_depth::SampleableDepth` est maintenant requis par les deux effets. Dans le
monde de rendu, il ajoute uniquement ce droit, avant la préparation de profondeur
Bevy, en conservant les autres usages et sans modifier la caméra authorée.
Un test vérifie la conservation des flags, l’idempotence et l’absence de changement
sur une caméra sans effet. La relance native v4 est PASS ; aucune erreur n’est
masquée en désactivant AO ou flare. Le probe s’arrête désormais dès qu’un arrêt
du renderer est demandé, plutôt que d’attendre son timeout de capture.

### Ordre final du flare découvert dans le navigateur

Le premier lancement WebGPU a réellement échoué : **0 pixel modifié** par le
flare. L'effet était après `PostProcess` mais sans ordre explicite avant
`upscaling`. Sur la scène importée, le haze imposait cet ordre par transitivité ;
sur une scène indépendante, le set de haze vide ne suffisait pas. L'effet pouvait
être encodé après la copie finale et donc ne pas apparaître dans la capture.

Le plugin commun impose maintenant son exécution avant l'upscaling et l'UI.
Deux initialisations navigateur indépendantes passent les six comparaisons,
sans modifier un shader ni réduire le seuil de détection. Le harness conserve
désormais les images et les entrées CPU même si une comparaison est en échec ;
il maintient une sortie native non nulle et un affichage FAIL dans ce cas.
Le [bilan de validation](../evidence/architecture/VALIDATION_20261018.md)
rapproche ces acquisitions, leurs empreintes et leurs limites.
