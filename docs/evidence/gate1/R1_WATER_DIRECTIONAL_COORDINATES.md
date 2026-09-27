# R1 — rides directionnelles et coordonnées métriques

## 9 septembre — recherche externe et limite du candidat optique

Recherche demandée par le propriétaire, puis confrontation aux sources locales
Bevy 0.19.1 et Forge. Analyse seulement : aucun shader, asset, binaire ou profil
modifié, aucune capture ni mesure relancée. Gate 1 reste ouverte.

Références primaires consultées :

- [Alex Tardif, Water Walkthrough](https://alextardif.com/Water.html) :
  composition des normales, reflets écran avec repli environnement, réfraction
  rejetant les objets devant la surface et adoucissement des intersections.
  L'auteur précise que le SSR apporte surtout aux eaux plus calmes avec un
  décor à refléter, et peut coûter plus que son bénéfice sur une eau agitée.
  Son exemple est artistique, pas un modèle physique à recopier tel quel.
- [GPU Gems, Effective Water Simulation from Physical Models](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models) :
  séparation déplacement/détail, filtrage des petites ondes et composition
  des reflets selon l'angle. Une surface animée seule ne remplace pas son
  environnement réfléchi. Les techniques décrites ne nécessitent pas de
  reprendre les assets ou le code des démonstrations.

### Confrontation au code courant

| Élément | État inspecté | Conséquence |
| --- | --- | --- |
| Normales animées et filtrées | `visual_valley_water.wgsl`, `water_flow_detail.wgsl` | Déjà implémentées ; ne pas ouvrir un nouvel essai de bruit sans défaut isolé. |
| Absorption RGB et Fresnel | Branche `STANDARD_MATERIAL_SPECULAR_TRANSMISSION` du shader Forge | Déjà présents ; leur présence ne prouve pas une eau réaliste. |
| Contact de berge | `water_contact_coverage`, mélange final avec le fond | Déjà présent ; ne corrige pas un trou géométrique et ne justifie pas de masquer les ombres du pont. |
| Reflets locaux du décor dans le candidat clair | `PreparedWaterScene::enable_depth_optics_preview` force Forward, transmission 1 et `self.ssr = None` | Limite explicite `preview_environment_only_no_water_ssr`, pas un effet simplement sous-réglé. |
| SSR Bevy existant | `bevy_pbr-0.19.1/src/ssr/ssr.wgsl` lit `deferred_prepass_texture` et reconstruit le matériau depuis le G-buffer | Réinstaller le composant caméra ne prouve pas que l'eau transmissive devient une surface réceptrice SSR. |
| Repli environnement Bevy | `render/pbr_functions.wgsl`, bloc `ENVIRONMENT_MAP` | Le spéculaire environnement est conservé hors SSR ; ne pas décrire le candidat comme dépourvu de tout reflet. |

La perte de SSR était déjà documentée : ce n'est pas une découverte de bug ni
la preuve qu'elle explique à elle seule les défauts visuels. La recherche la
repositionne comme écart fonctionnel à traiter pour R1-023, plutôt que poursuivre
les seuls réglages de rides. Pas de gain visuel ou de performance revendiqué.

### Suite technique ciblée, non implémentée

Établir une voie de réflexion du décor compatible avec l'eau transmissive,
en réutilisant d'abord les entrées et utilitaires Bevy existants. Examiner les
profondeurs/couleurs disponibles avant la passe d'eau, l'exposition, le rejet
du premier plan et le repli hors écran. Ne pas activer aveuglément le SSR différé,
additionner deux fois le spéculaire environnement, ou ajouter une seconde caméra
de rendu de toute la forêt sans budget mesuré. Aucun fork de Bevy autorisé ici.

Le critère utile du prochain candidat est un décor opaque identifiable dans son
reflet sur l'eau claire, avec réfraction encore lisible et repli stable pendant
un déplacement. La réflexion du feuillage BLEND dépend du contenu réellement
disponible dans les buffers ; elle ne doit pas être promise par analogie avec
les objets opaques. Vérifier ensuite le coût sur ce même candidat complet, pas
sur l'ancienne eau opaque. Ces vérifications ne remplacent pas l'acceptation
artistique ni les critères globaux de Gate 1.

Provenance : consultation des articles, sans téléchargement d'assets ni reprise
de code externe. Contrôle documentaire `git diff --check` seulement ; aucun test
de rendu nécessaire pour cette note, aucun résultat graphique nouveau.

## 9 septembre — gradient par jacobienne, vérifié en natif

Le détail renvoie désormais sa pente dans les coordonnées UV métriques de la
rivière. Le fragment applique la règle de chaîne à `dpdx/dpdy` des UV et de la
position mondiale XZ, puis résout le système 2×2. L'ancien retour par tangent de
courant supposait à tort un repère UV orthonormal, notamment dans les virages
ou les variations de largeur. Les empreintes dégénérées donnent une pente nulle
au lieu d'une division instable ; le seuil est relatif à la taille de l'empreinte.
Ni amplitude/fréquence, macro-vagues, profil optique, géométrie ni sources changés.
Seul le candidat transmissif consomme cette correction ; voie opaque inchangée.

Principe mathématique apparenté aux gradients de surface décrits par
[Mikkelsen, Bump Mapping Unparametrized Surfaces on the GPU](https://mmikk.github.io/papers3d/mm_sfgrad_bump.pdf).
Implémentation Forge du changement de coordonnées 2D, sans reprise de code/texture
de l'article, sans moteur ou dépendance supplémentaire.

Validation : 662 tests workspace PASS / un ignoré, Clippy strict moteur/Runtime,
build natif et check Studio WASM PASS. L'exemple GPU optique existant inclut la
nouvelle fonction réelle WGSL : onze jeux de données sur RX 7800 XT Vulkan,
tolérance absolue 0,0001, tous PASS. Champ connu `u=2x+z`, `v=-x+3z`,
`h=0.2u-0.1v` : gradient attendu `(0.5,-0.1)`, y compris écran inversé.
Bases écran issues du buffer d'entrée, pas seulement constantes compilées ;
cas colinéaire/nul également vérifiés. Le périphérique de preuve est détruit.
Cela prouve le helper numérique, pas un débit hydraulique ni toutes les dérivées
rastérisées possibles.

Deux captures 1920×1080 inspectées, kit courant de 1662 arbres, milieu clair,
caméra `water_close`, temps figé, frames 600/900. Différence visuelle faible,
pas de régression évidente sur cette vue ; ombre du pont, contact et motifs
de fond persistent. Correction conservée pour la justesse du repérage, **pas
comme gain artistique net ou performance**. Pas de rebuild/exécution Studio,
ni séquence temporelle qualifiée. Ne pas relancer cette paire inchangée.

Racine externe `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-gradient-jacobian-20260909/`.
`Prepare.py` ajoute seulement la caméra diagnostique à une copie du WorldSketch
courant, conserve les entiers exacts et compare toutes les autres données.
Premier lancement refusé avant rendu pour liaison kit/hash WorldSketch périmée,
sortie 2/PID 31708 fermé, journal conservé. `LinkPreview.py` crée une copie de
preview liée au hash typé réellement chargé, après égalité du monde hors caméra ;
placements, terrain, kit et originaux inchangés. Aucun contrôle désactivé.

Paire finale sorties 0, aucun ERROR, captures vérifiées ; PIDs 33460/19076 fermés,
aucun exécutable du dossier restant. Logs tests/Clippy/GPU/build/WASM et pins
conservés. Pas de navigateur ouvert.

- EXE après : `CC8D15534E23556555F0970E5DBE29B23DCA00430B08371221FC9386A7106182`.
- PNG avant : `0AFDA8D4B4D6878FA9A2D02057B874C8B2D324CD4D707FC72672E755F88AAB56`.
- PNG après : `238FF4DB3247A79A21EEC4AA2BC839AFABEA9E0FBA1FB12DE8505C8F1636D2D1`.

## Historique — premières coordonnées métriques

9 septembre 2026. Branche Rust, Gate 1 ouverte, aucun commit/push.

## Essai mondial rejeté

La première variante orientait du bruit anisotrope en projetant la position
mondiale dans le repère local du courant. 36 tests et build natif PASS, capture
inspectée : stries trop régulières. Le code comporte en outre une dépendance
indésirable à l'origine mondiale lorsque la direction varie. Cette méthode
n'est pas retenue comme règle générique.

Archive locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-directional-detail-20260909/`.

- `forge_runtime.exe` : `857D2D3CB799CB73D80EB0E99EB28AD8A30B3AB2C75CFAF56A7E84E9C59D2881`.
- `after.png` : `4FCE79040E0A005BAA68DC2529591CD8A4E96F04EA47A9BB93C25640E2A87EB5`.
- `after.stderr.log` : capture vérifiée, sortie 0, aucune erreur runtime.
- PID 39904 fermé. `Capture.ps1 -Variant world` reproduit cette variante archivée.

## Correction métrique

L'adapter calcule maintenant les UV du maillage d'eau en mètres : distance
horizontale cumulée du centre de la rivière, puis écart transversal signé au
centre. Le calcul inclut l'extension sous les berges. Les UV des berges ne
changent pas. Aucun nouveau flux de sommets, texture, document ou dépendance.
Positions, indices et collisions restent indépendants de ces coordonnées.

Le candidat transmissif échantillonne ces UV, allonge les crêtes transversalement
au courant et ramène les pentes dans le repère mondial. Le filtre écran utilise
l'empreinte de ces mêmes UV. Les profils d'opacité/réfraction, le lit minéral,
les ombres et les macro-vagues restent inchangés. Le rendu opaque par défaut
ne consomme pas ces UV pour ses vagues.

Un test synthétique couvre un coude, deux segments de cinq mètres, largeur
nominale/extension de berge et translation (+1024, -2048) mètres. Il compare les
coordonnées attendues et les indices. Première compilation du test refusée
pour type de paramètre ambigu, corrigé par `offset: f32` sans affaiblir le test.

Ce modèle est une animation de surface, pas une simulation hydrodynamique.
Il ne prouve ni les tourbillons autour d'obstacles ni l'absence de distorsion
dans tous les virages. Qualification visuelle/temporelle et Studio distinctes.

## Validation métrique

37 tests ciblés eau PASS, dont le nouveau test métrique, Clippy moteur strict
all-targets/all-features PASS et build natif PASS. Tests workspace complets,
WASM et Studio non rejoués pour ce candidat ; pas de preuve numérique GPU de
l'orientation, ni de séquence animée acquise.

`Capture.ps1 -Variant metric` : même WorldSketch de preuve, caméra `water_close`,
temps figé, frame 300 / sortie 420, sans kit gouverné, comme les témoins précédents.

- `metric.exe` : `497E435C8F5A19F1C5ACDEEC528AB489CEC236513CE5372FB9D5CA047B551823`.
- `metric.png` : `7B8A14DCF6E019C5201BBA3F4C333AEF298345957A9D3867F13FE3979F45939A`.
- `metric.stderr.log` : capture vérifiée, sortie 0, aucune erreur runtime.
- PID 43728 terminé. Aucun navigateur ouvert pour ces essais.
- Helper WGSL : `02C21245D7A10502AEF150E0EA409FAF328128D373A88AEAD2E7373332F309FD`.
- Shader eau principal : `2F196B16AE575DDCA067CFE2125178AC09D74FC06C1EB4380B24353EDC43659F`.
- Adapter `water.rs` : `1C0336B492BC57B6C7A0EFC10F5077FE133CADE32B4CC2BA02A10770AEAE23B1`.

Capture inspectée : aspect proche de l'essai mondial, rides toujours régulières,
zone sombre sous le pont et fond brun conservés. Aucun gain artistique net
n'est établi par cette image. La version métrique est conservée comme correction
du repérage générique, pas comme eau réaliste ou validation de R1-022/023.

Cadence glissante affichée, non benchmark : moyenne 9,90 ms, p95 17,14 ms,
1 % low 49,7 FPS. 70 995 triangles eau/berges, inchangés. Aucun gain de
performance déduit de ce relevé court.

Suite nécessaire : observation du courant en mouvement et sur une portion
courbe lisible avant réglage supplémentaire des rides. Ne pas confondre le
test de coordonnées synthétiques avec cette qualification visuelle. Les
anciens bundles Studio ne contiennent pas cette correction.
