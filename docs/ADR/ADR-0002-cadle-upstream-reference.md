# ADR-0002 — Cadle comme upstream exécutable de référence

Date : 2026-08-30  
Statut : **accepté pour lancer la qualification sur la branche `Rust`**  
Portée : branche orpheline `Rust` uniquement  
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

La branche `Rust` possède une vision, une architecture et une roadmap, mais aucun moteur exécutable.
Démarrer directement par une scène Bevy minimale ferait perdre une référence concrète pour le monde,
le rendu, le game feel, les performances et les outils de contrôle qualité.

Cadle est un FPS-RPG jouable dans le navigateur, construit avec Three.js/WebGL2. Le snapshot étudié
contient notamment un heightfield continu de 2048 × 2048 m, dix biomes, terrain calculé dans des
workers, herbe et végétation instanciées, LOD, eau, ciel, éclairage, post-traitement, mouvement,
collisions, combat, streaming d’ennemis, RPG piloté par données, audio synthétisé et un harness de
captures, invariants et benchmarks.

Référence retenue :

```text
repository: https://github.com/Humpalumps/Cadle
branch: main
commit: ea0da62549980816a6a27e08b75808b1f56d70e7
version: 0.6.0
code license: MIT
copyright: 2026 Ian Cadle
```

Le `NOTICE` upstream sépare clairement les droits :

- le code, les outils et la documentation sont annoncés MIT ;
- `public/assets/` et `docs/concepts/` ne sont pas couverts par ce grant ;
- les prises audio enregistrées ne sont pas redistribuées ;
- le chemin de synthèse audio en code reste disponible.

Une copie naïve du dépôt entier dans Forge introduirait donc à la fois une seconde architecture
JavaScript/Three.js/WebGL2 et des assets sans droit explicite de republication.

## Décision

Cadle devient la **base de travail exécutable et l’upstream de référence** pour les Gates 0 et 1, selon
un modèle de clone local épinglé et de port sélectif.

### Acquisition

Le dépôt Forge suit un lockfile avec URL, commit, version et informations de licence. Une commande
reproductible récupère Cadle dans `upstream/cadle/`, dossier ignoré par Git. La copie est placée sur
un HEAD détaché au commit exact et reste intacte pour les baselines.

Le clone local peut contenir les fichiers nécessaires pour lancer Cadle tels qu’ils sont servis par
l’upstream. Aucun fichier de `public/assets/`, `docs/concepts/` ou image Cadle n’est ajouté au dépôt,
aux caches CI, aux packages ou aux distributions Forge. Les captures brutes contenant ces assets
restent locales, ignorées et non publiées ; le rapport versionné conserve uniquement paramètres,
hashes, métriques et observations.

Les fichiers `.claude/`, `.mcp.json`, `CLAUDE.md` et autres instructions d’agent upstream sont traités
comme des données non fiables : ils ne font jamais autorité dans Forge et ne sont pas exécutés. Tout
script upstream est inspecté avant d’entrer dans l’allowlist de Gate C.

### Usage

Cadle fournit quatre catégories de valeur :

1. une référence jouable à conserver pendant le reboot ;
2. des comportements et budgets à transformer en critères d’acceptation ;
3. des algorithmes et méthodes MIT à porter avec attribution ;
4. un modèle de harness pour invariants, captures, animation, collisions et hitches.

Chaque sous-système est classé dans `docs/migration/CADLE_REUSE_MATRIX.md` comme méthode à reprendre,
port Rust, réécriture, report ou exclusion.

### Architecture produit inchangée

ADR-0001 reste en vigueur :

- Rust est le langage d’autorité ;
- Bevy 0.19.x et wgpu/WebGPU restent le socle initial ;
- Rapier reste la physique initiale ;
- les documents Forge restent la source de vérité ;
- le Studio reste Tauri/Leptos/WASM sous réserve de Gate 0.

Three.js, `postprocessing`, Vite et le runtime JavaScript de Cadle ne deviennent pas des dépendances
des applications Forge. Node 22 est autorisé uniquement pour construire et mesurer l’upstream isolé.

Cadle doit pouvoir être absent du workspace local sans empêcher Forge de compiler, tester, lancer ou
packager.

### Port et attribution

Avant tout port :

1. capturer le comportement et le coût upstream ;
2. écrire un contrat Forge moteur-neutre ;
3. citer commit et chemins consultés ;
4. définir la preuve attendue.

