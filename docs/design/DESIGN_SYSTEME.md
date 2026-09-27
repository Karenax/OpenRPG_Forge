# Système de design global — OpenRPG Forge

| Champ | Valeur |
|---|---|
| Identifiant | `openrpg-forge.design-system.fr.v0.1` |
| Version | **0.1** |
| Date | **30 août 2026** |
| Statut | **Proposition normative, à qualifier progressivement par les gates** |
| Branche de rédaction | **`Rust`** |

Ce document définit la charte graphique et les contrats d’expérience communs à OpenRPG Forge. Il
réconcilie l’atelier technique bleu nuit du Studio Gate 0 avec la direction éditoriale plus lumineuse
du Manuel du joueur. Il sert de base à la future implémentation des tokens, composants, interfaces,
mondes 3D et contenus officiels.

Il ne prouve pas que les interfaces ou les systèmes décrits comme **CIBLE** sont déjà implémentés.
La vision produit reste portée par [`PROJECT_CONTEXT.md`](../../PROJECT_CONTEXT.md), les décisions
techniques par les ADR, et les preuves par `docs/evidence/` et `docs/reports/`.

## 1. Vocabulaire de statut

Chaque règle importante doit être lue avec l’un des statuts suivants :

| Statut | Signification |
|---|---|
| **EXISTANT** | Présent dans l’arbre de travail et directement inspectable. |
| **PROUVÉ** | Exécuté ou rendu avec une preuve liée à une version précise. |
| **VALIDÉ** | Décision approuvée et preuve suffisante pour son périmètre. |
| **CIBLE** | Contrat à respecter lors d’une implémentation future. |
| **EXPLORATION** | Hypothèse à comparer avant adoption. |
| **OUVERT** | Décision qui demande encore approbation, test ou ADR. |
| **DIFFÉRÉ** | Hors de la gate active. |
| **INTERDIT** | Contraire à l’identité, à l’accessibilité ou aux frontières du projet. |

Une maquette, une illustration ou une capture générée ne transforme jamais une **CIBLE** en
**EXISTANT**. Une compilation ou une métrique ne transforme jamais seule un résultat visuel en
**VALIDÉ**.

## 2. Autorité et périmètre

### 2.1 Ce que la charte gouverne

La charte s’applique à :

- la marque OpenRPG Forge et ses suffixes produit ;
- le Studio Tauri/Leptos et son viewport Bevy/WebGPU ;
- les interfaces communes du runtime joueur ;
- le Creator guidé ;
- le lanceur et les écrans de distribution ;
- la documentation, les manuels et les supports promotionnels officiels ;
- `visual_valley`, les scènes de démonstration et les contenus officiels Forge ;
- l’iconographie, les illustrations, les personnages et créatures officiels ;
- les contrats de thèmes proposés aux modules.

### 2.2 Ce qu’elle ne doit pas imposer

OpenRPG Forge est un moteur générique. Un module tiers peut choisir une autre époque, une autre
architecture, une palette différente ou un rendu stylisé. La charte ne doit pas repeindre ni
normaliser silencieusement les assets d’un auteur.

Lorsque les contrats de thèmes/modules existeront, ils devront préserver :

- la lisibilité des commandes et des états critiques ;
- les contrastes et le focus clavier ;
- l’absence de dépendance exclusive à la couleur ;
- les zones sûres et l’échelle d’interface ;
- la provenance et les licences ;
- la séparation entre thème décoratif et signification sémantique ;
- l’identification claire de la coque Forge dans le Studio, le lanceur, l’installation et les
  diagnostics.

Dans un runtime plein écran, la signature Forge peut rester discrète ou être limitée aux écrans et
métadonnées de plateforme. Le monde et le HUD du module conservent leur liberté artistique sous
réserve des contrats d’accessibilité et de sémantique critique.

La **direction Fantasy de référence** décrite plus loin gouverne les contenus officiels, le manuel et
les démonstrateurs. Elle est un profil artistique remplaçable, pas une dépendance moteur.

## 3. Vérité actuelle du produit

| Surface | Statut au 30 août 2026 | Source ou limite |
|---|---|---|
| Coque Studio | **EXISTANT** | Topbar, hiérarchie, viewport et inspecteur dans `apps/forge_studio`. |
| Sélection Studio | **PROUVÉ techniquement** | Donnée partagée, sélection et IPC ; qualité ergonomique finale non qualifiée. |
| Thème Studio | **EXISTANT, transitoire** | Bleu nuit, cyan et vert dans `apps/forge_studio/styles.css`. |
| Icône « F » | **EXISTANT, provisoire** | `apps/forge_studio/app-icon.svg` et dérivés Tauri. |
| Runtime natif | **PROUVÉ techniquement** | Scène Gate 0 et overlay ; ce n’est pas une direction artistique de jeu. |
| Manuel du joueur | **EXISTANT** | Parchemin, vert, bronze, personnages réalistes et extérieurs lumineux. |
| Illustrations du manuel | **EXISTANT, provenance initiale** | Onze images avec hashes et licence provisoire ; manifeste à compléter avant diffusion. |
| Système de design global | **CIBLE de ce document** | Aucun registre machine de tokens ni bibliothèque de composants n’existe encore. |
| `visual_valley` | **CIBLE Gate 1** | Terrain, végétation, eau, atmosphère et personnage non encore qualifiés. |
| Éditeur de monde complet | **CIBLE Gate 2** | Ne pas confondre avec la coque Gate 0. |
| HUD et interfaces RPG | **CIBLE à partir de Gate 3** | Le manuel décrit les besoins, pas leur implémentation. |
| Creator RPG | **CIBLE Gate 5** | Surface guidée à définir. |
| Lanceur | **CIBLE Gate 7** | Aucun produit de lancement Rust qualifié. |
| Multijoueur | **DIFFÉRÉ Gate 8** | Aucune surface ne doit être présentée comme disponible. |

[`ADR-0005`](../ADR/ADR-0005-studio-web-confirmed-gate0.md) retient localement Tauri 2, Leptos/WASM
et le viewport Bevy/WebGPU pour la continuation. Cette décision technique ne qualifie ni la charte,
ni l’ergonomie, ni les pixels. Les rapports antérieurs qui présentent encore ce choix comme ouvert
doivent être synchronisés dans un lot documentaire séparé avant publication.

