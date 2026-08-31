# Gate 1 — Preuve R1-027 à R1-031 personnage et caméra

Date de validation locale : **2026-08-31**

Branche : **`Rust`**

Commit code qualifié : **`3bb82b2b802c591c5870dbad691f7e98a3e51534`**

Tree Git : **`372e4e9118096cc9f5540cfffbdf79fc74e3c72f`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL — R1-027 À R1-031 SOUS ADR-0006**

Le contrôleur personnage Rapier, les pentes et marches, la caméra troisième personne, son occlusion et
le basculement première personne sont démontrés par les tests ciblés, le replay workspace et un probe
natif borné. Gate 1 reste **OUVERTE** : cette preuve ne qualifie ni la revue humaine comparative, ni le
benchmark RTX 3060/RX 6600, ni les autres lots de Visual Valley.

## Résultat livré

- `forge_project` définit un `PlayerControllerProfileDocument` strict, versionné, sans type Bevy, dont
  le JSON canonique possède un hash stable. Les valeurs non finies, champs inconnus, versions ou
  relations invalides sont refusés.
- `forge_world` charge le même profil embarqué pour Visual Valley et en expose le hash au runtime.
- `forge_engine` projette une capsule KCC Rapier déterministe à 60 Hz. Marche, sprint, accélération,
  freinage, gravité, saut, pente, glisse, marche et snap au sol sont pilotés par le profil.
- Les commandes sont séquencées et coalescées : la dernière direction tenue gagne, le look est
  additionné, le saut utilise un OU logique et le basculement caméra conserve sa parité.
- L'ordre fixe est explicite : commande/probe → `Drive` → synchronisation Rapier → simulation →
  `Writeback` → `Observe` → caméra. Le probe ne modifie directement ni le transform du joueur ni
  celui de la caméra.
- La caméra conserve une seule entité en troisième et première personne. Son sphere cast exclut la
  capsule et les capteurs du joueur, rapproche immédiatement la caméra face à un obstacle et applique
  une récupération monotone bornée lorsque l'obstacle disparaît.
- La première personne place la vue à hauteur d'yeux, masque le mesh joueur et applique son FOV ; le
  retour troisième personne restaure la visibilité, la pose et le FOV attendus.
- Les 64 bindings terrain, l'identité backend Rapier, l'entité joueur, l'entité caméra et leurs assets
  restent stables pendant le scénario.
- Le runtime fournit `--player-camera` pour l'utilisation interactive et
  `--player-camera-probe` pour la preuve automatisée avec watchdog interne de 30 secondes.

Le lot n'ajoute aucun paquet au lockfile : il rend seulement explicite, pour le runtime, l'arête
Rapier déjà résolue dans le workspace. Il ne contient aucun asset externe ou Cadle.

## Profil canonique

| Paramètre | Valeur |
|---|---:|
| rayon / hauteur capsule | 0,35 m / 1,80 m |
| hauteur des yeux / pivot caméra | 1,62 m / 1,50 m |
| vitesse marche / multiplicateur sprint | 5,0 m/s / 1,6 |
| accélération / freinage | 24 / 32 m/s² |
| gravité / impulsion saut | -24 / 8 m/s |
| marche / snap sol | 0,40 m / 0,25 m |
| pente franchissable / glisse | 45° / 50° |
| boom troisième personne / rayon collision | 4,50 m / 0,18 m |
| récupération caméra | 8,0 m/s |
| pitch / FOV troisième-personne / première-personne | ±70° / 60° / 75° |

