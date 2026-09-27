# R2-024 — points de croisement authorés

13 septembre 2026, branche Rust, worktree courant modifié conservé. Aucun commit,
push, asset externe ou nouvelle dépendance. Gate 2 reste ouverte.

## Implémenté

`forge_world::path_intersections::prepare` produit une seule `ZoneRequest::Edit`
sur le hash et la révision exacts de la zone. Le Studio expose « Examiner les
points de croisement » dans le panneau chemins : PreviewZone, diff des points,
aperçu de surface existant, puis Apply/Undo/Save normaux. Aucun writer alternatif.

- Croisements transversaux, jonctions en T, plusieurs tracés au même point et
  auto-croisements non adjacents : insertion ordonnée de contrôles communs X/Z.
- Points antérieurs, noms, IDs, profils largeur/accotement et autres corrections
  conservés ; pas de snapping de routes proches, pas de modification de relief.
- Extrémité existante conservée exactement ; doublons de points insérés éliminés.
- Verrou revalidé par `prepare_path` pour chaque chemin réellement modifié. Une
  branche verrouillée déjà terminée sur le croisement ne subit aucune modification.
- Superposition colinéaire positive refusée, sans fusion ambiguë ; contrôles
  insuffisamment espacés et surfaces repliées refusés par la projection existante.
- Maximum 128 points par chemin, 1 000 000 paires examinables ; refus complet
  avant commande en cas de dépassement, sans résultat tronqué.
- Les points acceptés sont des corrections existantes de chemin : aucun nouveau
  format d'archive, aucune migration implicite ni réseau canonique parallèle.

## Vérifié

`cargo test -p forge_world --test path_intersections --locked` : **5 PASS**, 0,01 s
d'exécution. Cas X/T/multiples/self, préservation, absence de snapping, verrou
modifié ou intact, colinéarité, quotas, Preview sans mutation, un reçu/Undo,
retry idempotent, Redo, régénération avec nouvelle seed, relecture intégrale,
compacte et delta, nouvelle commande sur ancienne révision refusée.

Clippy world/lib/test strict PASS ; Clippy Studio WASM strict PASS (35,47 s).
L'avertissement transitif de compatibilité future `proc-macro-error2 2.0.1` reste
visible, sans désactivation. Formatage et diff contrôlés. Premier build de test
corrigé pour appeler l'API réelle `from_archive_bytes`; assertion de retry alignée
sur le contrat existant : ancien reçu retourné sans mutation, pas réapplication.

## Prévu / non vérifié

Ce lot insère des **ancres éditables** : il ne résout pas encore la fusion des
rubans/accotements au carrefour ni leur superposition coplanaire. Il ne crée pas
de lien durable entre les déplacements futurs des points ; pas de graphe de
navigation, priorité routière, pont ou jonction hydrologique implicite.

Prochain travail R2-024 : surfaces de jonction dérivées communes et raccord aux
largeurs/accotements, sans doubles surfaces ni perte de retouches. Puis parcours
graphique regroupé du candidat ; le bouton présent dans le source n'est pas
encore observé. Le candidat B2E07599 rivière conservé précède ce lot.

Aucune qualification graphique de ce changement, aucun R2-024 coché, aucune
qualification de performance déduite de la durée des tests CPU.
CI distante : **NOT RUN — infrastructure billing**.

## Suite — union des surfaces et des accotements

Implémentation ajoutée après le lot d'ancres : `path_surface::project` partitionne
maintenant les triangles recouvrants, y compris entre chemins et aux
auto-croisements. Chaque fragment conserve la couverture alpha maximale des
rubans d'origine ; en cas d'égalité, l'ordre stable ID/face départage. Un
accotement ne remplace donc pas le cœur opaque d'un autre chemin. Les IDs et les
contrôles restent inchangés ; des chemins entièrement couverts peuvent avoir
une géométrie dérivée sans indices, sans suppression de leur recette.

Les intersections sont calculées en f64 dans X/Z, puis quantifiées au format
f32 du renderer. Les triangles dégénérés après quantification ne sont pas émis.
Nouveaux sommets : hauteur et normale du terrain effectif, surélévation visuelle
historique de 2 cm, aucune correction de terrain enregistrée. Les anciens
sommets sont réutilisés. Le résultat complet est préparé avant remplacement.

Index spatial à cellules de 2 m, quotas explicites : 4 000 000 entrées,
10 000 000 comparaisons, 256 fragments par face, 250 000 sommets et 1 500 000
indices. Dépassement = erreur, pas de surface partielle. Aucun nouveau moteur,
shader, asset, stockage canonique ou dépendance. Le raccord existant Studio
consomme cette projection pour l'aperçu et l'état accepté ; pas de voie spéciale.

### Preuves CPU

