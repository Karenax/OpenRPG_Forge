# Gate 1 — aperçu de forêt gouvernée, 2026-09-06

Statut : **implémenté en option native, smoke réussi ; résultat visuel non accepté**.
Suite de `R1_PROTOTYPE_CATALOG.md`, dans le périmètre approuvé d'ADR-0010. Aucun passage de gate.

## Implémenté

`--prototype-forest-preview` exige le catalogue préparé et son kit vérifié. La préparation seule
ne change toujours pas la forêt ; omettre le drapeau conserve le mode procédural de comparaison.
L'adapter utilise les placements de la projection active, sans régénération ni document divergent.

- Retrait des composants de dessin des arbres procéduraux proches/moyens, sans supprimer leurs
  identités de liaison ni leurs colliders.
- Projection des primitives normalisées des trois LOD, avec matériaux PBR/vent partagés,
  poses et tags de vent issus des instances existantes ; AABB augmentées de la marge de déformation.
- Reconstruction des anciens lots HLOD sans arbres, en conservant arbustes et fougères.
- Exécution après réconciliation de qualité ; la vue partagée et le profil déclenchent un rejeu.
- Limite explicite de 32 000 primitives. Validation des meshes/liaisons avant mutation du rendu.
- Overlay et JSON marqués comme aperçu : `prototype_preview.procedural_geometry_counts_valid=false`.
  Les anciens compteurs géométriques/visibilité restent présents mais ne doivent pas être utilisés
  pour qualifier cet aperçu. Les compteurs séparés indiquent arbres et primitives projetés.

Le lointain utilise ici des instances partagées LOD2 avec culling individuel, **pas le regroupement
HLOD final optimisé**. Aucun `WorldAsset` complet ou collider supplémentaire par arbre.

## Vérifié

Le test ECS ajouté utilise un prototype synthétique et déroule High → Low → High : 636 arbres,
trois LOD chacun, anciens dessins retirés, IDs couverts et 636 colliders conservés. Il ne simule
pas le GPU. Le test CLI couvre l'option explicitement requise, son absence par défaut et le rejet
sans catalogue. Les contrôles existants des sources et de la qualité restent actifs.

**430 tests workspace PASS**, un probe timing ignoré ; Clippy workspace/all-targets/all-features
strict PASS, build runtime et check moteur WASM PASS. Les erreurs initiales d'import et le contrôle
de longueur du scénario ECS restent dans les logs. Le scénario ECS est conservé comme un seul
test de cycle avec justification locale de longueur, sans désactiver ses assertions.

Smoke natif final : RX 7800 XT, Vulkan, 1920×1080, 420 frames, capture à la frame 300.
Catalogue et kit : `r1-foliage-runtime-20260906`, même chêne candidat que les preuves précédentes
(GLB `f71fb7bb6b8e0b6ab9e818f23172e6a55b6763e3ef21e1435de3a2e9dc49ac50`).

- **636 arbres, 7 632 primitives**, remplacement de 1 272 dessins procéduraux proche/moyen.
- 12 meshes normalisés, 4 matériaux de vent partagés préparés en amont.
- Kit manuel toujours à 20 scènes chargées et 36 colliders prêts, indépendants de la forêt.
- Sortie 0 sans erreur runtime observée ; nouvelle branche du shader réellement utilisée.
- Capture finale inspectée avec diagnostic d'aperçu corrigé. Les arbres importés sont présents,
  mais leur feuillage devient trop clairsemé, particulièrement à distance : **rejet visuel**.
  Aucune amélioration artistique acceptée ni performance qualifiée n'est revendiquée.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-preview-20260906/`.

- `tests-final.log` SHA-256 `b5fb126c9c29743149630b260c2e5b3ad049dc9ff50eb608b60b7d43c54e8989`.
- `hero-final.png` SHA-256 `cf2390118e6e2309a7a529bfde36e602fd6ead0594c83ab0e5d4895dd7e72188`.
- `clippy-hud.log`, `build-final.log`, `wasm-final.log`, `runtime-final.log`, `metrics-final.json`.
- Le premier smoke/capture et les logs intermédiaires sont conservés, pas substitués aux finaux.

## Restant / limites

Corriger la couverture de feuillage et les silhouettes LOD à la taille réelle ; qualifier le vent
et l'ancrage GPU sur plusieurs temps, puis la mouillure et les transitions en mouvement. Une image
statique ne prouve pas ces comportements. Comparaison performance contrôlée avant/après, mémoire,
extraction et regroupement HLOD restent à réaliser ; les timings courts du HUD ne suffisent pas.

Les chunks HLOD composés uniquement d'arbres sont pour l'instant refusés explicitement. Le rejeu
recrée les entités de primitives dérivées, mais conserve les IDs canoniques ; la stabilité de leurs
Entity Bevy n'est pas promise. Hot-reload, retrait du drapeau à chaud et échec après un changement
de qualité ne sont pas qualifiés. Le scénario ECS couvre les cycles réussis, pas ces échecs.
Studio Web/Tauri et matériel cible non qualifiés. Arbre Git sale conservé, CI distante NOT RUN.
Gate 1 reste ouverte, sans promotion d'asset, publication Git ni début de Gate 2.

## Correction — lots lointains composés uniquement d'arbres

La limite de refus des chunks tree-only décrite plus haut est levée. Si le retrait des anciens
arbres laisse un lot HLOD vide, son composant de dessin est retiré et son compteur logique passe
à zéro ; l'entité et le handle de mesh restent conservés pour la réconciliation de qualité.
Les lots contenant encore arbustes/fougères suivent toujours la reconstruction existante.

