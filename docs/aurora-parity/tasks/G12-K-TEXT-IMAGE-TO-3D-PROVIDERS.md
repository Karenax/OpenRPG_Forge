# G12-K — Fournisseurs texte/image vers 3D

## Portée

- exigence : `ORF-AI-022` ;
- garder les fournisseurs 3D interchangeables et optionnels ;
- fournir un mode offline déterministe, plus des contrats locaux TRELLIS et TripoSR ;
- confiner les endpoints au loopback, référencer les secrets par variable d’environnement et exiger
  une autorisation réseau explicite ;
- contrôler licence, provenance, taille, topologie, staging et acceptation humaine avant mutation ;
- partager proposition, dry-run, permission, lot atomique et revert entre Toolset, CLI et MCP ;
- normaliser ensuite l’OBJ par les opérations Blender typées existantes.

## Résultat livré

`Model3DProviderDefinition` décrit un fournisseur v1 par UUID, clé, type, endpoint, secret indirect,
timeout, plafond d’octets, licences autorisées et provenance. Le mode
`deterministic-offline-3d` fonctionne sans réseau. Les modes `TrellisLocal` et `TripoSrLocal`
n’acceptent que HTTP(S) loopback sans information d’authentification dans l’URL ; le jeton éventuel
est lu à l’exécution depuis l’environnement. Le transport borne délai et réponse, bloque les
redirections hors loopback et refuse tout résultat autre que CC0-1.0, OBJ UTF-8 et topologie bornée.

Le service accepte un prompt texte ou une image relative au workspace. L’image est limitée à
16 Mio et hachée avant envoi. La proposition ne crée aucun fichier. Elle expose coût, fournisseur,
licence, provenance, cinq preuves, diff et un reçu de staging non accepté. L’acceptation humaine
ajoute sa provenance au reçu ; elle est obligatoire pour le dry-run puis l’application.

Le handler unique vérifie révision, permission, plan, empreinte et absence d’altération, puis écrit
atomiquement quatre fichiers : OBJ sous `assets/staging/ai-3d/`, reçu JSON, registre
`assets/external-edits.json` et requête Blender. Undo/redo restaure les quatre fichiers et l’état
canonique. La fenêtre **Texte/Image vers 3D**, la commande CLI `assets generate-model3d` et les
quatre outils MCP utilisent exactement ce service et ce handler.

Blender reçoit uniquement `validate_model`, `normalize_model`, `generate_lods`,
`generate_collision`, `export_glb`, `generate_thumbnail` et `audit_quality`. L’OBJ de staging est
recontrôlé par SHA-256 avant et après le worker. Le smoke réel produit un GLB, une miniature et un
audit à partir de la sortie offline ; le même passage s’applique aux sorties locales TRELLIS ou
TripoSR après acceptation.

## Non-objectifs et limites

- TRELLIS et TripoSR ne sont ni téléchargés ni imposés par OpenRPG Forge ; l’auteur branche une
  instance locale compatible lorsqu’il le souhaite ;
- le provider offline produit une géométrie de validation déterministe, pas un modèle artistique
  final ;
- aucune licence incertaine ou non-CC0 n’est importée automatiquement ;
- aucun fournisseur 3D n’est requis par le runtime ni par l’ouverture future d’un module ;
- aucun Python fourni par un modèle, un auteur ou MCP n’est exécuté.

## Preuves

- 3 tests domaine : contrat valide, endpoint/secret/licence refusés, image/path/topologie et
  acceptation obligatoires ;
- 3 tests application : provider offline déterministe et transport loopback factice avec
  autorisation, secret d’environnement, contrat JSON et refus non-CC0 ;
- 4 tests ModuleFormat : texte, image confinée et hachée, proposition sans écriture, acceptation,
  dry-run exact, altération, transaction quatre fichiers et undo/redo ;
- test MCP propose/plan/apply/revert avec licence, provenance et restauration des quatre fichiers ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_PASS` ;
- smoke CLI : `OPENRPG_FORGE_G12K_MODEL3D_CLI_PASS` ;
- smoke Blender 4.5 réel : `OPENRPG_FORGE_G12K_MODEL3D_BLENDER_PASS`, avec 1 mesh source,
  2 LOD, 1 collision, 12 triangles, miniature 512 × 512 et GLB 2 de 4 432 octets ;
- `scripts/Verify.ps1` protège les smokes Toolset, CLI et Blender dans la porte intégrée.