À la date de rédaction, l’ADR-0005 existe dans l’arbre de travail mais n’est pas encore suivi par Git.
Cette charte ne doit être publiée qu’avec une référence ADR versionnée, ou après remplacement de ce
lien par la décision publiée correspondante.

## 4. Essence de la marque

> **Une forge technique ouverte sur un monde vivant.**

OpenRPG Forge combine deux expressions d’une même identité.

### 4.1 L’atelier Forge

L’outil est précis, calme et contemporain. Le fond sombre réduit la concurrence avec le viewport ;
le cyan indique l’interaction ; le vert indique une opération saine ; le monospace isole les données
techniques. L’atelier ne doit pas devenir une interface de science-fiction néon, ni une imitation de
grimoire en bois et métal.

### 4.2 Le monde vivant

Les publications et contenus officiels sont chaleureux, matériels et ouverts à la lumière : forêts,
forteresses, villages, pierre, bois, tissu, bronze, ciel et végétation. La nuit, les souterrains et la
magie existent, mais ils ne définissent pas toute l’identité.

### 4.3 Positionnement

La marque doit évoquer :

- la construction plutôt que la consommation passive ;
- la liberté de créer sans exiger de savoir programmer ;
- la matière, la lumière et les conséquences physiques ;
- une fantasy adulte crédible, sans reprendre une licence connue ;
- la précision d’un outil professionnel sans froideur administrative.

Elle ne reprend aucun logo, symbole, personnage, composition, vocabulaire propriétaire ou identité
visuelle de Dungeons & Dragons, Neverwinter Nights, Cadle ou d’un autre produit.

## 5. Principes non négociables

1. **Crédibilité avant décoration.** Les formes, matériaux, vêtements et interactions semblent
   appartenir au même monde.
2. **Lisibilité avant ambiance.** Une nuit, un brouillard ou un sort ne cache jamais l’action utile.
3. **La lumière appartient à l’identité.** Les extérieurs lumineux ne sont pas des exceptions.
4. **Cohérence des personnages.** L’anatomie, les visages et les proportions restent réalistes d’une
   illustration à l’autre.
5. **Manipulation directe avant abstraction.** L’utilisateur édite d’abord l’objet, le texte, le
   terrain ou la propriété ; les graphes et données brutes restent des diagnostics avancés.
6. **Cause et effet visibles.** Poids, contact, hauteur, eau, feu, mécanisme et magie montrent leur
   trajectoire et leur conséquence.
7. **Aucune classe visuelle imposée.** Un personnage est défini par ses actions, ses outils, ses
   habitudes et son histoire, pas par un uniforme de guerrier, mage ou voleur.
8. **Accessibilité native.** Aucun état critique n’est communiqué seulement par une couleur, un son
   ou une animation.
9. **Performance honnête.** Aucun effet n’est ajouté uniquement pour embellir une capture isolée.
10. **Preuve réelle.** Toute affirmation de qualité visuelle exige une exécution, une capture et une
    inspection humaine explicite.

## 6. Architecture de la marque

### 6.1 Noms

La forme principale est **OpenRPG Forge**. Les produits utilisent un suffixe descriptif stable :

- **OpenRPG Forge Studio** ;
- **OpenRPG Forge Runtime** seulement dans la documentation technique ;
- **OpenRPG Forge Creator** si cette surface distincte est confirmée ;
- **OpenRPG Forge Launcher** tant qu’un nom français ou commercial n’est pas approuvé.

Dans l’interface joueur, le nom du module peut devenir le titre principal ; OpenRPG Forge reste une
signature de plateforme discrète.

### 6.2 Logo et icône

L’icône actuelle, un « F » géométrique cyan sur bleu nuit avec une base verte, est **provisoire**. Elle
est lisible à petite taille et peut rester le signe de développement, mais ne verrouille pas le logo
commercial.

Règles minimales applicables dès maintenant :

- ne pas déformer, incliner ou entourer l’icône d’un effet de glow ;
- ne pas placer le signe sur un fond qui réduit son contraste ;
- conserver une zone libre au moins égale à un quart de sa largeur ;
- ne pas transformer le « F » en blason médiéval détaillé ;
- employer le wordmark complet dans les écrans d’accueil et la documentation officielle ;
- conserver les originaux vectoriels et la provenance des dérivés.

Le logo définitif, sa grille, ses tailles minimales et ses variantes monochromes restent **OUVERTS**.

## 7. Couleurs et tokens

### 7.1 Architecture des tokens

Les implémentations futures utilisent trois couches :

```text
tokens de marque → tokens sémantiques → tokens de composants
```

Un bouton ne doit pas demander directement « le cyan Forge » ; il demande une couleur sémantique
`action`, `focus`, `danger` ou `selected`. Ce découplage permet un thème clair, sombre ou module sans
modifier la signification.

### 7.2 Palette consolidée — EXISTANT + CIBLE

| Token proposé | Valeur | Statut | Rôle |
|---|---:|---|---|
| `forge-night-950` | `#04080F` | **EXISTANT** | Noir bleuté du canvas et des overlays. |
| `forge-night-900` | `#07101D` | **EXISTANT** | Fond Studio. |
| `forge-night-800` | `#0C1727` | **EXISTANT** | Surface Studio. |
| `forge-night-700` | `#122238` | **EXISTANT** | Surface surélevée. |
| `forge-line-600` | `#273D59` | **EXISTANT** | Séparateur décoratif sombre. |
| `forge-line-strong` | `#526B86` | **CIBLE** | Limite interactive accessible. |
| `forge-cyan-400` | `#56C5FF` | **EXISTANT** | Interaction, focus et information. |
| `forge-cyan-300` | `#7BDCFF` | **EXISTANT** | Éclat de l’icône, usage limité. |
| `forge-cyan-600` | `#218DCC` | **EXISTANT** | Variante profonde de l’icône. |
| `forge-growth-400` | `#6FE0A8` | **EXISTANT** | Succès et état sain. |
| `forge-forest-700` | `#274C3B` | **EXISTANT** | Structure éditoriale. |
| `forge-forest-900` | `#173428` | **EXISTANT** | Titre éditorial sombre. |
| `forge-bronze-500` | `#A66F36` | **EXISTANT** | Accent décoratif et grands titres. |
| `forge-bronze-text` | `#8A572A` | **CIBLE** | Bronze assombri pour petit texte clair. |
| `forge-parchment-100` | `#F5EEDC` | **EXISTANT** | Papier secondaire. |
| `forge-limestone-50` | `#FBF8F0` | **EXISTANT** | Papier principal. |
| `forge-slate-600` | `#496573` | **EXISTANT** | Texte éditorial secondaire. |
| `forge-charcoal-900` | `#2D2D2A` | **EXISTANT** | Encre. |
| `forge-link-600` | `#2E6F9E` | **EXISTANT** | Lien éditorial. |
| `forge-warning-400` | `#F2C66D` | **CIBLE** | Attention sombre. |
| `forge-danger-400` | `#FF7D87` | **EXISTANT** | Erreur sombre. |

