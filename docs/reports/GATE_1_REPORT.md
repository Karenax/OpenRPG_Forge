# Rapport Gate 1 — Visual Valley

Date de consolidation : **2026-09-01**

Verdict : **POURSUIVRE GATE 1 — GATE 1 OUVERTE**

Gate suivante : **Gate 2 interdite sans passage de Gate 1 ou nouvelle dérogation explicite acceptée**

## Résumé de décision

Visual Valley conserve une paire native/Browser exacte au commit `d9e2670`, des captures fixes, des
ledgers versionnés et une provenance CC0/Cadle auditée. Les deux variantes Browser passent chacune
**24/24 contrôles automatisés**, sans échec réseau ni warning/erreur console. Le validateur croisé
natif/Studio passe **46/46** contrôles et les sept hashes communs concordent. La matrice native
`5eadec7f` qualifie 10/10 runs Low/Medium/High/Ultra, adaptatif et benchmark, avec pipelines stables et
tier eau synchronisé. Le commit source courant `afb270c3` ajoute les temps CPU/GPU du pass opaque à
l'overlay et les rend lisibles dans deux captures natives exactes clear/rain.

Ces résultats ne ferment pas Gate 1. Le correctif forward + IBL supprime le warning wgpu enregistré
dans la campagne historique `a34596bd` et corrige les palettes rose/moutarde ainsi que les grandes
ombres noires. Des contours de végétation sombres et du stipple/aliasing restent visibles ; l'eau
n'est pas isolable dans ces cadrages. La présentation GPU Browser reste `not_observed`, la cible RTX
3060/RX 6600 n'a pas été mesurée, le parcours temporel reste à qualifier, et les captures n'ont pas
reçu d'acceptation humaine comparative. Les profils sont fonctionnels, mais cela ne corrige pas leur
stipple/aliasing visible et ne constitue pas une acceptation artistique.

Le verdict `R1-055` est **poursuivre Gate 1**, corriger et requalifier les preuves manquantes. Il ne
constitue ni un passage administratif de Gate 1, ni une autorisation implicite de commencer Gate 2.

## Identités exactes et frontières de preuve

| Portée | Commit | Tree | Usage dans ce rapport |
|---|---|---|---|
| source courante et overlay CPU/GPU | `afb270c328b7095a84d452b93b9fdd38e2737f33` | `5eeb9098b8300dead8679de7d478f5096a572fd1` | temps CPU/GPU du pass opaque visibles, fallback indisponible testé, deux ledgers/captures High exacts |
| matrice profils | `5eadec7f6f9f9b5b6734ce62ea3518f0297496f9` | `a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1` | 10 ledgers/captures natifs, quatre tiers, adaptation High vers Ultra, benchmark High et eau synchronisée |
| campagne Browser exacte | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | Browser clear/rain, manifeste Studio, assets HTTP et captures Browser |
| natif performance/captures exact-commit | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | RX 7800 XT/Vulkan, métriques v8, captures et logs natifs exacts |
| campagne graphique historique | `a34596bd733131699c70028dab86cf2c612b896a` | `8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6` | ancienne paire exacte conservée byte-for-byte ; warning et dérive visuelle reproduits |
| provenance/indépendance | `92286910a9bab622d5d54905551c0f03172e98e7` | `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe` | audit CC0, comparaison Cadle et absence de dépendance produit |
| inventaire conditionnel `R1-033` | `28985328f0d2de1fa79c705d5c3aa1bc522dbe35` | `ec71501d231ea6469eb97dc0af3d0230f4daf6eb` | inventaire automatisé des glTF gouvernés, rôles du manifeste et représentation procédurale du joueur |
| référence Cadle extérieure | `ea0da62549980816a6a27e08b75808b1f56d70e7` | `f0d52e434454fe302052d22eafde203bc361cc1f` | comparaison de provenance uniquement, clone local détaché et propre |

Le natif et le Browser forment une paire exacte d9. Le hash scenery
`6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f` est identique aux deux
manifestes Browser. Les preuves `a34596bd` et `92286910` restent historiques et ne sont pas utilisées
pour conclure la parité courante `R1-043`.

## Validation source finale

La source exacte courante `afb270c328b7095a84d452b93b9fdd38e2737f33` a été rejouée
séquentiellement :

