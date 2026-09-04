# Gate 2 — brosses dans le viewport du Studio

Date : 4 septembre 2026. Branche `Rust` uniquement.

**R2-007/008/009/010/011/013 PASS local : Gate 2 atteint 11/43, toujours ouverte.**
Cette tranche qualifie les outils de relief et leur cercle de rayon/intensité, pas la finition d'une
vallée ni le passage de Gate 2. R2-012 (matériaux) et R2-F restent ouverts ; R2-014, ouverte au moment
de ce candidat, est qualifiée ultérieurement par son rapport séparé `PARTIAL_TERRAIN_REFRESH.md`.
Gate 1 conserve ses réserves ADR-0011 ; CI distante `NOT RUN — infrastructure billing` sous ADR-0006.
Aucun audit PresentMon, aucune modification de `main`, aucun asset Cadle ou autre asset externe.

## Candidat exact et réalisation

- Raccordement : `c2d28258fb46da63e8b4785596d1bd3dfd776006`.
- Correctif qualifié : `c7fc510dd66a04fbe8d780ce641216957ef8f81f`, arbre
  `d042fcf6f6f7191b47e81fa4e6f4ca825529c013`.
- Exécutable Release SHA-256 : `fe5c0872dbb13d94c72101880759197c0764dd636a053e714e1ad8e036bda4f0`.
- WASM embarqué SHA-256 : `12182cf07be634a165992c67b7b7b1d79b3c954ebea74caaeef36ddf1dcfc336`.

Monde propose Navigation, Lever, Abaisser, Lisser, Aplatir, Bruit et Pente, avec paramètres typés.
Le picking interroge les meshes réels des chunks, pas un plan approximatif ni le cube de départ.
Le cercle doré épouse le terrain ; le repère vertical blanc indique l'intensité. Rayons 16/32 m et
intensités 50/75 % capturés. Le repère blanc reste petit à cette distance : lisibilité perfectible.

Un glisser est accumulé en au plus 64 empreintes, puis soumis au relâchement comme une seule commande,
une seule révision et une seule entrée d'historique. Un échantillon dédié conserve le départ du trait
malgré le délai de lecture du picking. Le chemin interpole les écarts, ignore le jitter et refuse
atomiquement un dépassement. Les générations/révisions obsolètes annulent le trait. Échap et
pointercancel abandonnent l'aperçu. Alt + glisser conserve l'orbite ; les champs éditables ne déclenchent
pas les raccourcis d'historique. La coque crème/dorée, les documents moteur-neutres et les dépendances
sont conservés. Les sources restent exclusivement mutées par les commandes Rust existantes.

## Défaut initial conservé, correctif validé

Dans c2d2825, la première tentative ne trouvait aucun terrain et affichait « Visez le terrain » ;
la révision restait 7. `viewport-01-first-stroke-stalled.png` conserve ce refus, malgré son nom :
ce n'est ni un trait appliqué, ni une preuve de blocage du processus.

c7fc510 place le picking en PostUpdate après propagation des transforms et de la visibilité.
`RayCastVisibility::Visible` s'appuie sur la visibilité héritée, pas sur le drapeau transitoire par
vue. Le test Bevy dédié touche le terrain visible même sans ce drapeau, exclut un marqueur plus proche
et refuse le terrain caché. Le cercle puis les traits réels ci-dessous confirment le correctif en UI.

## Parcours natif réellement acquis

Studio autonome, client 1600 × 900, Monde agrandi, vue d'ensemble conservée. Entrée : projet
`9d72c00d-02a6-50c2-87b3-411787233851`, snapshot r7 déjà qualifié, deux modules et trois zones.
Seule `5426f526-6ac5-508b-98b3-da80d3df7116` — Clairière du bureau — est sculptée.
Les coordonnées ci-dessous sont celles des captures de fenêtre (1602 × 932, cadre compris).

| Opération | Paramètres / glisser écran | Révision / samples modifiés |
|---|---|---|
| Lever | rayon 16 m, intensité 50 %, amplitude 4 m ; (850,580) → (910,580) | r8 / 550 |
| Annuler puis rétablir | Ctrl+Z puis Ctrl+Maj+Z ; sauvegardes r9 et r10 | r9 sans deltas ; r10 identique à r8 hors révisions |
| Abaisser | rayon 32 m, intensité 75 %, amplitude 16 m ; (980,600) → (1030,600) | r11 / 1336 |
| Lisser | rayon 32 m, intensité 75 % ; (955,585) → (1050,585) | r12 / 1859 |
| Aplatir | rayon 32 m, intensité 75 %, altitude 4 m ; (980,600) → (1030,600) | r13 / 1363 |
| Bruit | rayon 32 m, intensité 75 %, amplitude 16 m, seed 1 ; (740,550) → (790,550) | r14 / 1385 |
| Pente | rayon 32 m, intensité 75 %, altitude 4 m, X=1 et Z=0 m/m ; (1050,500) → (1110,500) | r15 / 1591 |
| Annuler les cinq derniers traits | cinq Ctrl+Z, captures r16..r20 ; sauvegarde r20 | retour exact aux données r10 hors révisions |
| Rétablir les cinq traits | cinq Ctrl+Maj+Z, captures r21..r25 ; sauvegarde r25 | retour exact aux données r15 hors révisions |

