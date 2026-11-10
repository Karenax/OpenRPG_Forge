# ADR-0064 - Profil de règles NWN séparé

**Date :** 8 novembre 2026.
**Statut :** accepté pour M4 local ; portée limitée au sous-ensemble prouvé.
**Piste :** hors numérotation des gates ; Gate 7 reste ouverte, Gate 8/9 non ouvertes.
**Décision liée :** ADR-0063.

## Contexte

M3 a livré `forge_nwn_runtime` : parseur NCS borné, VM déterministe, registre de natives, dialogue
conditionnel, journal, objet, porte, combat simulé et reprise idempotente. Le combat M3 déclenche
le script `ab_guard_death` sans appliquer de règles d20.

M4 doit reproduire les mécaniques nécessaires au corpus NWN sans remplacer le ruleset action-RPG
natif d'OpenRPG. Les tables 2DA et le ruleset complet ne sont pas disponibles dans le bundle
`module-migration-bundle@1.0.0`. Il faut donc un profil data-driven, explicitement partiel, et des
diagnostics pour tout ce qui n'est pas couvert.

## Décision

1. **Nouvelle crate `forge_nwn_rules`.** Elle est séparée de `forge_gameplay`, de `forge_project` et
   du runtime natif. Elle ne dépend ni de Bevy, ni de Tauri, ni de Rapier.

2. **Profil versionné.** `forge.nwn-rules-profile@1` sélectionne explicitement le ruleset
   (`vanilla`, `shadows_of_undrentide`, `hordes_of_the_underdark`), les limites de niveau, le seuil
   d'hostilité, les règles de critique, les limites de rounds et de déplacement. Le profil par
   défaut est `nwn-ee-vanilla-m4-subset`. Le document de référence est
   `docs/aurora-bridge/nwn-rules-profile.v1.json`.

3. **RNG injectable.** `DeterministicRng` utilise SplitMix64 avec une graine explicite, un journal
   borné de jets étiquetés et un snapshot restaurable. Aucune horloge murale ni entropie système
   n'intervient dans les décisions de jeu.

4. **Combat d20 borné.** Le moteur couvre : initiative, AC (armure, bouclier, Dex plafonnée, naturel,
   diversion, esquive, taille), BAB et attaques itératives, d20 naturel 1/20, menace et
   confirmation de critique, concealment, réduction de dégâts, immunités, dés de dégâts et PV. Les
   attaques spéciales, les effets complets et les règles de taille avancées restent hors périmètre.

5. **Objets transactionnels.** Les définitions, instances, stacks, poids, slots, équipement et
   transferts sont typés. Un transfert refuse atomiquement une destination pleine. Les propriétés
   inconnues restent visibles.

6. **Sorts et repos bornés.** Le registre couvre `magic-missile`, `cure-light-wounds` et `bless` :
   slots par niveau, consommation, repos, dégâts, soins, buffs temporaires et expiration. Les VFX,
   la concentration, les AoE, les invocations et les composants ne sont pas revendiqués.

7. **Factions et IA déterministes.** Les réputations du bundle sont lues ; le seuil d'hostilité est
   un paramètre du profil. L'IA de mêlée se déplace vers la cible puis attaque. La perception, la
   fuite, le pathfinding de navigation et la traversée de zone par IA restent hors périmètre.

8. **Quête et sauvegarde M4.** `RulesSnapshot` persiste personnages, inventaire, sorts, effets,
   quête, RNG, round et état d'encounter. `M4Save` l'emballe avec `M3Save` (session, portes, zones
   visitées, journal, or, transcript, scheduler). Une reprise ne rejoue ni l'init, ni la récompense,
   ni le travail différé.

9. **Oracle séparé.** Le harnais `compare_encounters` compare deux traces. L'oracle NWN local n'est
   pas exécuté en M4 : le rapport porte `oracleStatus = not-run` et aucune divergence n'est
   masquée.

10. **Aucune modification du ruleset natif.** `forge_gameplay` et ses quotas restent inchangés. Les
    tests ciblés du ruleset natif restent la référence de non-régression.

## Conséquences

- Le ruleset NWN est sélectionnable et testable sans contaminer le gameplay natif.
- Les valeurs non sourcées par une 2DA sont marquées `synthetic` dans leur champ `source`.
- Les limites sont visibles dans le rapport et la matrice, jamais converties en compatibilité
  générale.
- Une future extension devra ajouter des tables 2DA, un oracle NWN réel et les mécaniques
  manquantes sans élargir silencieusement le profil.

## Alternatives rejetées

- **Modifier `forge_gameplay` pour ajouter d20 :** mélange des rulesets et régression native.
- **Recopier les tables 2DA dans le bundle M4 :** hors contrat, données propriétaires et périmètre
  non autorisé.
- **Utiliser le moteur NWN comme runtime :** dépendance externe, licences et autonomie.
- **Déclarer le combat « exact » sans oracle :** revendication non prouvée.
- **Simuler le combat avec des valeurs codées en dur :** non data-driven et non auditable.

## Suivi

| Élément | Jalon |
| --- | --- |
| Profil et contrat | AB-M4-01 |
| Combat d20 | AB-M4-02 |
| Personnages | AB-M4-03 |
| Objets | AB-M4-04 |
| Sorts | AB-M4-05 |
| Factions/IA | AB-M4-06 |
| Quête/persistance | AB-M4-07 |
| RNG | AB-M4-08 |
| Oracle | AB-M4-09 |
| Sauvegarde multi-zone | AB-M4-10 |
| Budgets | AB-M4-11 |
| Matrice | AB-M4-12 |

Backlog : [`../aurora-bridge/BACKLOG.md`](../aurora-bridge/BACKLOG.md).