| Contrôle | Résultat |
|---|---|
| `git diff --check` puis `cargo fmt --all -- --check` | **PASS** |
| `cargo test -p forge_engine --lib --locked -j1` | **122/122 PASS** |
| `cargo test --workspace --all-targets --locked -j1` | **298/298 PASS** — tous les crates et intégrations |
| `cargo fmt --all -- --check` | **PASS** |
| `cargo clippy --workspace --all-targets -- -D warnings` | **PASS**, zéro warning |
| check Studio `wasm32-unknown-unknown`, features `web` | **PASS**, 8,24 s |

Le check WASM conserve uniquement l'avertissement de future incompatibilité transitive connu pour
`proc-macro-error2 v2.0.1`. Cette validation couvre la source et les contrats. Le rebuild Release et
la preuve overlay afb et la matrice native 5e sont documentées ci-dessous ; la paire Browser/native d9
reste une campagne historique exacte distincte.

## Overlay CPU/GPU courant `afb270c3`

Le binaire Release exact mesure 81 745 408 octets et porte le SHA-256
`9c7a85d9719101e53a25f79d8846462561c81dd9f7e5b1f72743d0964f2caffa`. Deux runs High,
clear puis rain, ont produit 120 samples chacun, des pipelines 69/69 puis 71/71 et des logs vides.
Les captures affichent respectivement CPU 0,010 / GPU 0,674 ms et CPU 0,012 / GPU 0,661 ms pour le
pass opaque. Les ledgers mesurent 0,0101 / 0,68072 ms et 0,0153 / 0,7246 ms à des frames différentes.

Les six artefacts, leurs hashes et le contrôle d'absence de référence Cadle sont versionnés sous
[`native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay`](../evidence/gate1/native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay).
Cette preuve ferme `R1-034`. Elle ne fournit ni temps GPU de frame entière, ni compteur global de draw
calls, triangles ou instances ; `R1-037` reste donc ouverte.

## Matrice native courante `5eadec7f`

Le binaire Release courant mesure 81 743 872 octets et porte le SHA-256
`33b3ea91dff8d5d0aa4af054aacf6f575bc1dc449572b914452518acdeb1c3eb`. Dix runs 1920 x 1080,
VSync off, caméra joueur, ont été retenus sur RX 7800 XT/Vulkan : quatre tiers clear/rain,
adaptatif clear et benchmark clear.

Les dix ledgers v8 contiennent chacun 120 samples, 9/9 modèles CC0, des pipelines prêts avant et
pendant la mesure et un tier eau identique au tier appliqué. Les comptes pipelines vont de 59/59 à
74/74. L'adaptation effectue High vers Ultra à la frame 604 ; benchmark reste High. Les moyennes vont
de 5,6130 à 10,2077 ms, les p95 de 6,7886 à 13,9956 ms et les 1 % low de 56,293 à 134,937 FPS.
Low/rain contient deux hitches ; les neuf autres runs zéro.

Les vingt artefacts, leur manifeste SHA-256, les deux séries rejetées et la revue visuelle bornée
sont consignés dans
[`R1_039_040_NATIVE_QUALITY_MATRIX.md`](../evidence/gate1/R1_039_040_NATIVE_QUALITY_MATRIX.md).
Cette preuve ferme `R1-039`, `R1-040` et la condition « profils qualité fonctionnels ». Elle ne ferme
ni le GPU cible, ni le parcours représentatif, ni l'acceptation artistique.

## Preuve Browser d9 clear/rain

Les artefacts locaux sont sous :

```text
E:\OpenRPG_Forge\.gate1-browser-smoke-artifacts\d9e2670111624aa836ab138afc35cddf441865ef
```

Le lot documentaire contient leurs copies byte-for-byte sous
`docs/evidence/gate1/browser/d9e2670111624aa836ab138afc35cddf441865ef/` et les deux PNG sous
`docs/evidence/gate1/captures/`. Leur cartographie complète est fournie par
[`R1_BROWSER_WEBGPU_SMOKE.md`](../evidence/gate1/R1_BROWSER_WEBGPU_SMOKE.md).

Le manifeste externe mesure 2 503 octets et porte le SHA-256
`c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7`. Les principaux fichiers :