### 7.3 Thème Atelier sombre

Le thème sombre est la valeur initiale du Studio, car il entoure un viewport 3D. Il n’est pas le
thème obligatoire des manuels, du lanceur ou des contenus narratifs.

Le bloc suivant est le contrat consolidé : les valeurs `border-strong` et `warning` sont encore
**CIBLE**, les autres proviennent du CSS ou de l’icône actuels.

```css
--forge-color-canvas: #04080F;
--forge-color-app-bg: #07101D;
--forge-color-field-bg: #081220;
--forge-color-toolbar-bg: #091321;
--forge-color-surface: #0C1727;
--forge-color-surface-raised: #122238;
--forge-color-selection: #17436B;
--forge-color-border-subtle: #273D59;
--forge-color-border-strong: #526B86;
--forge-color-text: #E7F0FA;
--forge-color-text-muted: #8FA5BB;
--forge-color-focus: #56C5FF;
--forge-color-info: #56C5FF;
--forge-color-success: #6FE0A8;
--forge-color-warning: #F2C66D;
--forge-color-danger: #FF7D87;
```

**CIBLE :** `border-subtle` sert aux séparateurs. Les champs, poignées de redimensionnement, zones de
dépôt et autres frontières interactives utiliseront `border-strong`.

### 7.4 Thème éditorial clair

Le bloc conserve la palette du manuel ; `editorial-bronze-text` est un complément **CIBLE** destiné au
petit texte accessible.

```css
--forge-color-paper: #FBF8F0;
--forge-color-paper-muted: #F5EEDC;
--forge-color-paper-line: #E8E4DA;
--forge-color-ink: #2D2D2A;
--forge-color-editorial-muted: #496573;
--forge-color-editorial-heading: #173428;
--forge-color-editorial-accent: #274C3B;
--forge-color-editorial-bronze: #A66F36;
--forge-color-editorial-bronze-text: #8A572A;
--forge-color-editorial-link: #2E6F9E;
```

Le bronze `#A66F36` est décoratif ou réservé aux grands titres : il n’atteint qu’environ `3,67:1`
sur le parchemin `#F5EEDC`. Le bronze texte `#8A572A` atteint environ `5,22:1` et remplace le bronze
clair pour les petites tailles.

### 7.5 Contrastes mesurés — existant et compléments cibles

| Paire | Ratio approximatif | Usage |
|---|---:|---|
| Texte `#E7F0FA` / surface `#0C1727` | `15,63:1` | Excellent. |
| Texte secondaire `#8FA5BB` / surface | `7,08:1` | Excellent. |
| Cyan `#56C5FF` / surface | `9,27:1` | Excellent. |
| Succès `#6FE0A8` / surface | `11,05:1` | Excellent. |
| Erreur `#FF7D87` / surface | `7,31:1` | Excellent. |
| Trait `#273D59` / surface | `1,63:1` | Décoratif seulement. |
| Hover `#122238` / surface | `1,12:1` | Insuffisant comme seul retour. |
| Trait fort `#526B86` / surface | `3,26:1` | Cible pour limites interactives. |

Toute nouvelle paire est mesurée avant intégration. Les ratios ne remplacent pas une inspection en
contexte, notamment sur le viewport, les textures ou les fonds semi-transparents.

### 7.6 Thèmes de modules — CIBLE

Un module pourra fournir des tokens décoratifs versionnés, par exemple : accent secondaire, image de
couverture, teinte de carte et matière de panneau narratif. Il ne peut pas remplacer :

- le focus ;
- le danger, l’attention et le succès ;
- le texte et les contrastes minimaux ;
- les confirmations destructives ;
- la sélection de l’éditeur ;
- l’identité des diagnostics Forge.

Le futur pipeline devra rendre le thème prévisualisable, validé et réversible. Aucune couleur de
module ne sera injectée dans les règles métier.

## 8. Typographie

### 8.1 Familles

| Usage | EXISTANT | CIBLE |
|---|---|---|
| Interface dense | `Inter, "Segoe UI Variable", "Segoe UI", sans-serif` | Pile système redistribuable à figer ; `"Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif` tant qu’aucune fonte n’est embarquée. |
| Lecture éditoriale | `Georgia` | `Georgia, Cambria, serif`, ou fonte gouvernée équivalente. |
| Données et code | `"Cascadia Mono", monospace` | `"Cascadia Mono", "Cascadia Code", Consolas, monospace`. |

`Inter` est déclaré dans le CSS actuel mais n’est pas embarqué. Tant qu’un WOFF2 et sa licence ne sont
pas gouvernés, la pile système reste l’autorité réellement disponible. Aucune police distante n’est
chargée au runtime.

### 8.2 Échelle UI cible

| Token | Taille / interligne | Usage |
|---|---|---|
| `text-xs` | `11 / 16 px` | Métadonnée non critique uniquement. |
| `text-sm` | `12 / 18 px` | Labels et aide secondaire. |
| `text-md` | `14 / 20 px` | Texte et contrôles par défaut. |
| `text-lg` | `16 / 24 px` | Introduction, dialogue, contrôle important. |
| `text-xl` | `20 / 28 px` | Titre de panneau ou écran. |
| `text-2xl` | `24 / 32 px` | Titre de page. |
| `text-display` | `32 / 40 px` | Accueil et communication, jamais dans l’éditeur dense. |

Règles :

- limiter les capitales et le tracking fort aux sourcils de marque courts ;
- réserver le gras aux hiérarchies et actions, pas à des paragraphes entiers ;
- utiliser des chiffres tabulaires dans les métriques et tableaux ;
- conserver des lignes de lecture longue entre 55 et 80 caractères ;
- ne pas utiliser une serif décorative dans les inspecteurs ou tables denses ;
- ne pas descendre sous 11 px, même à 4K ; l’échelle suit le DPI, pas le nombre de pixels physiques.