4 nouveaux tests `path_union` PASS (0,02 s), 9 régressions `path_surface` PASS
(0,12 s) et 5 tests `path_intersections` PASS (0,02 s), compilation 6,03 s.
X/T : aire analytique de l'union ; grilles de points hors arêtes : une seule
couverture et alpha maximal attendu. Autres cas : oblique à largeurs différentes,
superposition colinéaire, doublon exact, auto-croisement, terrain retouché,
déterminisme et source inchangée. Les tests ne constituent pas une preuve GPU.

Première exécution : deux échecs de recouvrement. Cause corrigée : un sommet
dupliqué par le découpage créait une arête nulle, dont les deux demi-plans
retenaient toute la face. Déduplication consécutive et fermeture de polygone
normalisées ; tests inchangés réussis, instrumentation temporaire retirée.
Clippy world/lib/test strict PASS (1,93 s), Studio WASM strict PASS (25,66 s).
Formatage/diff vérifiés ; aucun processus `forge-studio.exe` encore présent.
L'avertissement transitif de compatibilité future reste signalé sans désactivation.

### Limites restantes

La fusion visuelle est **codée et vérifiée géométriquement sur ces cas**, pas
encore observée à l'écran. Restent notamment le rendu WebGPU, les jonctions
sur relief accidenté à fort grossissement (jointures/quantification), le coût
sur de grandes collections et le parcours auteur regroupé. Les quotas ne
constituent pas une qualification de réactivité UI. Aucune fenêtre graphique
ouverte après l'interruption utilisateur. R2-024 et Gate 2 restent ouverts.

## Qualification locale finale — 21 septembre 2026

Cette section complète les constats historiques ci-dessus. `R2-024` est
**qualifié localement** ; elle ne clôt pas Gate 2, dont `R2-025` reste ouvert.
Le worktree Rust modifié a été conservé. Aucun commit, push, asset externe,
nouvelle dépendance ou publication n'a été réalisé.

### Implémentation retenue

Les nouvelles ancres d'un chemin profilé conservent maintenant le profil
vertical : leurs altitudes sont interpolées longitudinalement pendant la
préparation des intersections. Les sommets issus du découpage d'une surface
profilée interpolent leurs attributs de face (hauteur, normale et couleur),
sans seconde surélévation. Les chemins non profilés continuent de suivre le
terrain. Les refus restent atomiques : un profil incomplet ou non fini ne
modifie ni chemin ni archive.

### Validation locale Luna max

- `cargo fmt --check` : PASS.
- `cargo test -p forge_world --test path_intersections --locked` : **8/8 PASS**.
- `cargo test -p forge_world --test path_union --locked` : **6/6 PASS**.
- `cargo test -p forge_world --test path_surface --locked` : **10/10 PASS**.
- `cargo test -p forge_engine authoring_paths --locked` : **2/2 PASS**.
- Clippy strict `forge_world`, `forge_engine`, Studio natif et Studio WASM :
  PASS. Seul l'avertissement de compatibilité future du tiers
  `proc-macro-error2` subsiste, sans désactivation.

Le candidat auteur final est
`artifacts/forge-runtime/gate2-resume-20260919/r2-024-validation-20260921-safe-anchor-authoring-final` :
`forge-studio.exe` SHA-256
`68AEA50FBB9F7A4DD6CDE27F9EFCDE7C1150780AE908DB9B87A5A7B7C3682F5A` ; WASM
SHA-256 `DB091055F4B3B4E74903E0DF152347D167540130427F67B4F31CDE06E013FD7D`.

### Qualification graphique et parcours auteur Luna max

La preuve visuelle a été inspectée dans la fenêtre native par Computer Use via
`@oai/sky`. Aucune PNG locale n'a été produite par ce parcours ; les constats
graphiques ci-dessous reposent sur cette inspection directe.

- La carte réelle a refusé proprement les croisements non sûrs ; l'archive
  initiale est restée à `3EF87C85`.
- Deux chemins synthétiques ont été créés par l'UI : horizontal
  `(160,160)–(180,160)` et vertical `(170,150)–(170,170)`. Preview a affiché
  44 surfaces / 91 points, sans mutation. Apply a inséré `(170,160)` sur les
  deux chemins et l'acquittement CPU/Rapier a été observé.
- Undo a rétabli deux points, Redo trois points. Save a donné l'archive
  `F5478051BBC7F5B871753603F46E6CA267DA4161E6EF4C6AFD3FBF0C8810A10D` ;
  Close/Open a relu les deux chemins et leurs trois points respectifs.
- Le playtest a rendu visiblement terrain, eau et capsule bleue, puis le retour
  à l'édition a conservé le même hash et le même `LastWrite`, preuve d'absence
  de sauvegarde implicite. La fenêtre et le processus Studio ont été fermés.

GPU, optique, navigation et physique de l'eau restent **non qualifiés**.
CI distante : **NOT RUN — infrastructure billing**.
