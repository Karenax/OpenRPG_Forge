# R1-051 — Reprise de l'identité de la référence WebGPU

Date de l'audit : **2026-09-03**

Statut : **PAQUET PRÊT POUR REVUE HUMAINE — ROUTE OUVERTE, RÉFÉRENCE SOURCE NON FIGÉE**

Cette preuve corrige l'affirmation antérieure selon laquelle aucun nom ni URL de la référence n'était
retrouvable et lie désormais le protocole sémantique versionné au paquet de revue humaine.
Le dépôt ne contient aucun média externe, ne qualifie aucune comparaison visuelle et ne ferme ni
`R1-051`, ni Gate 1. Deux captures de l'état live courant et une
capture du rendu actuel du post X historique avec un frame de sa vidéo embarquée sont conservées
uniquement dans la racine de preuve externe `.gate1-tools` et liées par SHA-256 ci-dessous. Aucune ne
constitue une capture contemporaine du rendu vu le 29 août 2026 ni le média source de la vidéo.

## Identité reprise

La référence probable fortement établie est **Three.js Grassworks**, démonstration WebGPU de Bilal
Khan (`@Bk23544`) :

- post déclencheur : <https://x.com/Bk23544/status/2093382978105966827/video/1> ;
- démo interactive liée par l'auteur : <https://grassworks.techredux.co/demo> ;
- walkthrough public lié par l'auteur : <https://www.youtube.com/watch?v=Nhim18rc-XE> ;
- publication contemporaine de l'auteur :
  <https://www.patreon.com/MuhammadBilalKhan/posts/200-hours-later-167946382> ;
- publication publique contemporaine avec la même description et les mêmes liens :
  <https://www.reddit.com/r/threejs/comments/1w1e7of/i_spent_200_hours_building_a_realtime_grass/>.

Ces URL identifient la référence ; elles ne figent pas les octets d'une version de la démo. Le reçu
machine de cette reprise est
[`r1-051-reference-recovery.v1.json`](r1-051-reference-recovery.v1.json).

## Provenance utilisateur historique

La conversation ChatGPT intitulée « Comparaison rendu Godot », identifiant
`6a933ea2-af00-83eb-b408-a696ea67fa8b`, contient le tour utilisateur
`1c87d172-b1c7-4dba-a36a-eae7a5a51073`, commencé le **2026-08-29 à 20:18:42 UTC**. Le message
utilisateur est :

> https://x.com/Bk23544/status/2093382978105966827/video/1 j'ai l'impression que je me fait
> vraiment chier avec godot, quand je voi le rendu de cette vidéo, c'est beaucoup plus jolie ...

La réponse de ce même tour identifie la vidéo comme **Three.js Grassworks**, décrit la végétation
dense, le terrain, les arbres, l'eau, la pluie, les interactions et les LOD, puis conclut que cette
vidéo justifie de remettre le choix du moteur sur la table. Un tour ultérieur de la même conversation
produit le contexte Rust et reprend explicitement la formule « démonstration WebGPU qui a déclenché la
remise en question de Godot ».

Le message utilisateur avec l'URL exacte constitue la provenance humaine historique. Une nouvelle
question demandant quelle référence était visée serait donc redondante.

## Corroboration publique du 3 septembre 2026

Une lecture sans média de l'endpoint oEmbed public de X pour ce statut renvoie :

- URL canonique : `https://x.com/Bk23544/status/2093382978105966827` ;
- auteur : `Bilal Khan`, profil `https://x.com/Bk23544` ;
- date affichée : `August 28, 2026` ;
- texte annonçant Three.js Grassworks, la démo interactive et son usage de WebGPU ;
- lien court `https://t.co/MImqQODiPx`.

Une requête HEAD avec suivi des redirections relie ce lien court à
`https://grassworks.techredux.co/demo`. Les publications Patreon et Reddit de l'auteur décrivent la
même démo WebGPU, avec terrain, arbres, eau, pluie, interaction joueur, effets environnementaux, LOD
et audio, et donnent le même walkthrough YouTube.

## Échantillon visuel live courant, hors dépôt

La démo publique a été ouverte dans un navigateur visible à **1920 x 1080**, qualité **High**. Deux
captures JPEG ont été produites par l'agent, sans interaction humaine et sans copier de bundle, code
ou asset de la démo dans Forge :

| Preset | Octets | SHA-256 |
|---|---:|---|
| Sunny | 456 471 | `96e5e96777ed63f778db989f0e77cb641618c0fe4bf7f401c9f9d1f1a73848d5` |
| Rain | 379 020 | `17703bcc42db55ea49b1caea5d804de9aee7f39c7470363fe8a470cc5a97f0d0` |

Les deux fichiers sont stockés sous
`E:/OpenRPG_Forge/.gate1-tools/evidence/r1-051-grassworks-current-20260903T142341Z/`. Leur format et
leurs dimensions 1920 x 1080 ont été relus depuis les fichiers, puis leurs hashes recalculés. Ils
figent seulement ce qui était visible le **3 septembre 2026** aux presets Sunny et Rain. La démo live
reste mutable et les compteurs FPS/TRIS sont dynamiques. Les cadrages sont désormais reliés au
protocole sémantique Forge, mais aucune appréciation humaine n'est enregistrée.