## 9. Espacement, géométrie et élévation

La grille repose sur 4 px.

```css
--forge-space-1: 4px;
--forge-space-2: 8px;
--forge-space-3: 12px;
--forge-space-4: 16px;
--forge-space-5: 20px;
--forge-space-6: 24px;
--forge-space-8: 32px;
--forge-space-12: 48px;

--forge-radius-sm: 4px;
--forge-radius-md: 6px;
--forge-radius-lg: 10px;
--forge-radius-panel: 14px;
--forge-radius-pill: 999px;

--forge-control-compact: 32px;
--forge-control-default: 40px;
--forge-control-touch: 44px;

--forge-focus-width: 2px;
--forge-focus-offset: 2px;
```

- Les panneaux emploient une élévation faible ; les ombres ne remplacent pas les bordures.
- Les overlays au-dessus du viewport utilisent un fond opaque ou suffisamment assombri pour rester
  lisibles sur une scène claire.
- Les modales sont rares. Une propriété contextuelle appartient d’abord à l’inspecteur ou à un
  panneau ancré.
- Les opérations destructives ne se cachent jamais dans un geste ou une icône sans libellé.

## 10. Mouvement et superposition

Les valeurs suivantes sont des **CIBLES initiales**, à ajuster après mesure :

| Interaction | Durée |
|---|---:|
| Retour immédiat de bouton | `80–120 ms` |
| Hover, focus, sélection | `120–160 ms` |
| Ouverture de panneau | `180–220 ms` |
| Dialogue ou changement de contexte | `220–280 ms` |

Principes :

- une animation est interruptible ;
- aucune transition ne retarde la commande réelle ;
- `prefers-reduced-motion` supprime déplacements, zooms et parallaxes non essentiels ;
- le chargement long affiche progression, étape et résultat, avec annulation lorsque l’opération est
  conçue comme annulable ;
- le tremblement de caméra, le flash et le zoom forcé sont configurables ;
- un état persistant ne ressemble pas à un effet éphémère.

Ordre de superposition cible : monde, gizmos, HUD, panneaux contextuels, notifications, dialogues,
modale bloquante, erreur fatale. Les tooltips ne passent jamais au-dessus d’une confirmation critique.

## 11. Contrat des composants communs

Chaque composant futur documente : anatomie, variantes, état normal, hover, focus, pressé, sélectionné,
désactivé, chargement, erreur, clavier, lecteur d’écran et densité.

| Famille | Règle principale |
|---|---|
| Boutons | Verbe clair, hiérarchie primaire/secondaire/destructive, icône non suffisante pour l’action critique. |
| Champs | Label persistant, unité visible, validation proche, limite interactive à `3:1`. |
| Curseurs | Valeur numérique éditable en plus du geste. |
| Arbres | Navigation clavier, état développé, sélection non fondée sur la couleur seule. |
| Onglets | Onglet actif annoncé sémantiquement et visuellement. |
| Tables | En-têtes persistants, tri annoncé, colonnes redimensionnables lorsque nécessaire. |
| Panneaux | Redimensionnables ou repliables, état restaurable, contenu scrollable. |
| Cartes | Une action principale, métadonnées essentielles et statut de provenance. |
| Badges | Courtes métadonnées ; jamais le seul porteur d’une erreur. |
| Menus | Recherche et raccourci lorsqu’ils dépassent une courte liste. |
| Notifications | Niveau, résultat et action suivante ; temporisation non destructive. |
| Progression | Étape réelle et reprise ; pas de barre fictive. |
| États vides | Expliquer pourquoi la surface est vide et comment agir. |
| Erreurs | Cause, impact, action et emplacement ; identifiant technique copiable en détail. |
| Diff et aperçu | Montrer avant application, portée, validation et inverse disponible. |
| Gizmos | Forme, axe, survol, actif, occlusion et précision ; couleur jamais seule. |

Les commandes avancées peuvent être denses. Leur densité ne justifie ni texte minuscule, ni états
ambigus, ni panneaux techniques imposés aux débutants.

## 12. Studio

### 12.1 Existant

La coque Gate 0 possède une topbar de 76 px, une hiérarchie de 230 px, un viewport dominant et un
inspecteur de 280 px. Elle offre un focus visible, de vrais boutons pour la hiérarchie, un
`role="alert"` pour l’erreur WebGPU et des données techniques en monospace.

Dettes connues :

- les panneaux ne sont ni redimensionnables ni repliables ;
- le frontend impose `min-width: 960px` ;
- les bordures interactives et le hover sont trop discrets ;
- les glyphes Unicode ne constituent pas une famille d’icônes finale ;
- les statuts dynamiques manquent de régions `aria-live` ;
- la résolution `1920 × 1080` affichée est codée en dur et peut être fausse ;
- les contrôles de hiérarchie restent proches de 36 px ;
- la capture et la revue humaine à l’état courant restent à qualifier.

### 12.2 Contrat cible

- Le viewport reste visuellement dominant.
- La manipulation directe est primaire ; l’inspecteur explique et affine.
- Les graphes, JSON, métriques et traces sont des vues avancées repliables.
- Une sélection apparaît par surface, contour, structure et libellé, pas uniquement par couleur.
- Les opérations longues ne bloquent ni le viewport ni l’annulation.
- Une mutation expose aperçu, validation, résultat et possibilité d’annulation lorsqu’ils existent.
- Les diagnostics utilisent les données réelles et ne se substituent pas au contenu.
- Un rerender de présentation ne reconstruit jamais le canvas ou le monde.

### 12.3 Layout et résolutions

Les tokens suivants sont **CIBLE** ; l’existant Gate 0 reste à `76/230/280 px` pour la topbar, la
hiérarchie et l’inspecteur.

```css
--forge-topbar-height: 72px;
--forge-toolbar-height: 40px;
--forge-panel-left-default: 240px;
--forge-panel-right-default: 320px;
--forge-panel-min: 200px;
--forge-panel-max: 480px;
```

| Largeur logique | Comportement cible |
|---|---|
| `≥ 1440 px` | Trois surfaces, inspecteur 320 px, densité confortable. |
| `1100–1439 px` | Trois surfaces compactes, inspecteur 280 px. |
| `< 1100 px` | Inspecteur repliable ou drawer ; pas de débordement imposé. |
| `< 800 px` | Hiérarchie et inspecteur en onglets/drawers pour la build navigateur. |

