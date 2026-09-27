# Gate 1 — Chargement initial du rendu Studio

8 septembre 2026, branche Rust, travail local conservé, aucune publication.
**Implémenté ; chargement puis scène colorée observés dans le candidat final. Gate 1 ouverte.**

## Objectif et portée

Les deux précédents lancements Studio montraient des zones noires avant l'image
stabilisée. Bevy crée des textures d'environnement provisoires vides dans
`bevy_pbr-0.19.1/src/light_probe/generate.rs::generate_environment_map_light`,
avant que ses passes de filtrage disposent de leurs pipelines. L'existence de
handles CPU n'est donc pas une preuve de préparation du rendu.
Cette lecture identifie un risque, pas une preuve de causalité GPU exhaustive.

Le nouveau module Studio `render_warmup` observe le cache public de pipelines
dans ExtractSchedule. L'écran initial couvre le canvas mais ne l'arrête pas.
Il attend une projection d'environnement cohérente, le kit prêt s'il est présent,
et huit observations fraîches sans pipeline en attente/échec, à nombre stable.
Il distingue les attentes de chargement des shaders des erreurs définitives,
en suivant les variantes exactes que Bevy remet en file : `ShaderNotLoaded` et
`ShaderImportNotYetAvailable` (`bevy_render/.../pipeline_cache.rs`, lignes 685–709).
Les erreurs de traitement/création restent fatales. Aucune comparaison de texte
d'erreur, nouveau renderer, dépendance, texture ou modification de carte.

Le garde cesse son observation après préparation ou échec ; il ne cache pas les
changements de qualité ultérieurs. Le délai de garde est 120 secondes de
`Time<Real>`, aligné sur le watchdog de capture natif existant. Le main loop
expose le dépassement même sans observation du render sub-app ; un blocage
complet du thread navigateur n'est pas résolu par ce mécanisme.
Le ledger `observations.render_warmup` décrit les compteurs, stabilité, pic des
attentes temporaires et temps observé. `prepared` ne signifie ni lecture des
pixels, ni fence GPU, ni acceptation graphique. Les erreurs de scène existantes
restent affichées au-dessus du garde. Gate 0 sans vallée n'utilise pas ce module.

## Essais rejetés, conservés

1. `r1-studio-render-warmup-20260908` : premier garde trop strict sur les erreurs
   temporaires. UI : 55 pipelines, 34 en attente, 19 classés en erreur ; capture
   `first-failure.jpg`, SHA `91458BB5F503070BD47393016982DC6B5ED806085A1E2625403792FC3DA7D9E2`.
   Exécutable SHA `32E3984D83DE8B5B2601FBA1A28EC0973BF698F1D72760120D2156564A70AA9C`.
   Une tentative de sauvegarde PNG de la capture UI JPEG a été refusée sans
   écrire de fichier ; le format JPEG réel a ensuite été conservé.
2. `r1-studio-render-warmup-final-20260908` : classification corrigée, mais limite
   initiale arbitraire de 30 s trop courte. Export réel à 33 036,7 ms : 8
   observations, 98 pipelines, 0 en attente, 0 erreur fatale, 4 observations
   stables, pic de 19 attentes temporaires. La condition de scène était satisfaite,
   contrairement à la première hypothèse après le timeout. Export source
   `forge-diagnostic-1788886476439.json`, copie `timeout-diagnostic.json` sur E:.
   `timeout.jpg` et `loading.jpg` conservés. Exécutable SHA
   `134E08EB67EA49B66E24CAEC0EC1A6684060902115CD5375C7B446ABA103E5F9`.

Tous les chemins ci-dessus sont sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/`. Le nom de dossier « final » n'est
pas un verdict de réussite. Les huit observations ne sont pas réduites ;
le watchdog est raccordé au budget existant de 120 s, pas à une qualification
de performance. Le temps de chargement lui-même n'est pas accéléré par ce garde.

Processus du premier essai 32460 / 25884 / 45364 / 37432 / 14536 / 10620 / 31756 /
39976 fermés ; ceux du second 45824 / 35944 / 39428 / 40140 / 41524 / 19884 /
16860 / 37396 également absents au contrôle CIM. Pas de navigateur utilisateur tué.

## Contrôles du code final

Quatre tests couvrent attente de scène, huit observations fraîches, nouvelles
queues, récupération des attentes temporaires, erreurs définitives, absence
d'observation, délai maximal et le scénario réel à 33 secondes.

Sous `r1-studio-render-warmup-verified-20260908`, logs complets :

- `cargo test --locked --workspace` : **635 PASS, un timing préexistant ignoré**.
- Clippy strict workspace tous targets/features : PASS.
- Clippy Studio WASM sans defaults, feature web : PASS.
- Formatage : PASS. Rust/WASM release : PASS (27,02 s), optimisation/assemblage
  terminés avec code 0 dans la session 75403.

Le troisième candidat a affiché l'écran de préparation puis la scène colorée,
sans les zones noires initiales observées auparavant. Captures UI JPEG conservées
dans cette même racine : `loading.jpg`, `ready.jpg`, `owner-exploration.jpg`.
SHA-256 respectifs :
`AB2A6BF8A5AAFC79D608DB9656EA20000D9163874C429AD6AE031F850C98D19F`,
`C79FA1B2FA7D6ABECF80B56AE9DF33DB49E3464DA856C18DD0C513DD77D40519`,
`417AAD2E29313370742D660EB96F39CCE4ADD51767E7528330F7297A10888C3B`.
WASM SHA-256 `9FBAC1810D616920B614D07AF6B564C7866EBE338A3C958B65DDE686FEA8F9D9` ;
EXE `050386991E8CD179816286BF9CF6D2157BAF7446D3D9067A1BBD47ABF4D2F376`.
Le propriétaire a ensuite pris les commandes et rejoint le pont. Le Studio
PID 39624 lui a été laissé : ce n'est pas un processus de test oublié à tuer.
Le diagnostic JSON final n'a pas été exporté par l'agent après cette prise en main.
Ces images prouvent la transition observée, pas un délai exact, une fence GPU,
une qualification exhaustive des démarrages ni une acceptation artistique.

Cache/temp sur E:, deux jobs Cargo et deux cœurs Binaryen. Warning futur connu
`proc-macro-error2 2.0.1` conservé. Tests synthétiques d'échec, pas injection d'une
vraie erreur shader fatale dans le candidat final. CI distante NOT RUN sous ADR-0006.