Nouveau test ECS sur projection dérivée synthétique sans arbustes/fougères : 64 lots cessent
d'être dessinés, tous leurs handles restent résidents, les 636 arbres disposent toujours de leurs
trois primitives synthétiques et les 636 colliders sont conservés. Le fixture commun de test est
extrait, ce qui permet de retirer l'exception de longueur du précédent scénario de qualité.
Le cycle High/Low/High existant reste testé séparément. Le test tree-only ne qualifie pas un
hot-reload de document réel ni une animation GPU.

**431 tests workspace PASS**, un probe timing ignoré ; Clippy workspace strict et check moteur
WASM PASS. Pas de nouvelle capture ni de rebuild du binaire de démonstration pour cette correction
ECS couverte synthétiquement. Les anciennes captures restent des preuves de leur version seulement.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-empty-hlod-20260906/`, `tests.log`
SHA-256 `e73e009cf85b8e6896a5a735527362d3c49c563259ec910a19c53849f6ab2ad8`, `clippy.log`, `wasm.log`.
Les autres limites de qualification et de reprise après échec restent ouvertes. Aucun push.

## Correction — reprise procédurale après échec d'aperçu (2026-09-06)

Un échec explicite du cache de vent ou de préparation déclenche maintenant la reconstruction
procédurale via le réconciliateur de qualité existant. Les primitives gouvernées sont retirées
seulement si cette reconstruction réussit ; une absence de binding de reprise est rapportée
comme un échec, jamais comme un fallback réussi. L'aperçu cesse ses tentatives après l'erreur.
Le diagnostic persiste dans les statistiques et l'overlay, y compris après changement de qualité.

Test ECS synthétique : échec injecté après projection, avec et sans changement préalable vers Low.
Dans les deux cas, zéro primitive gouvernée restante, 1 272 dessins procéduraux proche/moyen
rétablis et 636 colliders conservés. Un changement ultérieur vers Medium conserve l'erreur.
Ce test ne qualifie ni un défaut GPU réel, ni le hot-reload, ni le retrait du drapeau à chaud,
ni une restauration dont les bindings ont été détruits. Aucun document ou asset source modifié.

**432 tests workspace PASS**, un probe timing ignoré ; Clippy workspace/all-targets/all-features
strict, `cargo fmt --all --check`, `git diff --check` et check moteur WASM PASS.
Les refus Clippy intermédiaires sont conservés ; corrections par extraction de fonctions et
`clone_from`, sans affaiblissement des contrôles. Pas de nouvelle capture ou rebuild du binaire
de démonstration : la preuve de reprise est ECS, les captures antérieures gardent leur périmètre.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-recovery-20260906/`.
`tests-persistent.log` SHA-256
`8f11765ffdc66d2de31695c42a7eb6ab92496a9d7ab4120193f1066e058f666a`,
`clippy-persistent-final.log`, `wasm-persistent.log`. Branche Rust, modifications préexistantes
conservées ; aucune publication, aucune promotion, CI distante NOT RUN, Gate 1 ouverte.

## Règle réutilisable — densité décorative et obstacles (2026-09-06)

Les 636 arbres conservés en Low ne signalent pas un multiplicateur ignoré : les couches
avec collision sont protégées par `generate_render_density`, conformément au contrat de
`R1_040_VEGETATION_RENDER_DENSITY.md`. La projection gouvernée consomme le scatter de
présentation déjà sélectionné, pas une seconde génération indépendante.

Ajout d'un test ECS avec une variante synthétique de recette, collisions retirées uniquement
dans les données du test. Aucun fichier WorldSketch, asset, preset ou comportement de production
modifié. Le cycle High → Low → Medium → High → Ultra → High vérifie :

- moins de primitives décoratives en Low et davantage en Ultra ;
- inclusion des sélections successives et poses des survivants inchangées ;
- retour High : égalité exacte des identités, positions, rotations et échelles ;
- trois LOD par arbre sélectionné, aucun doublon placement/LOD dans cette fixture à une
  primitive synthétique par LOD ; chaque pose est confrontée au scatter actif ;
- hashes de sources rapportés stables, compte de primitives cohérent et aucun collider créé.

Le test existant couvre séparément les obstacles : 636 arbres et 636 colliders conservés
sur High → Low → High. Ne pas réduire ces arbres graphiquement en laissant leurs collisions
invisibles. Les placements manuels du kit ne passent pas par ce multiplicateur.

`cargo test -p forge_engine --all-features --locked vegetation::prototype_projection::tests
-- --nocapture` : **4 PASS**, 187 tests filtrés. Clippy moteur tous targets/features avec
warnings en erreurs PASS. Erreur initiale d'inférence d'un Option de map dans le nouveau test
corrigée par un alias typé, sans suppression d'assertion. Modification limitée aux tests :
pas de nouveau build/capture GPU, ni de nouveau verdict workspace/WASM.

Log : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-density-contract-20260906/tests.log`,
SHA-256 `084226d58258f540010b6606344d826ac76b53d758629b1a2a07640e330be98f`.
Module testé : `a9ca48da63c44fb502ded4ae6cbe81fb4e314a0fb6ab749694fe867cf4bcb3c8`.
Preuve de règle de présentation, pas d'édition manuelle, de sauvegarde/régénération depuis le
Studio, de nouvelle carte qualifiée ou de performance. Gate 1 reste ouverte ; aucun push.