Les captures de qualification utilisent Full HD au minimum, QHD comme référence préférée si elle est
disponible, et 4K avant une distribution qui prétend le supporter. La fenêtre historique est
configurée à `1600 × 900` et sa capture consignée avec décorations mesure `1602 × 932` : elle reste
une preuve Gate 0, pas la cible de qualification finale. À 4K, la mise à l’échelle système **doit être
testée** à 100, 150 et 200 %.

## 13. Runtime joueur

Cette section gouverne les interfaces RPG **à partir de Gate 3**. La Gate 3 exige d’abord le HUD
minimal, l’interaction, le dialogue, l’inventaire et les retours du parcours combat/quête/sauvegarde.
Le journal étendu, la carte et les menus plus riches restent des cibles ultérieures.

- Le HUD laisse le monde respirer et rapproche l’information critique de l’action.
- À mesure de leur introduction, exploration, dialogue, combat, inventaire, journal, carte et pause
  partagent une hiérarchie commune.
- Les retours de contact, fatigue, blessure, concentration et progression correspondent au
  [Manuel du joueur](OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md) sans recalculer les règles dans l’UI.
- Les détails d’un calcul sont accessibles à la demande, pas affichés en permanence.
- Les états de groupe ne couvrent pas le centre de l’écran.
- Les sous-titres, l’échelle UI, le contraste et la réduction des effets sont réglables.
- La pause tactique éventuelle conserve une lecture claire des intentions et conséquences.
- Le thème module peut adapter les matières décoratives, jamais la sémantique critique.

Aucun écran de HUD décrit ici ne doit être présenté comme déjà disponible.

## 14. Creator

Cette section est **CIBLE Gate 5**. Le Creator doit être plus guidé et moins dense que le Studio
technique, qu’il soit finalement un mode du Studio ou une surface distincte.

- Employer le vocabulaire de l’auteur, pas celui du moteur.
- Prévisualiser immédiatement la conséquence d’un choix.
- Organiser les tâches en parcours courts : créer, renseigner, vérifier, tester.
- Éditer directement les lignes de dialogue, objectifs, déclencheurs et propriétés.
- Conserver graphes de dépendances et formats bruts comme diagnostics avancés.
- Expliquer ce qui est temporaire, sauvegardé, invalide ou non publié.
- Ne jamais masquer une mutation IA : plan, diff, validation et annulation restent visibles.

Dialogues avancés, quêtes, factions, créatures, capacités et profils de règles restent des livrables
futurs tant que leurs gates ne sont pas prouvées.

## 15. Lanceur

Cette section est **CIBLE Gate 7**.

Le lanceur doit ressembler à une porte d’entrée vers des modules, pas à un installeur technique :

- bibliothèque de modules avec image, titre, version, provenance et compatibilité ;
- page de module lisible, avec action Jouer dominante ;
- installation, mise à jour, réparation et désinstallation expliquées ;
- paramètres graphiques et accessibilité disponibles avant lancement ;
- diagnostics séparés mais accessibles ;
- erreurs de contenu ou de version actionnables ;
- marque du module visible sans dissoudre la coque Forge.

Les fonctions réseau et multijoueur restent **DIFFÉRÉES Gate 8**.

## 16. Documentation et illustration

La documentation longue utilise le thème éditorial clair :

- calcaire/parchemin pour le support ;
- vert forêt pour la structure ;
- bronze pour l’accent, jamais pour du petit texte insuffisamment contrasté ;
- bleu pour les liens et informations ;
- serif confortable pour le corps ;
- sans-serif pour l’interface numérique ;
- monospace pour identifiants, données et code ;
- titres sémantiques, table des matières, en-têtes répétés et textes alternatifs.

Les illustrations actuelles du manuel constituent un **corpus de référence provisoire** : anatomie
réaliste, équipement plausible, encre détaillée, gouache texturée et modelé pictural retenu. Leur
lumière et leur palette varient selon le chapitre. La convergence des visages est engagée mais doit
encore être qualifiée par une future planche de personnages ; la cohérence faciale est une **CIBLE**,
pas une preuve déjà acquise.

![Devant une forteresse claire, des voyageurs négocient, étudient la herse, transportent des ressources et s’entraînent.](assets/player-handbook/bright-fortress-life.png)

*Référence éditoriale d’extérieur lumineux. Cette image ne constitue pas une preuve runtime.*

Une illustration officielle :

- raconte une situation et plusieurs actions cohérentes ;
- montre les conséquences et les objets utilisables ;
- conserve des visages adultes, des proportions naturelles et des expressions sobres ;
- évite l’alignement de héros posant pour une classe ;
- ne contient ni texte, ni logo, ni watermark dans l’image source ;
- n’imite aucun artiste vivant nommé ni licence connue ;
- possède source, hash, licence, usage et texte alternatif.

## 17. Direction Fantasy de référence

### 17.1 Piliers

- **Fantasy crédible** : un monde habité, construit, entretenu et réparé.
- **Lumière vivante** : jour, ciel, humidité et saisons participent à la lecture.
- **Densité lisible** : richesse aux trois plans, avec respirations et points d’attention.
- **Matérialité** : pierre, bois, métal, tissu, cuir, terre et plante réagissent différemment.
- **Aventure systémique** : l’image révèle hauteur, couverture, poids, corde, eau, feu ou mécanisme.
- **Échelle humaine** : portes, marches, garde-corps, outils et mobilier sont praticables.

### 17.2 Environnements et lumière

Le corpus artistique progressif doit couvrir : plein jour clair, heure chaude, couvert lumineux,
intérieur avec sources crédibles, forêt dense, nuit ou magie. Pour six captures représentatives
réparties au fil des gates :

- au moins trois extérieurs franchement lumineux : un naturel — forêt, vallée ou rive —, un
  architectural — fort, forteresse, village ou ville — et un troisième biome ou cadrage distinct ;
- au maximum deux scènes de nuit ou très sombres ;
- au moins une scène sociale, architecturale ou quotidienne ;
- au moins une interaction physique lisible.

Ce corpus est une cible globale, pas un prérequis ajouté à Gate 1. Gate 1 qualifie uniquement les
états de `visual_valley` prévus par la roadmap ; la scène sociale et les interactions RPG sont
rattachées aux gates qui les introduisent.

Les ombres ne doivent pas écraser les visages, chemins, portes ou objets nécessaires. Le contraste
d’ambiance ne justifie pas des noirs bouchés. Une source sombre est localisée et motivée : nuit,
souterrain, menace, météo ou magie.