| Objet | SHA-256 |
|---|---|
| profil joueur/caméra canonique | `941eceefb29835c0949a9801f83f421839bcb5b0cd79583899c22d6832a74f90` |
| document terrain Visual Valley | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` |

## Validation automatisée locale

Environnement : Windows 11 Professionnel 10.0.26200, AMD Ryzen 5 7600, AMD Radeon RX 7800 XT /
Vulkan, `rustc 1.98.0 (88d9e12ae 2026-08-18)`, Cargo `1.98.0`, hôte
`x86_64-pc-windows-msvc`. Cargo a été exécuté séquentiellement avec un profil mémoire réduit.

| Contrôle | Résultat observé |
|---|---:|
| `cargo fmt --all -- --check` | PASS |
| `git diff --check` | PASS |
| `cargo clippy -j1 --workspace --all-targets --all-features -- -D warnings` | PASS |
| `cargo test -j1 -p forge_engine --lib -- --nocapture --test-threads=1` | **48/48 PASS** |
| tests `forge_runtime` | **23/23 unitaires + 1/1 intégration PASS** |
| `cargo test -j 1 --workspace --all-targets --locked -- --test-threads=1` | **135/135 PASS** |
| `cargo build -j1 -p forge_runtime` | PASS |
| check `forge_engine` WASM, features web, lockfile figé | PASS en **16 min 59 s** |
| Clippy Studio WASM, features web, lockfile figé, `-D warnings` | PASS en **27 min 42 s** |
| `cargo deny check licenses bans sources advisories` | PASS, code de sortie 0 |
| `cargo audit --no-fetch` | PASS selon la politique, 20 avertissements de baseline autorisés |

Le replay workspace officiel inclut 3 tests assets, 3 tests d'intégration CLI, 5 core, 48 engine,
15 project, 24 runtime, 8 Studio frontend, 3 Studio natif et 26 world. Le dernier replay avec cache
chaud a terminé en environ 20,8 secondes ; une compilation à froid antérieure avait réellement pris
près de 29 minutes.

L'audit RustSec a chargé 1 226 avis et inspecté 911 crates. Les 20 avertissements autorisés restent
visibles, dont `glib 0.18.5 / RUSTSEC-2024-0429` et plusieurs dépendances transitives non maintenues.
Clippy Studio WASM a aussi signalé une future incompatibilité transitive de
`proc-macro-error2 2.0.1`, sans avertissement courant sous `-D warnings`. Ces dettes ne sont pas
présentées comme corrigées par ce lot. Aucun build Trunk supplémentaire n'a été exécuté pour ce lot.

## Couverture fonctionnelle des tests

- marche, sprint, accélération, freinage, saut, apex et atterrissage ;
- franchissement d'une marche de 0,35 m et refus d'une marche de 0,45 m, avec marge autour du seuil ;
- montée d'une pente de 39°, refus à 51° et glisse sur une pente de 56° ;
- conservation des identités ECS, assets et backend Rapier ;
- clamp du pitch initial et des deltas extrêmes, accumulation du yaw en `f64` puis normalisation ;
- caméra troisième personne, sphere cast, exclusions joueur/capteurs, rapprochement immédiat et
  récupération monotone ;
- première personne puis retour troisième personne avec visibilité, FOV et pose exacts ;
- sérialisation stricte, déterminisme, hash, valeurs non finies et interdiction de provenance Cadle.

## Probe natif borné

Le binaire qualifié a été exécuté avec `--visual-valley --player-camera-probe`, sous watchdog externe
de 45 secondes en plus du watchdog interne. Le processus a terminé en environ 5,5 secondes avec un
code de sortie **0**. La validation exigeait exactement un événement
`player_camera_probe_completed`, et pas seulement le code de sortie.

| Champ du probe | Valeur observée |
|---|---:|
| événements runtime parsés / succès / échec | 9 / 1 / 0 |
| ticks fixes totaux | 232 |
| séquences marche / arrêt / saut / première / troisième personne | 1 / 2 / 3 / 4 / 5 |
| marche / ticks au sol | 150 / 150 |
| début → fin du déplacement | `(16,000 ; 8,000)` → `(15,881 ; -3,626)` m |
| traversée de la couture centrale `z = 0` | oui |
| ticks aériens / apex / atterrissage | 38 / +1,267 m / oui |
| boom libre → occulté | 4,500 m → 1,843 m |
| échantillons de récupération | 20, tous monotones |
| mode caméra | troisième → première → troisième personne |
| bindings terrain préservés | 64 |
| joueur / caméra observés | `405v0` / `406v0` |

Le probe a injecté un obstacle Rapier procédural devant la caméra, vérifié le rapprochement, supprimé
l'obstacle puis observé la récupération. La distance finale de 0,733 m est un échantillon de la
récupération encore en cours au moment du retour troisième personne ; le test porte sur son caractère
monotone et borné, pas sur un retour instantané à 4,5 m.

## Artefacts natifs ignorés

Les artefacts restent sous `artifacts/forge-runtime/`, chemin exclu de Git. Ils servent de preuves
locales reproductibles et ne sont pas des assets produit.

| Artefact | SHA-256 | Taille |
|---|---|---:|
| `forge_runtime.exe` | `a5d06eb1a84f29de56c814f5cee4558c899b4b00c34e42409e7e5a497104a4c8` | 110 520 832 octets |
| `probe.stderr.jsonl` | `19e0573707ee04e8f836bb32c11bce8193c02f6adf49ce176ea6358041c5e447` | 5 541 octets |
| `probe.stdout.txt` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | 0 octet |
| `player-camera-3p.png` | `87eaac2141ce8d253787db5b292e457be2123f96b020894e77593b296f82159f` | 1 656 111 octets |
| `capture.stderr.jsonl` | `c08708730e419e59998ba8fd8290f36d04aa952d843816c608c760d9233c2110` | 4 869 octets |
| `run-metadata.json` | `42b96c8c9a40e4eefb49de567ff4106d1f054ec0cdbc3eba7a6c99e255140ea1` | 1 632 octets |

La capture 1920 × 1080 a été demandée à la frame 180 et le runtime s'est arrêté proprement à la frame
240. Elle a été inspectée par Codex : terrain, capsule, cadrage et overlay sont visibles, sans image
noire ou corrompue. Son statut exact reste
`PASS_BY_CODEX_NOT_HUMAN_COMPARATIVE_REVIEW` ; elle ne remplace ni la revue comparative humaine ni un
benchmark. Le FPS instantané de l'overlay n'est pas utilisé comme mesure Gate 1.

## Revue statique et limites connues

La revue finale n'a trouvé aucun P0/P1. Deux P2 détectés avant le commit ont été corrigés : le pitch
initial est désormais borné par le profil, et le yaw est accumulé en `f64`, normalisé et couvert par
des deltas extrêmes finis.

Les limites suivantes restent ouvertes :

- joueur et caméra sont simulés/projetés à 60 Hz sans interpolation de rendu ; le risque de judder à
  120/144 Hz doit être évalué humainement ;
- une fermeture externe pourrait terminer le processus sans événement de probe ; les validateurs
  doivent donc continuer d'exiger l'unique événement de succès ;
- la sensation de contrôle, le verrouillage curseur, la reprise de focus, le confort caméra et la
  lisibilité première personne exigent encore une revue humaine ;
- l'interaction végétation `R1-032` et l'animation optionnelle `R1-033` ne sont pas incluses.

## Exception CI distante ADR-0006

Le push du commit code a déclenché le run GitHub Actions
[`33390103329`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33390103329). L'interface affiche
`failure`, mais chacun des quatre jobs possède **zéro étape exécutée** et l'annotation GitHub indique
que le job n'a pas démarré à cause de la facturation ou du plafond de dépense.

| Job | ID GitHub | Conclusion d'interface | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `supply-chain` | `99481520888` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99481521176` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99481521229` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99481521392` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Le run n'a exécuté ni checkout, ni compilation, ni test. Ce statut n'est ni une CI verte ni un échec
produit ; il applique la dérogation distante autorisée par ADR-0006 et conserve les risques de
portabilité distants visibles.

## Limites et suite obligatoire

- Gate 1 reste ouverte : végétation, atmosphère, eau, interaction, profils qualité, télémétrie,
  performances, parité Studio, revue visuelle et rapport final restent à livrer.
- La revue humaine comparative de `R1-049` à `R1-052` n'est pas dérogée.
- Le benchmark High sur RTX 3060 ou RX 6600, le 1 % low et la qualification native/Studio restent
  ouverts ; la RX 7800 XT observée ici ne satisfait pas ce critère matériel.
- Aucun asset, shader, texture, nom, lore ou texte Cadle n'a été copié dans Forge. Les tests de
  provenance ne rencontrent le mot Cadle que pour en interdire l'introduction.
- La dérogation ADR-0006 ne couvre ni ces critères fonctionnels restants, ni une future distribution.
