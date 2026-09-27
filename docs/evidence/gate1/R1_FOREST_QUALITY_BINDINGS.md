# R1 — Stabilité des primitives de forêt lors des changements de profil

7 septembre 2026, branche Rust, worktree conservé, aucune publication.

## Constat et portée

Le projecteur de forêt suit déjà le scatter courant et les plages des trois LOD, mais
détruisait puis recréait toutes ses primitives à chaque changement de profil. Le candidat
réel compte 7 632 primitives pour 636 arbres ; ces entités dérivées ne sont pas des IDs
de documents et ne doivent pas être sérialisées. Leur remplacement intégral est inutile
pour les arbres qui survivent à une transition.

Cette correction ne suffit pas à lever `vegetation=Unqualified` : les statistiques de
géométrie/visibilité restent celles des recettes procédurales, et les arbres lointains
utilisent des instances LOD2, pas encore les HLOD de chunks qualifiés. Le garde-fou du
plan effectif est conservé, sans nouvelle exception ADR-0011.

## Implémenté

Le projecteur rapproche l'ancien et le nouveau jeu de primitives par la clé interne
(ID de placement, LOD, index de primitive). Une entité vivante correspondante est conservée,
avec mise à jour de ses plages, handles, transform et ombres. Une primitive nouvelle est
créée ; une primitive retirée est détruite. Préparation fallible avant la mutation comme
auparavant. La reprise après échec garde son retour procédural et son erreur explicite.

Aucune recette, seed, pose authorée, collision, mesh ou shader n'est modifié. Pas de nouveau
format, dépendance ou représentation persistante. Pas de gain de FPS annoncé sans benchmark.

## Vérifié

Cinq tests ciblés du projecteur PASS :

- cycle des quatre profils avec deux primitives par LOD : mêmes entités pour les 3 816
  primitives de cette fixture synthétique, plages conformes au profil et ombres cohérentes ;
- densité décorative : survivants conservés, éléments retirés absents, poses/sources stables ;
- colliders des 636 arbres préservés ;
- chunks HLOD devenus vides sans perte des bindings propriétaires ;
- échec injecté puis restauration procédurale sans mélange de draws/colliders.

Premier essai de compilation du test refusé car `VisibilityRange` n'implémente pas Debug ;
assertion d'égalité corrigée sans supprimer le contrôle. Les tests ont ensuite terminé avec
code 0. Puis **567 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict
workspace/all-targets/all-features et Studio WASM PASS, build Web code 0.
Log workspace SHA-256 `3E5D60E080D008CBC67F5037F559AE2015DAAF8A059D8B03FC441E8005557B28`.
Bundle `web-forest-bindings`, WASM
`A1568910887E5CF2683A529F7389512D85331421A6137D4B08616B7716CE0A94`.

## Rejeu Chrome sur la forêt réelle

Vue candidate `water_detail`, temps virtuel figé, kit/forêt opt-in, 1408 × 741.
High → Low → High par les commandes Studio : projection CPU acquise à chaque étape,
636 arbres/7 632 primitives constants et hashes source identiques. Trois PNG reçus et
inspectés : le profil Low réduit notamment l'herbe et les ombres, High retrouve l'aspect
initial. Aucune disparition de forêt constatée sur ces vues, console error vide.

| Profil | PNG | Diagnostic |
|---|---|---|
| High | 1788775647802 | 1788775650039 |
| Low | 1788775683573 | 1788775684659 |
| High restauré | 1788775714721 | 1788775716906 |

