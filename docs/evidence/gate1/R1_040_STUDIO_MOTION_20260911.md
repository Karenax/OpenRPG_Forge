# R1-040 — cycle Studio courant tenté, qualification en mouvement refusée

11 septembre 2026. **FAILED pour High→Low→High en déplacement**, pas une panne
de chargement ni un PASS temporel. Les preuves négatives sont conservées.

## Candidat courant et construction

Nouveau frontend et bundle intégrant le contrôleur corrigé au repos et les
préparations terrain/végétation réparties. Aucun code produit modifié dans ce lot.
279 fichiers source épinglés puis revérifiés sans écart. HEAD Rust
`f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié, pas une révision propre.

Racine externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-motion-20260911/`.
`Build.ps1` reproduit le frontend release avec wasm-opt=0 puis le shell Tauri.
WASM construit en 2 min 30 s. Bundle construit, avertissement futur de
`proc-macro-error2` conservé. Deux refus préliminaires également conservés :
`NO_COLOR=1` refusé par Trunk, puis toolchain externe 1.97.1 au lieu de 1.98.0.
Correction des variables du script seulement, aucune réduction de rust-version.

- WASM : `6966a3f15257101ce4514d035f0156fc44f601a9d5ea42cd651fb93ec683a287`.
- EXE : `574ce5a1d8f4d651327d8d1bfc708f53f649b204db522ef9220a197a2eb669f0`.
- JSON : `b5f3bbdc93b54ad38cd0eed09615cda29e1752116bdbcbe3041915db328c358d`.
- PNG : `5fe10bd6bbc8c336f96679e666bfe819594b10954c0f1f72df3500a028159cf7`.

Kit précédent réutilisé depuis `r1-terrain-staged-20260911/studio/web/forge-kit`,
octets vérifiés par le constructeur. URL : scène WorldSketch, kit gouverné,
forêt batches, parcours bridge, `forge_quality_motion=1`, eau clear-reflections,
nuages preview. Aucun asset Cadle ni carte retouchée.

## Résultat réel

Instance PID 11136 ouverte, interface inspectée via la compétence de contrôle
Windows. Export JSON puis PNG par les boutons du Studio. Premiers clics hors
zone visible/défilement sans export : pas déclarés réussis. Fichiers réellement
téléchargés `forge-diagnostic-1789111101233.json` et
`forge-viewport-1789111129150.png`, puis conservés et relus sur E:.

- Traversée **completed**, 42,5296 m, 518/518 ticks grounded, 434 contacts,
  aucune `failure_reason` du parcours.
- Sonde qualité : **deux** enregistrements, High tick 350 puis Low tick 626,
  avec déplacement et readiness. Troisième enregistrement absent.
- Après la traversée, High est réellement restauré : sept domaines appliqués,
  application `plan_application_ready`, identité séquence 3, génération 13,
  queue GPU terminée, 129 pipelines, zéro pending/failed.
- La sonde exige huit observations prêtes **pendant le mouvement** par niveau.
  Le retour High n'a pas obtenu ces observations avant l'arrêt du parcours ;
  son watchdog de 120 s produit `FAILED: délai ou parcours`. Ne pas transformer
  High prêt à l'arrêt en succès du scénario en mouvement.
- Kit 20/20, 36 colliders, forêt 1662 arbres / 16620 primitives affichées.
  PNG 1408×966 inspecté : personnage, route, forêt et ciel présents ; tramage
  encore perceptible sur certains troncs. L'eau est active mais hors de cette
  vue finale. Aucune qualification artistique ou de collision exhaustive.

Relevé de 120 frames après 60 ignorées : moyenne 184,619 ms, p95 286,5 ms,
maximum 7353,4 ms, 100 frames >33,3 ms. Ce relevé est global, **pas segmenté
par transition**, ni benchmark 1080p High stable. Il ne permet pas d'attribuer
les 7,35 s au seul commit CPU de végétation, à une compilation de pipeline,
à un upload GPU ou au chargement. La fluidité n'est pas prouvée et les mesures
natives isolées de `R1_040_CPU_COMMIT_PROBE.md` ne l'annulent pas.

## Suite ciblée et limites

Ne pas rejouer ce parcours identique en attendant un PASS. Isoler les durées
entre demande, préparation CPU, application et readiness dans le chemin Web
intégré. La fenêtre de marche finie ne doit ni masquer un délai, ni être remplacée
par une assertion de readiness immobile ; un protocole en mouvement prolongé
doit conserver collisions, contraintes de source et limite de temps explicite.
La réparation doit cibler la phase lente mesurée, pas la carte ni les seuils.

Cet essai actualise l'intégration Studio du contrôleur corrigé (traversée acquise),
mais ne ferme ni R1-040, ni le point 3, ni Gate 1. Dérogations inchangées.
Pas de nouvelle suite Rust revendiquée : sources de production inchangées depuis
les validations du contrôleur, seule une sonde native de test a été ajoutée ensuite.

Fermeture normale, sortie 0 du shell ; `cleanup.json` vérifié sans survivant :
11136, 2072, 29712, 21568, 30560, 22300, 8032, 29400. Aucun navigateur utilisateur
touché. Les deux acquisitions et les refus de build restent récupérables ;
aucune suppression, aucun commit/push, aucune Gate 2 démarrée.