| Artefact Browser | Taille | SHA-256 |
|---|---:|---|
| `validation.json` | 3 507 octets | `cd6b196e06ca1e02a970c94d47ed64a6aa613f017284cd92616d8040ee6a792f` |
| `visual-valley-clear-high.browser.png` | 1 293 570 octets | `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4` |
| `visual-valley-rain-high.browser.png` | 1 323 858 octets | `7703f0c4207052010343585d12b4e6bc94ca076114f6e39a4acf4cd10be76e5a` |
| ledger scène clear | 4 043 octets | `83093be5827ff053489a8636ba470b00d437ae928324af7ce06a65f37ba4383f` |
| ledger scène rain | 4 038 octets | `30716870ca853948657bf05f3167e43221cc377ef1d5c06e0322dda3063d32c3` |
| ledger frame clear | 1 771 octets | `1f606f2540a29b5d21c41188256d9464c9e8e6ee9ef565e363fd5cf383fd2e2f` |
| ledger frame rain | 1 796 octets | `d8c8829c6c1297f8d5ed12ad8584869878a9aa5780c987f1c9e07f4370d3d21f` |

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
| durée | 1 214,1 ms | 1 147,6 ms |
| moyenne | 10,1175 ms | 9,5633 ms |
| p50 / p95 / p99 | 9,8 / 12,1 / 12,8 ms | 9,3 / 10,9 / 11,6 ms |
| FPS moyen | 98,839 | 104,566 |
| 1 % low | 77,519 FPS | 85,106 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 120 / 0 / 0 | 120 / 0 / 0 |

Ces mesures proviennent de la boucle d'update Bevy WASM, pas d'une timestamp query GPU. Le ledger
conserve `webgpu.status=checking`, `webgpu_ready_ms=null` et `gpu_presentation=not_observed`. Le PASS
automatisé valide le chargement, les invariants CPU/ECS, le réseau, le DOM, les captures et les
métriques de boucle ; il ne doit pas être renommé « présentation GPU WebGPU qualifiée ».

## Preuve native d9 RX 7800 XT exact-commit

Le binaire Release reconstruit et exécuté sur `d9e2670` utilise une AMD Radeon RX 7800 XT, Vulkan
et le pilote AMD `26.8.1 (LLPC)`, en 1920 × 1080 High fixe, VSync off. Le build Release termine en
3 min 24 s ; le binaire de 81 735 680 octets porte le SHA-256
`e44bc941b70eabab8b7e6155a262ab93491d9c2d4763454cb09bfd480e24cc75`. Les deux ledgers
`forge.runtime-metrics` v8 contiennent 120 échantillons après warm-up :

| Mesure native | Clear | Rain |
|---|---:|---:|
| moyenne | 8,3478 ms / 119,792 FPS | 8,2920 ms / 120,598 FPS |
| p95 | 8,7132 ms | 9,0950 ms |
| 1 % low | 113,775 FPS | 96,354 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| mémoire wgpu allouée / réservée | 530 273 124 / 876 609 552 octets | 534 407 108 / 876 609 552 octets |
| pipelines prêts | 69/69 | 71/71 |

La fenêtre de mesure native dure environ 0,94 seconde à caméra fixe. Les valeurs mémoire décrivent
les sous-allocations wgpu, pas la VRAM physique totale. `gpu_draw_calls` reste `null` et aucun total
exhaustif de draw calls/triangles/instances GPU n'est disponible.

Les deux logs natifs d9 sont vides : aucun warning `wgpu_core::validation` ni `ERROR` n'y est
enregistré. Cette absence qualifie ces deux exécutions seulement ; elle n'est pas extrapolée à tous
les backends ou GPU.

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

## R1-033 — N/A conditionnel au snapshot courant

Le contrôle documentaire automatisé exact `28985328`, arbre `ec71501d`, établit que les neuf glTF
gouvernés correspondent aux neuf modèles du manifeste et totalisent **0 animation**. Les seules
catégories déclarées sont `tree`, `shrub`, `fern`, `rock` et `detail` : aucun personnage ou joueur
n'est disponible. Le joueur courant est construit sans asset avec `Capsule3d::new`, sous le nom
`Forge player capsule`, et son adapter ne référence aucune API d'animation Bevy.

