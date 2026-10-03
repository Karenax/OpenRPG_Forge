# Gate 3 Tidewater : recette de rig physique

**État : préflight non qualifiant — 2026-10-02**

Ce document décrit une géométrie candidate pour un lancer, un support
cassable et une interaction feu/eau. Il ne contient aucune preuve de
contact Rapier, de dégâts, de destruction, de chute de charge, de feu ou
d'extinction. Aucun build, playtest, copie de fixture ou changement du
moteur n'a été effectué pour ce préflight. Les positions proposées doivent
être traitées comme des entrées de mesure, pas comme des contacts garantis.

## Sources et méthode

La mesure provient de la fixture Tidewater canonique suivante :

* zone :
  `E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-authoring-001\e620dd19-ead9-5913-a6d6-0f93109148c0\authoring.json` ;
* raster :
  `E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-authoring-001\e620dd19-ead9-5913-a6d6-0f93109148c0\source-blobs\be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin` ;
* format : `Float32LE`, 2048 × 2048, origine XZ `(-1024,-1024)`,
  espacement 1 m, hauteur extérieure `-90 m` ;
* empreinte du raster :
  `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.

Les hauteurs suivent `SampledTerrain::height_at` dans
`crates/forge_world/src/sampled_terrain.rs:50-75` : coordonnées de cellules
centrées (`-0.5`) puis interpolation bilinéaire de valeurs Float32. Ce
point est important : une hauteur lue à un coin n'est pas une hauteur de
vertex arbitraire.

La copie d'auteur contient 23 placements importés, mais
`head.corrections.paths` et `head.corrections.rivers` sont vides. Elle ne
fournit donc pas de position d'eau maritime exploitable pour cette recette.
Les valeurs d'eau ci-dessous sont une procédure d'oracle à appliquer sur un
head Tidewater qui expose réellement une surface, pas une coordonnée
inventée pour cette fixture.

## Mesures raster pour le rig de lancer

Le rig demandé est centré en `(x=19, z=-270)` : plateau fixe de dimensions
`0.5 × 0.05 × 0.5 m`, puis cube enfant de dimensions `0.5 × 0.5 × 0.5 m`.
Les hauteurs du terrain sous le plateau sont :

| Point XZ | Hauteur terrain (m) |
| --- | ---: |
| centre `(19,-270)` | `58.57132339477539` |
| coin `(18.75,-270.25)` | `58.65581703186035` |
| coin `(19.25,-270.25)` | `58.825910568237305` |
| coin `(18.75,-269.75)` | `58.31231880187988` |
| coin `(19.25,-269.75)` | `58.49124717712402` |

Le maximum de l'empreinte est donc `58.825910568237305 m` et le relief
mesuré dans cette petite empreinte est `0.513591766 m`. La plateforme plane
est une géométrie authorée ; le raster seul ne fournit pas une dalle plane.
Pour conserver une marge de `0.02 m` au-dessus de ce maximum :

* centre Y de la plateforme :
  `58.825910568237305 + 0.025 + 0.02 = 58.87091056823731` ;
* dessus de la plateforme : `58.895910568237305` ;
* centre du cube enfant posé avec la même marge de `0.02 m` :
  `59.16591056823731` ;
* centre du cube en contact géométrique exact avec la dalle, sans marge :
  `59.14591056823731`.

Le calcul balistique de ce document utilise le premier centre du cube
(`59.16591056823731`) afin de ne pas commencer avec une pénétration de
collider. La relation `plateforme.supports = [cube]` est nécessaire si le
cube doit rester fixe avant le lancer ; elle est une relation de gameplay,
pas la preuve d'un contact physique. La plateforme reste `mass_kg=0`,
`pushable=false`, `throwable=false`. Aucun gel spécial ne doit être ajouté.

Le gardien demandé en `(16,-270)` se trouve à :

* `Hterrain(16,-270) = 57.556617736816406 m` ;
* centre attendu d'un acteur ajouté avec Y vide par l'éditeur :
  `Hterrain + 0.95 + 0.02 = 58.526617736816406 m`.

Le `0.95 m` vient de l'offset de placement des acteurs dans
`apps/forge_studio/src/authoring_gameplay/editor.rs:360-386`. La capsule
Rapier du gardien est `Collider::capsule_y(0.6, 0.35)` dans
`crates/forge_engine/src/gameplay/projection.rs:30-48` : son demi-hauteur
totale est `0.95 m`, sous réserve de vérifier la pose effective du runtime.
Le terrain descend donc de `1.014705658 m` entre les centres `(19,-270)` et
`(16,-270)`, soit environ `18.69°` sur cette ligne. Cette pente est une
mesure de terrain central, pas une qualification de marche ou de glissement.

Le point d'approche joueur `(21.5,-270)` a `Hterrain =
59.4508113861084 m`. La hauteur joueur n'est pas fixée ici : elle appartient
à la pose KCC et doit être lue dans le snapshot de playtest.

## Lancer de 30 kg : dérivation et limite

Dans `apps/forge_studio/src/authoring_gameplay.rs:296-318`, la direction
horizontale est calculée depuis le joueur vers l'objet, puis le lancer
utilise `speed=5` et `up=3`. Avec joueur `(21.5,-270)` et cube `(19,-270)` :

```text
delta XZ = (-2.5, 0) / 2.5 = (-1, 0)
impulsion pour 30 kg = (-1*30*5, 30*3, 0) = (-150, 90, 0)
contribution de vitesse attendue si la masse Rapier est 30 kg = (-5, 3, 0) m/s
```

La distance d'interaction est `2.5 m`, donc elle est sous le profil de
`3.0 m` construit par l'éditeur (`editor.rs:283-287`). Le joueur doit tout
de même avoir une `confirmed_contact` issue de Rapier :
`apply_impulse` refuse une action sans ce constat
(`crates/forge_gameplay/src/actions.rs:317-370`). Le lancer marque ensuite
le cube `released`; la projection passe le corps en dynamique et applique
`ExternalImpulse` (`crates/forge_engine/src/gameplay/projection.rs:95-117,
188-200`).

Pour une première comparaison seulement, en supposant une gravité réelle
de `9.81 m/s²` et l'absence de vitesse antérieure, la trajectoire du centre
est :

```text
x(t) = 19 - 5t
z(t) = -270
y(t) = 59.16591056823731 + 3t - 4.905t²
```

Le moteur doit fournir la gravité et la vitesse réellement observées ; cette
équation n'est pas un substitut à un traceur Rapier. Aux instants utiles :

| t (s) | X (m) | Y centre calculé (m) | Y bas du cube (m) |
| ---: | ---: | ---: | ---: |
| 0.0 | 19.0 | 59.165910568 | 58.915910568 |
| 0.2 | 18.0 | 59.569710568 | 59.319710568 |
| 0.4 | 17.0 | 59.581110568 | 59.331110568 |
| 0.6 | 16.0 | 59.200110568 | 58.950110568 |

À `t=0.6 s`, le centre passe sur le X du gardien. Le bas du cube reste
`1.393492831 m` au-dessus de la hauteur raster locale
`57.556617736816406 m`. Le centre du cube est environ `0.673492831 m` au
dessus du centre d'acteur attendu ; il reste dans la partie haute de la
capsule de hauteur totale `1.9 m`. Avec un cube de demi-largeur `0.25 m`, un
recouvrement idéal de volumes est donc plausible sur cette ligne droite.

Cela ne prouve ni le `CollisionEvent`, ni la normale de contact, ni le dégât.
Le CCD, les contacts avec la dalle/terrain, la rotation du cube, la gravité
effective, la restitution et la friction doivent être enregistrés. Les
positions `x=18.5` et `x=19` sont les premiers candidats à comparer :
`x=18.5` est exactement à 3 m du joueur et est donc plus sensible aux
arrondis ; `x=19` garde 2.5 m de portée. Des points plus proches du gardien
réduisent la distance mais ne réutilisent plus exactement ce calcul de
lancer.

La formule de dommage du domaine est
`E = 0.5 * masse * vitesse_normale²`, puis `floor(E) - protection`, borné à
la vitalité (`crates/forge_gameplay/src/environment.rs:225-267,
401-404`). Un gardien créé par l'éditeur a 100 de vitalité et 0 de
protection ; à titre de seuil mathématique seulement, 30 kg nécessitent une
vitesse normale d'au moins `sqrt(200/30) = 2.582 m/s` pour atteindre 100 J.
La vitesse normale effective ne peut pas être déduite de la vitesse initiale
et ne doit pas être transformée en promesse de neutralisation.

## Support cassable et charge de 20 kg

Le domaine accepte un support fixe cassable : `mass_kg=0`,
`pushable=false`, `throwable=false`, `break_energy_j` renseignée. La
validation accepte cette combinaison (`crates/forge_gameplay/src/validation.rs:199-216`).
Lorsqu'un contact dynamique atteint le seuil, le support est détruit et les
enfants de `supports` reçoivent `Released`
(`crates/forge_gameplay/src/environment.rs:249-257,
275-309`). Ce lien reste logique tant qu'aucun oracle Rapier n'a observé le
contact entre les volumes.

Recette de contrôle proposée, sans la présenter comme un piège qualifié :

1. conserver une plateforme ou un montant fixe de faible épaisseur, avec un
   seuil `break_energy_j` choisi après une mesure de projectile ; ne pas
   choisir un seuil en espérant un dégât déterminé ;
2. ajouter une charge de `20 kg`, dimensions `0.5 × 0.5 × 0.5 m`,
   `throwable=false` avant libération, dans `supports` du montant ;
3. ajouter un petit projectile distinct, par exemple `2 kg` et
   `0.25 × 0.25 × 0.25 m`, `throwable=true`, pour déclencher le support ;
4. vérifier l'absence de mouvement spontané avant action, le contact du
   projectile avec le support, l'énergie mesurée, puis l'ordre
   `Destroyed(support)` → `Released(charge)` ;
5. seulement après ces traces, mesurer si la charge tombe, roule ou atteint
   la capsule du gardien. Une relation `supports` ne donne pas à elle seule
   une direction de chute.

La géométrie actuellement mesurée expose une limite importante. Le lancer
à `(19,-270)` atteint le X du gardien à environ `Y=59.20 m`, tandis que le
sommet de sa capsule attendue est `59.476617737 m`. Un support suspendu
directement au-dessus du gardien devrait donc être plus haut que cette
trajectoire. Le petit projectile peut servir à qualifier la rupture sur un
support placé dans l'arc, mais il ne constitue pas encore une solution
qualifiée pour atteindre un support haut au-dessus du gardien. Sans montant,
mur ou guide gameplay canonique, un support fixe suspendu est une géométrie
de test et peut flotter visuellement ; la végétation importée n'est pas une
ancre de collision authorée.

À titre de coordonnées de préflight seulement, un support direct pourrait
être défini avec dimensions `0.6 × 0.1 × 0.6` à
`(16, 59.546617737, -270)`. Sa base serait à `0.02 m` au-dessus du sommet
de la capsule attendue (`59.476617737 m`). Une charge de `0.5 m` avec
`0.02 m` d'écart aurait alors un centre à `(16, 59.866617737, -270)`. Cette
géométrie respecte les dimensions calculées, mais son montage visuel reste
absent de la fixture. Elle doit rester un cas de mesure : avec la trajectoire
à `(19,-270)`, le petit projectile arrive à environ `Y=59.20 m` sur
`x=16`, soit environ `0.347 m` sous le centre du support ; ses demi-hauteurs
(`0.125 + 0.05 m`) ne suffisent pas à conclure à un contact.

La variante minimale à mesurer séparément est un support placé près du
sommet de l'arc, autour de `x=17.5`, avec son Y calculé à partir de la trace
réelle du projectile. Elle qualifie `break → release`, pas le fait que la
charge tombe sur le gardien. Pour un support réellement au-dessus du
gardien, il faut un montage générique visible et une mesure d'une trajectoire
de charge vers la capsule ; aucune hauteur arbitraire ne doit être inscrite
dans la recette.

Le document gameplay n'expose pas de réglage de friction ou de restitution
dans `ObjectPhysics`. La stabilité sur une pente, le roulement d'un cube et
le glissement vers `x=16` sont donc des observations à faire, pas des
propriétés garanties du rig.

## Feu et eau sur une surface Tidewater réelle

Les contrôles de l'éditeur actuel sont les champs génériques
`flammable`, `heat_damage_per_tick` et `break_energy_j`
(`apps/forge_studio/src/authoring_gameplay/editor.rs:221-228`). Si une
version v1 de l'UI ne présente pas les deux champs feu, la compatibilité
attendue est de conserver `flammable=false` et `heat_damage_per_tick=0` dans
le document sauvegardé et de consigner l'absence des contrôles ; elle ne
constitue pas un authoring feu. Une UI v2 doit exposer ces deux contrôles
avec cardinalité déterministe avant de réclamer une preuve de feu.

`Ignite` et `Extinguish` sont les actions génériques. L'extinction est
refusée sans `water_contact` (`crates/forge_gameplay/src/actions.rs:390-425`).
L'observation est fournie par le moteur, non par un booléen authoré :
`crates/forge_engine/src/gameplay/simulation.rs:85-170, 450-460` vérifie
`TidewaterPlayerState.water_query_step`, la proximité de la position de
requête à moins d'un mètre du joueur, `motion.water_mean`, puis le mode nage
ou une profondeur de wade positive. Les champs correspondants sont définis
dans `crates/forge_engine/src/tidewater_player.rs:33-49`.

Pour la prochaine mesure sur une vraie surface maritime :

1. charger un head Tidewater dont le snapshot d'eau et la projection sont
   prêts ; la fixture de ce document ne contient pas de rivière authorée ;
2. placer un objet inflammable au bord de la surface à partir d'un XZ lu du
   query state, sans inventer son Y ;
3. soumettre `Ignite` dans la portée et consigner le receipt, la combustion
   et les dégâts de chaleur ;
4. déplacer le joueur jusqu'à ce que `water_query_step>0` et que
   `water_mean`/`wade` ou `Swim` soient observés, puis soumettre
   `Extinguish` et consigner l'observation `water_contact=true` et le
   receipt accepté ;
5. répéter après sauvegarde/restauration pour séparer l'état authoré de la
   perception runtime.

`apps/forge_studio/src/authoring_playtest.rs:58-66, 226-245` refuse encore
les chemins/rivières auteur pour un terrain importé et attend la projection
d'eau lorsqu'elle est requise. C'est une contrainte de lancement à vérifier
avant cette partie de la recette, pas une preuve que la fixture actuelle
possède une surface jouable.

## Oracle minimal de qualification ultérieure

Une exécution native candidate doit conserver au minimum :

* hash du raster et coordonnées XZ/Y réellement sauvegardées pour plateau,
  cube, gardien et joueur ;
* état du cube avant interaction (fixe, sans mouvement spontané), action
  affichée, `confirmed_contact`, impulse demandée et événement de passage en
  dynamique ;
* trace à 60 Hz de position, rotation, vitesse, CCD et contacts ;
* paire source/cible, `normal_speed_m_s`, énergie, événement `Impact`, dégâts
  et vitalité du gardien ;
* pour le support, énergie seuil, `Destroyed`, `Released`, position et
  vitesse de la charge, sans déduire un contact de la seule relation
  `supports` ;
* pour l'eau, `water_query_step`, position de requête, `water_mean`, mode,
  `wade`, `water_contact` et receipt d'extinction ;
* sauvegarde puis restauration avec les états `released`, `burning`,
  `destroyed`, vitalité et inventaire.

Le verdict actuel reste **NO-GO pour une preuve Gate 3 physique Tidewater** :
le raster et la géométrie donnent un premier candidat mesurable, mais aucun
contact, dommage, break/release, feu, eau ou parcours visuel n'a été
observé dans ce préflight.
