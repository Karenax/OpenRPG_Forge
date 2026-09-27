# R1 — Variantes de forêt gouvernées : préparation

7 septembre 2026, branche Rust, Gate 1 ouverte. Continuité d'ADR-0010 : mêmes
placements canoniques et modèles gouvernés, pas de taxonomie ou de second scatter.

## Écart traité

Le catalogue v1 n'autorise qu'un GLB par couche ; la forêt candidate répète ainsi
le même arbre. Une diversification ne doit pas être une retouche manuelle de la
carte de preuve, ni dépendre du rang dans un batch ou du niveau de qualité.

## Implémenté

Le validateur moteur-neutre accepte une v2 explicite, avec couverture des couches,
paires couche/asset uniques, mêmes exigences de provenance/hash/dimensions, et
64 liaisons maximum. La v1 et son format sérialisé restent inchangés.
Le choix déterministe à poids égaux utilise le score SHA-256 canonique du tuple
`("forge.vegetation-prototype-choice.v2", layer_id, placement_id, asset_id)`.
Le score maximum gagne ; égalité départagée par l'identifiant d'asset.

Réordonner la liste ne change pas le choix. Le hash GLB, pivot et dimensions ne
sont pas dans le score : un réimport ne redistribue pas les modèles. Ajouter un
modèle ne peut déplacer un choix que vers ce modèle ; le retirer restaure les
anciens choix. Cela ne garantit pas la silhouette ni la conservation d'un futur
override manuel, dont le contrat appartient à Gate 2. Aucune pose n'est copiée.

Le renderer refuse explicitement v2 tant que caches, LOD, vent et projection ne
consomment pas ce choix. Cela empêche un écrasement silencieux par la dernière
liaison d'une même couche dans les caches actuels. Aucun catalogue candidat ou
asset existant n'est modifié ; aucun nouvel arbre affiché revendiqué.

## Vérifications ciblées

Cinq tests du module PASS : round-trip et anciens refus, v1 monomodèle inchangée,
v2 multivariantes, rejet des paires dupliquées, choix sur 256 identités, permutation,
réimport simulé, ajout et retrait de variante. Première passe Clippy a signalé
un emprunt inutile dans le test ; correction sans désactivation de lint.

Après correction : `cargo clippy --workspace --all-targets --all-features -- -D warnings`
PASS, `cargo test --workspace --quiet` terminé avec 596 PASS / 0 FAIL / 1 timing
déjà ignoré. `cargo fmt --all --check` et `git diff --check` PASS. Ces résultats
portent sur les contrats et régressions locales, pas sur une projection v2 ou un
nouveau rendu Web. Aucun build/capture supplémentaire justifié pour un aperçu v1 inchangé.

## Suite requise

Raccorder la même sélection aux caches partagés et aux trois LOD en natif/Studio,
sans recalcul par frame ni duplication de collider. Vérifier dimensions/enveloppe
des variantes gouvernées réelles, stabilité qualité, captures et coût comparatif.
Ne pas annoncer une forêt diversifiée à partir des seuls tests de sélection.
Les modèles candidats et la revue artistique ne sont pas promus par ce contrat.

## Raccordement moteur suivant — implémenté

Clés de caches distinctes par variante, catalogue validé transmis au cache de vent,
même sélection pour les trois LOD lors de la reconstruction de projection. Aucun
hash de sélection calculé par frame à état inchangé ; accès v1 direct conservé.
Les meshes/PBR restent normalisés et partagés ; les groupes lointains consomment
les primitives réellement choisies. Pas de collider ajouté pour un modèle.
Le refus transitoire de v2 est retiré après ce raccordement. Aucun catalogue existant
migré automatiquement ; l'activation reste l'option explicite de forêt candidate.

Test ECS ciblé PASS : deux handles de modèles, 650 placements, choix cohérent aux
trois LOD, cycle High/Low/High, poses exactes, nombre de meshes constant et 650
colliders conservés. Une clé de modèle absente est refusée. Caches synthétiques
injectés pour ce test ; il ne remplace pas la résolution GLB réelle.
Clippy workspace strict PASS après extraction du contrôle d'échec de chargement
et correction des remarques de lint, sans désactivation supplémentaire.

