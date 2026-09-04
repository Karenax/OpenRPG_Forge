# Gate 2 — commandes de brosses, qualification des sources

Date : 4 septembre 2026. Branche `Rust` uniquement.
Code : `5b503ff39c08b2edc879352ca3584799ee911764` ; arbre
`97b3cf8cb4905d6e516f81f3c3984c30c3adc277`.

**Résultat : fondation de commandes validée localement, outils Studio non encore raccordés.**
Gate 2 reste à **5/43**, ouverte. R2-007..014 et les routes générales R2-F restent non cochées.
L'objectif C → 0 → 1 → 2 → 3 → 4 n'est pas terminé. Aucune entrée en Gate 3.

## Réalisation

`forge_world::preview_terrain_brush` calcule une source candidate sans modifier le workspace emprunté.
Les cinq modes sont lever/abaisser, lisser, aplatir, bruit et pente. La brosse v1 possède un rayon
2..64 m, une intensité ]0,1] et une atténuation radiale smoothstep ; au maximum 65 × 65 positions de
grille sont examinées. Les empreintes sont coupées aux limites de la zone, sans échantillon extérieur.

Le lissage moyenne un voisinage 3 × 3 sur la source avant la commande : pas de rétroaction de l'ordre
de parcours. Aplatir/pente utilisent une hauteur absolue ; la pente ajoute deux gradients rise/run.
Lever/abaisser et bruit ont une amplitude de 32 m maximum par empreinte avant atténuation. Le bruit
utilise un seed u32 explicite et SplitMix64 ; les hauteurs produites sont bornées à ±1024 m.

Le patch persistant réutilise les deltas sparse du terrain v2 ; aucun mesh, handle, donnée GPU ni
nouveau format canonique. `WorkspaceChange::SetTerrainHeightDeltas` lie le patch au hash terrain
attendu et à la révision workspace. La validation du candidat inclut le budget JSON formaté de 1 Mio.
Le résultat de brosse conserve intention typée, CommandId, révision, IDs projet/module/zone,
nombre de samples modifiés, hashes avant/après et snapshot inverse.

`WorkspaceCommand::SculptTerrain` passe par le service natif existant. Son `dry_run` explicite
retourne le reçu candidat sans changer document, révision, dirty, historique ou reçu de la session.
Apply recalcule contre la révision courante et utilise le même historique de 32 opérations.
L'intention, le patch et l'inverse sont sérialisés avant mutation. Les autres zones restent intactes.
Le reçu est en mémoire : il ne constitue pas encore un journal d'audit durable ni une reprise crash.

## Preuves exécutées

| Contrôle | Résultat et portée |
|---|---|
| Workspace/all-targets | **450 PASS**, 0 échec, 0 ignoré ; dont 8 nouveaux tests world et 2 service. |
| Lever/abaisser | Centre exact, atténuation à mi-rayon, bord inchangé, source/inverse/hash et round-trip. |
| Lisser | Pic abaissé, voisins relevés symétriquement, terrain plat sans modification refusé. |
| Aplatir/pente | Hauteurs cibles et gradients vérifiés ; effet de l'intensité testé. |
| Bruit | Répétition identique à seed identique, seed différente discriminante, amplitude bornée. |
| Recalcul partiel CPU | Les six cas (lever et abaisser séparément) traversent quatre chunks ; remplacement égal à la génération complète, chunk témoin inchangé. Positions/normales/poids et données collider sont comparés. |
| Refus | Versions/paramètres invalides, non-finis, révision périmée/épuisée, IDs étrangers, hash incohérent, dépassement en hauteur, no-op. |
| Service/persistance | Dry-run intact → apply identique au candidat → undo → redo → snapshot immuable → nouvelle session et réouverture exacte. Les octets du snapshot antérieur restent identiques. |
| Clippy natif | **PASS**, workspace/all-targets avec `-D warnings`. |
| Clippy WASM | **PASS**, Studio lib/web ; compilation vérifiée, pas exécution WASM des nouveaux pinceaux. |
| Format/diff | `cargo fmt --all -- --check` et `git diff --check` PASS. |

Commandes finales :

```text
cargo test --workspace --all-targets --locked --offline
cargo clippy --workspace --all-targets --locked --offline -- -D warnings
cargo clippy -p forge_studio --lib --target wasm32-unknown-unknown --no-default-features --features web --locked --offline -- -D warnings
cargo fmt --all -- --check
git diff --check
```

Tests par cible : assets 3, CLI intégration 3, core 12, engine 158, project 62, runtime 76,
logging 1, Studio lib 68, Studio bin 5, world 62. Les premières passes ont signalé un appel de test
à `serde_json` absent de world, puis des lints de documentation/forme : corrigés sans ajout de
dépendance. Le round-trip IPC est testé dans Studio. Le warning tiers
de future incompatibilité `proc-macro-error2 2.0.1` reste distinct d'un warning du projet.

Logs locaux ignorés, hashes dans [BRUSH_COMMANDS_ARTIFACTS.json](BRUSH_COMMANDS_ARTIFACTS.json).
Le snapshot antérieur `modules-areas-2026-09-04/snapshots/9d72c00d-02a6-50c2-87b3-411787233851-r7.forge.json`
est encore intact : SHA-256 `297035827452c15186b9f17840784d575b98f660ddcfb8984102d10c8de61ae4`.

## Travail restant avant qualification des routes

- Choix des brosses et paramètres dans Monde, pointeur projeté sur le terrain réel, cercle de rayon/
  intensité, traits continus et regroupement en une opération annulable.
- Projection interactive des sources acceptées et du dry-run ; aucune capture UI de brosse n'a été
  réalisée dans ce lot. Le desktop doré et le renderer existant ne sont pas modifiés.
- Raccorder les mises à jour partielles au renderer sans reconstruire toute la scène. Le contrat
  partiel actuel exige encore `terrain.revision + 1` ; un workspace peut avancer de plusieurs révisions
  entre deux edits terrain, et undo/redo restampent les terrains. Traiter ces transitions explicitement
  avant de qualifier R2-014, sans fabriquer un hash ou relâcher silencieusement les contrôles.
- Peinture des matériaux, biomes/végétation par zones, autres fonctions de Gate 2 et scénario complet.
- Builds Release et parcours réel natif/WASM des nouvelles brosses, puis preuves visuelles.

CI distante non qualifiée dans ce lot, sous ADR-0006 ; aucune CI verte annoncée. Réserves Gate 1
conservées sous ADR-0011, campagne PresentMon toujours suspendue sous ADR-0010. Aucun accès à `main`,
aucun fichier Cadle ni asset externe repris. Les manifestes Cargo et le style CSS sont inchangés.