![Dans une forêt ensoleillée, quatre voyageurs pistent un animal, franchissent un ruisseau, examinent des plantes et observent des oiseaux.](assets/player-handbook/bright-forest-exploration.png)

*Référence de forêt claire et d’actions simultanées, pas standard technique du renderer.*

### 17.3 Architecture régionale

La charte exige une logique constructive, pas une architecture unique. Chaque pack officiel déclare :

- climat, ressources et niveau technique ;
- matériaux dominants ;
- structure, ouvertures, toitures et circulation ;
- palette régionale ;
- motifs décoratifs limités ;
- vieillissement causé par eau, soleil, passage ou réparation ;
- modules, sockets et règles de raccord.

Les masses sont crédibles, les entrées dégagées et les silhouettes reconnaissables. La végétation ne
bloque pas une porte, un escalier ou une zone jouable pour masquer une composition faible. Les
bâtiments sont utilisables, pas seulement des façades.

### 17.4 Matériaux PBR

- Workflow metal/roughness commun aux cibles native et WebGPU.
- Albedo sans ombre, reflet ou éclairage peint.
- Diélectriques non métalliques ; saleté et rouille ne sont pas du métal.
- Variation de roughness sur les surfaces importantes ; pas de plastique uniforme.
- Microdétail subordonné aux formes principales.
- Échelle UV et densité de texels cohérentes dans un même pack.
- Usure localisée par cause : arête, contact, ruissellement, chaleur, pas ou réparation.
- Matériau vérifié sous lumière neutre, soleil, ombre et humidité.
- Neige, mousse, poussière et wetness reposent sur des masques cohérents.

Les budgets numériques de texture et de matériau restent **OUVERTS** jusqu’aux mesures Gate 1. Ils
seront ajoutés dans une annexe technique plutôt qu’inventés ici.

### 17.5 Composition du monde

- Organiser grandes masses, formes intermédiaires et détails.
- Garder un horizon ou une ligne de fuite lisible dans les extérieurs.
- Donner à chaque zone un point d’intérêt et des repères d’orientation.
- Éviter la répétition évidente des mêmes arbres, rochers et façades.
- Stratifier lisière, canopée, sous-bois, sol et clairières.
- Intégrer eau, chemins et bâtiments au relief, sans objets flottants ou intersections artificielles.
- Conserver les entrées, seuils, collisions et circulations visuellement compréhensibles.

Les profils Low, Medium, High et Ultra préservent la même intention, les mêmes affordances et la même
lecture ; ils réduisent la densité et le coût, pas l’information de gameplay.

## 18. Personnages, créatures et équipement

### 18.1 Personnages officiels

- Anatomie adulte réaliste et proportions humaines naturelles.
- Réalisme crédible plutôt qu’hyperréalisme cireux.
- Diversité d’âges, traits, teintes de peau, morphologies et conditions physiques.
- Expressions naturelles, asymétries sobres et regard cohérent avec l’action.
- Vêtements superposés, réparés et adaptés au climat.
- Équipement attaché, porté et usé de façon plausible.
- Armures compatibles avec mobilité, poids, attaches et protection.
- Rôle lisible par action et matériel, jamais par uniforme de classe.
- Sexualisation gratuite, peau plastique et armures impossibles sont interdites.

![Dans une cour en ruine, des voyageurs s’exercent, étudient une carte, soignent une blessure et examinent un mécanisme.](assets/player-handbook/progression-by-practice.png)

*Référence provisoire de scène multi-action et de progression par l’action. Une planche de
personnages réaliste destinée aux visages et proportions reste **OUVERTE**.*

### 18.2 Créatures et bestiaire officiels

- Silhouette reconnaissable à distance et en mouvement.
- Squelette, locomotion, masse et centre de gravité cohérents.
- Anatomie fantastique fondée sur une logique biologique ou magique explicable.
- Habitat, alimentation, comportement et traces cohérents avec le monde.
- Menace télégraphiée par posture, rythme, son et forme, jamais seulement rouge/vert.
- Matériaux corporels distincts : peau, plume, fourrure, carapace, os ou manifestation magique.
- Variantes d’âge, de milieu ou de fonction sans simple recoloration.
- Aucun monstre, nom, symbole ou design dérivé d’une licence commerciale.

Un profil module peut adopter une autre direction artistique. Les contenus promotionnels et
démonstrateurs Forge utilisent la direction réaliste définie ici.

## 19. Caméra, FX et magie

### 19.1 Caméra

- Hauteur humaine et horizon stable par défaut.
- Avant-plan, plan d’action et profondeur clairement composés.
- Point d’intérêt séparé par lumière, forme ou mouvement.
- Profondeur de champ faible réservée aux dialogues et cinématiques.
- Vignettage, aberration chromatique et flou de mouvement faibles, désactivables.
- Les captures de validation utilisent un cadrage reproductible et non un angle qui cache un défaut.

### 19.2 Effets

- Cause, trajectoire, contact et conséquence restent visibles.
- La lumière d’un effet influence les surfaces proches sans brûler l’image.
- Bloom contenu ; pas de masse blanche ou cyan permanente.
- Particules bornées afin de ne pas masquer silhouettes, dangers et impacts.
- Télégraphes fondés sur animation, forme, son et contraste, pas seulement la couleur.
- Feu, eau, glace, fumée, poussière et débris respectent les surfaces et le système physique.
- Les profils graphiques conservent l’information même avec moins de particules.

![Dans un observatoire nocturne, quatre voyageurs préparent un cercle, des ingrédients et des instruments autour d’une faille contenue.](assets/player-handbook/magic-observatory.png)

*La nuit et la magie restent lisibles grâce à des sources localisées et des matières visibles.*

## 20. Iconographie

- Grilles de 16, 20, 24 et 32 px.
- Silhouettes simples, poids et terminaisons cohérents.
- État sélectionné communiqué par forme, bordure et couleur.
- Libellé obligatoire pour les commandes critiques.
- Pictogrammes modernes et précis dans le Studio.
- Pictogrammes plus illustrés possibles dans le runtime, encore lisibles à 24 px.
- Aucun emoji ou glyphe système comme icône produit définitive.
- Pas de crâne, rune, parchemin ou ornement fantasy sur chaque fonction.
- Toute famille tierce est licenciée, versionnée et attribuée.

