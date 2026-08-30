# ADR-0004 — Exception bornée Gate 0 pour la télémétrie des draw calls

Date : 2026-08-30
Statut : **accepté pour Gate 0 uniquement**
Portée : branche orpheline `Rust`, Gate 0, Bevy `0.19.1`
Commit qualifié : `7286740c18fe9afa6cc207e388d5f335495fc650`
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

`R0-025` demande un overlay FPS, frame time et draw calls pour le runtime natif. Au commit qualifié,
FPS et frame time sont fournis par `FrameTimeDiagnosticsPlugin`. `RenderDiagnosticsPlugin` fournit les
temps CPU/GPU des passes et, lorsque le backend le permet, des statistiques de pipeline telles que les
invocations de shaders et les primitives produites par le clipper.

Bevy `0.19.1` ne fournit toutefois aucun compteur global et backend-neutre des commandes de dessin.
Ses statistiques de pipeline ne comptent pas les appels `draw*`. `TrackedRenderPass` enveloppe certains
appels directs, indexés et indirects, mais ne porte aucun compteur global ; plusieurs passes Bevy
créent en outre directement un `wgpu::RenderPass`. Les commandes multi-draw indirectes ne permettent
pas non plus de déduire honnêtement, sans convention et instrumentation supplémentaires, le nombre de
sous-draws réellement exécutés.

Les nombres de maillages source, d'items de phase, de primitives et d'invocations de shaders mesurent
des objets différents. Les présenter comme des draw calls produirait une valeur inventée. Instrumenter
exhaustivement Bevy ou wgpu pour Gate 0 imposerait un fork ou un patch transversal du renderer, alors
qu'`AGENTS.md` interdit le fork de Bevy avant validation explicite et exige une limite mesurée avant
toute extension raw wgpu.

## Décision

Gate 0 accepte une exception strictement limitée au compteur global de draw calls sous Bevy `0.19.1`.
La preuve automatisée de `R0-025` est qualifiée seulement si :

- l'overlay affiche des FPS et un frame time issus des diagnostics Bevy ;
- l'overlay affiche littéralement `Draw calls GPU : indisponibles` tant qu'aucun compteur exhaustif
  n'existe ;
- le ledger sérialise `gpu_draw_calls` à `null` ;
- le ledger sérialise `gpu_draw_call_status` avec la valeur exacte
  `unavailable_bevy_0_19_no_global_counter` ;
- les statistiques de pipeline disponibles restent nommées comme primitives ou invocations et ne sont
  jamais relabellisées en draw calls.

Cette décision qualifie la **disponibilité explicitement diagnostiquée** du compteur, pas un compteur
livré. Elle autorise Gate 0 à ne pas rester bloquée sur ce seul point, sans modifier les objectifs de
performance ni les critères de `R1-037`. Aucun chiffre de draw calls ne peut être estimé, calculé ou
injecté à partir des entités, maillages, phases, primitives, lots, instances ou invocations shaders.

Une capture GPU externe peut être ajoutée comme preuve ponctuelle ultérieure si elle consigne l'outil,
sa version, le backend, la scène, la frame et la définition retenue pour les commandes multi-draw. Une
telle capture ne transforme pas automatiquement un compte ponctuel en compteur global d'overlay.

## Preuves au commit qualifié

Les preuves exactes suivies au commit `7286740c18fe9afa6cc207e388d5f335495fc650` sont :

- `crates/forge_engine/src/lib.rs` installe `RenderDiagnosticsPlugin`, expose
  `RenderWorkReport::gpu_draw_calls: Option<u64>` et fixe
  `GPU_DRAW_CALL_STATUS` à `unavailable_bevy_0_19_no_global_counter` ;
- `build_render_work_report` affecte explicitement `gpu_draw_calls: None` tout en conservant séparément
  les primitives et invocations GPU disponibles ;
- le test exact `tests::render_work_never_invents_gpu_draw_calls` vérifie le texte d'overlay, la valeur
  `None`, le statut exact et l'absence de substitution par les draws logiques ;
- `crates/forge_engine/README.md` distingue maillages source, statistiques de pipeline et draw calls
  indisponibles ;
- `docs/evidence/gate0/NATIVE_RUNTIME.visual-proof.json` conserve le contrôle
  `true_gpu_draw_calls` à `not_run` et indique qu'aucun vrai compteur n'a été collecté ;
- `docs/evidence/gate0/LOCAL_VALIDATION.md` conserve les 120 échantillons de frame time ainsi que la
  limite selon laquelle les diagnostics Bevy ne donnent aucun nombre fiable de draw calls.

Ces preuves sont volontairement contradictoires avec toute déclaration d'un nombre réel : le test et
le ledger empêchent qu'une absence d'API soit masquée par un proxy.

## Conditions de validité

L'exception reste valide seulement si :

- la version renderer qualifiée demeure Bevy `0.19.1` avec sa pile wgpu verrouillée ;
- sa portée reste Gate 0 et le runtime de fondation ;
- `gpu_draw_calls` reste `null` tant qu'une instrumentation exhaustive n'est pas prouvée ;
- le statut exact et la limite restent visibles dans l'overlay, le ledger et le rapport de gate ;
- le test `render_work_never_invents_gpu_draw_calls` reste vert ;
- aucune métrique proxy n'est annoncée comme draw calls ;
- `R1-037` n'est ni coché ni qualifié par la présente exception.

Une violation de ces conditions rouvre `R0-025` et invalide cette exception pour le passage de Gate 0.

## Déclencheurs de réouverture avant R1-037

Cette décision doit être revue avant toute qualification de `R1-037`. La revue est également
obligatoire dès qu'un des événements suivants survient :

- montée de Bevy au-delà de `0.19.1` ou changement de la version wgpu résolue ;
- ajout dans Bevy ou wgpu d'une API publique de comptage global ou d'un hook exhaustif de commandes ;
- introduction d'un renderer Forge, d'un patch Bevy, d'une passe raw wgpu ou d'une nouvelle famille de
  commandes indirectes ;
- ajout d'un outil de capture GPU reproductible dans le protocole de qualification ;
- utilisation d'un nombre de draw calls dans un budget, une comparaison native/WASM ou une décision de
  profil graphique.

Avant de passer `R1-037`, Forge devra soit fournir une instrumentation exhaustive et testée avec une
sémantique documentée des multi-draw, soit produire une capture GPU externe reproductible pour chaque
backend qualifié. Si aucune voie n'est alors proportionnée, une nouvelle décision spécifique à Gate 1
sera nécessaire : la présente exception ne peut pas être prolongée tacitement.

## Conséquences

Gate 0 conserve un overlay automatisable, une mesure réelle de FPS/frame time et une sortie honnête
pour la capacité absente, sans fork renderer ni nouvelle dépendance. Le passage de Gate 0 peut traiter
`R0-025` comme **PASS par exception bornée**, mais ne peut jamais déclarer qu'un compteur de draw calls
a été livré. Le comptage réel, les triangles et les instances restent intégralement dus à `R1-037`.
