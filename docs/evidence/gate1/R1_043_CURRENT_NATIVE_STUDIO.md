# R1-043 — natif courant comparé au Studio du 11 septembre

## Candidat avec chevauchement — 11 septembre, acquisition native

Les sections suivantes restent historiques. Nouveau natif construit depuis les
**mêmes 284 fichiers** que `r1-mesh-preparation-overlap-20260911` : pins vérifiés
avant/après build et après acquisition, aucun écart accepté. Les 717 tests et Clippy
natifs/Web de ce candidat restent la preuve de code, non annoncés rejoués ici.

`r1-overlap-native-parity-20260911/` : build offline dev optimisé PASS en 1 min 38 s ;
cycle High/Low/Medium/Ultra/High PASS, cinq captures et cinq observations prêtes.
Préchauffage Low/Medium/High/Ultra/High en 17,544 s, pipelines 144/145/156/156/157.
Le vérificateur contrôle aussi les identités et séquences croissantes, inventaires,
effets, pipelines, retour High et l'unique événement de cycle terminé.

Sources, hash de politique, réglages effectifs, substitutions et contraintes de
berges concordent avec les trois reçus mobiles High/Low/High du Studio courant.
Les champs f32 déclarés restent comparés bit à bit, sans tolérance générale.
Medium/Ultra natifs acquis ; complément Studio du même binaire maintenant acquis
dans `r1-overlap-studio-medium-ultra-20260911/`, sans réutiliser leurs anciennes
preuves comme courantes. **Comparaison fonctionnelle Low/Medium/High/Ultra PASS**
sur les snapshots actuels identiques. Cela ne clôt pas les autres critères de Gate 1.

PNG Low et retour High 1920 × 1080 inspectés : vallée, pont, eau, forêt présents ;
nuages et portée/densité varient avec le profil. Caméra aérienne native distincte
de la caméra joueur Studio : pas d'équivalence pixel. Les trois autres PNG sont
acquis/hashés, pas présentés comme inspectés en détail. Cadences d'overlay non
retenues comme benchmark. Aucun verdict de fluidité ni acceptation humaine ajouté.

Sortie 0, aucun événement ERROR, deux processus 17196/10756 fermés, zéro survivant.
SHA-256 : runtime `f7b39f7536dbcfcc8180daf6ac614e96b9804633f4cd2f5524fc5258ad27bd5d`,
JSONL `bb00ac73550ba97ab15cc0a6ca81a6ed28f5af48a9c3258cf64e144433cb202c`,
nettoyage `4a4c3cf5e3d90d733927b838136cd5ecd0f74b2453310b07b085c14a003bb3f8`.
Les cinq hashes PNG et paramètres complets figurent dans `verification.json` et
`clear-cycle/arguments.json` sous cette racine. Gate 1 ouverte, aucune publication.

### Complément Studio Medium/Ultra du candidat avec chevauchement

Même EXE `8f1e2003…13d90`, frontend et kit que le cycle mobile réussi, sans rebuild.
Le parcours au démarrage a de nouveau affiché PASS, puis Medium et Ultra ont été
sélectionnés par l'interface ; deux JSON et deux PNG 1408 × 966 acquis. Vérificateur
exécuté : zéro dérive des 284 sources, identités GPU/application/plan valides,
séquences 9 puis 11 croissantes, sept domaines projetés, inventaires et effets prêts,
aucun pipeline en attente/échec, mêmes cinq groupes de réglages que le natif.
Six contrôles négatifs de reçus et trois contrôles f32 PASS. Les deux PNG ont été
inspectés : forêt et avatar présents, densité d'herbe supérieure en Ultra, tramage
visible sur les arbres Medium ; eau hors champ. JSON/PNG successifs, non atomiques.

| Export actuel | SHA-256 |
| --- | --- |
| Medium JSON | `04d42335133e2ac2f111a00eba72ba5ed933177c0ff9ace522ca675089dd37ee` |
| Medium PNG | `e4bb8e95e1d6959fdd9ff0f2830384d21adc06c60abebc593ceb33b4d9982347` |
| Ultra JSON | `5cb3595d0895060ed0c01c07b5bbdf7f91b34328299946975147e7802b99c067` |
| Ultra PNG | `041bafa78b1f8db8bc0b687a03588a36e1461cf044af5e5d2cc951d969e8f29d` |