La famille d’icônes définitive reste **OUVERTE**. Les glyphes `▱`, `◈`, `☀` et `◉` de Gate 0 sont des
placeholders fonctionnels.

## 21. Accessibilité

La cible du Studio, du Creator, du lanceur et de la documentation numérique est WCAG 2.2 AA.

- Contraste d’au moins `4,5:1` pour texte normal.
- Contraste d’au moins `3:1` pour texte large, icônes informatives et limites interactives.
- Focus visible sur tous les contrôles, liens, gizmos clavier et menus.
- Ordre clavier logique et sans piège.
- Aucun état communiqué seulement par couleur.
- Cible minimale de 24 px ; 40 px par défaut et 44 px pour actions principales ou tactiles.
- Zoom, DPI et échelle UI sans perte ni clipping.
- Texte alternatif pour toute illustration informative.
- Sous-titres avec locuteur et indication hors champ lorsque l’audio existe.
- Réduction du mouvement, des flashes et du tremblement de caméra.
- Mode de contraste renforcé prévu comme **CIBLE**, sans le déclarer existant.
- Messages d’erreur actionnables et statut dynamique annoncé par `aria-live` lorsque nécessaire.
- Le canvas possède un nom accessible et une alternative décrivant son état.

Tout signal sonore critique possède un équivalent visuel ou textuel. Toute information visuelle
critique possède un libellé ou une explication accessible.

## 22. Cohérence audiovisuelle — CIBLE

L’identité sonore détaillée dépend du moteur audio encore **OUVERT**, mais les contrats d’expérience
sont déjà définis :

- un son d’interface est court, discret et associé à une action ou un état réel ;
- succès, attention et danger se distinguent par rythme, hauteur, forme visuelle et texte, pas par un
  jingle ou une couleur seuls ;
- les priorités empêchent une notification mineure de couvrir dialogue, danger ou accessibilité ;
- le silence reste un état valide : aucune boucle décorative n’est obligatoire dans un outil dense ;
- les sous-titres indiquent le locuteur, le hors-champ et les informations sonores utiles ;
- un indicateur directionnel visuel peut compléter une source hors champ sans devenir un radar
  permanent ;
- musique, ambiance, voix, effets et interface sont réglables séparément lorsque le mixeur existe ;
- tout son généré ou importé suit la même gouvernance de provenance que les images et modèles ;
- aucun token visuel ne dépend d’une API ou d’un moteur audio particulier.

## 23. Localisation, responsive et rédaction

- Le français est la langue actuelle ; les layouts tolèrent au moins 30 % d’expansion de texte.
- Les actions utilisent un verbe concret : « Importer », « Tester », « Annuler », « Réparer ».
- Les titres nomment l’objet ou la tâche, pas l’architecture interne.
- Une erreur explique cause, impact et action suivante.
- Les identifiants techniques sont copiables, mais placés dans un détail secondaire.
- Les termes restent cohérents avec le Manuel du joueur et les schémas versionnés.
- Une icône seule ne remplace pas un libellé dans un parcours rare ou risqué.
- Les points de suspension signalent une étape supplémentaire seulement lorsqu’elle existe.
- Le runtime conserve des zones sûres pour 16:9 et n’annonce ultrawide qu’après qualification.
- L’échelle UI est indépendante de la résolution de rendu et respecte le DPI du système.
- Creator et lanceur replient les informations secondaires avant de masquer l’action principale.
- Les panneaux d’édition deviennent repliables ou en drawers avant de provoquer un débordement.
- La documentation reste lisible à l’écran, en PDF A4 et lors d’un zoom à 200 %.
- Aucun ratio d’image, largeur de panneau ou nombre de caractères n’est codé comme donnée utilisateur.

## 24. Gouvernance des assets — CIBLE

Chaque asset officiel doit posséder avant qualification ou diffusion :

- identifiant logique ;
- source et auteur ou générateur ;
- licence ou `LicenseRef` ;
- hash SHA-256 ;
- format, dimensions et taille ;
- date et version ;
- transformations et outils ;
- relation entre original et dérivé ;
- usage autorisé et statut redistribuable ;
- texte alternatif si visuel ;
- validation technique ;
- revue visuelle humaine et statut ;
- stratégie de remplacement.

Règles :

- aucun asset Cadle n’entre dans Forge ;
- aucun asset extrait d’un jeu commercial ;
- aucun fichier sans provenance ;
- caches, bibliothèques locales et sources redistribuables restent séparés ;
- un asset généré n’est jamais présenté comme capture du runtime ;
- les images du manuel conservent `LicenseRef-OpenAI-Generated-Output` jusqu’à vérification des
  conditions de diffusion ;
- toute variante générée hérite de la provenance et référence son parent.

Les manifestes actuels constituent une base de provenance, pas encore le contrat complet ci-dessus.
Ils devront migrer explicitement sans perdre leurs hashes, sources ou relations de dérivation.

## 25. Validation visuelle

### 25.1 Paquet de preuve

Chaque changement visuel significatif doit produire pour être qualifié :

- commit ou état Git exact ;
- exécutable et chemin réellement lancés ;
- cible : Studio, navigateur, runtime, Creator ou lanceur ;
- résolution, DPI, profil et thème ;
- seed, heure et caméra pour une scène 3D ;
- GPU, CPU, backend et pilote lorsqu’ils influencent le rendu ;
- capture brute et non retouchée ;
- métriques pertinentes ;
- comparaison avec la référence précédente ;
- inspection humaine explicite ;
- défauts, limites et fallback ;
- provenance des assets visibles.

Les métriques, marqueurs et sidecars aident à diagnostiquer. Ils ne remplacent jamais la revue des
pixels. Les tests pixel-perfect sont réservés aux surfaces déterministes.

### 25.2 Matrice de la gate qualifiée

La matrice s’applique uniquement aux surfaces présentes dans la gate en cours. Gate 0 n’exige pas
magie ou créatures ; Gate 1 n’exige pas les sous-titres du futur runtime. Une ligne devient obligatoire
quand la surface correspondante entre réellement dans le lot.

