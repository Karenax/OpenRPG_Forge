# Acceptation Studio — UX8-M Expérience joueur multijoueur

État local au 22 août 2026 : **fonctionnel et qualifié** pour les quatre routes UX8-M. La vague
Préparer et publier atteint ainsi **47/51** routes accessibles dans Studio.

La page `launcher` expose une surface humaine unique pour configurer la découverte, le lobby, la
création et l'autorité des personnages. Le profil est persistant dans
`.forge/network/player-experience.json`, séparé du document projet et écrit atomiquement par
`WorkspaceNetworkPlayerExperienceStore`. L'absence initiale du fichier produit un profil explicite
en mémoire sans écriture implicite.

Depuis le 25 août 2026, le lanceur et le runtime possèdent par ailleurs un transport TCP direct réel,
décrit dans `docs/MULTIPLAYER_INTERNET.md` et l'ADR-0048. Cette évolution ne transforme pas
rétroactivement la surface Studio UX8-M en client réseau : elle configure des contrats plus riches,
tandis que le chemin actuellement raccordé au runtime couvre l'adresse IP/DNS, le port,
l'empreinte/protocole, l'admission, la présence et les déplacements.

## Routes couvertes

<a id="orf-mp-003"></a>
- **ORF-MP-003 — Découverte et connexion** : adresse directe, découverte LAN, annuaire curaté,
  favoris, serveurs récents et liens d'invitation sont présentés sans lancer de connexion.

<a id="orf-mp-004"></a>
- **ORF-MP-004 — Lobby et sélection de personnage** : profil de règles, version de contenu,
  capacités joueur/équipe, sélection visible, permissions et état prêt sont éditables dans un
  formulaire lisible à 1920×1080.

<a id="orf-mp-005"></a>
- **ORF-MP-005 — Création de personnage** : profil de règles, bornes d'attribut, modèles ordonnés,
  aperçu, import local contrôlé et approbation serveur alimentent la commande canonique.

<a id="orf-mp-006"></a>
- **ORF-MP-006 — Personnages locaux ou serveur** : modes local, import signé, gestion serveur et
  liaison au compte, avec signature, chiffrement, migration et liaison obligatoires configurables.

## Transaction, persistance et préservation

- sélection explicite du module et état initial honnête si le profil workspace n'existe pas ;
- validation complète du profil, refus des politiques incomplètes et valeurs hors contrat ;
- aperçu par `NetworkPlayerExperienceHistory` en dry-run, sans transport ni serveur ;
- révision serveur, empreinte canonique du profil source et empreinte binaire typée du brouillon
  approuvé revérifiées lors de l'application ;
- application, undo/redo et journal par la commande réseau canonique ;
- sauvegarde atomique, réouverture et comparaison canonique du profil ;
- projection runtime et ForgeScript réellement relues, sans exécuter de session ;
- politique de combat MP-008, identités internes et queues non représentées strictement préservées ;
- projet, historique, révision, journal, état dirty, zone, sélection et approbation source inchangés ;
- workspaces de preuve supprimés avant le marqueur PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **9/9** — Network 6 et ModuleFormat 3 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_M_PASS` avec quatre routes, sélection,
  invalidité, dry-run, commande, projection runtime/script, gardes stale, undo/redo, persistance,
  combat préservé, nettoyage, transaction UI, source inchangée et surface restaurée à `true` ;
- captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées pour Connexion, Lobby, Création
  et Autorité : valeurs complètes, onglets et six actions visibles, aucun UUID ni clipping ;
- portail incrémental : **PASS en 443,52 s** — build 1,30 s, tests 2,14 + 2,42 s, smoke
  428,45 s, capture 8,28 s et matrice 0,88 s ;
- matrice Studio après promotion : **628 `functional` / 89 `planned`**, dont UX8 **47/51**.
- graphe Roslyn officiel : **11 213 nœuds / 86 391 relations / 0 diagnostic**, frais et pleinement
  déterministe après `check --full`.

Artefacts :

- `artifacts/verification/studio-increment/ux8-m-network-player-experience-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-m-lobby-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-m-character-builder-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-m-character-authority-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-m-network-player-experience.summary.json`.

## Limites honnêtes

Studio configure et persiste un contrat d'expérience joueur. Cette surface d'acceptation ne lance
elle-même aucun transport, serveur, annuaire distant, lobby réel, client, session publique ou monde
persistant, même si le lanceur/runtime possède désormais le transport direct de l'ADR-0048. Aucun
secret, compte ou personnage joueur n'est stocké dans le module. La surface ne qualifie pas MP-008
et préserve cette politique sans la réécrire.

Le portail conserve `full_gate=false` et `graph_check=false` ; il ne remplace ni
`scripts/Verify.ps1`, ni une qualification réseau inter-processus, ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkPlayerExperienceProfileTests|' +
  'FullyQualifiedName~WorkspaceNetworkPlayerExperienceStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page launcher `
  -Evidence ux8-m-network-player-experience `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts/verification/studio-increment/ux8-m-network-player-experience-forward-plus-fhd.png'
```
