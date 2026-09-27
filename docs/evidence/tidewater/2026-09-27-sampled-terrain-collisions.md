# Collisions derivees du terrain importe — 27 septembre 2026

Le module sampled_terrain_physics prepare 256 heightfields Rapier de 128 metres,
129 x 129 sommets chacun, a partir de SampledTerrainPlan. Le domaine accepte est
2048 x 2048 metres, origine (-1024,-1024), pas source et grille de corrections de
1 metre. Les hauteurs sont evaluees sur la grille globale puis transposees en
colonnes pour Rapier. La resolution physique reste fixe, independante du CDLOD.
Les formes triangulent cette grille metrique ; elles ne pretendent pas reproduire
exactement toute la surface bilineaire entre sommets ni le morphing visuel CDLOD.

PreparedSampledTerrainPhysics termine tous les calculs avant remplacement ECS.
Chaque tuile porte document_hash et minimum_cell ; la ressource conserve le plan
et les entites derivees. Une nouvelle adoption retire les anciennes entites.
L'installation ECS ne prouve pas la synchronisation de la query world Rapier.
Le plugin physique et l'acquittement de readiness restent du ressort de l'appelant.

Le curseur de brosse accepte maintenant soit TerrainRuntimeState procedural,
soit SampledTerrainPhysicsState importe. Son filtre de raycast exclut les anciens
hashes importes ; le contour utilise le sampler importe lorsque cette ressource
est presente. Le chemin procedural reste conserve. Le raccordement de cette
ressource dans le viewport Studio et la qualification du geste restent ouverts.

Probe sampled_terrain_physics_probe ajoute : 256 tuiles, rayons verticaux sur
plusieurs tuiles asymetriques, deux cotes d'une jonction, retouche +12 m, retour
exact au document initial et retrait des anciennes entites. Il verifie les formes
Rapier directement. Execution PASS : 256 tuiles, jonctions verifiees, hauteur
initiale -1,9840851 m, retouche 10,015915 m, restauration -1,9840851 m.
Clippy strict moteur et probe PASS, session 44899 (27,73 s). Build natif PASS.
Log : E:/OpenRPG_Forge/.tidewater-integration/sampled-physics-shapes-002.log.


Un second probe, sampled_terrain_backend_probe, utilise MinimalPlugins,
TransformPlugin et le vrai RapierPhysicsPlugin en FixedUpdate. Apres chaque
adoption et synchronisation, il exige 256 colliders dans le backend, un raycast
vers l'entite du document courant, une hausse de 12 m puis un retour exact.
Ce controle complete celui des formes locales sans qualifier les gestes GUI.
Clippy, build et execution PASS : backend de 256 colliders conserve apres
chaque remplacement, hausse de 12 m et retour exact verifies.
Log : E:/OpenRPG_Forge/.tidewater-integration/sampled-physics-backend-001.log.
Empreintes des executables : sampled-physics-proof-001.json dans le meme dossier.
Compilation Studio WASM PASS (session 71200, 52,20 s).
Le geste dans Studio reste NON QUALIFIE.


Controle de synchronisation ajoute : is_synchronized exige l'empreinte attendue,
256 cellules distinctes de la grille et les formes/poses effectivement presentes
dans le backend Rapier courant. Les colliders de terrain sont statiques sans
rigid body, ce qui reutilise le controle de synchronisation des primitives fixes.
Le probe exige un refus avant synchronisation, apres modification locale non
synchronisee et pour une empreinte documentaire differente. Tous ces controles
PASS, ainsi que le re-test des formes et jonctions avec colliders statiques.
Clippy moteur/probe --no-deps strict PASS (34,20 s), build PASS (5 min 39 s).
Le Clippy avec dependances reste bloque par les deux needless_pass_by_value
preexistants de forge_project/src/zone_authoring/session.rs:349-350.
Preuve : E:/OpenRPG_Forge/.tidewater-integration/sampled-physics-readiness-proof-001.json.
Ce controle ne constitue pas une preuve de presentation GPU ou de geste Studio.

Compilation Studio WASM apres controle de synchronisation : PASS, session 6603
(5 min 46 s incluant attente du build natif ; avertissement futur proc-macro-error2).
