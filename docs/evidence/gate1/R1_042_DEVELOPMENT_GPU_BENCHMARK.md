# R1-042 — Mesure prolongée RX 7800 XT, 2026-09-06

## Actualisation du 8 septembre — sensibilité à la résolution

Les résultats du 6 septembre ci-dessous portent sur 636 arbres et ne décrivent
pas le candidat actuel à 1 662 arbres. Celui-ci a été mesuré sans modification
du moteur, du kit BLEND ou des règles forestières. Binaire natif dev optimisé
SHA-256 `16011A7DE419835D8AAD93B03B97104DAA5A0A662629FF8A6F6763D7A89F954C`,
RX 7800 XT/Vulkan, High, VSync demandée désactivée, caméra du preview inchangée.
Comparaison A/B/B/A séquentielle, 300 frames de chauffe puis 899 échantillons.

| Ordre / fichier | Résolution réelle finale | FPS | p95 ms | 1 % low FPS |
| --- | --- | ---: | ---: | ---: |
| full-a2 | 1920 × 1080 | 40,388 | 26,232 | 35,127 |
| half-a2 | 960 × 540 | 47,298 | 22,805 | 37,900 |
| half-b2 | 960 × 540 | 47,192 | 22,879 | 38,565 |
| full-b2 | 1920 × 1080 | 40,514 | 26,122 | 34,491 |

La réduction à un quart des pixels apporte environ 17 % de cadence, mais reste
sous 60 FPS. Elle n'est ni un profil produit livré ni une qualification Full HD.
Le dernier relevé GPU de transparence passe de 5,84 ms (full-a2) à 3,61/3,73 ms
(half-a2/b2). Les coûts dépendants des pixels contribuent donc au résultat ;
la cause des coûts restants (géométrie, traitement CPU, synchronisation ou
instrumentation) n'est pas isolée. Ne pas annoncer un bottleneck CPU établi,
ni réduire la densité forestière ou la résolution cible pour déclarer la gate passée.
Ne pas répéter ce même test : la suite utile exige une mesure discriminant ces coûts.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-resolution-cost-20260908`.
`Compare.ps1` conserve commandes, deadline de 60 s par processus et nettoyage ciblé.
Quatre sorties 0 ; 899 échantillons, 1 662 arbres, hashes terrain et végétation
identiques contrôlés dans les JSON. Les deux redimensionnements sont confirmés
avant le préchauffage ; pas de captures ni de build concurrent pendant ces mesures.
Un premier essai `full-a` a échoué explicitement : redimensionner vers la taille
déjà active n'a pas produit le nouvel événement attendu par le harness. Journal
conservé ; script corrigé pour ne pas demander ce redimensionnement sans effet.
Cet échec ne fait pas partie des quatre mesures et n'est pas masqué.

| JSON | SHA-256 |
| --- | --- |
| full-a2 | `868CBFDE752E1BB412FF6FFCD40024425378ECB63E20A5EBFDE1A1D5F5A7F2E6` |
| half-a2 | `1B0F631705B78694F7E280BF5BF583650CD558C5574CEACB12D56D34FA4D86F6` |
| half-b2 | `CB8C046BB4A5ACB3D0FA230D630ADA2A31219F8913BF69B3C8AFDD44ACA4388A` |
| full-b2 | `45381711BAFB80B9AA5F4383DDA725B31C1F4A3497B8DF983D93C846518B9425` |

PIDs 14080, 5392, 29388 et 36468 terminés. Aucun navigateur ouvert pour ce lot.
Le candidat Studio reste inchangé. Aucune qualification artistique, GPU cible
ou Studio n'est déduite de cette comparaison native ; Gate 1 reste ouverte.

## Verdict borné

Trois mesures de développement terminées, cohérentes et vérifiées. La moyenne dépasse
60 FPS et le 1 % low dépasse 45 FPS ; **le p95 dépasse 16,7 ms aux trois passages**.
Ce résultat n'est pas une qualification de performance de Gate 1 : GPU de développement,
build dev optimisé partiellement, presets eau/environnement partiels, vue fixe et worktree
modifié. Ne pas extrapoler à RTX 3060/RX 6600, au Studio ou à un build release.

## Conditions

- Rust, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, divergence locale 0/103 avec
  origin/Rust, modifications préservées. Pas de commit, pull, nouvelle branche ou push.
- Windows 11 Professionnel 10.0.26200 ; Ryzen 5 7600 (6 cœurs/12 threads) ; RX 7800 XT,
  Vulkan, pilote AMD 26.8.1 (LLPC).
- Binaire natif fraîchement construit, SHA-256
  `5328bd6b4a4b886224f14f01c89cc2ad31b9c02e9bcf9ec21b8de917a281a06f`.
  Profil Cargo dev : code Forge opt-level 1, dépendances opt-level 3 ; debuginfo désactivé
  via RUSTFLAGS. Ce n'est pas un binaire release.
- 1920 × 1080 au début et à la fin ; AutoNoVsync demandé, VSync demandée false.
  High fixe, pas d'adaptation ; cinq domaines CPU projetés, eau/environnement Partial,
  `available_budgets_projected=true`, `full_preset_applied=false`.
- WorldSketch rev3, kit candidat révision 2, caméra canonique `human_bridge`, clear.
  20 instances du kit chargées, 36 colliders prêts, 636 arbres / 7 632 primitives LOD.
  Aucun asset Cadle, aucune réduction de scène entre les répétitions.
- Trois processus successifs, 600 images de préchauffage, arrêt à 6600. La fenêtre du
  collecteur exclut les bornes et contient **5 999 images** par passage. Aucune compilation,
  capture ou autre processus Forge lancé par cette tâche pendant les mesures. Les autres
  applications de l'utilisateur n'ont pas été fermées ; exclusivité système non garantie.

## Résultats

| Passage | Durée mesurée (s) | FPS moyens | p95 (ms) | 1 % low (FPS) | Images >16,7 ms | Images >33,3 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 98,46 | 60,93 | 17,50 | 49,80 | 1 540 | 0 |
| 2 | 97,41 | 61,58 | 17,21 | 50,65 | 998 | 0 |
| 3 | 97,54 | 61,50 | 17,28 | 49,89 | 1 027 | 0 |

Calculs du collecteur `forge_core::FrameDistribution` inspectés : FPS = 1000 / moyenne
des durées ; p95 nearest-rank ; 1 % low = 1000 / moyenne des 60 durées les plus lentes.
Les JSON conservent les agrégats, pas la série brute permettant un recalcul indépendant.
Les timings GPU par passe sont des derniers relevés, pas une distribution complète ni
une mesure de goulot d'étranglement. Aucun gain de performance n'est revendiqué.

Trois codes de sortie 0, aucun événement ERROR, `runtime_errors_observed=false`.
Les warnings de pipeline existants sur `@invariant` restent visibles dans les logs ;
absence d'erreur ne veut pas dire absence de warning ou qualification artistique.

## Reproduction et preuves

Racine externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-dev-gpu-benchmark-20260906/`.
`build.log`, `git-status.txt`, `run-1..3.log`, `run-1..3.json`, `validate.ps1`, `validation.log`.
Le validateur exécuté contrôle trois sorties propres, fenêtre, histogrammes, High partiel,
preuves matérielles eau, caméra, instances et forêt. Résultat PASS pour ces invariants,
**pas pour le seuil p95**.

