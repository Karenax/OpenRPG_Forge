# Rapport Gate 1 — Visual Valley

Date de consolidation : **2026-09-01**

Verdict : **POURSUIVRE GATE 1 — GATE 1 OUVERTE**

Gate suivante : **Gate 2 interdite sans passage de Gate 1 ou nouvelle dérogation explicite acceptée**

## Résumé de décision

Visual Valley dispose désormais d'une scène native mesurée, de deux exécutions Browser clear/rain,
de captures fixes, de ledgers versionnés et d'une provenance CC0/Cadle auditée. Les deux variantes
Browser passent chacune **24/24 contrôles automatisés**, sans échec réseau ni warning console.

Ces résultats ne ferment pas Gate 1. Le runtime natif Release a désormais aussi été reconstruit et
rejoué sur le commit exact `a34596bd` : clear/rain High, 1920 × 1080, RX 7800 XT/Vulkan, 46/46
contrôles croisés PASS et les sept hashes de source exposés par les deux formats de ledger concordent.
`R1-043` et la condition de parité de données native/Studio sont satisfaites. La présentation GPU
Browser reste `not_observed`, la cible RTX 3060/RX 6600 n'a pas été mesurée, les profils dynamiques ne
sont pas rejoués, le warning wgpu et les artefacts noirs persistent, et les captures n'ont pas reçu
d'acceptation humaine comparative.

Le verdict `R1-055` est **poursuivre Gate 1**, corriger et requalifier les preuves manquantes. Il ne
constitue ni un passage administratif de Gate 1, ni une autorisation implicite de commencer Gate 2.

## Identités exactes et frontières de preuve

| Portée | Commit | Tree | Usage dans ce rapport |
|---|---|---|---|
| source finale Browser | `a34596bd733131699c70028dab86cf2c612b896a` | `8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6` | Browser clear/rain, manifeste Studio, assets HTTP et captures Browser |
| natif performance/captures exact-commit | `a34596bd733131699c70028dab86cf2c612b896a` | `8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6` | RX 7800 XT/Vulkan, métriques v8, captures et logs natifs exacts |
| provenance/indépendance | `92286910a9bab622d5d54905551c0f03172e98e7` | `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe` | audit CC0, comparaison Cadle et absence de dépendance produit |
| référence Cadle extérieure | `ea0da62549980816a6a27e08b75808b1f56d70e7` | `f0d52e434454fe302052d22eafde203bc361cc1f` | comparaison de provenance uniquement, clone local détaché et propre |

Le natif et le Browser forment désormais une paire exact-commit. Le nouveau run natif porte le hash
scenery corrigé `6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f`,
identique aux deux manifestes Browser. Les anciennes preuves `92286910` restent historiques et ne
sont plus utilisées pour conclure la parité `R1-043`.

## Validation source finale

La source exacte `a34596bd733131699c70028dab86cf2c612b896a` a été rejouée séquentiellement
avec `CARGO_TARGET_DIR=E:\OpenRPG_Forge\.cargo-target-gate1-combined` :

| Contrôle | Résultat |
|---|---|
| `git diff --check` puis `cargo fmt --all -- --check` | **PASS** |
| `cargo test -p forge_engine --lib --locked -j1` | **119/119 PASS** |
| `cargo test -p forge_runtime --all-targets --locked -j1` | **32/32 PASS** — 31 bin + 1 intégration |
| `cargo test -p forge_studio --all-targets --locked -j1` | **31/31 PASS** — 28 lib + 3 bin |
| total | **182/182 PASS** |
| Clippy strict des trois packages, tous targets, `-D warnings` | **PASS**, zéro warning, 12,04 s Cargo |
| check Studio `wasm32-unknown-unknown`, features `web` | **PASS**, 1,06 s Cargo |

Le check WASM conserve uniquement l'avertissement de future incompatibilité transitive connu pour
`proc-macro-error2 v2.0.1`. Cette validation couvre la source et les contrats. Le rebuild et les deux
smokes Release natifs exact-commit sont documentés séparément ci-dessous.

## Preuve Browser finale clear/rain

