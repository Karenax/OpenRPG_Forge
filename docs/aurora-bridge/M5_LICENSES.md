# Licences et provenance M5

**Date :** 8 novembre 2026.
**Portée :** composants directs de la passerelle Aurora/NWN M5. Ce document ne remplace pas les
notices de distribution complètes.

## Composants directs

| Composant | Licence | Source |
| --- | --- | --- |
| `forge_nwn_rules` | MIT OR Apache-2.0 | workspace local |
| `forge_nwn_runtime` | MIT OR Apache-2.0 | workspace local |
| `forge_aurora_bridge` | MIT OR Apache-2.0 | workspace local |
| `forge_aurora_runtime` | MIT OR Apache-2.0 | workspace local |
| `forge_core` | MIT OR Apache-2.0 | workspace local |
| `serde` | MIT OR Apache-2.0 | crates.io |
| `serde_json` | MIT OR Apache-2.0 | crates.io |
| `thiserror` | MIT OR Apache-2.0 | crates.io |
| Fixture `ab_proving` et schéma du contrat | [MIT](OPENNEVER-LICENSE-MIT) | OpenNever Forge, créations originales (`AB-M0-12`) |

Le ruleset natif OpenRPG et ses dépendances conservent leurs licences existantes ; M5 ne les
modifie pas.

## Données exclues

- Neverwinter Nights:EE et ses assets propriétaires ;
- modules, HAK, TLK, portraits, sons, modèles et textures NWN ;
- bundle M2 local avec assets, jamais versionné ;
- sauvegardes natives Forge ou NWN.

## Provenance

La fixture `ab_proving` est produite par OpenNever Forge à partir de son générateur synthétique.
Aucun changement n'est apporté au manifeste d'export. Son classement restrictif par défaut
est conservé pour tester le comportement de l'importeur. La licence MIT couvre cette fixture
originale et le schéma du contrat.
Aucun texte, art, audio ou module communautaire n'est redistribué. Les preuves M5 contiennent
uniquement des rapports JSON, traces, logs et hashes.

## Limites

- `m5-sbom-full.json` couvre 909 composants du graphe Cargo avec leur expression de licence ;
- les textes de licence de chaque dépendance transitive ne sont pas agrégés ;
- une distribution publique exigerait les notices complètes et une revue de licences.
