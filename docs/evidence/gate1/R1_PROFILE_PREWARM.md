# R1-039 — préparation explicite des profils

10 septembre 2026 — **R1-039 PASS local natif/Studio pour le candidat et les variantes connues**,
pas un verdict Gate 1 ni une qualification de fluidité des transitions.

## Contrat implémenté

Un même service `forge_engine::ProfileWarmup`, installé avec le coordinateur de
qualité, est appelé par `--prewarm-quality` dans le runtime et le bouton
« Préparer les quatre profils » du Studio. Il reste inactif sans demande.

Il prépare Low, Medium, High, Ultra puis réapplique le profil global initial,
sur la scène et la météo courantes. Les documents auteurs ne sont pas modifiés.
Chaque étape exige le reçu du plan courant, les sept domaines projetés, le kit
prêt et huit observations fraîches et stables du cache de pipelines, sans
compilation en attente ni erreur permanente. Les imports shaders encore en
chargement restent en attente, pas en succès. L'adaptation est suspendue puis
restaurée. Les commandes de déplacement sont annulées sans réinitialiser leur
séquence. Le rendu continue et un bandeau explique la préparation.

Un nouveau choix manuel de qualité ou de météo annule la préparation sans
écraser ce choix. Échec et délai de 120 secondes sont explicites ; ils ne
permettent pas une capture déclarée prête. Une demande peut être relancée.
Le cycle global natif peut démarrer après la préparation, avec son propre délai
borné ; les autres sondes concurrentes restent interdites.

## Limites conservées

- Réapplication du profil global initial, pas restauration de chaque override
  individuel d'aperçu.
- Variantes connues pour la scène, la vue et la météo courantes ; aucune promesse
  de préparer toutes les scènes, positions ou conditions météorologiques futures.
- Les échantillons de frame pris pendant la préparation ne sont pas filtrés et
  ne constituent pas un benchmark à chaud.
- Le cache prêt ne prouve pas, seul, l'absence de saccade ni la qualité visuelle
  des transitions. R1-040 et les réserves temporelles restent distincts.
- ADR-0012 ne déroge pas à ce préchauffage dans le Studio.

## Acquisition

`scripts/Invoke-Gate1ProfileWarmup.ps1` réutilise les entrées épinglées du candidat
intégré, exige un dossier frais sur E:, vérifie les sources avant/après, conserve
la commande et le hash du binaire, et arrête uniquement son processus (245 s ou
3 Gio de mémoire de travail). Il ne produit jamais un verdict global de Gate 1.

Les journaux de développement et essais de ce lot sont conservés dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-profile-prewarm-20260910`.
Les premiers échecs de compilation/tests y restent historiques ; seuls les
contrôles finaux sur le code corrigé pourront être retenus.

## Résultats natifs acquis

Runtime SHA-256 `643bb8af9531d3616a42a515e0442735250921d59a768645409c7226958c0157`.
`tests-final-b.log` : 696 tests PASS, un timing ignoré ; les six tests du service
couvrent notamment priorité manuelle, changement météo, timeout et stabilité.
Build natif PASS. Le snapshot `source-inputs.json` épingle 273 entrées de build ;
ce n'est pas une révision Git propre.

- `native-clear` : préparation en 18,6297 s, cinq étapes Low/Medium/High/Ultra/High,
  état final `ready`, aucun message d'erreur du service, capture Full HD inspectée.
  Reçu GPU final du plan High prêt. Sortie 0, PID 29464 terminé, sources inchangées.
  Le filtre de log hérité conservait seulement les avertissements : le reçu
  détaillé est dans `metrics.json`, pas inventé depuis un journal INFO absent.
- `native-clear-cycle` : préparation en 11,5234 s puis cycle High/Low/Medium/Ultra/High.
  Cinq captures, cinq plans acquittés, sources identiques, sortie 0, PID 31792 terminé.
- `native-rain-cycle` : préparation en 12,2854 s puis même cycle sous pluie,
  cinq captures et acquittements, sortie 0. Vue finale inspectée : pluie et
  mouillage visibles, forêt/eau/pont présents. Aucune nouvelle acceptation artistique.
- Dans les deux cycles, un pipeline supplémentaire apparaît après la première
  capture (157→158 clair, 161→162 pluie), puis les observations restent stables,
  sans pending ni failed. L'implémentation Bevy 0.19.1 spécialise un pipeline de
  copie à la première capture (`view/window/screenshot.rs:392`) : explication
  compatible avec cette hausse, pas une attribution instrumentée certaine.

`Verify-Gate1ProfileWarmup.py` revérifie commandes/arrêts, pins, étapes, ordre
préparation puis cycle, identités GPU, sources et captures. Résultat natif PASS
dans `native-verification.json`, avec `studio_verified=false`,
`hitch_free_qualified=false`, `gate1_pass=false`. Aucun nouveau benchmark cible.

## Résultats Studio acquis

Frontend release construit hors ligne avec Binaryen 131 déjà installé sur E:.
Le premier assemblage échoué (outil absent du PATH) est conservé ; le second
réutilise la compilation Rust et termine avec l'optimisation `-Oz` configurée.
WASM SHA-256 `519c09948dfcd6ebe8520fbb00ef18ad894fa4da6e8093dd5f2440040a8600c9`.
Exécutable Studio `2f2f6f7657e90819f4a78293af594d06561b6a808ba6f630b29a3797b7d21861`.

Le bouton a été utilisé dans la vraie fenêtre Tauri, pas par injection de données.
L'état Warming a été observé puis Ready 5/5. Deux exports JSON et deux PNG
1408×966 ont été produits par les contrôles du Studio et copiés sur E: :

- Clair : **48,7433 s**, 142 pipelines à la dernière étape ; la demande lancée
  pendant le démarrage inclut l'attente du plan initial et des ressources du kit.
- Pluie : **13,5307 s**, 147 pipelines ; même instance et sources inchangées.
- Dans les deux cas : Low/Medium/High/Ultra/High, plan High courant GPU-ready,
  sept domaines projetés, aucun pending/failed, météo conservée, sources alignées.
- Vue après préparation réellement inspectée ; pluie et mouillage visibles.
  Ce ne sont ni des mesures 1080p/60 FPS ni de nouvelles acceptations artistiques.
- PID Studio 16144 et ses sept descendants WebView terminés ; aucun processus
  de compilation ou de rendu de ce lot conservé. Reçu `studio-cleanup.json`.

Le vérificateur contrôle maintenant aussi ces deux exports, les identités GPU,
les sources et la fermeture. La préparation des profils importants est donc
acquise localement dans les deux environnements, sans dérogation R1-039.
Les attentes humaines, R1-040 et le verdict final restent distincts et ouverts.