Pour chaque numéro N, avec le binaire ci-dessus et un chemin de sortie neuf :

```text
forge_runtime --world-sketch-visual-valley --quality high --no-vsync
  --metrics-warmup-frames 600 --exit-after-frames 6600 --metrics <racine>/run-N.json
  --weather clear
  --asset-kit-build <fixture>/asset-kit-build.v1.json
  --asset-kit-preview <camera>/human-preview.v1.json
  --prepare-vegetation-catalog <fixture>/prototype-catalog.v1.json
  --prototype-forest-preview
```

Fixture : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-runtime-20260906-b/`.
Camera : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-gpu-cycle-20260906/`.
Cette copie de preview sélectionne une caméra existante, sans modifier le projet.

SHA-256 des JSON :

- run-1 : `fcfa0411eb67df8800f08121cd14a6173c7a09a9854244ac8220dab4e6ab2e6a` ;
- run-2 : `dda8d15143d06e60a98a6311615deaa5aafac05d0ff635f170686c4cb44562bd` ;
- run-3 : `cf4cfe59325437de64188d0880e64d91a5a439a1d3db33ecaf1a8243435548f1`.

Les 443 tests et Clippy du lot précédent restent les preuves du code inchangé ; ici,
nouveau build natif et exécutions seulement, puis contrôles documentaires. Pas de nouvelle
preuve WASM/Tauri. Qualification finale sur arbre propre, build candidat, matériel cible,
scènes/météo complémentaires et acceptations humaines toujours requises. CI NOT RUN.
