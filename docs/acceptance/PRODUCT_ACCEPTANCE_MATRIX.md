# Matrice d'acceptation produit

Cette matrice relie le but final de `PROJECT_CONTEXT.md` à des preuves exécutables et à une courte
revue humaine. Un marqueur automatisé prouve le contrat et la non-régression ; il ne remplace pas la
lisibilité visuelle ni la compréhension par un nouvel utilisateur.

| Parcours | Preuve automatisée canonique | Revue humaine avant publication | État local du 12 août 2026 |
|---|---|---|---|
| 1. Projet et zones | `G705_AREA_PASS`, `T12_SMOKE_PASS`, round-trip `ProjectSessionTests` | Créer un module vierge, une zone intérieure et une extérieure sans ouvrir de JSON | Automatisé et inclus dans le marqueur de parcours produit |
| 2. Assets gouvernés | `G716_ASSETS_PASS`, `AV3_PLACEMENT_PASS`, smoke Forgotten Forest | Importer un modèle CC0, vérifier licence/aperçu, le placer et le recharger | Automatisé ; assets et aperçu revus visuellement |
| 3. Contenu RPG | `G8_AUTHOR_UI_PASS`, `R077` à `R096` | Créer créature, objet, porte, trigger, rencontre et faction depuis les écrans | Automatisé et inclus dans le marqueur de parcours produit |
| 4. Narration et règles | `R005` à `R008`, `R097` à `R101` | Créer dialogue, quête, journal, pouvoir et script puis corriger un diagnostic | Automatisé et inclus dans le marqueur de parcours produit |
| 5. Monde et simulation | `G714_NAVIGATION_PASS`, `G11H` à `G11M`, smoke runtime | Configurer navigation, environnement et caméra puis jouer depuis la sélection | Forward+ rejoué sur Crypte et Forgotten Forest |
| 6. Playtest et reprise | `G715_DIAGNOSTICS_PASS`, `T32_SMOKE_PASS`, smokes save create/resume | Lancer, corriger, sauvegarder, fermer et reprendre | Automatisé ; runtime contrôlé interactivement |
| 7. Distribution | gates Runtime/Creator/installateur et `.orpmod` round-trip | Installer sur un profil Windows propre, exporter et réinstaller dans un second workspace | Cycle propre complet réussi localement |
| 8. Assistant IA/MCP | `R020` à `R023`, scénario MCP intégré de `Verify.ps1` | Planifier, dry-run, approuver, appliquer, tester puis revert avec coûts et permissions visibles | Gouvernance automatisée ; fournisseur réel réservé à la qualification publique |

## Règle de décision

Un parcours est publiable seulement lorsque sa preuve automatisée passe depuis un checkout propre et
que sa revue humaine est datée avec résultat, capture ou journal, résolution, échelle UI et profil
utilisateur. Aucun statut `Tested` du tracker ne dispense de cette revue produit.

## Portails

```powershell
& .\scripts\Test-AuroraEvidencePaths.ps1
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
```

La distribution jouable possède en plus son portail indépendant :

```powershell
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```