Candidat de preuve local préparé sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-variants-20260907/` : même kit,
carte et vue qu'auparavant, catalogue v2 séparé utilisant les deux GLB existants.
Pour le conifère : nodes LOD0 sans transform additionnel, min Y -0,051885948 m,
max Y 18,450782776 m, hauteur déclarée 18,502668 m et cible 5,65 m. Ces valeurs
proviennent des accessors lus ; l'adapter confronte ensuite les sommets réels.
L'enveloppe physique et la revue artistique restent à qualifier, aucune promotion.

## Première projection native v2 vérifiée

`Run.ps1` du dossier ci-dessus : build natif puis lancement High, RX 7800 XT,
1920×1080, vue human_bridge, kit/forêt regroupée, sans nuages expérimentaux.
Sortie 0, aucun événement ERROR/panic trouvé. Les entrées épinglées sont vérifiées
inchangées après exécution. Deux GLB existants chargés et normalisés par le chemin
commun ; les données de carte et les anciens catalogues ne sont pas écrasés.

Capture `native/clear.png` ouverte et inspectée : chênes et conifères répartis dans
le scatter, diversité de silhouette visible ; le conifère proche paraît sombre.
SHA-256 `1e8335c5756c2237756e8f770bd2088a94d882edc9334d5501099291ecc892a7`.
Binaire conservé SHA-256
`69aae8b071684e9e311ccfb5f81bb1847ce8de2a3ed2cf9ef23d1000f9305c0b`.
Kit : 36 colliders prêts ; forêt : 650 arbres, 7 800 primitives LOD. Ces compteurs
ne sont pas des draw calls GPU. Avertissements wgpu `Equal`/`invariant` conservés ;
ils ne sont pas assimilés à des erreurs nouvelles ou à une preuve d'absence d'artefact.

Smoke avec capture : 64,25 FPS moyens, p95 18,015 ms, 1 % low 35,51 FPS,
239 échantillons. Pas de comparaison contrôlée ou de qualification sur GPU cible.
Web de cette v2, coût/mémoire comparatifs, silhouettes et enveloppe physique
restent à qualifier. La diversification fonctionnelle ne clôt ni l'art ni Gate 1.

Capture émise à la frame 353, callback 356, 116/116 pipelines prêts et huit
observations stables. Contrôles finaux : 597 tests workspace PASS / 0 FAIL /
1 timing déjà ignoré ; Clippy workspace strict, format et diff PASS.

## Rejeu Studio Web v2 — 7 septembre 2026

Clippy Studio/Web strict et Trunk PASS. Avertissement transitif de compatibilité
Rust future `proc-macro-error2 2.0.1` conservé. Bundle local `web/` servi sur 1453,
WASM SHA-256 `684a4d6a87a935409e39cb4106c0c5dce6407458b20cc5ac5e8bb48ef0ff0d0d`.
Catalogue v2 déployé identique au candidat natif, SHA-256
`5d75a1e7f1778de5352593fef92351c3651104da3383c6c25d2fb007377097b4`.
Les six GLB copiés sont vérifiés contre les hashes du rapport gouverné.

Chrome lancé sur la vallée WorldSketch, vue human_bridge candidate, kit gouverné
et forêt regroupée. Un pas de 250 ms initialise la physique, puis temps figé.
Kit 20/20, 36 colliders prêts, 650 arbres et 7 800 primitives LOD. Les deux captures
High initial/restauré sont ouvertes et inspectées : les deux silhouettes restent
visibles, conifère proche sombre. 1408×741, pas Full HD. Aucun ERROR console observé.

Cycle High → Low → High par boutons, avec trois diagnostics exportés et relus :

| Observation des meshes réels | High initial | Low | High restauré |
|---|---:|---:|---:|
| Entités mesh | 8132 | 6997 | 8132 |
| Meshes uniques | 654 | 654 | 654 |
| Triangles instanciés tous LOD | 36052148 | 34525180 | 36052148 |
| Triangles uniques | 3077324 | 1668468 | 3077324 |
| Triangles visibles CPU | 1600094 | 871252 | 1600094 |
| Meshes non comptables | 0 | 0 | 0 |

Les six compteurs initiaux sont restaurés ; hashes terrain/végétation et temps
0,250 s conservés, kit prêt pendant le cycle. Ce sont des observations CPU, pas
des draw calls. La sonde IBL signale ses textures/caméra remplacées après qualité :
aucune qualification IBL, équivalence pixel parfaite ou profil intégral déduite.
Pas de mesure de fluidité, de coût comparatif ou de Tauri/WebView2 par ce rejeu.

Fichiers conservés à la racine du dossier de preuve avec copie vérifiée :

| Fichier | SHA-256 |
|---|---|
| forge-viewport-1788796310988.png | bef19ddd4758d0eaaceb056602f869b4afbd17bb56970b6a2388297cecc06e07 |
| forge-viewport-1788796385712.png | a627411f89a98bc65061e9d6ea6bde809d8569e2349241cad13604d62db44906 |
| forge-diagnostic-1788796312105.json | bced78debbcc9e22b530657fa47ba67adad9ebad68e54cd1d3a05b776e7e2971 |
| forge-diagnostic-1788796345345.json | 1e56a90d586168f7d9044018f55991d74c832ce0ca0dc965aab74fd6767fa124 |
| forge-diagnostic-1788796387811.json | 02a0a502d742fdc27880c09b53d911607118322e0583889e97b2f5042faa6d3f |

Aucun code supplémentaire modifié pour ce rejeu. Les 597 tests précédents restent
la preuve de cette révision ; pas de nouvelle suite globale inutile. Gate 1 ouverte.

## Coût natif du catalogue mixte — comparaison bornée

`Compare.ps1` exécuté intégralement : ordre A1/B1/B2/A2, A = catalogue v1 chêne,
B = catalogue v2 chêne/conifère. Même exécutable conservé, entrées communes épinglées,
High 1920×1080 sans VSync/adaptation, météo claire, forêt regroupée, 420 frames de
préchauffage puis 1 199 échantillons par passage. Aucun build ou capture simultané.
Sorties 0, aucun ERROR/panic trouvé ; hashes inchangés contrôlés après chaque run.
Résolution, tier High, adaptation disabled, kit prêt et hashes terrain/végétation
identiques relus dans les quatre JSON. Aucun nouveau shader ou réglage modifié.

| Run | Catalogue | FPS moyen | p95 ms | 1 % low FPS |
|---|---|---:|---:|---:|
| A1 | chêne | 64,14 | 17,3854 | 29,77 |
| B1 | mixte | 60,23 | 18,2327 | 30,35 |
| B2 | mixte | 59,48 | 17,9958 | 29,43 |
| A2 | chêne | 61,70 | 17,3964 | 30,96 |

Paires A1/B1 et A2/B2 : perte de FPS moyens 6,09 % puis 3,59 % ; p95 plus élevé
dans les deux cas. Allocations backend : environ 1 024 Mo pour A contre 1 112 Mo
pour B, soit **84,22–84,29 Mio supplémentaires**. Réservation identique à
1 451 170 148 octets ; ce relevé n'est ni la VRAM résidente ni la mémoire totale.
Primitives opaques GPU observées : environ 605 250 contre 871 470. Cela indique
une charge géométrique supérieure, pas une attribution exhaustive des timings CPU/GPU.

Limites : deux paires courtes, RX 7800 XT locale, build dev opt-level 1, poste non
isolé et onglets Studio préexistants non fermés ; pas d'extrapolation au matériel
cible, pas de significativité statistique revendiquée. Les variations A1/A2 et les
1 % low insuffisants restent visibles. Aucun des catalogues ne passe les seuils finaux.

Décision technique : conserver le support générique de variantes, mais ne pas promouvoir
ce catalogue mixte comme candidat final de performance. Réduire/qualifier la géométrie
des dérivés, notamment lointains, et améliorer le conifère sombre avant promotion.
Ne pas masquer ce surcoût par un seuil relâché ou multiplier les mêmes smokes inchangés.
La v1 et les sources d'assets restent intactes ; Gate 1 reste ouverte.

Preuves locales `comparison/{a1,b1,b2,a2}.{json,log}`, `Compare.ps1`, pins vérifiés.
`comparison/summary.json` SHA-256
`342db6a944a3a019f6b201ecf79c60d64f11a2baf73cfdf6e8ba64eea6b611b3` ;
`comparison/pins.json` SHA-256
`6ada85f502ff9f3dcfd90d18fe19aab31f3897ac2a48f4315bf55b4a24721a21`.