Les artefacts locaux sont sous :

```text
E:\OpenRPG_Forge\.gate1-browser-smoke-artifacts\a34596bd733131699c70028dab86cf2c612b896a
```

Le lot documentaire contient leurs copies byte-for-byte sous
`docs/evidence/gate1/browser/a34596bd733131699c70028dab86cf2c612b896a/` et les deux PNG sous
`docs/evidence/gate1/captures/`. Leur cartographie complète est fournie par
[`R1_BROWSER_WEBGPU_SMOKE.md`](../evidence/gate1/R1_BROWSER_WEBGPU_SMOKE.md).

Le manifeste externe mesure 2 503 octets et porte le SHA-256
`a636033fc0fbf55cde89efc2c34a7f7b2772a2c49dfcf4b34c8aa83dfce7a97c`. Les principaux fichiers :

| Artefact Browser | Taille | SHA-256 |
|---|---:|---|
| `validation.json` | 3 507 octets | `913819da6299cfba198cf74df90b31a7cc2feb34f4f62fe4ab5aba28cb78b4f8` |
| `visual-valley-clear-high.browser.png` | 1 296 307 octets | `c30871ba1ca932de2f18b8dbb22b9c7013d4105c10927e068e17b47cd01ce646` |
| `visual-valley-rain-high.browser.png` | 1 290 285 octets | `d3b3326a34c6226e0475d625d9bf53c628a676e7c448f5938ed913a1ad2b34b0` |
| ledger scène clear | 4 042 octets | `36c99ceef5da02b5199ffbd7afe20eeaffba491ddcfcf738706275e6f0e524b3` |
| ledger scène rain | 4 038 octets | `772f79c0db48c7c610d4da7e7d40c40c51ef3be3bb66e7e1ba421f7186cfb65f` |
| ledger frame clear | 1 789 octets | `90afae69e4260be8d319e995bfea5afe53ea8e3fd8ca2f3b1454b3843ae3e9c0` |
| ledger frame rain | 1 786 octets | `95d8dd92ffff6ce65ac4d25a13caaa1a631cff420a2b491012bb3c646857353b` |

### Résultats automatisés

- clear : **24/24 PASS**, zéro contrôle échoué ;
- rain : **24/24 PASS**, zéro contrôle échoué ;
- `selected_manifest == runtime_manifest` et `parity_status=source_manifest_matched` dans chaque run ;
- huit hashes de source présents et identiques entre sélection et observation Studio ;
- une caméra 3D active sur une attendue ;
- eau `observed_main_world_components`, météo `observed_cpu_projection` et tier High appliqué ;
- décor `activated`, 9/9 modèles chargés, 0 échec, 125 instances demandées, dont 99 remplacements et
  26 détails ;
- 26/26 requêtes d'assets répondent HTTP 200 pour chaque météo ;
- aucune requête `.meta`, aucun échec réseau, aucun warning/erreur console et aucun message DOM
  d'indisponibilité ou d'erreur runtime.

Les PNG ont une taille de 1920 × 1080 et capturent la page Browser complète. Le canvas Bevy observé
mesure 1 393 × 1 067 ; les chiffres Browser ne doivent donc pas être décrits comme un benchmark GPU
plein écran 1920 × 1080.

### Mesures de boucle WASM

| Mesure après 60 frames de warm-up | Clear | Rain |
|---|---:|---:|
| échantillons | 120 | 120 |
| durée | 1 287,4 ms | 1 100,2 ms |
| moyenne | 10,7283 ms | 9,1683 ms |
| p50 / p95 / p99 | 10,3 / 13,4 / 17,1 ms | 9,1 / 10,1 / 11,9 ms |
| FPS moyen | 93,211 | 109,071 |
| 1 % low | 54,945 FPS | 84,034 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 2 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 118 / 2 / 0 | 120 / 0 / 0 |