Sortie 0 et huit processus Studio fermés, zéro survivant ; nettoyage SHA-256
`e9600dcfe0284e39e23184518ebb71360882903c931fd86ca4639762fe51e0e7`.
Pas d'équivalence de pixels/caméras, benchmark, revue humaine ou révision propre
déduits de la parité fonctionnelle. Le dossier final doit référencer ces nouvelles
preuves, sans substituer leurs captures claires aux anciennes acquisitions pluie.

## Complément Medium/Ultra — 11 septembre, 17:59–18:01 locales

**Comparaison fonctionnelle des quatre niveaux acquise sur les candidats épinglés.**
Medium et Ultra ont été sélectionnés dans le Studio existant après son cycle
préchauffé terminé. Deux diagnostics JSON et deux PNG 1408×966 exportés par
l'interface, sans reconstruction, changement moteur ou asset. Les paragraphes
ci-dessous décrivent l'acquisition initiale High/Low, désormais complétée.

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-medium-ultra-20260911/`
contient Run.ps1, Verify.py, les exports, journaux, cleanup.json et verification.json.
Vérificateur exécuté avec succès : pour Medium et Ultra, bindings des cinq
sources, hash du profil, réglages des sept domaines, substitutions et contraintes
de berge concordent avec leurs observations natives. Identités GPU/application/
plan cohérentes, travail GPU terminé, inventaire et effets prêts, aucun pipeline
en attente ou échoué au moment des exports. Six contrôles négatifs de reçus et
trois garde-fous numériques passent ; les deux champs f32 sont comparés bit à bit.

| Export | SHA-256 |
| --- | --- |
| medium.json | `99b63986b2947598d335b9380cc55c25670092cf8853fb26cd71ac2f9f617fe4` |
| ultra.json | `3be8c6c3dff53c0a6b3a2eb0bce91de8ba48bd704a1b278b42ce5544e2636cc8` |
| medium.png | `ff06114404d0d21a881b900725f7450aea6c6ac4a0b73fc644c838b80dbd05e9` |
| ultra.png | `5fd03dedfeb9d1406751c56e022b39f1762eb4cf07e50e2f9ed6710a1d0d8e17` |

PNG effectivement inspectés : même chemin forestier et avatar, végétation plus
dense en Ultra, tramage visible notamment sur les troncs Medium. Eau hors champ :
ses réglages sont comparés, pas son aspect dans ces images. Caméra native aérienne
et Studio joueur différentes, pas de parité pixel ou de qualification de fluidité.
JSON et PNG sont deux acquisitions successives, non atomiques.

EXE Studio `814d9f6d…77ec9`, frontend complet et preuves natives rehashés sans
changement. Les snapshots des deux builds restent identiques ; **281/283 fichiers
correspondent au worktree actuel**, deux différences explicitement enregistrées :
README moteur et module de test commit_probe.rs modifiés par la sonde suivante.
Ne pas prétendre à une nouvelle révision propre ni réécrire les pins historiques.

Le contrôle d'interface a d'abord échoué (« coordinate input geometry is
unavailable ») ; nouvelle sélection/activation du Studio puis sélection réelle
des boutons visibles. Aucun clic envoyé à l'autre application apparue dans une
capture. Sortie Studio 0, huit processus possédés fermés, aucun survivant :
9996, 28864, 27912, 30232, 15508, 25656, 30500, 4080.
Gate 1 ouverte ; réserves temporelles/optiques et révision finale restent dues.

## Acquisition initiale High/Low

**Parité fonctionnelle bornée acquise, Gate 1 ouverte.** Aucun changement moteur
dans ce lot : les 710 tests et Clippy natif/Web du lot préparation bornée restent
applicables aux 283 fichiers source revérifiés. Ils ne sont pas annoncés rejoués.

## Acquisition native

Build offline `cargo build -p forge_runtime --locked --offline`, profil dev
optimisé du cache existant, 1 min 46 s. Préparation puis cycle global
High/Low/Medium/Ultra/High avec forêt batches, eau/reflets et nuages : cinq
captures vérifiées par le runtime, cinq observations avec reçu GPU courant,
sortie 0 et aucun événement ERROR. Préchauffage Low/Medium/High/Ultra/High en
15,8105121 s ; pipelines 145/146/156/156/157. Pas de qualification FPS.

Le runtime utilise directement les neuf fichiers du kit actuellement embarqué
dans Studio ; aucune copie/dérivation d'asset. Sources et entrées rehashées
avant/après. Pas de caméra joueur ni parcours automatique dans cet essai natif :
la preuve native est un cycle statique, distinct du mouvement Studio déjà acquis.

## Comparaison au candidat Studio actuel

Référence : `r1-preparation-budget-20260911/studio-motion.json`, code WASM et kit
épinglés par son build-proof. Les snapshots de 283 fichiers sont identiques,
et leurs octets correspondent toujours au worktree courant. Pour chacun des
trois enregistrements Studio High/Low/High, comparaison au niveau natif correspondant :

- cinq bindings terrain/végétation/environnement/eau/météo identiques ;
- hash du profil source identique ;
- réglages effectifs des sept domaines concordants ;
- substitutions explicites et contraintes des berges identiques ;
- reçus GPU natifs valides et retour High restauré.

Premier comparateur décimal exact refusé sur `3.2` natif et `3.200000047683716`
Web. `EffectiveWaterSettings.refraction_max_depth_m` est un `f32`, confirmé dans
`forge_project/src/effective_quality.rs`. Comparaison corrigée **bit à bit en f32**
pour ce champ et `vegetation.density_multiplier` (également déclaré f32), sans
tolérance numérique générale. Trois garde-fous exécutés : round-trip accepté,
valeur différente et NaN refusés. Autres champs comparés exactement.
Le premier refus n'était donc pas une différence de réglage, et aucun réglage
produit n'a été modifié pour rendre la comparaison verte.

Les cinq niveaux natifs sont acquis ; la comparaison directe avec le nouveau
Studio porte sur High/Low, seuls niveaux enregistrés en détail par son cycle
mobile. Le préchauffage Studio couvre ses cinq variantes mais ne fournit pas
ici de nouveaux plans détaillés Medium/Ultra. Ne pas étendre cette paire à une
comparaison de tous les niveaux, des pixels ou des performances.

## Inspection et limites

PNG natifs 1920×1080 Low et retour High inspectés : vallée, pont, eau, forêt
présents ; nuages et densité/rendu distant varient avec le niveau. Les vues sont
aériennes, contrairement au Studio en caméra joueur 1408×966. Ces images ne
prouvent pas une identité optique ; pas de comparaison pixel ni de nouvelle
acceptation artistique. Les trois autres PNG sont acquis/hashés, pas annoncés
inspectés en détail dans ce lot. Les cadences glissantes de l'overlay ne sont
pas un benchmark ; les réserves temporelles et la dérogation point 2 restent séparées.

## Preuves et nettoyage

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-native-current-parity-20260911/` :
Build.ps1, Run.ps1, Verify.py, journaux, sources épinglées, cinq PNG et
`verification.json` conservés. Statut `functional_current_sources_parity_pass`,
`pixel_equivalence_verified=false`, `performance_qualified=false`,
`clean_revision_verified=false`, `gate1_pass=false`.

| Artefact | SHA-256 |
| --- | --- |
| runtime.exe | `20a9b1707e164d109408f864c62aef0295e7b8272cf3f46151ea37ada2786e6b` |
| clear-cycle/runtime.jsonl | `61d51e1fba909fdad4c7ee53cddf0038a9bf6e0e340ad056795147bff874029c` |
| PNG retour High | `cd427915063a3f6b0377ca81d6fb14f9ed236264c9bee4ab181c1e41872a9549` |
| PNG Low | `9e2353ceb5e3b21bed4e7c1e2c45993a4b76bfb6576759ee56e50ed3c775116a` |

Processus possédés 32820 et 15888 fermés ; reçu de nettoyage sans survivant.
Aucun Studio/navigateur supplémentaire lancé. Branch Rust, HEAD de base
`f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié préservé. Aucun commit,
push, suppression, asset Cadle ou passage de gate. CI distante NOT RUN.