La condition « si asset disponible » de `R1-033` est donc fausse pour cette tranche. La route est
fermée **N/A conditionnel**, et non PASS comme implémentation d'une animation. Elle se rouvre dès
l'introduction d'un asset de personnage animé gouverné ou le remplacement de la capsule procédurale.
La commande, les neuf résultats et les assertions de réouverture sont versionnés dans
[`R1_033_PLAYER_ANIMATION_NA.md`](../evidence/gate1/R1_033_PLAYER_ANIMATION_NA.md).

## État conservateur des routes

Nouvelles cases fermées par les preuves consolidées :

- `R1-019`, `R1-020`, `R1-021`, `R1-022` et `R1-024` ;
- `R1-035`, `R1-036`, `R1-038` et `R1-042` ;
- `R1-039` et `R1-040` par la matrice native exacte 10/10 et la transition High vers Ultra ;
- `R1-034` par l'overlay CPU/GPU exact `afb270c3` et ses deux captures lisibles ;
- `R1-043` pour la paire native/Studio exact-commit et les sept hashes communs concordants ;
- `R1-044` et `R1-045` pour l'acquisition de captures fixes ;
- `R1-053` et `R1-054` pour provenance et indépendance ;
- `R1-033` comme **N/A conditionnel**, faute d'asset joueur animé gouverné dans le snapshot courant ;
- `R1-055` par le présent rapport, avec verdict « poursuivre Gate 1 ».

Restent explicitement ouvertes :

- `R1-014`, `R1-023`, `R1-025` et `R1-026` ;
- `R1-037` et `R1-041` ;
- `R1-046` à `R1-052`.

`R1-043` est fermée : les deux cibles utilisent `d9e2670`, le scenery hash concorde et
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
| absence de stutter récurrent | **OUVERTE** | zéro hitch dans la paire d9, mais fenêtres courtes/fixes sans parcours représentatif |
| profils qualité fonctionnels | **SATISFAITE** | quatre tiers fixes, clear/rain, adaptation High vers Ultra et benchmark High sur 10/10 ledgers natifs |
| parité de données native/Studio | **SATISFAITE** | commit/arbre exacts communs, 46/46 contrôles et sept hashes communs MATCH |
| code sans dépendance propriétaire obligatoire | **SATISFAITE** | audits provenance/indépendance locaux PASS |
| rapport et captures versionnés | **SATISFAITE** | rapport, manifeste/ledgers Browser et captures Forge clear/rain ajoutés avec hashes |

## Limites visuelles et qualification humaine

Les captures d9 corrigent nettement la palette rose/moutarde et les grandes zones noires : le ciel
est gris-bleu, le terrain vert et les ombres sont remplies. Des contours de végétation très sombres,
du stipple/aliasing et quelques facettes sombres restent visibles. La pluie est visible, mais l'eau,
la réflexion et la réfraction ne sont pas clairement inspectables ; une image fixe ne prouve ni
animation, stabilité temporelle, impacts au sol ni wetness réversible.

Aucune affirmation de supériorité sur Godot ou de parité visuelle Cadle n'est acquise. La revue
humaine FHD/QHD, les vues hauteur humaine/lointaine, une vidéo de déplacement et les comparaisons
matériaux/composition/A-B restent dues.

## CI distante

La CI distante reste **NOT RUN — infrastructure billing**. ADR-0006 autorise une dérogation
administrative bornée jusqu'à Gate 4 lorsque les runners ne démarrent aucune étape. Cette dérogation
ne transforme pas la CI en PASS et n'efface aucune dette locale ou visuelle.

## Verdict et travail suivant

Verdict : **POURSUIVRE GATE 1**.

Ordre minimal avant toute nouvelle décision de passage :

1. acquérir un parcours temporel représentatif, les vues hauteur humaine/lointaine, l'eau dédiée et
   une vidéo courte ;
2. acquérir l'instrumentation exhaustive `R1-037` ou documenter une décision Gate 1 dédiée ;
3. mesurer RTX 3060 ou RX 6600 en 1080p High ;
4. effectuer la revue humaine comparative Godot/Cadle et statuer sur les routes visuelles restantes.

Tant que ces éléments ne sont pas acquis, Gate 1 reste **OUVERTE** et Gate 2 est **INTERDITE** sans
nouvelle dérogation explicite acceptée.
