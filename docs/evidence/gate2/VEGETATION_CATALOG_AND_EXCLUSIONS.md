# Gate 2 — catalogue de végétation et exclusions

Date : 4 septembre 2026. Branche : `Rust`.

## Verdict et frontière

**R2-016 et R2-017 sont `PASS` sur qualification fonctionnelle automatisée. Gate 2 atteint 17/43 et
reste ouverte.**

Le candidat est le commit `394de87f13f1561781b19159369e01d95a246255`, arbre
`a818ff6aa553631a431a06d34cb4ba702f5a6004`, construit directement sur
`92cf726022d7d9e692a3398f6c7077966b903fcc`. Ses 15 fichiers ne comprennent aucun manifeste Cargo,
aucune dépendance, aucun asset et aucun chemin ou contenu Cadle. Les versions de schéma workspace et
végétation restent respectivement 3 et 1 : ce lot réutilise les sources canoniques déjà persistées,
sans migration ni changement de format.

La qualification UI native est **`NOT RUN`**, conformément à la demande du propriétaire de privilégier
les fonctions et de ne plus répéter les inspections d'interface. Les commandes, leur sérialisation,
l'historique, la persistance et les caches sont qualifiés ; aucun clic, capture ou verdict ergonomique
n'est inventé. La CI distante reste **`NOT RUN — infrastructure billing`** sous ADR-0006. PresentMon
est **`NOT RUN`** et aucune mesure de cadence, GPU ou performance n'est déduite de ce lot.

## Fonctions livrées

- Chaque couche de végétation devient une entrée concrète du catalogue procédural. Le Studio peut
  ajouter un preset Forge `Tree`, `Shrub`, `Fern` ou `Grass`, renommer l'entrée, la sélectionner pour
  peindre sa densité et la supprimer tant qu'au moins une entrée reste. Les paramètres numériques des
  quatre presets reproduisent exactement les couches Forge antérieures ; aucune liaison d'asset n'est
  introduite avant Gate 4.
- Les IDs sont stables et réutilisent le premier emplacement libre `species_01` à `species_16`.
  L'ajout initialise la densité de la nouvelle espèce à zéro. Un remplacement de catalogue vérifie à
  la fois le hash recette et le hash densité, conserve densités et overrides par ID stable, puis valide
  atomiquement les deux sources avant publication. Un simple renommage laisse le document de densité
  bit-identique.
- Le preset `Grass` n'est pas proposé une seconde fois tant qu'une couche herbe existe : la couche
  Forge par défaut atteint déjà le plafond strict de 1 048 576 brins développés. Le lot ne relève pas
  ce budget pour faire passer artificiellement une recette plus lourde.
- Les exclusions persistantes acceptent un cercle ou un segment de passage avec demi-largeur. Elles
  utilisent le premier ID libre `exclusion_01` à `exclusion_32`, sont ajoutées ou supprimées en une
  commande révisionnée, et passent par les mêmes undo/redo, sauvegarde immuable et réouverture que les
  autres éditions de zone.
- Le pinceau cercle utilise le point d'appui et son rayon ; le passage conserve uniquement le premier
  et le dernier point et refuse un segment dégénéré. La commande n'est publiée qu'au relâchement : ce
  comportement ne constitue pas encore l'aperçu temps réel de R2-019.
- La génération rejette un placement lorsque **son centre** appartient à une exclusion. Les survivants
  gardent leur ordre, leurs IDs et leurs valeurs bit-identiques ; le rejeu est exact et retirer les
  formes restitue la baseline. La forme ne calcule pas l'empreinte visuelle du mesh : une couronne ou
  une branche peut donc dépasser légèrement la frontière géométrique.
- Le cache prouve qu'un renommage ou l'ajout d'une espèce à densité nulle conserve les 64 handles. Une
  exclusion circulaire locale remplace 1 chunk, en conserve 63 et produit le même résultat qu'une
  régénération complète. Le cache 3D du Studio couvre les mêmes transitions.

## Vérifications reproductibles

| Contrôle | Résultat |
|---|---|
| `cargo test -j 1 -p forge_project -p forge_world -p forge_studio --lib --locked --offline` | **265 PASS** : project 94, Studio lib 86, world 85 |
| workspace/all-targets hors `forge_runtime`, inchangé par le candidat | **461 PASS**, 0 échec, 0 ignoré, 10 cibles |
| workspace/all-targets complet | **NOT COMPLETED** : deux tentatives arrêtées au linkage du runtime par `LNK1318` PDB (`LIMIT`, puis `FILE_SYSTEM`) sur le volume E: saturé |
| Clippy natif pertinent, `-D warnings` | PASS |
| Clippy Studio WASM, `-D warnings` | PASS ; avis Cargo tiers `proc-macro-error2 2.0.1` conservé |
| interaction UI native | **NOT RUN — demande propriétaire** |
| CI distante | **NOT RUN — infrastructure billing** |
| PresentMon | **NOT RUN** |

La passe élargie est :

```text
cargo test -j 1 --workspace --all-targets --exclude forge_runtime --locked --offline
```

Elle inclut et rejoue les trois suites fonctionnelles du contrôle ciblé. `forge_runtime` n'est pas
modifié par le candidat, mais son exclusion n'équivaut pas à une passe workspace complète : les deux
logs d'échec sont donc conservés dans le manifeste et **aucun total de 519 tests n'est revendiqué pour
ce commit**.

Les tests couvrent les presets exacts, les bornes 16/32, les IDs réutilisés, l'atomicité et les hashes
périmés, la conservation de densité, les refus sans mutation, cercle et passage, undo/redo,
sauvegarde/réouverture, autres zones intactes, replay exact, survivants bit-identiques et réutilisation
64/64 ou 63/64 du cache selon le changement.

Le manifeste
[VEGETATION_CATALOG_AND_EXCLUSIONS_ARTIFACTS.json](VEGETATION_CATALOG_AND_EXCLUSIONS_ARTIFACTS.json)
fixe les cinq logs par taille et SHA-256. Le validateur en lecture seule :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate2VegetationCatalogEvidence.ps1
```

Il vérifie commit, arbre, parent, inventaire Git, versions de schéma inchangées, hashes d'artefacts,
461 résultats, tests fonctionnels requis, deux échecs linker correctement classés et les deux passes
Clippy. Il ne relance ni test, ni build, ni application et ne transforme pas un log en verdict visuel
ou de performance.

## Routes laissées ouvertes

- `R2-019` : aucun aperçu de densité ou d'exclusion n'est généré avant le relâchement de la commande.
- `R2-020` : aucune instance générée n'est promue, cuite ou retouchée individuellement de façon
  persistante.
- Le catalogue reste procédural et borné aux quatre presets Forge. Les modèles, matériaux et autres
  liaisons d'assets relèvent de Gate 4 ; aucun asset n'entre dans Forge avec ce lot.
- La vérification native regroupée de la surface catalogue/exclusions reste disponible pour une revue
  humaine ultérieure, sans bloquer le verdict fonctionnel automatisé de R2-016/R2-017.