| Surface | États à vérifier |
|---|---|
| Studio | Normal, hover, focus, sélection, chargement, erreur, panneau étroit, DPI élevé. |
| Viewport | Données identiques au runtime, picking, overlay clair sur scène sombre et lumineuse. |
| Runtime | Jour, couvert, intérieur, nuit, magie et interaction physique. |
| Personnages | Visages, proportions, équipement, animation et éclairage. |
| Créatures | Silhouette, locomotion, télégraphe, matériaux et variantes. |
| Documentation | Écran, PDF A4, navigation, tableaux, contraste et texte alternatif. |
| Accessibilité | Clavier, zoom, contraste, daltonisme, mouvement réduit, sous-titres. |
| Assets | Hash, licence, provenance, fallback et absence de placeholder. |

### 25.3 Résolutions

- Full HD `1920 × 1080` : preuve obligatoire.
- QHD `2560 × 1440` : référence préférée si disponible pour les interfaces denses et la 3D.
- 4K `3840 × 2160` : limite haute de qualification de cette version de la charte.
- Ultrawide : vérification des zones sûres et de la composition avant prise en charge annoncée.

Une gate de fondation peut employer un placeholder explicitement déclaré et borné. Un résultat ne
passe pas si les textes se chevauchent, si un panneau masque le viewport, si un fallback inattendu
apparaît dans le lot qualifié, si une entrée est impraticable ou si les visages changent de style.

## 26. Do / Don’t

| Faire | Ne pas faire |
|---|---|
| Montrer forêts, villages et forteresses en plein jour. | Enfermer toute la gamme dans le noir ou le brun. |
| Garder les mêmes proportions réalistes entre scènes. | Passer à des visages anime, cartoon ou plastiques. |
| Associer atelier froid et monde naturel par des tokens communs. | Transformer le Studio en grimoire décoratif. |
| Afficher les diagnostics comme couche avancée. | Imposer graphes, JSON ou métriques comme interface principale. |
| Rendre poids, contact et conséquences compréhensibles. | Masquer les interactions derrière un glow universel. |
| Employer forme, libellé et couleur pour un état. | Communiquer uniquement en rouge et vert. |
| Vérifier le runtime réel et les pixels. | Valider une qualité graphique avec un marqueur automatisé seul. |
| Gouverner chaque asset et dérivé. | Importer un asset inconnu, Cadle ou extrait d’un jeu. |
| Laisser les modules définir leur direction artistique. | Injecter une apparence de module dans la sémantique Forge. |
| Présenter les fonctions futures comme des cibles. | Montrer un mockup comme fonction déjà livrée. |

## 27. Contrat d’implémentation

Le Markdown reste l’autorité humaine initiale. Le format machine des tokens est **OUVERT**. Quand il
sera introduit, il devra :

- être versionné ;
- générer ou alimenter CSS et types Rust sans duplications divergentes ;
- séparer marque, sémantique et composant ;
- valider les couleurs et références ;
- supporter les thèmes sans exécuter de code de module ;
- fournir un fallback déterministe ;
- enregistrer les changements incompatibles.

Ordre d’adoption aligné sur les gates :

1. **Maintenant — documentation :** charte, statuts, tokens candidats et décisions ouvertes.
2. **Gate 0/1 :** extraire les tokens du CSS existant, corriger contraste, focus et placeholders sans
   ajouter de fonction tardive.
3. **Gate 1 :** bible 3D, matériaux, lumière, personnages de référence et captures de
   `visual_valley`.
4. **Gate 2 :** bibliothèque de composants Studio, panneaux, gizmos et manipulation directe.
5. **Gate 3 :** HUD, menus et feedbacks de gameplay.
6. **Gate 4 :** cartes d’assets, provenance et états d’import.
7. **Gate 5 :** parcours Creator.
8. **Gate 7 :** lanceur et distribution.
9. **Gate 8 :** surfaces multijoueur.

Chaque tranche suit : audit de l’existant, modification bornée, test, exécution, capture, inspection,
correction, puis documentation du statut. Aucun lot n’adopte tout le système de design d’un coup.

## 28. Décisions ouvertes

- Logo, wordmark et signature définitifs.
- Nom commercial éventuel de la nouvelle génération.
- Thème sombre seul ou thèmes clair/sombre pour le Studio.
- Police embarquée, formats et licence.
- Famille d’icônes officielle.
- Format canonique des tokens et stratégie de génération.
- Résolution minimale réellement supportée.
- Qualification ultrawide.
- Niveau exact de personnalisation visuelle accordé aux modules.
- Thème HUD Forge par défaut.
- Creator comme mode du Studio ou produit distinct.
- Approbateur artistique et procédure de dérogation.
- Politique finale de diffusion des images générées.
- Budgets de textures, matériaux, VFX et personnages après Gate 1.
- Synchronisation de l’ADR-0005, de la roadmap et du rapport Gate 0.

Une décision ouverte ne doit pas être résolue implicitement dans un composant isolé.

## 29. Critères d’acceptation de la charte

Cette version est prête à servir de base lorsque :

- les surfaces présentes et futures sont clairement distinguées ;
- les thèmes Atelier et éditorial partagent une même marque sans uniformité sombre ;
- les tokens et leurs usages sémantiques sont définis ;
- les contrastes sont mesurés ;
- les règles de personnages réalistes et d’extérieurs lumineux sont explicites ;
- la liberté des modules tiers est préservée ;
- les composants et états attendus sont inventoriés ;
- l’accessibilité et le responsive sont testables ;
- la gouvernance des assets est complète ;
- la validation exige une capture réelle et une inspection humaine ;
- aucune fonction future n’est annoncée comme disponible ;
- toute évolution normative incrémente la version du document.

## 30. Références internes

- [`PROJECT_CONTEXT.md`](../../PROJECT_CONTEXT.md) — vision et gates.
- [`AGENTS.md`](../../AGENTS.md) — contraintes d’architecture, d’assets et de preuve.
- [`ADR-0005`](../ADR/ADR-0005-studio-web-confirmed-gate0.md) — shell Studio retenu localement.
- [`MIGRATION_ROADMAP.md`](../planning/MIGRATION_ROADMAP.md) — ordre des gates.
- [`BROWSER_STUDIO_VALIDATION.md`](../evidence/gate0/BROWSER_STUDIO_VALIDATION.md) — preuve Gate 0 et limites.
- [`OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md`](OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md) — règles et expérience joueur.
- [`asset-provenance.json`](assets/player-handbook/asset-provenance.json) — charte et provenance des illustrations.
- [`styles.css`](../../apps/forge_studio/styles.css) — palette Studio réellement codée.
- [`app-icon.svg`](../../apps/forge_studio/app-icon.svg) — icône de développement actuelle.
