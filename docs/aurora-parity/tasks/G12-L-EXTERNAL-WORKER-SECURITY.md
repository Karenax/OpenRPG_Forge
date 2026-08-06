# G12-L — Sécurité canonique des workers externes

## Portée

- exigence : `ORF-AI-023` ;
- interdire explicitement toute exécution publique `exec`, `eval`, shell ou Python libre ;
- conserver uniquement des opérations Blender typées et des chemins relatifs confinés ;
- rendre la politique visible, versionnée, persistante, prévisualisable, annulable et auditée ;
- partager la même commande entre le Toolset et MCP ;
- appliquer réellement au worker les limites de durée, de sortie, de réseau et d’opérations.

## Résultat livré

`ExternalWorkerSecurityPolicy` est la source canonique v1. Elle possède un UUID stable, une
provenance, une permission minimale, quatre garanties non désactivables (Python libre, shell et
`eval` interdits, chemins confinés obligatoires), l’interdiction réseau, un timeout de 5 à 900
secondes, une sortie GLB bornée de 1 Mio à 2 Gio et une allowlist composée exclusivement des huit
opérations Blender typées déjà prises en charge. Validation du modèle et export GLB restent
obligatoires.

La commande `SetExternalWorkerSecurityPolicyCommand` exige un identifiant de transaction, un
`planId`, la révision courante et la permission `Configure`. Le handler mémorise l’empreinte du
dry-run ; une application absente, altérée, obsolète ou insuffisamment autorisée est refusée avec un
code stable. Le diff sémantique, l’inverse et l’audit sont produits par cette voie unique.

Le document `assets/worker-security.json` est écrit atomiquement. Son absence est traitée comme une
extension optionnelle : le profil sûr par défaut est chargé sans créer de fichier. Un document
invalide produit un diagnostic localisé et le profil sûr reste disponible sans accepter l’état
corrompu. L’écran **Sécurité workers** permet de modifier les budgets, l’allowlist et la provenance,
affiche garanties, UUID, schéma, coût nul, permission, transaction, plan et diff, puis propose
appliquer, annuler et rétablir.

Les outils MCP `GetExternalWorkerSecurity`, `PlanExternalWorkerSecurity`,
`ApplyExternalWorkerSecurityPlan` et `RevertExternalWorkerSecurityPlan` utilisent le même handler et
le même store. Aucun outil MCP générique d’exécution n’est ajouté.

Chaque requête Blender embarque la politique canonique. Le worker Python refuse les champs de
requête inconnus, toute opération hors allowlist, toute politique assouplie, tout réseau, timeout ou
plafond hors limites. Blender est toujours lancé par une commande fixe avec `--factory-startup` et
`--disable-autoexec`; le timeout canonique est transmis à `subprocess.run` et la taille du GLB est
contrôlée avant publication atomique. Une expiration produit `asset.worker.timeout` et un dépassement
`asset.worker.output_too_large`.

## Limites

- la politique protège le worker Blender livré par OpenRPG Forge ; elle ne tente pas d’administrer
  les logiciels externes lancés manuellement par l’utilisateur ;
- les garanties de sécurité fondamentales sont visibles mais verrouillées, car les désactiver
  contredirait le contrat P0 ;
- le runtime n’a pas besoin du worker et ignore proprement l’absence du document optionnel ;
- les fournisseurs 3D locaux restent régis séparément par leur consentement loopback de G12-K.

## Preuves

- 3 tests domaine : défaut sûr, diagnostics localisés, allowlist et opérations obligatoires ;
- 3 tests application : dry-run/apply, plan exact, permission, révision, audit et undo/redo ;
- 7 tests ModuleFormat ciblés : round-trip exact, défaut absent, refus invalide, politique embarquée
  dans la requête et opération désactivée ;
- test MCP : lecture, refus sans permission ou plan, diff, transaction, application, persistance et
  revert ;
- 16 tests Python : refus des champs `python`, `exec`, `eval`, `shell`, `script`, `command`, politique
  non sûre, opération désactivée, timeout et sortie trop grande ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_PASS` ;
- `scripts/Verify.ps1` protège le marqueur Toolset et les smokes Blender réels existants.
