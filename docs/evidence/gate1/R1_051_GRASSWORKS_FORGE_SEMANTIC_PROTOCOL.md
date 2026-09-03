# R1-051 — Protocole sémantique Grassworks / Forge

Statut : **TECHNIQUEMENT PRÊT POUR UNE FUTURE INTÉGRATION AU PAQUET HUMAIN — ROUTE OUVERTE**

Ce protocole fixe la comparaison visuelle qui manquait à `R1-051`. Son contrat machine canonique est
[`r1-051-grassworks-forge-semantic-protocol.v1.json`](r1-051-grassworks-forge-semantic-protocol.v1.json).
Il relie par taille, dimensions et SHA-256 deux captures live courantes de Three.js Grassworks, un
rendu actuel du post X historique et quatre vues Forge déjà acquises. Tous les médias restent hors du
dépôt et sont consultés in situ.

La définition du protocole ne constitue pas une revue humaine. Elle ne ferme ni `R1-051`, ni Gate 1,
et ne remplace ni une capture contemporaine du 29 août 2026, ni les octets de la vidéo source.

## Vérification technique

Depuis un worktree de la branche `Rust` contenant ce protocole :

```powershell
$grassworks = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-051-grassworks-current-20260903T142341Z'
$forge = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'

pwsh -NoProfile -File .\scripts\Test-Gate1R1051GrassworksProtocol.ps1 `
    -VerifyExternal `
    -GrassworksExternalRoot $grassworks `
    -ForgeExternalRoot $forge `
    -SelfTest
```

Le validateur refuse les propriétés ou valeurs de schéma inattendues, les chemins absolus ou avec
traversal, les racines situées dans le dépôt ou qui se recouvrent, les reparse points, les tailles,
hashes, signatures et dimensions divergents, les axes incomplets et toute tentative de transformer le
protocole en verdict ou autorisation de Gate 1. Les self-tests altèrent des copies en mémoire et des
fichiers temporaires ; ils ne produisent aucun verdict visuel.

## Médias liés, jamais embarqués

Racine Grassworks externe :
`E:/OpenRPG_Forge/.gate1-tools/evidence/r1-051-grassworks-current-20260903T142341Z/`.

| Observation externe | Dimensions | Octets | SHA-256 | Usage autorisé |
|---|---:|---:|---|---|
| `grassworks-high-sunny-1920x1080.jpg` | 1920 × 1080 | 456 471 | `96e5e96777ed63f778db989f0e77cb641618c0fe4bf7f401c9f9d1f1a73848d5` | comparaison sémantique live courante |
| `grassworks-high-rain-1920x1080.jpg` | 1920 × 1080 | 379 020 | `17703bcc42db55ea49b1caea5d804de9aee7f39c7470363fe8a470cc5a97f0d0` | comparaison sémantique live courante |
| `x-post-2093382978105966827-with-video-frame-1905x1072.jpg` | 1905 × 1072 | 128 035 | `43be0cebbaf0bd7d58dbbe08de02c7b60932383133580146f95b1d75db7d5a3c` | identité et contexte uniquement |

La troisième image est un **rendu navigateur effectué le 3 septembre 2026** du post publié le 28 août.
Elle montre un frame de la vidéo embarquée, mais elle n'est pas le média vidéo source et ne prouve pas
l'état exact vu par l'utilisateur le 29 août. Elle est donc exclue de tout verdict sur les cinq axes.

Racine Forge externe :
`E:/OpenRPG_Forge/.gate1-tools/evidence/r1-023-water-125ba0b-001/`.

| Vue Forge | Dimensions | Octets | SHA-256 |
|---|---:|---:|---|
| `captures/human-clear.png` | 1920 × 1080 | 2 083 983 | `b144fe5b4b428f25467ddcb9e6c4060c8e027eb9ef81755aa710c2e3e62d7b42` |
| `captures/distant-clear.png` | 1920 × 1080 | 2 030 378 | `df3d5693d0dcd3120a4407cd17283dbe06dfb8fd5da9ec6c6185fd6e47185e8e` |
| `captures/water-clear.png` | 1920 × 1080 | 2 138 344 | `ed03bfee3eaaecaf1af27cbe03e9af4cb4ad116157d99bfe37b8b6fc5f2b0cbc` |
| `captures/water-rain.png` | 1920 × 1080 | 2 177 341 | `5a97f46289469c0f33a7c011b73eb59e209c4400b7068b323279667d2b284506` |