Ces mesures proviennent de la boucle d'update Bevy WASM, pas d'une timestamp query GPU. Le ledger
conserve `webgpu.status=checking`, `webgpu_ready_ms=null` et `gpu_presentation=not_observed`. Le PASS
automatisé valide le chargement, les invariants CPU/ECS, le réseau, le DOM, les captures et les
métriques de boucle ; il ne doit pas être renommé « présentation GPU WebGPU qualifiée ».

## Preuve native RX 7800 XT exact-commit

Le binaire Release reconstruit et exécuté sur `a34596bd` utilise une AMD Radeon RX 7800 XT, Vulkan
et le pilote AMD `26.8.1 (LLPC)`, en 1920 × 1080 High fixe, VSync off. Le build Release termine en
3 min 23 s ; le binaire de 81 728 000 octets porte le SHA-256
`70966031f73569eecc31035497bb853cb7cd0251525dc4b2909a70c47093ee9e`. Les deux ledgers
`forge.runtime-metrics` v8 contiennent 120 échantillons après warm-up :

| Mesure native | Clear | Rain |
|---|---:|---:|
| moyenne | 7,4702 ms / 133,866 FPS | 7,6855 ms / 130,115 FPS |
| p95 | 7,7622 ms | 8,1653 ms |
| 1 % low | 125,519 FPS | 115,730 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| mémoire wgpu allouée / réservée | 561 528 460 / 876 609 552 octets | 565 725 588 / 876 609 552 octets |
| pipelines prêts | 68/68 | 69/69 |

La fenêtre de mesure native dure environ 0,94 seconde à caméra fixe. Les valeurs mémoire décrivent
les sous-allocations wgpu, pas la VRAM physique totale. `gpu_draw_calls` reste `null` et aucun total
exhaustif de draw calls/triangles/instances GPU n'est disponible.

Chaque log natif contient exactement un warning `wgpu_core::validation`, sans `ERROR` : un vertex
shader écrit `@builtin(position)` sans `@invariant` alors que le pipeline utilise la comparaison
`Equal`. wgpu signale un risque d'artefacts selon la machine ; les logs ne sont pas propres.

Les commandes, 46 contrôles, six hashes natifs et limites détaillées sont consignés dans
[`R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`](../evidence/gate1/R1_043_NATIVE_STUDIO_EXACT_COMMIT.md),
complété par
[`R1_034_043_PERFORMANCE_PARITY.md`](../evidence/gate1/R1_034_043_PERFORMANCE_PARITY.md).

## Provenance, CC0 et indépendance

Le rejeu final de provenance sur `92286910` est **PASS automatisé local** :

- sous-ensemble Quaternius Stylized Nature MegaKit Standard 1.0, licence `CC0-1.0` ;
- 27 fichiers suivis, 9 modèles et 16 652 493 octets, tous déclarés et hashés ;
- 27/27 copies byte-for-byte depuis le coffre normalisé ;
- 507 fichiers Cadle hachés, 39 fichiers Forge canoniques comparés ;
- 0 copie exacte, 0 ligne normalisée commune d'au moins 40 caractères, 0 payload Cadle suivi et
  0 dépendance produit vers le clone ;
- Visual Valley fonctionne sans clone Cadle produit ; le clone extérieur n'est qu'une référence de
  comparaison en lecture seule.

Cela qualifie `R1-053`, `R1-054` et la condition « code sans dépendance propriétaire obligatoire ».
Cela ne constitue ni une expertise juridique exhaustive, ni une revue artistique, ni une preuve GPU.
Voir [`R1_053_054_PROVENANCE_INDEPENDENCE.md`](../evidence/gate1/R1_053_054_PROVENANCE_INDEPENDENCE.md)
et [`R1_CC0_SCENERY_OVERLAY.md`](../evidence/gate1/R1_CC0_SCENERY_OVERLAY.md).

## État conservateur des routes

Nouvelles cases fermées par les preuves consolidées :

- `R1-019`, `R1-020`, `R1-021`, `R1-022` et `R1-024` ;
- `R1-035`, `R1-036`, `R1-038` et `R1-042` ;
- `R1-043` pour la paire native/Studio exact-commit et les sept hashes communs concordants ;
- `R1-044` et `R1-045` pour l'acquisition de captures fixes ;
- `R1-053` et `R1-054` pour provenance et indépendance ;
- `R1-055` par le présent rapport, avec verdict « poursuivre Gate 1 ».

