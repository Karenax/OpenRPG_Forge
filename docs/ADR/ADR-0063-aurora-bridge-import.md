# ADR-0063 — Pont Aurora/NWN : import et runtime de compatibilité

**Date :** 7 novembre 2026.
**Statut :** accepté pour le cadrage ; implémentation non commencée.
**Piste :** hors numérotation des gates ; Gate 7 reste ouverte, Gate 8/9 non ouvertes.
**Décision liée :** ADR OpenNever 0011.

## Contexte

OpenRPG Forge ne contient aucun code Neverwinter Nights/Aurora. Il possède un conteneur multi-zone
(`ModulePackageManifest.zones`), une navigation native, un ruleset action-RPG et un runtime qui charge
une zone par `area_id`. Il ne sait ni lire un MOD/HAK/TLK, ni exécuter du NCS, ni importer un module
NWN complet.

OpenNever Forge possède les parsers, le Resource Manager, l'export area v1 et les writers nécessaires.
Faire d'OpenRPG un second lecteur Aurora dupliquerait ce travail et créerait des divergences de
formats et de licences.

L'audit du 26 octobre 2026 recommande l'option C : OpenNever producteur, OpenRPG consommateur et
runtime de compatibilité. La première livraison est un module synthétique à deux zones, avec PNJ,
dialogue conditionnel, porte, objet, combat, quête et sauvegarde/reprise.

## Décision

1. **OpenRPG est consommateur.** Il ne lit pas directement les MOD/HAK/TLK et ne duplique pas les
   parsers Aurora. Il reçoit des bundles `module-migration-bundle@1.0.0` produits par OpenNever.

2. **Nouvelle crate `forge_aurora_bridge`.** Elle est créée en M1 pour lire, valider et mapper le
   bundle. Elle est isolée, sans dépendance Tidewater, sans dépendance Cargo `path` vers OpenNever
   et sans code Rust partagé. Elle ne contient pas de VM NCS.

3. **Contrat miroir verrouillé.** Le schéma est copié sous `docs/aurora-bridge/contract/` et épinglé
   par `CONTRACT.lock.json` (version + SHA-256 + commit source). Un test refuse toute dérive. Le
   verrou actuel est un brouillon `draft-not-frozen` et ne doit pas être utilisé en production.

4. **Les zones NWN sont des objets sources.** Elles sont importées dans le conteneur multi-zone
   (`forge_project::module_package::objects`) et projetées vers les documents Forge. Elles ne sont
   pas converties en terrain Forge. La géométrie WOK/PWK/DWK est convertie en collision/navmesh
   seulement après validation, avec preuve de déplacement.

5. **Le runtime natif reste intact.** Les quotas du gameplay natif (32 scripts, 256 pas,
   128 variables narratives, 32 factions) ne sont pas augmentés pour absorber un module NWN. Un
   dépassement est diagnostiqué, jamais tronqué silencieusement.

6. **Le runtime de compatibilité est séparé.** `forge_nwn_runtime` (ou sous-crate équivalente) porte
   la VM NCS, le registre de fonctions natives, les événements, l'ordonnanceur et les sauvegardes de
   campagne. Il ne remplace pas le runtime natif et ne s'y branche pas par des écritures arbitraires
   dans l'ECS.

7. **Le ruleset NWN est un profil distinct.** `NwnRulesProfile` est séparé du ruleset action-RPG
   natif. Aucune régression du combat de contact physique natif n'est acceptée. La portée exacte
   (vanilla, extensions, patch, mods) sera précisée en M4.

8. **Les statuts restent séparés.** `importable`, `renderable`, `navigable`, `scriptable`,
   `playable`, `saveable` sont des preuves distinctes. Un import sans exécution est une
   prévisualisation. Un module partiellement supporté n'est jamais annoncé complet.

9. **La piste est hors gates.** Elle est suivie dans `docs/aurora-bridge/` et dans
   `Preuves-et-analyses/actives/aurora-bridge/`. Elle n'ouvre ni Gate 8, ni Gate 9, ni une nouvelle
   gate, et ne modifie aucun verdict historique.

## Conséquences

- OpenRPG gagne un adaptateur d'import et un runtime de compatibilité, sans devenir lecteur Aurora.
- Le contrat est testable des deux côtés sans dépendance de build croisée.
- Les assets et les scripts restent locaux et soumis à leurs droits.
- Le runtime natif et ses gates existantes ne sont pas détournés par le chantier.
- Une future version majeure du contrat exige un nouveau verrou et une migration explicite.
- Les sauvegardes de compatibilité sont versionnées séparément des sauvegardes natives Forge.
- La licence des artefacts de contrat reste bloquée avant publication (`AB-M0-12`).

## Alternatives rejetées

- **Lire les MOD directement dans OpenRPG :** duplication des parsers, divergence et maintenance.
- **Faire d'OpenNever le moteur :** second moteur, duplication d'OpenRPG, mauvaise séparation.
- **Créer un troisième dépôt au MVP :** gouvernance et publication prématurées.
- **Traduire tous les NCS vers le langage de scripts Forge :** perte de sémantique et limites
  incompatibles ; la VM de compatibilité est nécessaire.
- **Augmenter les quotas natifs :** casse les garanties du gameplay Forge et ne couvre pas NWN.
- **Utiliser NWN/OpenWinter comme moteur d'arrière-plan :** dépendance externe et licences non
  autonomes ; oracle isolé uniquement.
- **Ouvrir une nouvelle gate pour le pont :** détournerait la priorité de Gate 7 sans décision
  explicite.

## Suivi

| Élément | Jalon |
| --- | --- |
| Audit multi-zone/sauvegarde/runtime | AB-M0-16 |
| Crate `forge_aurora_bridge` | AB-M1-17 |
| Miroir et gel du contrat | AB-M1-18 |
| Test d'intégration producteur → consommateur | AB-M1-21 |
| Première visite jouable | AB-M2-10 |
| VM NCS et quête scriptée | AB-M3-12 |
| Ruleset NWN et sauvegardes multi-zones | AB-M4-12 |
| Campagne avancée et qualification | AB-M5-12 |

Backlog consommateur : [`../aurora-bridge/BACKLOG.md`](../aurora-bridge/BACKLOG.md).
Backlog maître : `E:\OpenNever_Forge\docs\aurora-bridge\BACKLOG.md`.