Ces vues proviennent de la campagne Forge exacte
`125ba0b8bb1fe04b2b34cae5abc41c26d1919b00`, arbre
`6d4639c824ccfa81c3d5ed911903cec2bb3c5074`. Le protocole n'affirme pas que les caméras, mondes,
assets ou moteurs sont géométriquement équivalents.

## Panneaux de comparaison

Ouvrir les images à leur résolution originale et utiliser la même échelle d'affichage à l'intérieur
d'un panneau. Ne pas appliquer de filtre, retouche ou recadrage. La comparaison est sémantique : elle
cherche un impact visuel comparable malgré deux directions artistiques et deux scènes différentes.
Une comparaison pixel à pixel est interdite.

| Panneau | Grassworks | Forge | Axes |
|---|---|---|---|
| hauteur humaine ensoleillée | `grassworks_high_sunny_current` | `forge_human_clear_125ba0b` | densité, naturel, profondeur, lumière, finition |
| profondeur paysagère ensoleillée | `grassworks_high_sunny_current` | `forge_distant_clear_125ba0b` | densité, naturel, profondeur, lumière, finition |
| pluie, eau et atmosphère | `grassworks_high_rain_current` | `forge_water_rain_125ba0b` | naturel, profondeur, lumière, finition |
| réponse clair → pluie | Sunny puis Rain | `forge_water_clear_125ba0b` puis `forge_water_rain_125ba0b` | naturel, lumière, finition |

Le dernier panneau compare deux états indépendants ; il ne prouve pas une transition temporelle
synchronisée entre les moteurs.

## Cinq axes obligatoires

Pour chaque axe, le relecteur humain écrit au moins une observation concrète reliée aux panneaux
requis et choisit exactement `accepted`, `rejected` ou `inconclusive` :

1. **Densité** — sous-bois, herbes et volumes végétaux suffisamment riches, sans masquer chemins,
   silhouettes ou lecture de l'espace.
2. **Naturel** — variation des espèces, échelles, orientations, transitions de sol et réactions météo ;
   absence de répétition artificielle dominante.
3. **Profondeur** — séparation du premier plan, plan moyen et lointain, silhouettes, brume et échelle.
4. **Lumière** — cohérence du soleil, des ombres, de l'exposition, des reflets et de la réponse à la
   pluie.
5. **Finition** — intention visuelle globale, continuité des matériaux et absence d'artefact dominant,
   de rupture flagrante ou d'impression de démo vide.

`accepted` signifie que Forge est jugé au moins aussi convaincant que ces échantillons Grassworks sur
l'axe, compte tenu des différences de direction artistique. `rejected` ou `inconclusive` exige un
problème bloquant explicite. Aucun score, hash ou test automatique ne peut produire ce verdict.

## Frontières et travail restant

- la démo live est mutable et aucun commit, tag ou numéro de version public n'est identifié ;
- Sunny et Rain décrivent uniquement l'état visible le 3 septembre 2026 ;
- aucune capture locale contemporaine du 29 août 2026 n'est disponible ;
- les octets et le SHA-256 de la vidéo source du post ne sont pas disponibles ;
- aucun code, bundle, média ou asset Grassworks n'est conservé dans Forge ;
- aucun asset Cadle n'est impliqué dans ce protocole ;
- `human_review_performed=false`, `r1_051_closed=false` et
  `gate1_transition_authorized=false` restent obligatoires.

Une future révision du paquet humain pourra référencer le commit qui introduira ce protocole et son
SHA-256, puis faire vérifier les deux racines externes avant de recevoir un reçu signé par le
propriétaire humain. Le paquet humain actuel n'est volontairement pas modifié dans ce lot : l'identité
Git du protocole doit être connue avant de pouvoir l'y lier sans ambiguïté.