Le même dossier externe contient aussi un rendu navigateur actuel du post canonique X : **1905 x
1072**, 128 035 octets, SHA-256
`43be0cebbaf0bd7d58dbbe08de02c7b60932383133580146f95b1d75db7d5a3c`. La capture affiche l'auteur,
le texte du post, le lien Grassworks, la date « 28 août 2026 » et un frame `RAIN` de la vidéo
embarquee. Elle lie visuellement la référence au post historique, mais reste une capture effectuée le
3 septembre : les octets de la vidéo source ne sont pas conservés ni hashés, et cette image ne prouve
pas à elle seule que l'état servi n'a jamais changé.

## Protocole comparatif versionné

Le contrat
[`r1-051-grassworks-forge-semantic-protocol.v1.json`](r1-051-grassworks-forge-semantic-protocol.v1.json)
et son
[`guide de revue`](R1_051_GRASSWORKS_FORGE_SEMANTIC_PROTOCOL.md) relient les trois JPEG externes et
quatre vues Forge de la campagne `125ba0b` par chemins relatifs, tailles, dimensions et SHA-256. Ils
fixent quatre panneaux sémantiques et les cinq axes **densité, naturel, profondeur, lumière et
finition**, sans comparaison pixel à pixel. Le validateur dédié recoupe les sept fichiers in situ et
refuse les paths hors racine, les hashes altérés, un schéma divergent ou toute prétention de verdict.

Le protocole est introduit au commit exact
`73db783975251cf7b4439dce9a12328a4c55c5ec`, arbre
`466cb51b12203e2358aec7ebcc9051eb58a1234e`, et son fichier JSON porte le SHA-256
`cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d`. Le paquet humain lie
explicitement ces identités, les trois JPEG Grassworks externes et les quatre vues Forge. Il classe
R1-051 `ready_for_human_review`, sans enregistrer de relecteur ou de verdict et sans fermer la route.

## Pourquoi les autres références ne sont pas substituées

- **Cadle** est la baseline Three.js/WebGL2 distincte de `R1-052` ; son identité, ses captures et son
  protocole ne remplacent pas `R1-051`.
- Le site Babylon.js **« La Forêt des Brumes »** a été demandé dans la conversation
  `6a9344c0-f7bc-83eb-883c-ede531f5e582` le 29 août 2026 à **20:44:48 UTC**, soit après le message
  Grassworks déclencheur. Il constitue une expérimentation ultérieure, pas la référence qui a motivé
  la remise en question initiale de Godot.
- Les démonstrations trouvées par simple recherche web mais absentes de cette chaîne historique ne
  sont pas retenues.

## Limites de reproductibilité

L'identification est acquise, mais la référence n'est pas encore une preuve reproductible :

1. le code Grassworks et le code de la démo ne sont pas publiquement versionnés ; les publications de
   l'auteur présentent le plugin comme un produit privé/commercial ;
2. aucun commit, tag, numéro de version ou archive publique correspondant à l'état vu le 29 août 2026
   n'est disponible ;
3. l'URL de démo est mutable. Le HEAD HTTP observé le 3 septembre 2026 porte
   `Last-Modified: Wed, 02 Sep 2026 05:16:36 GMT` et l'ETag alors observé
   `W/"2b0645c77c3780b3d7a7c2d934f7358c"`, postérieurs au message déclencheur. Une relecture
   indépendante du même corps a ensuite renvoyé la même valeur sans préfixe faible ; l'ETag n'est donc
   pas traité comme un identifiant stable. Ces métadonnées caractérisent seulement l'état courant et
   ne prouvent pas l'état historique ;
4. aucune capture **contemporaine** du 29 août ni aucun SHA-256 du média vidéo source n'est
   disponible ; le rendu actuel hashé du post et les deux captures live courantes ne comblent pas
   complètement cette lacune ;
5. le protocole `R1-051` versionné fixe désormais des panneaux sémantiques à partir des observations
   courantes et des vues Forge existantes, mais il ne transforme pas ces observations en snapshot
   historique reproductible ;
6. aucune revue humaine n'a produit de verdict ; l'intégration du protocole au paquet ne remplace pas
   cette attestation.

Le statut fail-closed reste donc nécessaire. Le paquet humain emploie désormais
`ready_for_human_review` pour la disponibilité technique des éléments, tout en conservant
`human_review_performed=false`, `gate1_routes_closed=0` et `gate1_transition_authorized=false`. Cette
readiness ne transforme pas les observations actuelles en preuve source historique ou en verdict.

## Travail restant pour fermer R1-051

Un lot ultérieur, exécuté hors d'une acquisition PresentMon et sans intégrer d'asset au produit, doit :

1. acquérir, si la route exige les octets source, une représentation externe autorisée et immuable de
   la vidéo historique ; le rendu actuel du post est hashé, mais la vidéo source ne l'est pas ;
2. enregistrer hors des assets produit l'URL, la date d'acquisition, les métadonnées utiles, la taille
   et le SHA-256, avec une frontière de redistribution explicite ; ce point est acquis pour les deux
   captures live courantes et le rendu actuel du post historique, mais pas pour la vidéo source ;
3. faire juger humainement densité, naturel, profondeur, lumière et finition, en reconnaissant
   explicitement les limites de source ;
4. conserver un verdict explicite `accepted`, `rejected` ou `inconclusive` sans transformer les
   métriques automatisées en jugement visuel.

Jusqu'à ces quatre résultats, `R1-051` reste ouverte. Aucun média, bundle, code source ou asset
Grassworks n'a été copié ou versionné dans Forge pendant la présente reprise. Seules deux captures
d'écran de l'état live courant et un rendu du post historique sont conservés dans la racine de preuve
externe `.gate1-tools`.
