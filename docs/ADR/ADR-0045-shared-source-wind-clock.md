# ADR-0045 — Horloge et transport du vent source communs

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Écart constaté

Le module importé possédait une intégration privée des rafales : déplacement
à la vitesse du vent, wrap à 8540 m et index modulo 16. La référence utilisait
déjà `Clock::advance`, avec transport `0.7 * vitesse + 1.5`, accumulation double
précision et cycle shader de 64 phases. Le même matériel de rendu ne suffisait
donc pas à reprendre ce fonctionnement.

## Décision

`forge_engine::source_wind` possède le temps/vent de scène. Son sous-module
`vegetation` possède l'horloge source existante, déplacée depuis les casters.
Référence, Studio et module nomment ces propriétaires directement. Le lecteur
de phase acquise reste dans son codec d'intégration, sans façade de temps.

L'hôte importé réemploie cette horloge et retire son intégrateur privé. Sa phase
initiale reste celle du module (zéro seconde/index/déplacement), sans injecter
la phase acquise de la carte de référence. Force et fréquence auteur restent
canoniques : la force conserve sa conversion de vitesse existante et la
fréquence règle la phase de la forme d'onde, pas le transport des rafales.
Le delta de scène reste borné à 0,1 s. Le temps double précision est converti
au dernier moment ; le cycle de dithering suit les 64 phases source.

L'échantillon est préparé sur un candidat. L'horloge n'est validée qu'après
publication réussie de la frame GPU, qui conserve la vraie pose précédente.
Les vues hors écran communes sont exclues de la caméra de simulation ; plusieurs
caméras interactives restent refusées. Aucun document, format ni réglage auteur
persistant n'est modifié.

## Vérification et limites

Comparer au même `Clock` source sur deltas nuls/lents, changements de direction,
wrap d'index, fréquence auteur et refus d'entrées invalides. Rejouer référence,
module réel et contrôles natif/WebGPU. Le changement corrige le mouvement des
rafales du module ; il ne prétend pas conserver ses pixels antérieurs.

Cette reprise du vent ne raccorde pas les récepteurs PCSS/PCF ou l'éclairage
de référence au chemin PBR actuel du module. Ces capacités et leur configuration
canonique restent des travaux distincts de Gate 7 ; aucune parité générale ou
clôture n'est déduite des seuls tests de vent.
