# Gameplay M4 - profil NWN séparé

**Version :** 8 novembre 2026.
**Portée :** sous-ensemble vérifié du corpus `ab_proving`; aucune compatibilité NWN générale.
**Code :** `crates/forge_nwn_rules`, `crates/forge_nwn_runtime/src/m4.rs`, `forge_cli aurora-gameplay`.
**Décision :** [ADR-0064](../ADR/ADR-0064-nwn-rules-profile.md).

## 1. Résultat vérifié

`forge_cli aurora-gameplay` exécute la séquence suivante sur le bundle `ab_proving` :

1. chargement du module, camp, deux branches de dialogue ;
2. entrée dans la crypte et trigger de relique différé ;
3. coffre et ajout de la relique à l'inventaire M4 ;
4. `bless` sur le combattant, `magic-missile` sur le gardien ;
5. rencontre de mêlée déterministe : initiative, déplacement, attaque du gardien, attaque du
   combattant, mort du gardien ;
6. `cure-light-wounds` sur le combattant blessé ;
7. repos : slots restaurés, PV restaurés, effets temporaires nettoyés ;
8. retour au camp, récompense, branche de dialogue terminée ;
9. migration du journal M3 vers l'état de quête M4 ;
10. sauvegarde M4, reprise et contrôle d'idempotence.

Résultat observé avec la graine `2026` :

```text
winner=synthetic-fighter rounds=2 actions=4 ai_moves=2
party_hp=24 guardian_hp=0
journal=[10, 20, 30] gold=50 quest=completed
spells=bless, magic-missile (4 dégâts), cure-light-wounds (1 PV soigné)
rests=1 rng_trace=8 oracle=not-run
save_sha256=06211452…
resume_idempotent=true
```

Le gardien agit en premier (initiative), inflige 1 dégât, puis le combattant le tue. La relique
et les propriétés inconnues restent visibles dans le rapport.

## 2. Frontière du profil

Le profil `forge.nwn-rules-profile@1` est indépendant du ruleset action-RPG natif. Le document
canonique est [`nwn-rules-profile.v1.json`](nwn-rules-profile.v1.json).

| Champ | Valeur M4 | Effet |
| --- | --- | --- |
| `ruleset` | `vanilla` | sélection explicite |
| `maxLevel` | 40 | borne de validation |
| `hostileReputationMax` | 0 | une réputation 0 est hostile |
| `criticalConfirmation` | vrai | jet de confirmation |
| `naturalOneMisses` | vrai | 1 naturel = échec |
| `naturalTwentyHits` | vrai | 20 naturel = réussite |
| `maxRounds` | 50 | borne de rencontre |
| `moveSpeedMPerRound` | 3 | déplacement de mêlée |
| `meleeReachM` | 2 | portée de mêlée |

`forge_gameplay` n'est pas modifié. Les quotas natifs (32 scripts, 256 pas, 128 variables,
32 factions) restent inchangés.

## 3. Combat d20

- initiative : `d20 + modificateur de Dextérité`, égalité départagée par identifiant stable ;
- AC : `10 + armure + bouclier + naturel + diversion + esquive + taille + divers + Dextérité
  plafonnée` ;
- attaque : `d20 + BAB + modificateur de caractéristique + bonus d'arme + effets` ;
- attaques itératives : BAB, BAB-5, BAB-10, BAB-15, avec les bonus complets ;
- 1 naturel et 20 naturel selon le profil ;
- menace de critique selon l'arme (`19-20` pour l'épée longue), confirmation par un second jet ;
- dégâts : dés de l'arme + modificateur + bonus, multipliés sur critique confirmé ;
- concealment, réduction de dégâts et immunités de type de dégâts ;
- PV bornés entre 0 et le maximum du template ;
- attaques spéciales bornées : `power-attack`, `improved-power-attack`, `called-shot`, `knockdown`
  et `stunning-fist`, avec modificateurs et jet de sauvegarde explicite.

Les armes à distance, les tailles avancées, les effets complets et les règles de monture restent
hors périmètre.

## 4. Personnages

Les personnages sont des `CharacterTemplate` data-driven avec scores, classes, niveau, PV, BAB,
AC, sauvegardes, arme, RD, immunités, concealment, feats, compétences et `source`.

Le combattant M4 est un template synthétique de niveau 3, explicitement marqué
`synthetic M4 fixture; not a 2DA extraction`. Le gardien est construit depuis
`catalogs/blueprints.json` du bundle : PV 8, Dextérité 10, AC naturelle 0, faction 1, classe
Barbarian (index 0 de `classes.2da`), niveau 1. Son `Equip_ItemList` est vide ; il attaque donc à
mains nues (1d2), pas avec une arme supposée. Les tables 2DA ne sont pas copiées ; `aurora-2da`
peut auditer des tables extraites localement (43 classes, 840 sorts, 1116 feats, 30 races) sans les
versionner. Une valeur absente est refusée par l'adaptateur, jamais remplacée silencieusement.