Restent explicitement ouvertes :

- `R1-014`, `R1-023`, `R1-025`, `R1-026` et `R1-033` ;
- `R1-034`, `R1-037`, `R1-039`, `R1-040` et `R1-041` ;
- `R1-046` à `R1-052`.

`R1-043` est fermée : les deux cibles utilisent `a34596bd`, le scenery hash corrigé concorde et
46/46 assertions croisées passent. Le ledger natif n'expose pas le hash autonome du contrôleur
présent dans le manifeste Studio ; cette limite est documentée et ne doit pas être transformée en un
huitième match de ledger.

## Conditions de passage Gate 1

| Condition | État | Motif |
|---|---|---|
| qualité visuelle clairement supérieure à la base Godot | **OUVERTE** | aucune revue humaine comparative acceptée |
| impact au moins comparable à Cadle | **OUVERTE** | pas de comparaison A/B humaine sur protocole équivalent |
| 60 FPS moyens en 1080p High sur GPU milieu de gamme | **OUVERTE** | RX 7800 XT seulement ; Browser canvas et boucle WASM non équivalents |
| p95 `<=16,7 ms` | **OUVERTE** | paire exact-commit dans le budget, mais pas sur GPU cible |
| 1 % low `>=45 FPS` | **OUVERTE** | paire exact-commit dans le budget, mais pas sur GPU cible |
| absence de stutter récurrent | **OUVERTE** | fenêtres courtes/fixes ; deux hitches Browser clear |
| profils qualité fonctionnels | **OUVERTE** | High fixe seulement ; transitions/adaptation non rejouées |
| parité de données native/Studio | **SATISFAITE** | commit/arbre exacts communs, 46/46 contrôles et sept hashes communs MATCH |
| code sans dépendance propriétaire obligatoire | **SATISFAITE** | audits provenance/indépendance locaux PASS |
| rapport et captures versionnés | **SATISFAITE** | rapport, manifeste/ledgers Browser et captures Forge clear/rain ajoutés avec hashes |

## Limites visuelles et qualification humaine

Les captures native et Browser clear/rain sont très contrastées : palette claire moutarde/rose et
palette pluie verte/cyan. Elles montrent des motifs noirs stippled/jagged sur terrain, herbe,
végétation et silhouettes. La pluie est visible, mais l'eau, la SSR et la réfraction ne sont pas
clairement inspectables ; aucune capture fixe ne prouve l'animation, la stabilité temporelle, les
impacts au sol ou la wetness réversible.

Le warning wgpu natif est compatible avec un risque d'artefacts de profondeur, sans permettre
d'attribuer ces défauts à un shader précis. Aucune affirmation de supériorité sur Godot ou de parité
visuelle Cadle n'est donc acquise. La revue humaine FHD/QHD, les vues hauteur humaine/lointaine, une
vidéo de déplacement et les comparaisons matériaux/composition/A-B restent dues.

## CI distante

La CI distante reste **NOT RUN — infrastructure billing**. ADR-0006 autorise une dérogation
administrative bornée jusqu'à Gate 4 lorsque les runners ne démarrent aucune étape. Cette dérogation
ne transforme pas la CI en PASS et n'efface aucune dette locale ou visuelle.

## Verdict et travail suivant

Verdict : **POURSUIVRE GATE 1**.

Ordre minimal avant toute nouvelle décision de passage :

1. corriger ou expliquer le warning wgpu et les artefacts noirs, puis recapturer ;
2. qualifier Low/Medium/High/Ultra et l'adaptation, puis un parcours temporel représentatif ;
3. mesurer RTX 3060 ou RX 6600 en 1080p High ;
4. effectuer la revue humaine comparative Godot/Cadle et statuer sur les routes visuelles restantes.

Tant que ces éléments ne sont pas acquis, Gate 1 reste **OUVERTE** et Gate 2 est **INTERDITE** sans
nouvelle dérogation explicite acceptée.