Observations : lever crée une crête basse, abaisser creuse une cavité ; le lissage modifie plus
subtilement ses bords. Aplatir remonte la partie réellement touchée par le rayon, sans effacer toute
la cavité. La pente produit un relief asymétrique distinct. Le bruit à forte amplitude donne des
pics très abrupts, non un relief naturel fini : cette limite reste visible, pas masquée par la preuve.

Toutes les révisions appliquées r8 et r10..r15 sont sauvegardées, ainsi que r9, r20 et r25.
Les comptes de deltas persistés sont distincts des samples modifiés d'une commande : r8/r10/r20=550,
r9=0, r11=1886, r12/r13=2633, r14=4018, r15/r25=5609.

Après sauvegarde r25, fermeture normale et disparition de l'ancienne fenêtre/processus vérifiées.
Une nouvelle instance démarre sans projet. Fichier → Ouvrir charge explicitement r25 ; Monde →
Clairière du bureau réaffiche tous les reliefs et 64 chunks. Historique vide après redémarrage,
comme prévu par le contrat de session. Diagnostics : un démarrage du renderer, révision 25,
aucune erreur, hash terrain `ca6415d25c2babe2750f4b75743e80c9da2c43a7be5ed295af249932687ca6ab`.

Le fichier r25 (826 654 octets) reste identique avant/après réouverture :
`467470990459c12c33a96093b2cad80d6dccd5368185a10d9276c52e6af469b3`.
La baseline r7 reste intacte : `297035827452c15186b9f17840784d575b98f660ddcfb8984102d10c8de61ae4`.
La comparaison de tous les documents vérifie noms, IDs, seeds (y compris u64 MAX), départs,
paramètres et autres zones inchangés. Seuls les deltas de la zone éditée et les révisions diffèrent.

## Vérifications et rejeu

Sur c7fc510 : **456 tests PASS, 0 échec, 0 ignoré, 13 cibles** ; Clippy workspace/natif et Studio
WASM stricts PASS ; formatage PASS ; Trunk Release puis build natif embarqué terminés avec succès.
Les six tests ajoutés depuis la fondation couvrent chemins/traits composés, atomicité, une entrée
d'historique, IPC des modes et picking réel/visibilité. Les 450 tests antérieurs sont conservés.
Le warning tiers `proc-macro-error2 2.0.1` de future incompatibilité reste signalé.

Les logs `viewport-fix1-{tests,clippy-native,clippy-wasm,trunk-confirmed,native-confirmed}.log` sont
les preuves finales. `viewport-fix1-native-release.log` appartient à une tentative arrêtée pendant
l'attente d'un verrou de build, pas à un PASS. Un chemin de log relatif devenu invalide pendant
PushLocation a nécessité un rejeu Trunk avec chemin absolu ; seul le rejeu confirmé fait foi.
Aucun rebuild lourd supplémentaire n'a été nécessaire pour cette clôture documentaire.

Le [manifeste](BRUSH_VIEWPORT_ARTIFACTS.json) inventorie 72 fichiers : binaires, logs, 11 snapshots,
27 PNG et leurs textes d'accessibilité. Captures/logs/snapshots sont locaux et ignorés, sous
`artifacts/gate-2/terrain-brushes-2026-09-04/`. Le texte AX du curseur 32 m est vide : seule son image
est une preuve. Les acquisitions 00/01 appartiennent au candidat initial, pas au résultat corrigé.

```powershell
pwsh -NoProfile -File scripts/Test-Gate2BrushEvidence.ps1
```

Le vérificateur en lecture seule recoupe hashes/tailles, 11 snapshots, quatre paires d'historique
normalisées uniquement sur les révisions, métadonnées/autres zones, résultats des 456 tests et
diagnostics r25. Il ne rejoue pas l'UI, ne remplace pas l'inspection des images et ne mesure pas le GPU.
Pour reproduire le parcours : utiliser une copie du snapshot r7 dans une session neuve, ouvrir sa zone,
appliquer les gestes/paramètres du tableau, sauvegarder dans un dossier de preuve neuf, annuler/
rétablir, puis fermer et rouvrir le fichier final. Aucun JSON n'a été édité manuellement pour produire
les reliefs de cette acquisition.

Les deux binaires du candidat `c7fc510` ont ensuite été archivés dans le bundle ignoré sous `bin/`.
Le manifeste référence ces copies immuables : une reconstruction ultérieure du Studio ne peut donc
plus invalider cette preuve historique tout en conservant les mêmes tailles et SHA-256.

## Limites et prochain travail

- Le relief ne se déforme pas pendant le glisser : cercle/compteur d'empreintes seulement ; application
  au relâchement. Pas encore d'aperçu 3D du dry-run.
- **Limite au candidat `c7fc510`** : chaque edit/historique régénérait puis remplaçait les 64 chunks et
  un écran momentanément vide était observé. Cette limite historique est fermée par le candidat
  `b290e7f` et sa preuve R2-014 séparée ; elle reste vraie pour les captures de ce rapport.
- Les captures prouvent un rendu, pas une performance interactive qualifiée. Le temps de boucle CPU
  affiché n'est ni une cadence présentée ni un timestamp GPU.
- Peinture des matériaux, biomes/végétation par zones, splines, placements et playtest restent dus.
  Bruit naturel et lisibilité des contrôles après défilement demandent encore une finition.
- L'historique est borné à 32 opérations et non durable ; crash recovery, audit et bus générique,
  ainsi que les interactions simultanées pendant un trait, ne sont pas qualifiés par ce parcours.
- Aucun passage de Gate 2, démarrage de Gate 3 ou fermeture des réserves Gate 1 n'est revendiqué.
