# ADR-0052 — Assemblage et récepteurs de lumière des surfaces dans le moteur

Date : 26 octobre 2026. Statut : réalisation Gate 7 en cours.

## Décision

La consigne propriétaire impose un moteur commun pour les mécanismes réutilisables
de Tidewater et des autres modules. L'assemblage du shader de surfaces, son rebond
indirect et les récepteurs matériels sous l'eau restent pourtant dans la crate de
carte. Les transférer, avec leurs gardes et tests existants, à
`forge_engine::source_surface::{receivers,underwater}`. Tous les consommateurs
nomment ces propriétaires directement ; retirer les anciennes façades.

Conserver l'ordre des adaptations, les programmes WGSL et notices, identifiants GPU,
liaisons, contrôles de dérive, traitement de l'émissif et cadre neutre de 32 octets.
Les paquets admis restent les sources des programmes et textures de chaque module.
L'absence explicite d'eau installe le même cadre neutre ; elle n'admet aucune eau
auteur manquante. Aucun algorithme ni scénario maritime n'est ajouté par ce transfert.

## Qualification et limites

Le transfert conserve une copie bornée des sources précédentes et les SHA-256 des
programmes déplacés dans la campagne centrale `gate7-monde-distribution`, sous
`shared-surface-receivers`. Rejouer les tests affectés, les contrôles de frontières,
Clippy natif/Web, puis les consommateurs graphiques sur les mêmes entrées admises.
Comparer les sorties sans transformer une simple compilation en preuve visuelle.

Cette décision ne termine pas l'extraction de tous les effets, l'océan, la sélection
Nature, les modules sans origine conservée ni la comparaison de performances. La
qualification de la tranche précédente demeure dans
`studio-common-runtime-validation-001.json` ; les dix critères Gate 7 restent ouverts.


## Point de contrôle — qualification UI encore en attente

Le transfert conserve les quatre programmes octet pour octet. Les entrées
`shared-surface-receivers-inputs-003.json` passent 2 744 tests (67 ignorés),
Clippy strict natif/Web et 24 frontières. Le rejeu natif 006 passe et ses deux
captures sont inspectées. Une répétition de retouche dans Studio révèle un
identifiant workspace réutilisé après annulation ; le correctif du host commun
est reproduit rouge puis vert, sans retrait des gardes. Le nouveau Studio est
compilé, mais son rejeu UI reste dû après l'arrêt par Échap. Le catalogue
`shared-surface-receivers-checkpoint-001.json` distingue ces verdicts ; cette
tranche et Gate 7 ne sont pas déclarées complètement qualifiées.
