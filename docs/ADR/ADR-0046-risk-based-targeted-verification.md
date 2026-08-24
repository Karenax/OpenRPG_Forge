# ADR-0046 — Vérification ciblée fondée sur le risque

Date : 24 août 2026
Statut : accepté
UUID : `9ed182cb-2e37-4d8b-9192-5cddcf7d4120`
Schéma : v1
Provenance : `author:OpenAI Codex sous instruction utilisateur`

## Contexte

Le dépôt possède onze projets de test .NET, plusieurs suites Python et de nombreux smokes produit.
Le portail transversal est nécessaire avant publication, mais son exécution après chaque correction
Studio répétait toutes les routes, les hôtes runtime, les assets, le réseau et la distribution. Un
filtre VSTest identique pouvait aussi être envoyé à plusieurs projets : un projet sans correspondance
retournait alors un succès de processus sans avoir exécuté de test.

Une boucle rapide ne peut cependant pas devenir une preuve globale. Les relations Roslyn sont utiles
pour le code statique, mais elles ne couvrent pas seules les chargements Godot, la réflexion, les
formats, les scripts, les assets ou les scénarios réseau.

## Décision

`scripts/Get-VerificationPlan.ps1` devient le routeur déterministe. Il produit un plan JSON v1 à
partir des fichiers, feature IDs, relations du graphe et du palier `Development`, `Lot`, `Milestone`
ou `Publication`. Chaque étape déclare sa cible, sa raison, son coût estimé et ses effets de bord. Une
relation absente provoque un repli vers la suite du projet ou un profil plus large, jamais un plan vide.

`scripts/Invoke-TargetedVerification.ps1` exécute le plan séquentiellement et s'arrête au premier
échec. Chaque projet .NET reçoit son propre filtre et son propre TRX. Zéro test exécuté est un échec,
même si VSTest retourne le code zéro. Le plan et le résumé chiffré sont conservés sous
`artifacts/verification/targeted/`.

`scripts/Verify.ps1` conserve `Full` comme comportement par défaut et expose six profils bornés :
`Core`, `Studio`, `Runtime`, `Assets`, `Network` et `Distribution`. `Full` est réservé aux fins de
vague, à trois à cinq lots liés, dix à vingt routes, un changement de contrat partagé important ou
une préparation de publication.

Le smoke Studio accepte trois portées :

- `Route` exécute le socle d'édition et la route canonique demandée ;
- `Wave` exécute le socle et toutes les routes de la vague UX ;
- `Full` conserve tous les smokes historiques.

Les captures, restaurations et contrôles de non-mutation restent actifs dans les trois portées. Un
smoke ciblé ne promeut ni la matrice Studio ni le statut d'un gate global.

## Preuves

- Tests : `tests/Forge.Studio.Core.Tests/Verification/StudioSmokeSelectionTests.cs`
- Exécuteur : TRX et rejet explicite de zéro test sous `artifacts/verification/targeted/`
- Smoke réel : marqueur `OPENRPG_FORGE_STUDIO_SCOPED_SMOKE_PASS`
- Profils ciblés : Studio, Runtime, Assets, Network, Distribution et Core passés séparément
- Portail transversal : `OPENRPG_FORGE_VERIFICATION=PASS`, 2 130 tests .NET, 64 tests Python,
  durée 873,66 s le 24 août 2026
- Documentation : `docs/planning/TARGETED_VERIFICATION.md`

## Conséquences

- la boucle quotidienne devient explicable et proportionnée au lieu de relancer chaque produit ;
- les validations restent mono-processus pour respecter le budget mémoire du poste ;
- un changement dynamique ou runtime exige toujours un smoke explicite, même si le graphe propose
  des tests statiques ;
- `Distribution` vérifie une distribution existante sans la reconstruire ou la publier ;
- une capture automatisée et un marqueur headless ne remplacent jamais l'inspection visuelle humaine ;
- toute évolution du schéma de plan crée une nouvelle version et conserve la lecture des preuves v1.

## Remplacement

Cette décision complète les ADR 0032, 0033 et 0045 sans les remplacer.