Copies vérifiées par SHA-256 et invariants contrôlés par `Collect-Forest-Bindings.ps1`,
artefacts dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907/forest-bindings-cycle`.
Les IDs ECS sont prouvés par les tests, pas par ces PNG/diagnostics. Pas de benchmark,
de fluidité acceptée, ni de validation physique Web (le kit affiche encore 0 colliders).
Le blocker végétation et readiness false sont conservés. Tauri et nouveau runtime natif
non rejoués pour ce lot. Gate 1 reste ouverte.

## Remplacement du cache de prototypes — 7 septembre, complément

Inspection suivante : le raccourci du projecteur ne comparait que scatter et profil.
Remplacer les handles d'un prototype à scatter/profil constants laissait les anciens
draws en place. Le cache appliqué est maintenant comparé au cache courant (catalogue,
liaisons mesh/matériau et erreur). Seuls ces descripteurs et handles sont conservés,
aucune copie des meshes ni nouvelle source canonique. Une différence repasse par la
préparation fallible et la réconciliation existantes, sans recréer les arbres survivants.

Test synthétique de remplacement PASS : nouveau handle effectivement utilisé par les
trois LOD, entités et transformations identiques. **11 tests projecteur PASS**,
Clippy moteur strict et check Studio WASM PASS (avertissement futur préexistant
proc-macro-error2 2.0.1 conservé). Fmt et diff vérifiés.

Limites : pas de surveillance de fichiers ni réimport Blender/GLB complet ajouté.
Une mutation en place du contenu sous le même handle n'est pas détectée par cette
comparaison de descripteurs ; aucune qualification de hot-reload, des transitions
GPU ou du HLOD n'en découle. Le dernier smoke natif appartient au binaire précédent.
Le refus de capacité forêt reste inchangé. Après les contrôles ciblés, la suite
`cargo test --workspace --locked --offline -q` a réellement terminé avec sortie 0 :
**609 tests PASS, 0 FAIL, 1 timing ignoré**. Ce nouveau total inclut le test de
remplacement ; il ne transforme pas la fixture synthétique en preuve de réimport produit.

### Reprise après échec de cache

Le verrou `failed` empêchait toute reprise, même après correction du cache. La
projection peut désormais réessayer lorsque ses descripteurs de prototypes changent.
Un échec inchangé reste arrêté, y compris après changement de profil : pas de boucle
de reconstruction/logs. L'erreur diagnostique n'est effacée qu'après réussite de la
projection individuelle et du regroupement éventuel ; les anciens logs ne sont pas
effacés. Les contrôles de source et la qualification forêt ne sont pas affaiblis.

Test ajouté : échec injecté, absence de nouvelle reconstruction à cache identique,
cache corrigé, retour des 1950 primitives synthétiques, 650 colliders seulement et
stabilité au passage suivant. Premier passage : 12 tests projecteur PASS ; Clippy
refuse ensuite une expression redondante, simplifiée sans exception de lint.
Clippy corrigé PASS ; rejeu final des 12 tests PASS, sortie 0. Check WASM passé avant cette
simplification logique. Ce contrôle ne qualifie pas un réimport Blender ni un cycle GPU.

## Cycle Web du candidat rev.4 actuel — 7 septembre, 21:06–21:09 UTC

Bundle `r1-bank-road-studio-20260907`, port 1460, WASM SHA-256
`10726ed3e97520e788fd4f1fbe3ae00f1d584d210d320b68d97c4827d5e9a2e5`.
Aucun rebuild ni changement de code dans ce lot. Vue candidate `human_bridge`,
caméra fixe et temps 0,250 s, forêt batches, kit rev.4. High initial provient du
diagnostic/capture du lot routes immédiatement précédent, même onglet sans recharge.
Clics réels Low puis High, chaque profil observé avant export PNG/JSON.

| Observation CPU | High initial | Low | High restauré |
|---|---:|---:|---:|
| Arbres gouvernés | 650 | 650 | 650 |
| Brins d'herbe projetés | 620 640 | 154 048 | 620 640 |
| Entités mesh végétation | 8 132 | 6 997 | 8 132 |
| Triangles des meshes uniques | 2 594 482 | 1 185 626 | 2 594 482 |
| Colliders du kit annoncés prêts | 36 | 36 | 36 |

Les sources liées au plan sont identiques aux trois étapes, profils demandés et
végétation projetée concordants. PNG 2048×1157 reçus et inspectés : moins d'herbe
en Low, retour de la couverture High, pas de disparition globale de la forêt ou
du pont. Cela ne prouve pas des identités ECS/colliders inchangées ni un rendu High
pixel-identique. Le diagnostic de sonde IBL indique textures/caméra remplacées
après changement de qualité ; ne pas utiliser sa référence initiale comme preuve
de stabilité de ces handles. Les images restent des observations distinctes.

Copies/hash comparés aux téléchargements et bindings contrôlés par PowerShell :
`CYCLE_SOURCES_COUNTS_AND_COPIES_PASS`. Répertoire externe :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-road-studio-20260907/quality-cycle/`.

- High initial PNG : `175bf51adc1675193c273610e42127f64ad2f7f471338960d65547744057de01` ;
  JSON : `3c1673db698d03905765c346c6fac0c37e8ca391711973795f2f798294ec5a1c`.
- Low PNG : `e1d001bb089a42bf92db46a7e833f2dd7d3c3655c04f851b297437995860c9d9` ;
  JSON : `65b242e93e293bc72521ad2bd91eda74ad8e3eec0208075f54da0744a0cf45fd`.
- High restauré PNG : `06a19e1b8a56236041787e3f2210dbcf7f4103b0cdfd2f67c2a4f6af59591aab` ;
  JSON : `d7dfc80fb23f2c1a57498a8a3d62ee5cfa0a56cb0d4df78de787b8ee5048e498`.

`full_preset_applied`, `gpu_ready`, `gate_passed` et `human_acceptance` restent faux,
blocker végétation conservé. Medium/Ultra non rejoués, pas de franchissement des
seuils LOD en mouvement ni de vent isolé. Les délais de boucle Web affichés autour
d'une seconde dans cette session non focalisée ne constituent pas un benchmark.
Pas de traversée physique, performance cible ou Tauri qualifiés. Ce cycle au niveau
de qualité changeant ne remplace pas le test de remplacement d'une politique à
niveau constant. Aucun critère global de Gate 1 promu.