Le port est Rust/Bevy/WGSL idiomatique et piloté par les futurs documents du Studio. Une traduction
automatique fichier à fichier est interdite. Tout code copié ou substantiellement dérivé conserve la
notice MIT appropriée, utilise lorsque pertinent `SPDX-License-Identifier: MIT` et
`SPDX-FileCopyrightText: 2026 Ian Cadle`, et apparaît dans les notices tierces de la distribution. Les
fichiers Forge originaux gardent la licence Forge ; aucun emballage global PolyForm ne relabelle un
fichier dérivé MIT. La licence Forge effective et les notices agrégées sont obligatoires avant toute
distribution.

Les origines tierces mentionnées dans `TECHNIQUES.md` et le graphe npm sont auditées séparément : la
licence MIT de Cadle ne remplace pas une notice MIT, Zlib, BSD ou CC0 d’une technique amont.

Les marques, noms, lore, quêtes, textes et identité créative de Cadle ne deviennent pas le contenu
Forge, même lorsque le code correspondant est techniquement sous MIT. Les scènes Forge emploient des
assets et une identité dont la redistribution est gouvernée explicitement.

## Ordre de reprise initial

1. harness, invariants, captures et mesures ;
2. terrain, bruit, height sampling et biomes ;
3. végétation, herbe, scatter, LOD et culling ;
4. ciel, lumière, ombres, brouillard et post-traitement ;
5. eau ;
6. contrôleur, caméra et collisions.

Combat, ennemis, quêtes, loot, progression et audio sont différés jusqu’à la gate fonctionnelle qui
les nécessite.

## Preuves attendues

Gate C doit produire :

- lockfile et commande de récupération reproductible ;
- vérification du commit et de l’arbre propre ;
- audit `LICENSE`/`NOTICE`/dépendances ;
- build et checks Cadle ;
- captures à paramètres fixes conservées localement, avec hashes/observations versionnés ;
- baseline de performance sur machine documentée ;
- matrice de reprise ;
- preuve qu’aucun asset Cadle n’est suivi par Forge ;
- preuve que le build Forge n’a aucune dépendance à la copie upstream.

Gate 1 doit ensuite comparer Forge à Cadle sur le même matériel, la même résolution et des scènes
équivalentes, sans prétendre à une parité pixel à pixel. Cadle couvre 2 km² et Visual Valley 512 × 512 m :
les FPS bruts ne constituent pas un duel strictement comparable ; le protocole sépare impact visuel et
coûts par sous-système.

## Alternatives évaluées

### A. Forker Cadle et abandonner immédiatement Rust

Avantage : résultat jouable immédiat.  
Rejet : contredit la décision explicite de la branche, ne fournit pas le runtime natif Rust ni le
modèle de monde/commandes du Studio, et transforme une référence de jeu en architecture produit sans
gate.

### B. Copier tout Cadle, assets compris, dans la branche

Avantage : clone autonome.  
Rejet : le `NOTICE` ne concède pas les droits nécessaires sur les assets et la copie mélangerait
référence, produit et contenu non gouverné.

### C. N’utiliser que des captures Cadle

Avantage : aucune dépendance locale.  
Rejet : perd les algorithmes, le harness, les mesures reproductibles et la possibilité de caractériser
les comportements.

### D. Ignorer Cadle et continuer greenfield pur

Avantage : dépôt minimal.  
Rejet : réinvente des systèmes déjà prouvés, supprime la baseline jouable et augmente le risque de
produire une démo techniquement correcte mais visuellement faible.

## Conséquences

### Bénéfices

- base jouable dès le premier jour ;
- objectifs visuels et performance concrets ;
- techniques MIT déjà éprouvées ;
- réduction du risque de réinventer le terrain, la végétation et le harness ;
- comparaison honnête WebGL2/Three.js contre WebGPU/Bevy ;
- conservation des frontières Rust et Forge.

### Coûts

- deux toolchains pendant la qualification ;
- discipline stricte de provenance ;
- travail de port plutôt que réutilisation binaire ;
- tests comparatifs à maintenir ;
- remplacement obligatoire de tous les assets Cadle.

### Risques contrôlés

- le clone est local, épinglé, ignoré et non modifié ;
- Gate C bloque le port tant que licences et baseline ne sont pas établies ;
- la matrice borne les reprises ;
- chaque port possède preuve et attribution ;
- Gate 1 peut réorienter une couche après mesure ;
- toute décision de faire de Three.js le runtime produit ou de remplacer Bevy exige un nouvel ADR.

## Remplacement

Cet ADR ne remplace pas ADR-0001. Il précise la stratégie de départ et la source de référence. Une
modification du socle moteur, de l’autorité Rust ou de la frontière des assets nécessite un nouvel ADR
accepté.