## 5. Objets

`Inventory` gère les définitions, instances, stacks, poids, slots, équipement et transferts.
L'équipement applique les bonus connus (`attack-bonus`, `damage-bonus`, `armor-bonus`,
`shield-bonus`, `natural-bonus`). Les propriétés inconnues restent listées dans le rapport.

Le scénario équipe une épée longue, une chemise de mailles et un petit bouclier, puis récupère la
relique. Un transfert vers un inventaire plein est refusé atomiquement. `Store` ajoute l'achat et
la vente transactionnels avec stock, or et prix de rachat ; un refus ne modifie ni l'inventaire, ni
l'or, ni le stock.

## 6. Sorts et repos

| Sort | Niveau | Effet M4 |
| --- | ---: | --- |
| `magic-missile` | 1 | 1d4+1 dégâts magiques |
| `cure-light-wounds` | 1 | 1d8+2 PV soignés, plafonnés aux PV max |
| `bless` | 1 | +1 attaque pendant 3 rounds |
| `sleep` | 1 | zone ennemie, jet de Volonté DD 11, contrôle 2 rounds |

`cast_spell` cible une créature ; `cast_area_spell` consomme un seul slot et applique le sort à
toutes les cibles hostiles. Les slots sont consommés par niveau, le repos les restaure et les
effets temporaires expirent au tick suivant leur durée. Les VFX ne sont pas rendus : le rapport les
déclare non supportés.

## 7. Factions et IA

Les factions et réputations sont lues depuis `factions/factions.json`. Le gardien (faction 1) et
le combattant (faction 0) ont une réputation 0, donc hostiles. L'IA de mêlée se déplace de
`moveSpeedMPerRound` vers la cible tant qu'elle est hors de portée, puis attaque. L'ordre des tours
suit l'initiative. `Perception::can_perceive` fournit une garde de portée déterministe ; le
scénario principal utilise le chemin sans garde pour rester compatible avec la trace M4.

La poursuite/fuite, le pathfinding de navigation et la traversée de zone par IA ne sont pas
implémentés.

## 8. Quête et sauvegarde

`QuestState` migre le journal M3 (`10/20/30`) et mémorise les récompenses déjà accordées.
`M4Save` contient :

- `M3Save` : session multi-zone, portes, zones visitées, journal, or, transcript, scheduler, états
  de quête, portes, politique d'effets et continuation VM optionnelle ;
- `RulesSnapshot` : profil, graine, RNG, personnages, inventaire, slots, effets, quête, round,
  encounter résolu ;
- rapport de combat, sorts et nombre de repos.

La reprise restaure le RNG, vérifie les personnages, l'inventaire, les slots, les effets et la
quête, puis avance le RNG d'un jet de contrôle. Elle refuse un schéma inconnu, des PV hors bornes,
un personnage dupliqué ou un objet inconnu.

## 9. Oracle et écarts

`forge_nwn_rules::compare_encounters` compare deux `EncounterReport` et produit des divergences
par champ (`winner`, `rounds`, `natural`, `total`, `damage`, `targetHpAfter`, `note`). Les tests
couvrent l'égalité et la détection d'une trace mutée. `aurora-oracle-log` ingère un log externe
`forge.nwn-oracle-log@1` et le compare au rapport M4 ; le format est documenté et testé, mais aucun
log NWN réel n'a été produit.

**L'oracle NWN local n'a pas été exécuté.** Le rapport porte `oracleStatus = not-run` et
`oracleNote`. Aucune fonction n'est déclarée `exact` sur la seule base de ce harnais.

## 10. Limites explicites

- sous-ensemble de `ab_proving` uniquement ;
- pas de tables 2DA, de races/classes complètes ni de multiclasses réelles ;
- sorts complets, concentration, invocations, composants, dissipation et AoE complexe non couverts ;
- propriétés magiques complètes, loot tables et marchandage non couverts ;
- perception bornée à une garde de portée ; fuite, pathfinding de navigation et traversée de zone
  par IA non couverts ;
- continuation VM sérialisable mais non suspendue au milieu du scénario M4 ;
- pas de VFX, de rendu ou de capture M4 ;
- oracle NWN réel non exécuté ; seul le harnais de log externe est fourni ;
- budgets de performance limités au scénario CPU et à une empreinte headless ; pas de FPS ni GLB.

## 11. Reproduction

```powershell
cargo test -p forge_nwn_rules -p forge_nwn_runtime -p forge_cli
cargo run -q -p forge_cli -- aurora-gameplay `
  crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1 `
  --seed 2026 `
  --report E:/OpenRPG_Forge/Preuves-et-analyses/actives/aurora-bridge/m4/m4-report.json
```

Les preuves détaillées sont dans
`Preuves-et-analyses/actives/aurora-bridge/m4/README.md`.
