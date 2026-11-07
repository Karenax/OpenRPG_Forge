# Gate 6 — premier lot de gouvernance, 22 octobre 2026

Actualisation du 23 octobre : **clôturée localement**, selon
[le rapport final](GATE6_CLOSURE_20261023.md). Les sections ci-dessous conservent
la chronologie et les verdicts de chaque étape.

État initial du premier lot au 22 octobre :

**Gate ouverte, clôture non acquise.** Objectif : réaliser Gate 6 jusqu'à sa
clôture documentée sur preuves. Le [plan](../../planning/GATE6_IMPLEMENTATION_PLAN.md)
conserve tous les critères.

## Réalisation

`forge_project::assistant` fournit un aperçu immuable dérivé du bus existant :
requête typée, révision/hash de base, diff des sources, politique de portée et
zones protégées, empreinte du diff approuvé, provenance IA explicite. Politique
et approbation appartiennent à l'application de confiance ; ces types ne sont
pas désérialisables depuis un fournisseur ou des arguments MCP.

L'application revalide portée, permissions, révision et reçu. Une retouche depuis
l'aperçu provoque un refus sans mutation. La reprise d'une commande identique
déjà acceptée rend son reçu sans ajouter de transaction ni écraser une modification
ultérieure. Réutiliser un ID avec d'autres paramètres est refusé. Budgets :
requête/revue 512 Kio, 256 changements, 128 chemins de portée. Les changements de
taille d'un tableau exigent la permission sur son remplacement entier ; les
sous-arbres protégés priment.

L'hôte natif expose `prepare_assistant` et `apply_assistant` en conservant les
validations de candidats et transactions workspace. Aucun chemin ni shell n'est
ajouté au protocole. Save/Open et Undo restent les opérations communes.

## Preuves disponibles

Campagne : `E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate6-ia-mcp/`, stockage
physique `F:/OpenRPG_Forge/Preuves-et-analyses/actives/gate6-ia-mcp/`.

| Preuve | Résultat et portée |
| --- | --- |
| `source-inventory-before-product-001.json` | 1 517 fichiers sources/configuration, HEAD et statut Git ; après ouverture documentaire, avant modifications produit |
| `governance-managed-test-003.log` et reçu | PASS : 101 tests bibliothèque, 8 gouvernance, 1 hôte assistant, 4 workspace, 1 hôte zone ; 115 au total |
| `governance-clippy-002.log` et reçu | PASS : Clippy strict bibliothèque et quatre suites d'intégration affectées |
| `governance-wasm-check-001.log` et reçu | PASS : check WASM de forge_project ; pas une qualification Studio/WebGPU |
| rustfmt et diff ciblés | PASS sur les fichiers du lot ; le contrôle Git global rencontre les anciens chemins `.tidewater-integration` inaccessibles, sans les modifier |

Les premières passes sont conservées comme historique. Le rangement final des
déclarations de module par rustfmt ne change pas le comportement testé ; Clippy
002 l'inclut. Les tests démontrent refus atomiques, annulation, audit sauvegardé,
réouverture, préservation d'une correction humaine de terrain, reprise sans doublon
et isolation du module voisin. Les fixtures sont synthétiques.

## Limites et suite

- Lot A partiel : politique et hôte testés ; sélection d'IDs/région et diff dans
  le vrai Studio à réaliser.
- Assistant UI, MCP, inspection ciblée/assets, jobs persistants, staging,
  budgets financiers et retries restent à réaliser.
- La reprise testée conserve le review et l'approbation en mémoire ; elle ne
  qualifie pas une reprise de job après arrêt du processus.
- Porte/dialogue et aventure R6-P03, comparaison manuelle/IA réelle R6-P07 et
  jeu sans fournisseur R6-P08 ne sont pas qualifiés par ces tests.
- OpenAI avec clé locale est le choix propriétaire. Aucune variable
  `OPENAI_API_KEY` n'était accessible dans Process/User/Machine ; aucun secret
  affiché ni appel payant réalisé. Sources pour l'adaptateur :
  [sorties structurées](https://developers.openai.com/api/docs/guides/structured-outputs)
  et [Responses API](https://developers.openai.com/api/docs/guides/migrate-to-responses).
- Ni build/lancement du Studio complet, ni capture UI, ni distribution. Aucun
  commit, push, contrôle GitHub ou ouverture de gate suivante.

Campagne active : aucun archivage de clôture ni retrait d'originaux. Les changements
de conversion préexistants sont conservés.

## Deuxième lot — sélection et planification ciblée

`assistant::selection` résout une sélection d'IDs de placements, d'objets et de
répliques dans une région finie de la zone, liée au hash/révision. Le contexte
de partage est borné à 64 Kio et 64 entrées : il exclut les dialogues, objets et
acteurs non sélectionnés. Les placements verrouillés, IDs inconnus, doublons,
régions inversées ou dépassant la zone sont refusés. Les chemins de permission
sont calculés par l'application depuis les IDs, jamais fournis par le modèle.

`assistant::planning` reçoit un plan non fiable et compile au plus 32 opérations
en une seule commande ZoneRequest existante : nom de zone, altitude, déplacement
de placement, déplacement d'objet et texte d'une réplique. La transformation
conserve rotation, échelle et référence d'asset. Les capacités absentes sont
explicites et bloquent la mutation ; une instruction shell n'est pas une opération
acceptée. Cette première liste n'est pas la couverture complète de Gate 6.

`planning-test-001` a échoué à la compilation sur le nom du champ de translation ;
le reçu d'échec est conservé. Après correction vers le champ Forge `translation_m`,
`planning-test-002` passe : 6 nouveaux tests de planification et 13 tests de
gouvernance/hôte/workspace. `planning-clippy-001` passe en mode strict.

Les six tests couvrent notamment modification sélectionnée d'une porte et d'une
réplique dans une seule transaction, variante locale conservant ces données après
réouverture, absence de fuite des contenus non sélectionnés, refus hors région,
refus de plan périmé et absence de mutation partielle. Ces tests restent de domaine,
sans preuve de création d'aventure, de fournisseur ou d'interface Studio.

### Serveur local proposé par le propriétaire

Sondes réelles vers `http://192.168.1.41:8080/v1`, sans clé ni données projet :

- GET `/models` : HTTP 200, `qwen3.8-flash-next-iq4-xs` déclaré chargé,
  contexte annoncé 262 144 tokens, entrées texte/image et sortie texte.
- POST `/chat/completions` : HTTP 200, texte `ACCESSIBLE`, durée observée 11,17 s,
  62 tokens de prompt et 123 de complétion déclarés.
- POST `/chat/completions`, schéma strict de sonde : HTTP 200,
  `{"status":"ready"}`, fin `stop`, durée 5,40 s, 151 tokens de prompt et
  39 de complétion déclarés.

Les compteurs et capacités sont ceux annoncés par le serveur ; ils ne prouvent
ni sa compatibilité avec tous les endpoints OpenAI, ni le schéma complet des
plans Forge. Le serveur n'a pas reçu de contenu privé ou d'asset. Les résultats
sont rapprochés dans `planning-and-local-provider-001.json` de la campagne centrale.

## Troisième lot — fournisseur réel et journal durable

Le contrat `assistant::provider` produit les formats Chat Completions et Responses,
avec schéma strict restreint aux opérations/IDs du contexte. Le destinataire,
modèle, brief, contexte, schéma, limite de sortie et réglage de raisonnement
participent au hash de partage approuvé. Changer le destinataire ou activer le
raisonnement invalide une ancienne approbation. Les résultats exigent un plan
complet, des compteurs de tokens et le bon contexte ; refus, troncature ou outils
inattendus sont rejetés. Le raisonnement du modèle n'est pas conservé.

Le transport natif utilise reqwest 0.13.4 épinglé, sans proxy, redirection ni retry
automatique ; connexion 10 s, requête 120 s, réponse 1 Mio maximum. L'HTTP est
limité aux IP LAN/loopback explicites et localhost ; les fournisseurs distants
exigent HTTPS avec vérification TLS normale. Les URL avec identifiants, query ou
fragment sont refusées. La clé ne figure que dans l'en-tête natif, sans Debug,
sérialisation ou corps d'erreur renvoyé. Cargo.lock ajoute les dépendances TLS
natives ; aucune version existante n'a été remplacée.

`assistant::jobs` est un journal borné et rejoué à la lecture : Prepared, Running,
Interrupted, Staged, Applied, Cancelled et BudgetExceeded. Un envoi débite une
réservation avant départ ; un appel incertain la conserve. Les prix sont des
entiers micro-USD par million de tokens, configurés explicitement ; les essais
locaux utilisent zéro, sans inventer un tarif cloud. Les plafonds de tokens,
tentatives (1 à 5) et coût sont contrôlés. Une sortie valide dépassant les limites
du journal est retenue en état bloqué. Un résultat tardif ou doublonné ne remplace
pas le résultat courant.

Le stockage natif utilise DocumentStore : un seul writer, save atomique avec
contrôle de hash, refus de liens/jonctions dans les sous-dossiers de jobs,
Started sauvegardé avant HTTP. Recharger un job Running ne le renvoie pas : une
interruption explicite et une nouvelle tentative autorisée sont nécessaires.
Une sortie Staged se rouvre sans nouvel appel. Aucun jeton humain d'approbation
n'est sérialisé. L'état Applied du journal est un accusé, pas une autorité :
l'orchestrateur devra toujours le rapprocher des reçus canoniques du projet.

### Résultats réels et raisonnement

- `provider-live-plan-001.log` : PASS, 13,47 s ; plan réel de renommage synthétique,
  diff, application gouvernée et réouverture ; 700/262 tokens déclarés.
- Sonde suivante avec `enable_thinking=true` : HTTP 200, 5,0 s ; raisonnement
  présent (198 caractères), résultat arithmétique `5461`, 75/84 tokens déclarés.
  Seuls ces indicateurs sont conservés, sans contenu de raisonnement.
- `provider-live-reasoning-job-002.log` : PASS, 7,34 s ; même fournisseur exact,
  raisonnement explicitement demandé, Started durable, résultat Staged,
  fermeture/réouverture du job sans renvoi, application de la proposition et
  réouverture de la zone ; 700/262 tokens déclarés. Réponse
  `chatcmpl-1ff5addb259f4f56b320741d`, hash résultant
  `7ed13572faa79c3ec9dec4a888f0cedabfaa0beac4f9ab871c749cb30a1324ea`.

Seules des données synthétiques de test ont été envoyées. Les tests appellent
réellement l'API mais n'exécutent pas un parcours humain dans le Studio.

### Validations et réserves

Les tests de contrat, de ledger et de stockage passent dans `provider-wire-test-001`,
`jobs-test-001` et `provider-jobs-native-test-002` (le test réseau est ignoré dans
la passe ordinaire puis exécuté explicitement ci-dessus). Clippy strict domaine
et natif passe dans `provider-jobs-clippy-002` et
`provider-jobs-native-clippy-002`. Les premières passes Clippy en échec sont
conservées ; documentation, extraction d'une validation et placement d'imports
ont été corrigés avant les deux verdicts PASS.

Le lot C reste partiel : orchestration Studio/MCP, découverte/reprise des jobs,
interface de récupération des saves pending, refus de promotion via les permissions
courantes et rapprochement automatique avec les reçus projet à réaliser. La
preuve réelle porte sur un renommage de fixture ; elle ne vaut pas comparaison
manuelle/IA complète R6-P07, création d'aventure, qualification UI ou clôture.

### Passe finale du lot fournisseur/jobs

Les sources sont figées dans `source-inventory-provider-jobs-001.json` puis
revérifiées après les contrôles, sans changement des 1 532 entrées inventoriées.
`provider-jobs-validation-001.json` rapproche les hashes et reçus :

- `provider-jobs-regression-002` : 128 tests de bibliothèque/gouvernance/jobs et
  hôtes, PASS ;
- `provider-jobs-live-final-003` : 4 tests natifs, y compris l'appel réel avec
  raisonnement explicite et job durable, PASS ; appel 7,93 s, 700/267 tokens,
  réponse `chatcmpl-251bd1afe35044f7be1c7bd1` ;
- `provider-jobs-clippy-002` et `provider-jobs-native-clippy-002` : PASS strict ;
- `provider-jobs-wasm-check-001` : PASS pour le domaine commun ;
- rustfmt et diff ciblés : PASS.

Ces résultats valident le lot décrit, sans fermer les critères UI/MCP ou le
parcours produit complet restant au plan.

## Quatrième lot — coordinateur et panneau du Studio

Le coordinateur natif détient les sélections, requêtes, revues et journaux ; la
génération HTTP se déroule hors des verrous de l'hôte et du coordinateur. Le
callback est lié au numéro de tentative : un ancien résultat ne peut pas
consommer l'appel courant. Les confirmations de partage et de mutation restent
des actions UI séparées. L'IPC refuse les URL/credentials fournis par la requête
et n'est accessible que depuis la fenêtre principale. La configuration appartient
au fichier local du Studio ou à `FORGE_ASSISTANT_CONFIGURATION`, variable native
bornée qui contient des réglages et, éventuellement, le nom d'une variable de clé.
Les credentials ne passent pas au frontend.

Le panneau est placé dans Objets : brief, propriétés de zone, modèle sélectionné,
objet RPG et réplique autorisés, région, aperçu exact du corps d'envoi, confirmation
de partage, génération, diff, confirmation d'application et reprise du dernier
job. Une modification des paramètres affichés ou de la révision désactive les
confirmations précédentes. Le brief de reprise est mémorisé localement ; une
indisponibilité du stockage navigateur n'empêche pas le job courant, gardé en
mémoire. Les retouches acceptées rafraîchissent la projection commune du Studio
et restent à sauvegarder par la commande habituelle.

La reprise d'un résultat Staged ne régénère pas. Après perte de processus entre
Apply source et accusé de job, les reçus canoniques sont rapprochés du fournisseur
enregistré, puis le journal est réconcilié sans mutation supplémentaire. L'appel
encore actif ne peut pas être déclaré perdu ; un appel Running rechargé après
redémarrage exige une interruption explicite avant toute nouvelle tentative.

### Preuves et construction en cours

- `assistant-coordinator-test-001` : six tests PASS, transport simulé, couvrant
  les phases distinctes, les refus de révision, les briefs modifiés, les candidats
  natifs refusés, la reprise Staged/Running et la perte d'accusé après Apply.
  Le test de retouche pendant génération est ensuite renforcé pour différer le
  callback ; ce renforcement reste à rejouer dans la passe finale.
- `assistant-coordinator-clippy-002` : PASS strict natif après passage du draft
  par référence ; les premières erreurs d'inférence/style restent dans les reçus.
- `assistant-desktop-check-001` : PASS de la cible desktop avec le nouvel IPC.
- `assistant-ui-wasm-check-002` et `assistant-ui-wasm-clippy-003` : PASS ; les
  premières passes en échec et le warning futur de proc-macro-error2 sont conservés.
- `assistant-frontend-build-001` : arrêt avant compilation par une archive TLS
  toutes plateformes absente du cache offline. `assistant-build-dependency-fetch-001`
  complète les deux archives épinglées manquantes sans modifier Cargo.lock.
- `assistant-studio-source-inputs-001.json` inventorie physiquement 2 120 fichiers,
  279 202 248 octets, en deux passes identiques, sans suivre de lien/jonction et
  sans parcourir les caches. Le périmètre inclut shaders et assets sources, au-delà
  des premiers inventaires limités aux extensions de code/configuration.
- `assistant-frontend-build-002` est lancé ensuite dans
  `E:/OpenRPG_Forge/.build/frontend/studio-current`, avec cache Cargo commun et
  sans kit privé. Son verdict final et le contrôle d'absence de changement restent
  à recueillir. Le lanceur `scripts/gate6/Invoke-AssistantStudioBuild.ps1` ne crée
  pas de copie de code ou de binaire propre à un essai.

La fixture `assistant_qualification_fixture` prépare un module, une carte, une
porte et une réplique synthétiques via l'hôte canonique, dans un nouveau répertoire
de données isolées. Elle reste à exécuter après la compilation frontend. Aucun
parcours graphique du nouveau panneau, aucune recette MCP, aucune comparaison
manuelle/IA complète ou clôture ne sont déduits de ces contrôles.

### Recette graphique réelle et réouverture

`assistant-frontend-build-002` et `assistant-desktop-build-001` passent : frontend
commun sans assets privés, application Tauri construite dans le cache partagé.
`assistant-studio-source-inputs-002.json` confirme les 2 120 entrées identiques
après le frontend. Le contrôle `003` après les deux builds relève uniquement la
conversion d'erreur du helper `assistant_qualification_fixture`, corrigée puis
compilée/exécutée dans `assistant-fixture-run-002`. Le premier échec de compilation
du helper est conservé dans `assistant-fixture-run-001`. Ce helper n'entre pas
dans le binaire Studio. `assistant-coordinator-test-002` rejoue les six tests avec
le callback effectivement différé pendant la retouche humaine : PASS.

Le parcours suivant est exécuté dans la vraie fenêtre Windows du Studio par
la compétence `computer-use`, sur les données isolées
`E:/OpenRPG_Forge/.build/qualification-data/gate6-assistant-ui` :

1. ouvrir le module Assistant local et la carte Village de recette ;
2. autoriser uniquement les propriétés de carte et demander son renommage ;
3. lire le corps exact de l'envoi, avec `enable_thinking=true`, sans porte ni
   dialogue dans le contexte ; confirmer le partage, puis lancer la génération ;
4. relire le diff unique `/corrections/name`, confirmer cette révision, appliquer ;
5. sauvegarder, quitter, relancer, rouvrir la carte et reprendre le dernier job.

Le fournisseur réel `qwen3.8-flash-next-iq4-xs` renvoie
`chatcmpl-db2e0be8c93f41f3a0050785`, 718/237 tokens déclarés, une seule opération
`rename_zone` vers Village du raisonnement. La mesure de 13,67 s est le délai
entre le clic et l'observation du résultat, pas une latence HTTP instrumentée.
Le journal durable contient exactement Started, Staged et Applied. La reprise
affiche le rapprochement avec le reçu canonique et reste à une tentative ; aucun
nouvel appel ou Apply n'est nécessaire. Le hash de tête sauvegardé est
`90e45f086165821fb69dbd7888e80ac847c0012977e4469e4c42c0b801cceb41`.

`assistant-studio-smoke-001.json` rapproche reçus, sources, captures et archives :
une seule commande de ce job dans les sources, provenance IA du modèle réel,
base et gameplay strictement identiques avant/après, seule correction de nom
modifiée, workspace pointant sur la tête acceptée. Les captures et arbres UI
`gate6-assistant-ui-applied-001` et `gate6-assistant-ui-reopened-001/002` sont
centralisés dans la campagne, avec SHA-256. La configuration de recette sans
clé est conservée comme preuve puis retirée du dossier du binaire après arrêt
du Studio et vérification de son hash ; elle n'impose pas ce module aux prochains
lancements.

Les quatre états canoniques utiles (workspace, archives de zone avant/après et
journal de job) sont conservés dans `assistant-studio-smoke-data-001.zip`, 7 815
octets. `assistant-studio-smoke-data-001.verified.json` confirme lecture intégrale,
SHA-256 des entrées, absence de changement et conservation des originaux. Cette
archive partielle du smoke test ne clôture pas la campagne Gate 6.

La recette révèle un affichage initial erroné du fournisseur, dû à l'invalidation
du job lors du montage du panneau, et un plafond insuffisamment visible. Le
panneau conserve maintenant la description du fournisseur lors du changement de
zone et affiche plafond monétaire, plafonds de tokens et reprise manuelle avec
nouvelle confirmation. `assistant-ui-wasm-clippy-004` passe. Le helper est séparé
en lecture d'arguments et préparation canonique ; `assistant-fixture-clippy-002`+passe après le premier refus de longueur conservé. Les sources corrigées sont
figées dans `assistant-studio-source-inputs-004.json` ; la reconstruction frontend
`assistant-frontend-build-003` est lancée pour leur nouvelle recette. Ces dernières
corrections ne sont pas déduites du succès graphique du candidat précédent.

La reconstruction `assistant-frontend-build-003` puis
`assistant-desktop-build-002` réussit. `assistant-studio-source-inputs-005/006.json`
confirme l'absence de changement des 2 120 entrées après les deux builds. Une
nouvelle ouverture du Studio, carte chargée avant le montage d'Objets, montre
immédiatement le fournisseur réel, le raisonnement activé, le plafond de
0,000000 USD aux tarifs locaux déclarés, 100 000/2 048 tokens et l'absence de
retry automatique. La capture/arbre `gate6-assistant-ui-budget-001` et
`assistant-studio-display-validation-001.json` rapprochent ce contrôle et les
quatre reçus PASS. Le journal reste à une seule tentative. Le binaire reconstruit
porte le SHA-256
`5af38e87de98b000ba7cbab450f2c36de1367621b63be1cc71323f6492e1584d`.

Cette preuve porte sur un renommage de carte synthétique. Elle ne prouve pas la
création complète de l'aventure de référence, la recherche d'assets, MCP, la
comparaison manuelle/IA ou la jouabilité sans fournisseur. Ces critères restent
ouverts au plan, ainsi que les recettes élargies de quotas/reprise/conflits.

## Cinquième lot — protocole MCP et staging canonique

`assistant_mcp` fournit le framing stdio JSON-RPC, la négociation des versions
2025-11-25/2025-06-18, `initialize`, `notifications/initialized`, `ping`,
`tools/list` et `tools/call`. Le contrat suit les spécifications officielles
[transport](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports),
[cycle de vie](https://modelcontextprotocol.io/specification/2025-11-25/basic/lifecycle)
et [outils](https://modelcontextprotocol.io/specification/2025-11-25/server/tools).
Chaque message est borné à 192 Kio, chaque réponse à 512 Kio et une session à
512 requêtes. Une trame entrante trop longue termine le transport avant toute
lecture non bornée. Les notifications n'émettent pas de réponse stdout.

Le backend natif `assistant_mcp_native::Inbox` reçoit une sélection/région
autorisée par du code de confiance. Le catalogue ne contient que
`forge_inspect_selection` et `forge_propose_edits` ; il n'accorde ni permission,
ni approbation, ni shell, ni accès à un chemin arbitraire. Le schéma de proposition
est celui du fournisseur existant, calculé depuis le même contexte borné et ses
IDs sélectionnés. Le modèle ne fournit pas de `ZoneRequest` libre.

Une proposition MCP est compilée avec les commandes existantes puis passée à
`AuthoringHost::prepare_assistant`, y compris la validation native du candidat.
Elle reste dans une inbox de seize revues maximum, sans changer les sources.
Une proposition identique réutilise son entrée. L'application confirmée est une
méthode native distincte, absente des outils MCP ; elle exige le hash exact du
diff et passe par `AuthoringHost::apply_assistant`. La provenance est
`AI / mcp-proposal`, sans prétendre identifier un fournisseur réellement appelé.
Une mutation humaine périme l'autorisation de contexte et le diff précédent.

### Contrôles et limites du lot

- `assistant-mcp-test-002` : six tests PASS sur l'hôte canonique, couvrant cycle
  de vie, catalogue, refus de shell/approval/chemin, framing, staging sans mutation,
  déduplication, refus d'une fausse approbation, replay sans doublon, portée,
  révision périmée, candidat natif refusé et plafonds de session/inbox ;
- `assistant-mcp-clippy-003` : PASS strict du module et des tests ; les deux
  passes précédentes en échec sont conservées et corrigées sans suppression de lint ;
- `assistant-mcp-regression-001` : quinze tests natifs PASS (coordinateur, jobs,
  MCP et configuration fournisseur), un test réseau explicitement ignoré ;
- `assistant-mcp-provider-domain-001` : trois tests du contrat fournisseur PASS ;
- `assistant-mcp-domain-wasm-001` : domaine commun compilé en WASM, PASS.

`assistant-mcp-source-inputs-001/002.json` inventorie 2 123 entrées physiques.
Le contrôle après validation relève uniquement la mise à jour du README de
l'assistant : code et configuration identiques. Ce delta documentaire est
conservé comme tel, sans déclarer l'ensemble des entrées inchangé.

Le transport est exercé sur des flux mémoire dans les tests et le staging passe
par le véritable hôte. Le lancement stdio dans le processus Studio, l'autorisation
de sélection et l'inbox accessibles dans l'interface, la révocation et la recette
avec un client MCP externe restent à raccorder et qualifier. Aucun nouvel appel
au LLM, parcours graphique MCP ou clôture n'est déduit de ces tests. L'inbox est
encore éphémère ; elle ne remplace ni les jobs durables du fournisseur ni les
archives canoniques. Les critères restants du plan demeurent ouverts.

## Sixième lot — raccordement MCP au Studio commun

Le Studio accepte le mode explicite `--mcp-stdio`, destiné au processus lancé
par un client MCP. Le thread stdio partage le contrôleur natif et l'unique hôte
d'authoring du Studio ; il ne lance ni second hôte ni serveur réseau. Les appels
relâchent les verrous avant la notification fixe `forge-mcp-changed` à la fenêtre
principale. Aucun texte du modèle n'est exécuté comme JavaScript.

Le nouvel IPC UI `execute_mcp` est borné à 32 Kio, réservé à la fenêtre principale,
et distingue Status, Preview, Authorize, Revoke et Apply. Ces opérations ne sont
pas des outils MCP. Preview calcule le contexte exact et révoque l'ancien
périmètre. Authorize vérifie la révision, le hash du contexte, la sélection et
la région qui ont réellement été prévisualisés, ainsi que l'ouverture du transport.
Le backend de découverte n'expose aucun outil tant que l'autorisation manque ou
que la révision a changé. La fin du transport révoque l'autorisation et son inbox.

Le panneau MCP est placé sous l'assistant dans Objets. Il réutilise les champs de
sélection/région mais n'exige ni brief de génération, ni fournisseur configuré.
Il affiche le JSON exact avant le partage, la confirmation de périmètre, le
compteur d'appels, les propositions/diffs, une confirmation propre à chaque diff
et la révocation. Les notifications d'appels actualisent l'inbox sans polling
permanent. L'application renvoie la projection d'authoring commune ; Save/Undo
restent les commandes existantes. Les propositions restent encore éphémères,
ce qui est indiqué dans le panneau ; ce lot ne remplace pas les jobs durables.

### Contrôles avant recette réelle

- `assistant-mcp-controller-test-001` : dix tests PASS (six protocole/staging et
  quatre contrôleur), y compris Preview sans partage, transport absent, faux
  hash, région modifiée, mutation exclusivement via confirmation UI, révocation,
  déconnexion et conflit avec retouche humaine ;
- `assistant-mcp-desktop-check-001` : compilation desktop PASS ;
- `assistant-mcp-ui-wasm-check-001` : compilation du panneau WASM PASS ;
- `assistant-mcp-integration-native-clippy-001` : PASS strict ;
- `assistant-mcp-integration-wasm-clippy-002` : PASS strict, après correction du
  nom ambigu du signal de révision ; le refus initial est conservé.

Le client externe de recette `scripts/gate6/mcp_studio_client.py` ouvre de vrais
pipes stdio vers le binaire partagé. Il attend l'autorisation dans le Studio,
propose un renommage synthétique, vérifie sa déduplication et le refus d'un outil
Apply, puis attend l'application/sauvegarde UI pour vérifier les archives.
Il n'appelle aucun endpoint d'approbation UI, ne modifie pas les fichiers Forge
et n'utilise pas de LLM. La distinction avec le fournisseur réel est explicite.
Son exécution et ses résultats restent à recueillir après les builds.

`assistant-mcp-ui-fixture-run-001` crée la fixture isolée
`E:/OpenRPG_Forge/.build/qualification-data/gate6-mcp-studio-ui`, tête initiale
`61f487c41e7fbe735744ad8495a12635c95eb7f1e0077baa5f77c3044aef4158`.
`assistant-mcp-ui-source-inputs-001.json` fige 2 130 entrées physiques, puis
`assistant-mcp-frontend-build-001` construit le frontend commun. La recette ne
commence pas sur le binaire précédent. Aucun succès graphique MCP ou clôture
n'est déduit des contrôles ci-dessus.

### Premier parcours stdio/processus/Studio exécuté

`assistant-mcp-frontend-build-001` puis `assistant-mcp-desktop-build-001`
réussissent. Les manifestes `assistant-mcp-ui-source-inputs-002/003.json`
confirment les 2 130 entrées inchangées après les deux builds. Le client externe
est lancé sur la fixture avec `FORGE_AUTHORING_ROOT` limité à son processus enfant,
sans configuration fournisseur ni clé et sans modifier le réglage global du Studio.

`mcp-studio-client-001.result.json` confirme la recette réelle sur pipes stdio :
outils absents avant autorisation, contexte partagé uniquement après Preview et
confirmation dans l'interface, proposition synthétique reçue, réutilisation de
l'entrée pour le même plan et refus de l'outil `apply`. L'application n'est
réalisée que par la confirmation propre au diff dans le panneau, puis Save.
L'audit canonique `AI / mcp-proposal` contient une seule transaction
`435c7b67-6998-5369-8560-4ffb1d553357`, tête sauvegardée
`6ccd1b2b580340404f13634786fd525e30ad99a62648771c02b954c2aa511969`.
La base et toutes les corrections autres que le nom sont identiques avant/après.
Le contexte précédent n'est plus exposé après cette mutation. La révocation est
ensuite exécutée et observée dans le Studio, avant Enregistrer et quitter ; le
client se termine avec code 0. Aucun LLM n'a généré cette proposition de test.

`mcp-studio-ui-validation-001.json` rapproche le client, les builds et les captures
`gate6-mcp-ui-review-001`, `gate6-mcp-ui-applied-001` et
`gate6-mcp-ui-revoked-001`. Les quatre archives canoniques utiles sont conservées
dans `mcp-studio-data-001.zip` (8 644 octets), avec lecture intégrale, hashes et
absence de changement vérifiés dans son reçu `.verified.json`. Les originaux
restent présents. Cette archive partielle ne clôture pas la campagne.

### Corrections issues de ce parcours

Des notifications d'outils rapprochées peuvent arriver pendant une requête UI :
l'inbox s'affiche, mais le compteur peut rester à deux au lieu de trois jusqu'à
Actualiser. Le panneau conserve maintenant une demande de rafraîchissement en
attente et la rejoue après la requête courante, sans polling permanent ni appels
concurrents à l'hôte. Le diff présentait également un nom hérité de la base comme
absent : le panneau affiche maintenant sa valeur effective de contexte avant la
retouche. Le coordinateur fournisseur conserve ce nom d'origine dans sa vue,
même après Apply, pour le même affichage dans l'assistant LLM. Le diff canonique
et son hash d'approbation restent inchangés.

`assistant-mcp-display-native-test-001` passe (six tests coordinateur et quatre
contrôleur MCP), avec contrôle du nom d'origine avant/après Apply ; Clippy strict
passe dans `assistant-mcp-display-native-clippy-001` et
`assistant-mcp-display-wasm-clippy-001`. Le client de recette accepte désormais
un numéro de passe et un nom cible bornés, pour conserver les premiers reçus.
Une nouvelle fixture vierge `gate6-mcp-studio-ui-002` est créée par
`assistant-mcp-ui-fixture-run-002`, afin de rejouer le cas du nom hérité plutôt
que masquer ce défaut par un nom de correction déjà présent.

`assistant-mcp-ui-source-inputs-004.json` fige les corrections, puis
`assistant-mcp-frontend-build-002` est lancé. Leur recette graphique, notamment
le compteur à trois sans actualisation manuelle et l'affichage du nom d'origine,
reste à recueillir ; elle n'est pas inférée du succès du premier candidat.

### Seconde recette exécutée après reprise

L'arrêt physique du contrôle d'interface avait suspendu l'objectif avant toute
autorisation de la seconde passe. À la reprise demandée, le même client
`mcp-studio-client-002` est vérifié vivant puis repris sans relancement. Les builds
`assistant-mcp-frontend-build-002` et `assistant-mcp-desktop-build-002` passent ;
les manifestes `assistant-mcp-ui-source-inputs-005/006.json` confirment les 2 130
entrées inchangées depuis le manifeste `004`.

`mcp-studio-client-002.result.json` se termine avec code 0. Le compteur arrive
automatiquement à trois sans Actualiser, et le diff affiche bien Avant :
« Village de recette », Après : « Village MCP corrigé ». Tab place le focus
visible sur Appliquer ; Entrée applique la confirmation de diff et Ctrl+S
sauvegarde. La révocation est ensuite observée, puis Enregistrer et quitter
termine le processus créé pour la recette.

Le reçu canonique `ac3a5e20-de8e-52b9-9629-baf70592cace` n'apparaît qu'une fois ;
tête sauvegardée
`919b3aefc119eb5ed40169bf280f3c520f1b3b0a1f705fbc627a597a6e614385`.
La base et les autres corrections restent identiques. Le plan vient du client
de test synthétique, sans appel à un LLM. Le rapprochement et les captures sont
dans `mcp-studio-ui-validation-002.json`, notamment
`gate6-mcp-ui-auto-counter-002`, `gate6-mcp-ui-review-002`,
`gate6-mcp-ui-applied-002` et `gate6-mcp-ui-revoked-002`.
Les quatre archives canoniques utiles sont conservées dans
`mcp-studio-data-002.zip`, 8 621 octets ; son reçu `.verified.json` confirme
lecture intégrale, hashes, absence de changement et conservation des originaux.

La revue UX ciblée, avec les nouvelles règles du propriétaire, est consignée
dans `Preuves-et-analyses/rapports/gate6-ux-mcp-20261022.md` depuis la racine du
workspace. Elle utilise la fenêtre Tauri réelle par computer-use ; aucun outil
Playwright n'est exposé dans ce tour et aucun verdict Playwright/WCAG n'est
annoncé. Elle distingue les contrôles clavier et captures réels des heuristiques.

Deux petites corrections de rédaction suivent cette recette : aide MCP
raccourcie sans retirer la conséquence de révocation, et détail technique de
configuration du fournisseur conservé sous Diagnostics de l'assistant, avec
message métier dans le parcours. `assistant-ux-check-002.log` relève zéro
avertissement sur les deux panneaux, après l'avertissement initial de texte long.
`assistant-ux-wasm-clippy-001` passe strictement. Ces textes nouveaux restent à
relire dans le prochain build fonctionnel ; le succès de la recette précédente
ne leur est pas attribué rétroactivement.

Une répétition de la confirmation d'accès sur le même périmètre conserve aussi
désormais les propositions et leur hash de revue, au lieu de recréer l'inbox.
`assistant-mcp-reauthorize-test-001` passe cinq tests du contrôleur, dont ce cas,
et `assistant-mcp-reauthorize-clippy-001` passe strictement. Preview reste
l'opération explicite de remplacement du périmètre. Cette correction native
postérieure est à inclure dans le prochain candidat.

La suite prioritaire reste le lot B : inspection et recherche des assets réellement
disponibles, puis capacités de création nécessaires au parcours de référence.
Les propositions MCP éphémères, la récupération des saves pending, les parcours
étendus de reprise/quotas, la comparaison manuelle/IA et le jeu sans fournisseur
restent ouverts. Aucun de ces essais ne clôture Gate 6.

## Lot B — Références d’assets et ajout gouverné

Le formulaire de l’assistant possède une recherche locale dans la bibliothèque
canonique, par pages de douze et avec seize références cochées au maximum.
Les références partagent seulement des métadonnées bornées : identités, empreintes,
nom, famille, dimensions, triangles et droits déclarés. Les chemins privés,
profils complets, sources et octets de modèles ne sont pas envoyés au fournisseur.
L’intégrité du modèle vérifiée par le natif ne devient pas une qualification
runtime ou une validation juridique.

L’ajout de modèles exige une autorisation distincte du partage des références.
Le natif résout les IDs sous la révision exacte du catalogue et vérifie les GLB
et leurs empreintes. Pour un ajout, les paquets physiques et de particules
déclarés sont également vérifiés par les transports existants, puis leurs pins
sont conservés dans les placements. Aucun chemin ni hash de modèle libre n’est
accepté dans le plan du fournisseur.

La sixième opération typée `place_asset` compile vers `PlacementEdit::Insert`.
Les IDs sont déterministes par commande et clé d’opération ; région, rotation,
échelle, nom, doublons et pins sont contrôlés. Les entrées humaines existantes,
y compris verrouillées, sont conservées. Le diff d’insertion porte sur le tableau
canonique complet, puis reste soumis à l’approbation exacte et à la transaction
commune. Un périmètre de création peut autoriser uniquement ces ajouts, sans
accorder le nom ou l’altitude de la zone. Un changement de sélection invalide
les confirmations ; un changement de zone retire les références de l’assistant.
Les outils MCP ne disposent pas encore de cette recherche ou de cette permission.

Preuves dans la campagne centrale `gate6-ia-mcp` :

- `assistant-assets-placement-domain-test-003` : quinze tests PASS, dont six
  dans `assistant_assets`, six de planning et trois de contrat fournisseur.
  Les nouveaux cas couvrent permissions, modèles non sélectionnés, région,
  refus atomiques, doublons, transaction unique et réouverture conservant les
  retouches humaines et pins physiques.
- `assistant-assets-placement-native-test-001` : deux tests PASS sur une vraie
  bibliothèque de fixture isolée : données partagées sans chemins privés,
  modèle vérifié, permission liée à la sélection native, refus de corruption,
  doublons et catalogue périmé. Le GLB de fixture n’est pas qualifié visuellement.
- `assistant-assets-placement-live-provider-001` : appel réel à
  `qwen3.8-flash-next-iq4-xs` sur le serveur local autorisé, avec
  `chat_template_kwargs.enable_thinking=true` inclus dans la requête approuvée.
  Le fournisseur propose exactement une porte à (1, 0, 2), rotation Y 90°,
  échelle 1 ; le plan est compilé, approuvé par le chemin de test de confiance,
  appliqué puis réouvert. 1 045 tokens d’entrée et 508 de sortie, test en
  23,03 s ; tête obtenue
  `70cad7e57ee70f0381960e21e386e113abd5eb409799a3aa7677edd2cf81d074`.
  Il s’agit de métadonnées synthétiques de confiance, sans vrai modèle Vault,
  sans parcours graphique et sans conservation du contenu du raisonnement.
- `assistant-assets-placement-domain-clippy-001`,
  `assistant-assets-placement-wasm-clippy-003` et
  `assistant-assets-placement-desktop-clippy-004` : PASS strict.
  L’avertissement futur Rust sur `proc-macro-error2` reste visible dans le reçu
  WASM ; il ne s’agit pas d’un avertissement du code Forge.

Les premiers essais de compilation et Clippy échoués restent conservés. Les
corrections extraient les blocs UI de préparation, de sélection et de reprise,
et la gestion clavier du canvas. Dans les travaux communs de brouillons/création,
seuls la condition de compilation, l’ordre d’une fonction et les imports ont été
ajustés pour les contrôles ; leur comportement métier est conservé. Les fichiers
communs continuent d’évoluer : ces contrôles ne figent pas un candidat graphique.

Le formulaire nouveau, son clavier, les erreurs, les références issues du Vault
réel et leur ajout visible doivent encore être qualifiés dans le Studio compilé.
La limite de 512 Mio borne les modèles vérifiés ; les paquets annexes gardent
leurs limites natives et leur budget agrégé doit encore être rapproché de celui
du job. Les créations de personnages, dialogues, quêtes et lieux nécessaires
à l’aventure de référence, le raccordement des assets au MCP, les saves pending,
la comparaison manuelle/IA et le jeu sans fournisseur restent ouverts. Aucun
nouveau critère de clôture n’est coché sur la seule base de ce lot.

`assistant-assets-placement-fmt-001` vérifie aussi le format des dix fichiers
assistant/assets concernés : PASS. Le manifeste `source-inputs-001` contient
2 141 entrées ; `source-inputs-002` constate neuf entrées changées et 2 142 fichiers
pendant les corrections et travaux UX concurrents. Son refus d’égalité est
conservé : ce n’est pas une qualification de sources inchangées.

Une compilation UX du frontend commun est observée active ; elle n’est ni
interrompue ni doublée par cette recette. Le lanceur Gate 6 utilise désormais
la destination commune `.build/frontend/studio`, le profil WASM release et
`apps/forge_studio/tauri.shared.conf.json`, sans override qui retirerait les
assets canoniques pour une conversation. La syntaxe PowerShell est vérifiée ;
ce lanceur actualisé n’a pas encore produit un nouveau candidat. Les preuves
historiques conservent leurs chemins et verdicts d’origine. La distribution
canonique et son profil local restent à préserver lors du prochain build.

## Préparation de la recette avec le Vault réel

Le plafond natif de vérification est maintenant commun aux GLB, aux paquets
physiques et aux paquets de particules : 512 Mio au total par préparation.
Une réservation refusée conserve le budget précédent ; le modèle suivant est
refusé avant lecture si sa taille dépasse le solde. Les lecteurs de paquets
gardent aussi leurs propres limites avant décodage. Un dépassement des annexes
refuse toute la préparation, sans créer de permission ni de plan partiel.
`assistant-assets-total-budget-test-001` passe trois tests : la frontière du
budget agrégé et les deux cas de bibliothèque natifs précédents. Le journal
conserve un avertissement de code non utilisé sur `DraftLedger::clear`, issu
des travaux communs de brouillons ; ce n’est pas un échec de ces tests.
`assistant-assets-total-budget-clippy-001` passe aussi strictement pour la
bibliothèque native, en 4,81 s après l’attente du cache commun.

La recette future utilise la fixture isolée
`actives/gate6-ia-mcp/gate6-assets-studio-ui-001`, créée par l’outil de données
existant, sans copie de produit. Sa tête initiale est
`61f487c41e7fbe735744ad8495a12635c95eb7f1e0077baa5f77c3044aef4158`.
Le pilote `scripts/gate6/assistant_studio_client.py` fournit au processus enfant
les réglages du serveur local avec raisonnement et le Vault canonique ; il ne
modifie aucun profil du propriétaire. Il ne génère aucun plan et ne confirme
aucun diff à la place de l’UI : après le parcours graphique, il compare les
archives réellement sauvegardées et le journal durable, exige une seule
promotion et un seul reçu canonique, et vérifie que les autres corrections
restent identiques. Sa syntaxe Python est vérifiée ; son exécution graphique
et son résultat restent à produire.

La compilation commune `ux-editeur-corrections-20261022/frontend-build-001`
reste observée en cours pendant cette préparation ; aucun second frontend
n’est construit en concurrence. La présence du processus n’est pas un verdict
de build ni de recette graphique. Ces préparatifs ne clôturent pas Gate 6.

## Rapprochement des critères avant recette étendue

Cette matrice conserve la portée complète de la roadmap. « Partiel » signifie
que la preuve citée existe sur son cas borné, sans démontrer tout le critère.
Les anciens reçus restent historiques ; ils ne qualifient pas automatiquement
les changements ultérieurs du checkout commun. La clôture exigera un candidat
actuel et un rapprochement final, y compris des contrôles transversaux.

| Critère | Preuve acquise et portée | Travail restant pour la clôture |
| --- | --- | --- |
| R6-A inspection projet | Contexte sélectionné lié au head, sans contenu non autorisé ; tests de planning et inspect MCP réel | Inspection étendue aux lieux/personnages utiles au brief, toujours gouvernée |
| R6-A recherche d’assets | Deux tests de bibliothèque native, filtrage de métadonnées et pins réels de fixture | Recherche dans le Vault réel depuis le Studio ; raccordement de la recherche au MCP |
| R6-A proposition de carte | Ajouts `place_asset` compilés et plan réel du fournisseur sur métadonnées synthétiques | Proposition et création nécessaires à l’aventure de référence depuis le produit |
| R6-A génération de plan | Fournisseur local réel, contrat JSON strict, six opérations bornées | Couvrir la création de l’aventure et les références réelles de sa carte |
| R6-A dry-run | Préparation canonique ; tests de gouvernance/hôte, revues fournisseur et MCP dans Studio sur renommage | Rejouer sur les créations étendues et les conflits du candidat final |
| R6-A approbation | Deux confirmations distinctes, hashes exacts ; recettes graphiques de renommage | Qualifier aussi la sélection d’assets et l’aventure ; invalider les confirmations obsolètes |
| R6-A application transactionnelle | Une transaction dans les recettes de renommage ; tests d’insertion préservant les placements verrouillés | Recette du Vault réel et opérations de création de l’aventure |
| R6-A undo | Tests de gouvernance, inverse et réouverture | Annuler/rétablir depuis le Studio les créations étendues sans perte de retouches |
| R6-A tests | Suites ciblées des lots, journaux et reçus préservés | Non-régressions et contrôles du candidat final correspondant aux critères complets |
| R6-A captures | Captures Tauri du fournisseur et des deux recettes MCP sur renommage | Captures des assets, erreurs/reprises et parcours d’aventure complet |
| R6-A audit | Reçus AI natifs uniques et source sauvegardée ; journaux durables | Correlation complète des jobs, diffs, promotions et retouches de l’aventure finale |
| R6-A quotas | Journaux financiers/tokens/retries testés ; limite agrégée modèles/annexes de 512 Mio | Arrêt/reprise et restitution des résultats au plafond dans la recette étendue |
| R6-A absence de shell arbitraire | Allowlist RPC/commandes typées et tests de refus ; aucun outil Apply/approval/shell MCP | Refaire l’audit sur les capacités de création et le catalogue MCP final |
| R6-P01 | Brief réel vers plan de renommage et placement ; capacités manquantes explicites | Lieux, personnages, règles et assets disponibles dans un parcours de création complet |
| R6-P02 | Commandes communes ; sélection/région et contexte testés ; UI de renommage prouvée | Régions et diffs des créations étendues, limites visibles et vérifiées dans le Studio |
| R6-P03 | Tests synthétiques porte/réplique puis variante locale ; fixture de recette disponible | Créer l’aventure de référence avec l’assistant, retoucher porte/dialogue à la main, variante locale et réouverture sans perte |
| R6-P04 | Refus de contexte/head périmés et confirmations liées aux hashes ; tests de callback différé | Recette graphique étendue de conflit, replanification et nouvelle approbation |
| R6-P05 | Staging durable/restart et perdu-ACK testés ; reprise Applied sans renvoi prouvée dans le Studio | Récupération explicite des saves pending, interruption/reprise complète dans l’UI ; propositions MCP éphémères à traiter |
| R6-P06 | Configuration fournisseur/raisonnement, données, coût et retries dans le panneau ; budget natif testé | Qualifier états et refus au plafond sur le nouveau candidat graphique, en conservant les résultats |
| R6-P07 | Temps/tokens des appels réels enregistrés ; distinction des plans synthétiques | Comparaison manuelle/IA à tâches et données comparables : temps total, corrections, coût déclaré et qualité obtenue |
| R6-P08 | Documents Forge indépendants du fournisseur ; réouvertures canoniques acquises | Édition et partie de l’aventure créée, avec le modèle inaccessible et sans dépendance au fournisseur |

L’attente de build actuelle est une attente vérifiée d’un processus vivant ;
elle ne transforme aucune de ces cases restantes en réserve acceptée ou en
critère satisfait. Aucun changement de périmètre de Gate 6 n’en est déduit.

La reconstruction suivante est lancée après la sortie effective du processus
Trunk partagé : `assistant-assets-frontend-build-001`, puis build desktop et
comparaisons des entrées conditionnés à sa réussite. Le manifeste
`assistant-assets-ui-source-inputs-002.json` contient 2 143 entrées physiques ;
dix-neuf fichiers ont changé depuis la préparation 001, notamment les travaux
UX communs. Ce manifeste devient la référence de comparaison de ce candidat,
sans attribuer aux changements récents les anciens verdicts. Le frontend est
encore en compilation lors de cette note ; aucun verdict de build ou de recette
n’en est déduit avant lecture des reçus et vérification après construction.

### Relecture des frontières de confiance du lot assets

Relecture du code courant pendant la construction, sans modification des entrées
du candidat. Elle complète les tests cités ; elle ne remplace ni la recette
graphique ni l’audit des futures capacités de création.

| Frontière relue | Constat dans le code courant | Limite de cette preuve |
| --- | --- | --- |
| Fournisseur natif | `assistant_provider_native.rs` vérifie hash approuvé, API, modèle et destination ; redirections, proxy implicite et retry HTTP désactivés ; réponse bornée à 1 Mio, connexion à 10 s et requête à 120 s | Relecture de l’adaptateur, sans qualification d’un fournisseur cloud ou de sa facturation |
| RPC MCP | `assistant_mcp.rs` n’expose que l’inspection de sélection et la proposition ; outils absents sans contexte autorisé ; noms inconnus et champs non prévus refusés ; frames et réponses bornées | La recherche d’assets et la création d’aventure ne sont pas encore des outils MCP disponibles |
| Proposition vers source | `assistant_mcp_native.rs` compile sous la portée native, prépare le vrai diff et stocke une proposition ; seule l’entrée UI `apply_confirmed` peut demander l’application ; déduplication du plan identique avant application | Inbox encore éphémère ; la reprise durable complète reste à traiter |
| Notifications Studio | `native_mcp.rs` exécute une chaîne JavaScript constante pour notifier un changement ; aucun texte du modèle n’est interpolé dans ce signal | Ne constitue pas un audit exhaustif de tous les écrans du Studio |
| Assets et plan | `search_assistant_assets` borne la requête à 4 Kio et la réserve à la fenêtre principale ; les six opérations typées de `planning.rs` ne proposent ni shell ni chemin libre ; le natif résout les pins du modèle et des annexes | Le transport de métadonnées réelles et le placement visible doivent encore être exercés dans l’UI |

Aucun nouvel outil shell, d’approbation ou d’application MCP n’a été trouvé dans
ces routes inspectées. Le verdict reste borné à ces routes et à ces six opérations.

### Échec d’outillage release et correction

`assistant-assets-frontend-build-001` échoue après le succès de compilation Rust :
Trunk ne trouve pas `wasm-opt` et le mode hors ligne interdit son téléchargement.
Le reçu FAIL et son journal sont conservés ; aucune optimisation n’est retirée
pour contourner cet échec.

L’outil existe déjà sous `.build/tools/binaryen-131-001/bin/wasm-opt.exe` sur E:.
Son exécution `--version` confirme 131 ; empreinte SHA-256 locale
`a3e857e565f18773e24cb3d81d00baa70cee9be24efe775e1960565fea5e65b3`,
licence Apache 2.0 conservée dans ce répertoire commun.
Le lanceur vérifie cette empreinte, ajoute le dossier au PATH de son processus
uniquement, enregistre le pin dans le reçu de build et restaure le PATH en sortie.
L’optimisation `data-wasm-opt="z"` et le mode hors ligne sont conservés.

Le manifeste `assistant-assets-ui-source-inputs-005.json` ouvre une nouvelle
passe `assistant-assets-frontend-build-002`, avec les comparaisons 006/007 et le
build desktop 002 conditionnés à sa réussite. Onze entrées ont changé depuis
le manifeste 002, dont le lanceur réparé et les retouches UX communes ; les
contrôles ultérieurs doivent utiliser ce nouveau point de départ. Cette passe
est en cours, sans recette graphique ni verdict de clôture attribué à ce stade.

La version installée est Trunk 0.21.14. Son
[implémentation `cargo_build`](https://github.com/trunk-rs/trunk/blob/v0.21.14/src/pipelines/rust/mod.rs#L378)
lance une première compilation, puis une autre invocation avec sortie JSON pour
recueillir les noms des artefacts. Les deux processus successifs observés ne
proviennent donc pas d’une relance du script par l’agent. Cette constatation
explique la seconde invocation, sans établir à elle seule pourquoi Cargo doit
recompiler le module dans cette passe. Le build 002 reste suivi sur sa même
session ; sa première compilation Rust passe en 8 min 57 s, mais la préparation
du frontend complet n’est pas encore terminée lors de cette note.

Le build frontend 002 réussit finalement avec `wasm-opt` actif et 3 000 fichiers
forge-imports plus 11 fichiers forge-kit authentifiés. La comparaison 006 refuse
cependant le candidat comme snapshot inchangé : `authoring_controls.rs`,
`authoring_gameplay/editor.rs`, `authoring_gameplay/editor/draft.rs`,
`editor_draft.rs` et `workspace_controls/create.rs` ont changé pendant le build.
Le script s’arrête donc avant le desktop 002 ; le PASS frontend et le refus de
comparaison restent distincts et conservés.

Une nouvelle référence est capturée dans
`assistant-assets-ui-source-inputs-008.json`, 2 143 fichiers. La construction
`assistant-assets-frontend-build-003` réutilise les caches communs ; les
comparaisons 009/010 et le desktop 003 sont conditionnés à sa réussite et à
l’absence de changement. Cette passe reste en cours. La fixture isolée et le
pilote graphique préparés sont conservés pour la recette suivante.

## Récupération native des enregistrements de jobs interrompus

Le frontend 003 s’arrête à son tour sur `E0425` dans `creator_controls.rs` :
`profile_rule_send` n’existe pas. À l’inspection suivante, la correction vers
`profile_send` est déjà présente dans le checkout ; aucune réécriture de ce
travail commun n’est faite par ce lot. Les checks WASM
`assistant-assets-ui-wasm-check-002` et `003` passent ensuite sur le checkout
actualisé. Ces checks de typage ne qualifient pas un nouveau frontend optimisé.

Le travail reprend aussi un critère encore ouvert, au niveau du stockage natif :
`assistant_job_native/recovery.rs` ajoute l’inspection de l’enregistrement pending
et sa récupération explicite. Le code réutilise `DocumentStore::recover` ; avant
promotion ou conservation de l’ancienne version, les deux contenus sont archivés
sous leurs empreintes. Les chemins restent construits depuis la racine native
autorisée et l’ID typé du job, avec les contrôles de répertoire et de liens existants.

La vue de récupération est une donnée de présentation, sans autorité. La décision
relit les deux versions et exige leurs hashes exacts. Le journal candidat doit
être valide, appartenir au même job et prolonger l’historique enregistré d’au
plus un événement, avec les mêmes métadonnées de requête, contexte et plafonds.
Sans version enregistrée, seule la préparation initiale sans événement peut
être promue. Un candidat invalide peut être archivé en conservant une version
enregistrée valide ; aucune version invalide n’est promue automatiquement.

La promotion ne lance aucun appel fournisseur et n’applique aucune mutation du
projet. Un Started récupéré garde sa tentative et sa réservation incertaine :
il ne redevient pas un Prepared renvoyable. Les snapshots de journal ne prouvent
pas à eux seuls la présence d’un reçu canonique de source ; cette réconciliation
reste de la responsabilité du coordinateur.

Preuves centrales :

- `assistant-pending-recovery-native-test-002` : huit tests PASS, dont les deux
  cas de stockage précédents et six nouveaux cas couvrant récupération exacte,
  hashes périmés, saut d’historique, métadonnées étrangères, contenu invalide,
  préparation initiale et refus de rollback d’un Started durable. Les tests
  vérifient également les octets archivés, la réouverture et le refus d’un renvoi.
- `assistant-pending-recovery-native-clippy-002` : PASS strict de la bibliothèque
  native et de cette suite ; les premières passes 001 restent conservées.
- `assistant-assets-ui-wasm-check-003` : PASS du frontend typé actuel ; la réserve
  future Rust sur `proc-macro-error2` reste visible.

Cette récupération n’est pas encore exposée dans le protocole du panneau ni
dans son parcours graphique. Il reste à raccorder le coordinateur, invalider
ses anciens diffs et confirmations, afficher les versions et décisions utiles,
et exercer la reprise depuis le Studio. La frontière entre application en mémoire
et sauvegarde de source doit également être couverte lors des essais d’arrêt.
R6-P05 reste donc partiel ; aucune case de clôture n’est cochée pour ce seul lot.

## Raccordement de la récupération et perte avant sauvegarde de source

La récupération est maintenant exposée par les requêtes typées
`inspect_recoveries` et `recover_job`. La découverte parcourt des pages de
32 identités, sans parcours récursif ni suivi de liens. Une demande active ne
peut pas être récupérée. La décision exige les hashes des deux versions ; elle
retire ensuite l’entrée chargée du coordinateur afin de perdre l’ancien diff,
son approbation et son contexte. La reprise passe à nouveau par Prepare, avec
vérification du brief, du fournisseur, du contexte et des sources actuelles.
Le protocole refuse un chemin de stockage ou des drapeaux de validation fournis
par l’appelant. L’IPC ne crée aucun outil MCP d’approbation ou d’application.

Le panneau distingue les versions enregistrée et interrompue, affiche leurs
états et résumés, exige une relecture explicite et propose seulement les choix
autorisés par le stockage natif. Les identités et détails complets restent dans
Diagnostics. Le brief local est conservé avant la première création native,
pour pouvoir reprendre aussi une préparation dont la sauvegarde initiale a été
interrompue. Les confirmations de partage et d’application ne sont pas stockées.
Un journal récupéré n’applique rien au projet et ne déclenche aucun appel HTTP.

Un nouveau test de redémarrage complet a reproduit un défaut distinct : après
Apply sans Save puis perte du processus, le journal était Applied tandis que
la source sauvegardée restait à son état initial. La preuve d’échec
`assistant-recovery-unsaved-regression-001` est conservée. Le journal peut
désormais ajouter un événement `source_application_lost` lorsque le coordinateur
ne retrouve aucun reçu canonique et vérifie la requête du contexte initial exact.
L’événement conserve la réponse, les tentatives, les coûts et l’ancien accusé
dans l’historique, puis remet seulement la proposition en Staged. Il ne réécrit
ni l’historique ni les sources. L’ancien diff est abandonné ; une nouvelle
relecture et approbation sont nécessaires. L’interface explique que la retouche
précédente n’a pas été retrouvée dans la carte sauvegardée.

La source avec reçu déjà sauvegardé est réconciliée sans nouvelle application.
Si des corrections humaines sont intervenues depuis l’état initial, l’ancien
contexte ne peut pas être repris : sources et octets du journal restent intacts.
Un rejeu modifié visant un autre hash d’application est également refusé.

Preuves centrales déjà terminées :

- `assistant-recovery-ui-native-check-001` et
  `assistant-recovery-ui-wasm-check-001` : PASS de typage du raccordement initial.
- `assistant-recovery-coordinator-test-001` : dix tests de coordinateur et huit
  de stockage PASS, avant l’ajout du cas de perte avant sauvegarde.
- `assistant-recovery-journal-test-001` : cinq tests de domaine PASS, dont la
  conservation du résultat, du coût et de l’historique après perte de source.
- `assistant-recovery-coordinator-test-003` : douze tests de coordinateur et
  huit de stockage PASS, couvrant notamment perte avant Save, réouverture,
  nouvelle approbation et refus devant une correction humaine ultérieure.
- `assistant-recovery-coordinator-test-004` : treize tests de coordinateur et
  huit de stockage PASS après extraction du helper et ajout du cas où le
  coordinateur conserve l’ancien diff pendant la réouverture de la source.
- `assistant-recovery-native-clippy-003` et
  `assistant-recovery-domain-clippy-001` : PASS strict du lot natif final et du
  journal de domaine, tests compris.
- `assistant-recovery-wasm-clippy-001` : PASS strict du panneau et de la
  bibliothèque WASM ; avertissement futur Rust de `proc-macro-error2` conservé.
- `assistant-recovery-ui-source-inputs-001.json` : manifeste intermédiaire de
  2 147 entrées physiques, 279 465 697 octets ; le test supplémentaire du
  coordinateur resté chargé a été ajouté ensuite, ce manifeste n’est pas celui
  d’un candidat graphique final.
- `assistant-recovery-ui-source-inputs-002.json` : état final de ce lot,
  2 147 entrées et 279 467 008 octets ; formatage ciblé vérifié. Aucun candidat
  graphique n’est qualifié par cet inventaire.

La première passe Clippy native du raccordement a détecté une fonction trop
longue ; la reprise d’un job déjà chargé est extraite dans le module de
récupération, sans suppression de contrôle. Les échecs intermédiaires restent
conservés. Le contrôle UX source du panneau n’a émis aucun avertissement nouveau
dans `assistant_ui/recovery.rs` ; deux textes longs de l’aide fournisseur
préexistante restent à apprécier visuellement, sans suppression automatique.

Le nouveau panneau n’a pas encore été exécuté dans un candidat graphique à
entrées stables. Ces résultats ne ferment pas R6-P05, ne qualifient pas la
recherche réelle du Vault, la création de l’aventure, la variante sans perte ou
la comparaison manuelle/IA. Gate 6 reste ouverte.

## Création additive de contenu RPG

La capacité `add_content` complète les six opérations précédentes. Elle n’est
proposée que si l’auteur autorise explicitement `create_content` dans la portée.
L’autorisation est désactivée par défaut, réinitialisée au changement de carte
et incluse dans les données préparées. Son changement invalide les confirmations
de partage et d’application. Le même choix entre dans la portée MCP ; les outils
externes ne peuvent toujours ni approuver ni appliquer.

La proposition utilise les types canoniques des personnages, objets,
inventaires, lieux, dialogues, quêtes et définitions narratives. Elle ne reçoit
aucun joueur, profil de règles, identité de module, script ou chemin à remplacer.
Le compilateur ajoute les nouvelles entrées à une copie des sources puis passe
par `prepare_gameplay` et la transaction commune. Les données existantes restent
identiques. Les nouvelles positions et patrouilles sont bornées à la région.
Une validation isolée des ajouts interdit leurs liens vers des définitions
existantes non partagées ; le joueur partagé reste identifiable. La validation
complète rejette ensuite doublons d’identités, références ou graphes invalides,
capacités manquantes et dépassements de quotas. Les surfaces masquées doivent
être réactivées par l’auteur dans le Creator ; le plan ne peut pas les activer.

Les ajouts narratifs à solutions multiples exigent le Creator déjà configuré.
Le plan ne migre pas silencieusement un profil ni n’élargit ses capacités. Le
contexte partagé contient les règles effectives et les capacités/surfaces
disponibles, sans noms de personnages, répliques ou autres contenus non choisis.
Le schéma strict commun fournisseur/MCP décrit les nouvelles définitions et leurs
conditions/effects typés. Les requêtes sans création gardent leurs instructions
et leur schéma précédents ; aucune nouvelle donnée de création n’est sérialisée
dans leur contexte.

Preuves centrales terminées au stade initial de ce lot :

- `assistant-content-domain-test-001` : 25 tests PASS, dont cinq nouveaux tests
  de création, six de placement, six de planification, cinq de journal et trois
  de contrat fournisseur. La fixture synthétique ajoute village, forêt,
  intérieur, personnages, porte, dialogues et quête à deux solutions ; les deux
  chemins sont exercés, récompense et sauvegarde/reprise contrôlées. L’archive
  de source se rouvre et Undo restaure les corrections précédentes, dont une
  porte et une réplique humaines préexistantes. Cette fixture n’est pas la
  recette produit de l’aventure de référence.
- `assistant-content-domain-check-002`, `assistant-content-wasm-check-001` et
  `assistant-content-native-check-001` : PASS de typage du domaine, du panneau
  WASM et des cibles natives.
- `assistant-content-domain-clippy-002` : PASS strict du domaine et des tests de
  création/planification après correction des diagnostics du schéma.

Le contrôle UX source du panneau ne signale aucun nouvel avertissement ; les
deux aides fournisseur longues déjà relevées restent conservées. Aucun contrôle
source ne vaut recette graphique. L’essai fournisseur réel
`assistant-content-live-provider-001`, raisonnement demandé et une seule
tentative autorisée, est lancé sur des métadonnées synthétiques avec journal
durable sous `assistant-content-live-001`. Son verdict doit être ajouté après
la fin effective du processus. Le lot reste non qualifié graphiquement.

Les deux essais réels de création complète sont désormais terminés et refusés :
`assistant-content-live-provider-001` expire à 120,42 s, sans résultat reçu ;
`assistant-content-live-provider-002` reçoit HTTP 502 à 218,37 s. Le second essai
utilise le même modèle et le raisonnement explicite, avec un délai client étendu
mais borné à 480 s pour son budget de 8 192 tokens. Il ne s’agit donc pas d’un
second timeout client. Les répertoires `assistant-content-live-001` et `002`
conservent chacun leur Started, sans Staged ni Applied. Aucun résultat ni source
appliquée n’est inventé pour ces essais. Le second conserve également la requête
exacte synthétique dans `outbound.json`, sans clé ni contenu de raisonnement.
Un GET `/v1/models` réussit ensuite avec le modèle attendu : cela confirme
l’accès au catalogue, pas la réussite d’une génération.

Le transport borne maintenant son délai d’appel selon le plafond de sortie :
120 s jusqu’à 2 048 tokens, puis des tranches de 120 s, maximum 480 s. Le délai
effectif est affiché dans le panneau. Connexion bornée à 10 s, absence de retry
HTTP, contrôle de destination et de taille restent en vigueur. Cette adaptation
n’autorise aucune reprise automatique d’un Started incertain.

Un défaut du contrôle de budget est également corrigé : le parseur transport
ne jette plus une réponse valide et bornée parce que son compteur déclaré de
sortie dépasse le plafond. Le journal durable conserve cette réponse et la
marque BudgetExceeded ; le coordinateur bloque Review, Apply et Begin. La preuve
`assistant-content-budget-native-test-001` passe avec quatorze tests de
coordinateur, huit de stockage, six de MCP et cinq de contrôleur MCP. Le nouveau
test traverse le parseur de réponse puis vérifie conservation après réouverture,
absence de mutation et refus d’application ou de renvoi. Les réponses tronquées,
incomplètes, refusées, sans compteurs ou hors contexte restent rejetées.

`assistant-content-native-clippy-003` et
`assistant-content-wasm-clippy-001` passent sur leurs états intermédiaires ;
les vérifications finales doivent couvrir les retouches de budget et les
libellés de diff ajoutés ensuite. Un petit appel de diagnostic avec le même
schéma de contenu est séparé de la recette complète ; sa réussite éventuelle
ne qualifierait ni une aventure complète ni son accessibilité dans le Studio.

Le diagnostic `assistant-content-small-probe-001.json` réussit : HTTP 200,
21,37 s, 3 552 tokens d’entrée et 625 de sortie déclarés. Le même schéma strict
de création, avec raisonnement actif et champ de raisonnement présent, produit
un seul lieu conforme au petit brief. Il n’y a aucune application de cette
sonde dans un projet. Ce résultat confirme la compatibilité du schéma pour un
ajout court ; il ne démontre pas la cause du HTTP 502 sur le brief complet.

`assistant-content-source-inputs-001.json` inventorie ensuite 2 150 entrées
physiques, 279 516 170 octets ; formatage ciblé et contrôle de whitespace des
rapports passent. Le Clippy final de domaine
`assistant-content-domain-clippy-003` est encore en attente du verrou du cache
commun lors de cette note. L’inventaire des processus confirme plusieurs Cargo
et rustc concurrents ; aucun de ces processus n’est arrêté. Le contrôle en
attente n’est ni relancé ni compté PASS. Les versions antérieures de Clippy et
les tests terminés conservent leur portée exacte.

## Diagnostic de génération longue et commande de quête concise

Le contrôle en attente `assistant-content-domain-clippy-003` se termine PASS.
Les contrôles finaux `assistant-content-native-clippy-004` (bibliothèque,
exécutable et tests natifs concernés) et `assistant-content-wasm-clippy-002`
passent également avant l’ajout de la commande concise ci-dessous.

Le diagnostic `assistant-content-stream-probe-001.json` reprend le brief complet
du second essai, avec `stream=true`, `include_usage=true` et raisonnement actif.
Le serveur ouvre une réponse HTTP 200 de type text/event-stream mais termine
à 216,229 s sur un événement d’erreur, sans événement de complétion, texte,
compteur ou marqueur DONE reçu. Aucun raisonnement n’est enregistré. Cela ne
prouve pas l’origine interne du défaut ; un transport en flux n’est pas ajouté
au produit sur la base de cet échec. Le format de diagnostic suit la
[référence officielle des événements Chat Completions](https://developers.openai.com/api/reference/resources/chat/subresources/completions/streaming-events).

La commande typée `create_fetch_quest` réduit ensuite la quantité de graphes et
d’identités que le modèle doit écrire. Elle décrit une quête de récupération,
ses personnages et textes, son objectif, récompense, journal, éventuelle porte
et jusqu’à quinze approches alternatives de témoignage ou de combat. Aucun
nom de carte, de personnage ou contenu de référence n’est encodé dans le moteur.
Le compilateur de plan attribue les identités à partir du job et des clés,
puis produit les mêmes définitions canoniques éditables de dialogue, conditions,
effets, variables et solutions. Il ne crée ni nouveau format de source ni
runtime spécifique. Les règles et le joueur existants restent identiques.

La commande n’est offerte que pour une portée autorisant la création et un
Creator déjà configuré. Son expansion passe par les contrôles de ContentAddition
et `prepare_gameplay` : régions, surfaces, capacités, références, budget et
transaction atomique restent exigés. Les clés, quantités, doublons et le nombre
d’approches sont bornés. Une nouvelle approbation du diff complet est nécessaire.
Les ajouts bruts `add_content` restent disponibles, notamment pour les lieux.

`assistant-quest-command-domain-test-002` passe : huit tests de contenu, six de
planification et trois de fournisseur, un test externe explicitement ignoré.
Une déclaration synthétique de 931 octets produit 4 616 octets de définitions
canoniques ; collecte et témoignage sont joués, sauvegardés et repris. Le cas
combat utilise les dégâts et contacts confirmés du moteur métier existant ; il
ne qualifie pas une observation physique native. Les sources réouvertes gardent
profil, configuration et joueur. `assistant-quest-command-domain-clippy-003`
et `assistant-quest-command-native-clippy-001` passent strictement. Les premières
passes refusées sur rédaction de doc et longueur de fonction sont conservées ;
la retouche de dialogue est extraite sans suppression de ses contrôles.

L’essai réel `assistant-content-live-provider-003` conserve encore un refus
HTTP 502 après 236,10 s. Il demandait la même aventure complète avec deux
opérations, une quête concise et les trois lieux liés, sur 8 192 tokens de
sortie maximum et raisonnement actif. Son journal et sa requête sont conservés
sous `assistant-content-live-003`, sans Staged ni Applied. L’essai suivant 004
compare le même brief avec 2 048 tokens de sortie ; ses exigences de contenu
ne sont pas réduites et son résultat reste à constater. Aucune réussite de la
création complète n’est déduite des tests synthétiques ou des petits diagnostics.

## Création réelle réussie avec budget de raisonnement accru

L’essai 004 se termine HTTP 502 après 54,04 s. Le diagnostic séparé
`assistant-content-error-classification-001.json`, limité à 128 tokens, reçoit
HTTP 502 après 3,727 s avec le code serveur `structured_output_failed` et les
indications `length` et `incomplete`. Cela établit un épuisement du plafond pour
cette sonde ; le corps brut et le raisonnement ne sont pas conservés. Le message
produit identifie désormais ce cas borné et propose d’augmenter le plafond, sans
refléter de texte arbitraire du serveur.

Le plafond autorisé monte à 16 384 tokens. Il entre dans le hash de la requête et
la réservation du journal : une ancienne approbation ne peut pas autoriser le
budget accru. Le délai transport suit ce plafond par tranches, de 120 à 960 s,
sans retry automatique. Cette évolution remplace la borne intermédiaire de
480 s décrite plus haut. Les limites de connexion et de taille restent actives.
`assistant-reasoning-budget-domain-test-001` passe : huit tests de contenu, six
de journal et trois de fournisseur, avec le parcours externe ignoré.
`assistant-reasoning-error-native-test-001` passe ses deux tests de diagnostic,
dont l’absence de réflexion de données privées dans les erreurs.

**`assistant-content-live-provider-005` réussit en 325,1985 s**, sur le serveur
local fourni, modèle `qwen3.8-flash-next-iq4-xs`, raisonnement explicitement
demandé et une seule tentative. Compteurs déclarés : 4 561 tokens d’entrée et
12 776 de sortie. Le résultat comporte exactement une création de quête et un
ajout des trois lieux liés. Le journal atteint Applied après validation du plan,
revue, approbation exacte et transaction canonique. La source sauvegardée se
rouvre avec le même profil, joueur et Creator. Les données restent sous
`assistant-content-live-005`, dont `outbound.json` et `source.archive.json`.
Empreinte source :
`19ca18dda40dd6ffcdb54705d6d3485f931741e7bfef72267490bb4d93eaa9d8` ;
empreinte de revue :
`cf704a23061087247a8246d26ab1680b5c9387046a4478fc6320d252a790e87e`.

`assistant-content-live-playback-001` rejoue ensuite **la sortie réelle sauvegardée**,
sans nouvel appel fournisseur. Les solutions collecte et témoignage passent
chacune en trois actions ; porte ouverte, récompense unique, sauvegarde/reprise
et replay du reçu sont vérifiés. Les saves font 3 067 et 3 161 octets. Cela
qualifie le parcours métier de cette sortie réelle sur fixture synthétique,
pas une aventure complète observée dans Studio ou un contact physique natif.
Les lieux nommés ne prouvent pas à eux seuls les environnements visuels exigés.

## Propositions MCP durables sans approbation persistée

Le natif conserve désormais les propositions MCP sous la racine d’auteur
configurée, dans `assistant-mcp-proposals/<contexte>/<proposition>/authoring.json`.
Les chemins ne viennent ni du client ni d’un argument UI. Les enregistrements
bornés contiennent le plan, ses empreintes et sa date, jamais une permission ou
une approbation. La reprise exige une autorisation explicite du même contexte,
recompile les propositions et prépare de nouvelles revues. Un nonce natif par
autorisation lie le hash d’approbation à cette reprise ; un ancien hash est
refusé même si la proposition est identique. L’identité de commande reste stable
pour éviter un doublon lors d’un renvoi.

La reprise de pending vérifie les empreintes avant promotion. Une corruption,
un lien, un dépassement ou un échec de stockage n’autorise aucune application.
Le panneau explique cette conservation et garde les erreurs détaillées dans
Diagnostics, sans les effacer lors du rafraîchissement automatique du statut.
`assistant-mcp-durable-test-001` passe : six tests de transport et neuf de
contrôleur, couvrant redémarrage, nouvelle approbation, renvoi identique,
corruption, échec d’écriture et reprise pending. Les contrôles stricts
`assistant-mcp-durable-native-clippy-002` et
`assistant-mcp-durable-wasm-clippy-001` passent. La recette graphique de cette
reprise et les autres critères de clôture restent à exécuter.

Le périmètre MCP utilise maintenant aussi la sélection d’assets du panneau.
Preview et Authorize résolvent à nouveau cette sélection dans la bibliothèque
native : révision, modèle et éventuels paquets sont vérifiés avant de construire
le contexte. Les données reçues du client ne peuvent pas fabriquer un catalogue
vérifié. Le catalogue borné et son autorisation de placement font partie du hash
du contexte ; modifier le droit de placement invalide l’ancienne confirmation.
La reprise durable conserve ce contexte exact et exige toujours une nouvelle
approbation. L’application utilise les commandes et contrôles natifs existants.

`assistant-mcp-assets-native-test-003` passe le parcours de bibliothèque de test :
vérification obligatoire, absence de chemin privé partagé, staging sans mutation,
reprise avec identité stable et nouveau hash, refus de l’ancien hash et d’un
catalogue corrompu. Les deux premiers essais refusés de fixture sont conservés.
Après factorisation des helpers, `assistant-mcp-assets-final-test-001` passe
**226 tests**, quatre explicitement ignorés : 211 unitaires Studio, six de
transport et neuf de contrôleur MCP. `assistant-mcp-assets-native-clippy-005`
passe toutes les cibles Studio ; `assistant-mcp-assets-wasm-clippy-001` passe
le panneau WASM. Les refus intermédiaires de Clippy restent historiques. Le
contrôle UX source des deux panneaux retrouve seulement les deux aides longues
déjà relevées ; il ne constitue pas une observation graphique.

Le manifeste `assistant-mcp-content-source-inputs-001.json` capture ensuite
2 153 entrées physiques pour 279 581 252 octets. La compilation frontend commune
`assistant-content-frontend-001` est lancée sur cet état. La nouvelle recette
UI prépare seulement le Creator d’une carte de test, sans aventure préfabriquée ;
le pilote observe les journaux et sources sauvegardés, sans appeler les actions
d’approbation, le fournisseur ou les commandes de mutation à la place du Studio.
Le build et le parcours graphique ne sont pas comptés PASS avant leur fin.

`assistant-content-project-regression-001` passe ensuite les 58 suites projet :
339 tests réussis, aucun échec, deux tests externes explicitement ignorés. Cela
couvre les commandes, archives, régénérations, undo et workspace, en plus des
tests d’assistant. La fixture Creator est préparée par
`assistant-content-studio-fixture-002`, après correction d’une conversion
d’erreur dans l’exemple ; son premier refus de compilation est conservé.
`assistant-content-fixture-clippy-001` passe. Son head initial est
`d66d411446602c2c49f7c82837889d37e1ad01e99f0b999d0b2822d330c33ebe`.

Les inventaires 002 à 004 tracent précisément les changements pendant le build :
uniquement l’exemple de préparation de fixture puis le pilote d’observation de
recette, sans modification d’une entrée produit du frontend. Ils ne portent
pas un verdict global « source inchangée ». Le pilote accepte aussi des
placements du catalogue dans le plan de création et vérifie leur correspondance
avec les ajouts sauvegardés. Il est exercé hors fournisseur sur l’archive réelle
005 : deux acteurs, deux objets, deux dialogues, trois lieux, deux items et une
quête ajoutés, deux solutions, aucun placement dans cette sortie historique.
La recette Studio doit encore produire ses propres preuves.

À la demande de vérification d’activité du propriétaire, le test réel 006 est
relancé avec le même modèle et un plafond de 16 384 tokens. Une connexion TCP
Established vers `192.168.1.41:8080` est constatée dès **17:37:54 heure locale**,
processus 21952. Le fichier outbound confirme le modèle exact et
`chat_template_kwargs.enable_thinking=true`. L’essai
`assistant-content-live-provider-006` réussit en **231,7098 s**, 4 561 tokens
d’entrée et 9 129 de sortie déclarés. Il produit à nouveau les deux opérations
complètes, puis une source revue, appliquée et réouverte. Empreinte source :
`7b2d540c924118760183e4c48b5a6b48ba7e80b5d26bab6adbcaadb9d4280e7c`.
Il s’agit toujours de données synthétiques et non d’une recette Studio.
Les phases de compilation et de tests sans fournisseur laissent effectivement
le serveur au repos ; l’assistant Codex de ce chat n’est pas exécuté sur Qwen.

Le frontend `assistant-content-frontend-001` se termine avec exit 0 à
15:36:55 UTC, après 16 min 55 s, avec 3 000 fichiers forge-imports et 11
forge-kit authentifiés. `assistant-content-desktop-001` passe à son tour ; le
binaire réel est `E:/OpenRPG_Forge/.build/cargo-target/debug/forge-studio.exe`
(le message de Tauri affiche un nom de package avec underscore qui n’existe
pas comme fichier). Les inventaires 005 et 006 ne signalent aucun changement
depuis 004. Le profil et l’exécutable canoniques du propriétaire restent en place.
La recette `assistant-content-studio-client-001` est maintenant lancée sur la
fixture isolée, avec sessions de jeu distinctes et réutilisation du contenu
immuable configuré. Son verdict reste à constater dans l’interface.

Dans ce Studio reconstruit, la recherche du Vault retourne le modèle converti
`Arbre nu ramifié — SM_MountainTree_04`, distinct de sa source non plaçable.
Une seule référence est cochée, puis son droit de placement séparé est autorisé.
Le contexte affiché et relu confirme `model_verified=true`,
`runtime_qualified=false`, le modèle
`04471efb33cad71c33d9e1ddc101e07fb6e7c8f12008e30adf80222eb5fded65`,
la licence `LicenseRef-Unity-Asset-Store-EULA`, 6 316 triangles et les dimensions
mesurées. Cela vérifie la référence disponible, sans accepter son rendu final.

Le brief métier en français demande une quête à deux approches, trois lieux
liés et un exemplaire de cet arbre en (5,0,5), échelle 0,25. La région affichée
est X/Z [-10,10], Y [-4096,4096]. Le partage contient les règles et le joueur
existants, sans la porte ni la réplique humaines hors sélection. Les opérations
autorisées sont uniquement `add_content`, `create_fetch_quest` et `place_asset`.
Le contexte porte le hash
`9c0905b1e4dbb76ce4cdf8437f655b7e669445ce9cf59bfaaea6cc74022fdaeb`.

Après ouverture et revue des données exactes, consentement puis bouton d’envoi,
le processus **Studio 21200** établit sa propre connexion vers
`192.168.1.41:8080` à **17:58:49 heure locale**. Le job
`9509159d-1f38-5184-8d0d-eaf315f54df9` conserve Started, tentative 1. Raisonnement
explicitement demandé, plafond 16 384 tokens et délai 960 s ; aucun nouvel appel
n’est lancé pendant cette attente. La capture de revue
`assistant-content-outbound-ui-001.png` porte le SHA-256
`6c1ab3d7c0ef1b75a5132b7abe1c77b5962255347d220974c830c37925cbab72`.
`assistant-content-running-ui-001.png` conserve aussi le panneau pendant l’appel.
Le compteur du dernier snapshot reste à zéro dans le panneau jusqu’au retour,
alors que son statut d’appel et le journal natif sont en cours ; cette limite
d’affichage doit être rapprochée du critère R6-P06.

L’automatisation rencontre un redimensionnement de fenêtre et une capture
secondaire de favicon 16×16. Les actions suivantes utilisent explicitement la
capture complète 2560×1392 ; la capture de revue conservée est celle du Studio.
Aucun résultat de génération, application, sauvegarde ou environnement visuel
complet n’est déduit à ce stade de l’envoi réussi.

La tentative de cette recette UI se termine par un **refus de validation** :
`UUID parsing failed: invalid character: found v at 0 at line 1 column 1690`.
Le plan reçu contient donc au moins une identité non UUID ; son contenu brut et
le raisonnement ne sont pas conservés. Aucune proposition exploitable n’atteint
Staged. Le journal conserve Started puis Interrupted, tentative 1, réservation
maintenue et aucun retry automatique. Après fermeture par « Enregistrer et
quitter », la source reste exactement au head initial
`d66d411446602c2c49f7c82837889d37e1ad01e99f0b999d0b2822d330c33ebe`.
Le pilote termine exit 1, car aucune création n’a été appliquée ni sauvegardée ;
ce résultat n’est pas transformé en PASS. La capture
`assistant-content-refused-ui-001.png` montre le refus. La durée de cette réponse
UI n’étant pas instrumentée précisément, aucun temps exact n’est revendiqué.

Le schéma de création emploie désormais une définition UUID partagée pour les
identités et références, avec motif hexadécimal canonique 8-4-4-4-12. Il ne suffit
plus d’une chaîne quelconque telle qu’un slug. La validation métier reste exigée
pour l’unicité, les références, les règles et la région. Les contextes sans
création gardent leur schéma antérieur. Un appel réel 007 est lancé avec le
nouveau schéma ; sa fin doit être constatée avant tout verdict.

Le panneau est aussi corrigé sur les constats de recette : le bouton annonce
l’appel en cours avec `aria-busy`, le dernier compteur Prepared n’est plus
présenté comme le compteur courant pendant l’attente, et les refus de génération
ont un message métier avec détails dans Diagnostics. Ces retouches doivent
encore être compilées et observées dans le nouveau candidat.
`assistant-content-id-schema-domain-test-001` passe ses 17 tests, avec un test
externe ignoré. La Gate 6 reste ouverte, dont le parcours complet et R6-P06.

`assistant-content-id-schema-native-clippy-001` passe toutes les cibles projet
et Studio ; `assistant-content-id-schema-wasm-clippy-001` passe aussi. Le contrôle
UX source conserve seulement les deux aides longues déjà recensées. Le manifeste
007 identifie exactement les corrections du schéma et du panneau, trois fichiers
produit, 2 153 entrées pour 279 582 837 octets.

**`assistant-content-live-provider-007` passe en 355,7965 s**, avec le schéma UUID
renforcé, le même modèle local et le raisonnement demandé. Compteurs déclarés :
4 866 tokens d’entrée et 13 015 de sortie. Les identités des trois lieux et leurs
liens sont maintenant des UUID valides ; les deux opérations de la fixture sont
validées puis appliquées et la source réouverte. Empreinte source :
`8e32897f4be645cce055483f613071c6529509617c465bbb6eda830a1474306b` ;
revue : `eadf8ee4b7519690c4c1c773195fdda978fa8704b87428e9636b01beee7b9863`.
Cette réussite fournisseur n’efface pas le refus de la recette UI précédente.

La reconstruction `assistant-content-frontend-002` reste en cours. Le suivi du
candidat attend son reçu terminal, puis ne lance `assistant-content-desktop-002`
que si la comparaison 008 des entrées depuis 007 est inchangée ; une comparaison
009 suit le packaging. Aucun verdict de ce pipeline ou nouvelle recette graphique
n’est anticipé. Le lancement et le profil du propriétaire sont conservés.

### Contre-épreuve UUID et lieux à clés locales

La sonde contradictoire `assistant-content-uuid-grammar-probe-001.json` envoie
le motif UUID exact à ce même serveur, avec un brief demandant la chaîne
`village`. HTTP 200 en 19,544 s, 217 tokens d’entrée et 625 de sortie déclarés,
raisonnement demandé et champ présent : le serveur renvoie pourtant `village`.
Le motif n’est donc pas une garantie de contrainte effective sur ce serveur.
La réussite 007 demeure valide pour son résultat, sans qualifier l’application
générale du schéma. Le validateur natif conserve son refus des identités invalides.

Le frontend 002 et son suivi sont arrêtés volontairement avant packaging,
uniquement sur leurs processus vérifiés, pour intégrer la correction métier.
Le journal partiel et `assistant-content-frontend-002.cancelled.json` sont
conservés ; aucun reçu PASS ou desktop 002 n’est fabriqué.

La nouvelle commande `create_places` porte des noms, positions et clés locales
ASCII distinctes, avec des liaisons limitées aux clés du même ajout (1 à 32 lieux,
32 liaisons distinctes par lieu). Forge produit les identités stables depuis
l’identité de commande et la clé, puis applique les validations canoniques,
de région, capacités et surfaces existantes. Elle ne fournit aucune autorité
supplémentaire et reste proposée seulement dans un contexte de création autorisé
avec la surface et la capacité Lieux. Les sources restent des définitions de lieux
éditables ordinaires. Le schéma et les consignes fournisseur présentent cette
commande ; `add_content` reste disponible pour les autres ajouts contrôlés.

`assistant-content-keyed-places-test-001` passe : 9 tests, 1 externe ignoré.
Les références stables et réciproques, la préservation du contenu existant et les
refus de clés dupliquées, chemins, références cachées, positions hors portée et
tailles excessives sont vérifiés. La compilation, l’essai Qwen en langage naturel
et la nouvelle recette Studio restent à constater. Le premier lancement du
test fournisseur (`assistant-content-provider-008`) échoue avant tout appel,
sur un nom de package incorrect ; il est conservé distinct du lancement corrigé
009. Gate 6 reste ouverte.

Le lancement corrigé 009, données `assistant-content-live-008`, passe sur le
serveur local en **152,6142 s** : 5 103 tokens d’entrée et 4 966 de sortie déclarés,
raisonnement activé. Le brief français ne cite aucun nom interne d’opération.
Le modèle propose `create_fetch_quest` et `create_places`, sans capacité manquante ;
la transaction est validée, approuvée dans le test, appliquée et réouverte.
Head : `1e1f0f8916b554d31d76495ca741ffd5ae605568001b4708bff74db5c2cf11a8` ;
revue : `0aedb795c48d148d84720607ba0538fa872a0cf84e304a6628d54c5a1b4b53bc`.
La connexion du processus de test 38156 à `192.168.1.41:8080` est observée
établie à 18:36:07 heure locale. Le payload conservé confirme le modèle exact,
`enable_thinking=true`, 16 384 tokens et `stream=false`. Le raisonnement lui-même
n’est pas conservé. Ce succès concerne le fournisseur réel sur métadonnées
synthétiques, sans qualification graphique ou runtime natif implicite.

Les contrôles `assistant-content-keyed-places-native-clippy-001` et
`assistant-content-keyed-places-wasm-clippy-001` passent. Le manifeste 010 fixe
2 153 entrées, 279 589 603 octets, avant le frontend 003 commun en cours.

`assistant-content-keyed-places-playback-001` passe sans appel fournisseur :
chaque solution prend trois actions métier, donne une récompense unique et
ouvre la porte ; sauvegarde, reprise et rejeu identique sont vérifiés. Les
observations de ligne de vue sont synthétiques et ne qualifient pas le jeu natif.
Le suivi 003 attend le reçu frontend, compare les sources au manifeste 010
avant packaging, puis les compare de nouveau après desktop. Une dérive ou un
échec interdit ce candidat ; aucun résultat de ce suivi n’est anticipé ici.

`assistant-content-keyed-places-regression-001` termine avec code 0 : **706 tests
PASS, zéro échec, 10 ignorés, 78 suites**, pour les bibliothèques et intégrations
`forge_project` et `forge_studio`. Les tests ignorés ne deviennent pas des preuves
acquises. Cette passe inclut la nouvelle opération mais ne remplace pas la recette
graphique en attente du frontend 003.

### Candidat 003 et seconde recette de création dans Studio

Le frontend 003 termine code 0 (16:37:44–16:55:14 UTC, compilation Rust
13 min 52 s puis liaisons/optimisation WASM et staging canonique vérifié).
Le desktop 003 termine code 0, compilation 34,53 s. Les manifestes 011 puis 012
retrouvent exactement les 2 153 entrées et 279 589 603 octets du manifeste 010,
avant et après packaging. Le binaire commun réellement lancé est
`.build/cargo-target/debug/forge-studio.exe`, SHA-256
`d40386897c161abb8608529fde0c4180e23e3eab66eea4f304f8b2e62e691c64`.
Le chemin avec underscore annoncé par Tauri n’est pas utilisé comme preuve.

La seconde recette reprend les mêmes données isolées, toujours au head initial
`d66d411446602c2c49f7c82837889d37e1ad01e99f0b999d0b2822d330c33ebe`.
Le brouillon mémorisé conserve sa demande et sa sélection, sans autorité d’envoi.
La préparation refuse sa révision de bibliothèque périmée. L’ancienne référence
est désélectionnée puis recherchée explicitement dans le catalogue actuel ;
le même arbre converti est choisi, empreinte modèle inchangée, nouvelle révision
`28aa6ea73e18e6db233bda05642e7b06dc212fff8f459f8cb0e303f18aef9c39`.
Une nouvelle préparation crée le job
`e85f4b1d-7af8-5046-b50f-2dd9490b4d62`, sans réutiliser une approbation antérieure.

La revue UI exacte confirme : `create_fetch_quest`, `create_places`, `add_content`
et `place_asset` disponibles ; aucun objet ou dialogue existant partagé, région
X/Z ±10 m, propriétés générales non autorisées, un seul modèle vérifié et
placement autorisé. Hash de contexte :
`3a7cec5f56681c629e764e89e2a285da388c86869a90592899527c81237c353f`.
L’autorisation d’envoi puis la génération sont actionnées dans le Studio à
17:01:50,363 UTC (19:01:50 locale). La connexion du processus Studio 30152 vers
`192.168.1.41:8080` est observée établie ; journal natif Started, tentative 1.
Le modèle exact, `enable_thinking=true` et le plafond 16 384 tokens sont relus
dans les données proposées à l’envoi. Aucun résultat n’est encore déduit.

`assistant-content-running-ui-002.png` montre les nouveaux libellés d’attente :
appel du modèle en cours, compteurs actualisés au retour, carte éditable.
SHA-256 : `25c4a1225777bdb7ecdc3bc073ad20ab46d20d286edcf040ee62fd697a03c391`.
La capture de confirmation `assistant-content-review-ui-002.png` est aussi
conservée. La fenêtre et le profil du propriétaire sont préservés.

La génération atteint Staged, une seule tentative : **5 615 tokens d’entrée,
10 643 de sortie déclarés**. L’observation UI montre encore l’appel en cours à
17:06:32,247 UTC et le résultat à 17:07:08,997 UTC, soit 318,634 s après le clic.
Ces observations bornent l’attente affichée ; elles ne constituent pas une
mesure instrumentée de la durée HTTP exacte. Aucun raisonnement brut conservé.

La revue native expose huit rubriques : acteurs, dialogues, inventaire,
variables/solutions, objets RPG, lieux/liaisons, quête et placement. Les valeurs
Avant/Après affichées confirment le joueur, la porte et le dialogue préexistants
identiques. Deux acteurs, deux items, une quête à deux solutions, deux nouveaux
objets, deux dialogues, trois lieux réciproquement reliés et un arbre sont
ajoutés. Les deux solutions ont une condition active et ouvrent la même nouvelle
porte ; l’objet preuve est consommé uniquement par sa solution, récompense unique.
Le placement reste en (5,0,5), rotation identité, échelle 0,25 et empreinte du
modèle choisi. La preuve est à 4 m du départ : le jeu natif doit vérifier le
déplacement, elle n’est pas considérée ramassable depuis la position initiale.

Après lecture, approbation de ce diff puis application et Sauvegarder sont
actionnés dans Studio. `assistant-content-studio-client-002.result.json`
termine code 0 après fermeture « Enregistrer et quitter ». Il vérifie une seule
transaction IA canonique, un seul Staged et un seul Applied, les règles et tout
contenu initial préservés. Head sauvegardé :
`922d497125b2135d1801fac8f333192f0258c2201356ae5976c4107fc24c46ae`.
La source est vérifiée à 17:09:06,754 UTC et la fermeture à 17:10:03,817 UTC.

Les captures `assistant-content-staged-ui-002.png`,
`assistant-content-diff-ui-002.png` et `assistant-content-applied-ui-002.png`
sont conservées. La dernière montre le véritable modèle chargé et cadré dans
le viewport, pas seulement sa vignette. Ce parcours prouve création et sauvegarde
depuis le produit ; il ne suffit pas encore pour les retouches/variante, la
réouverture, le jeu, la comparaison manuelle ou la clôture complète.

Le refus de référence périmée reste sûr mais son texte technique ne guide pas
l’auteur. Une correction native locale le remplace par une invitation à relancer
la recherche et rechoisir les références. Elle ne change ni validation ni portée.
Ce module est exclu du frontend WASM par `cfg(not(target_arch = "wasm32"))` ; le
frontend 003 peut être réutilisé, avec un nouveau packaging natif et manifeste.
La vérification de cette correction dans le nouveau candidat reste due.

La correction du message passe le test natif de pins/corruption/catalogue périmé
et Clippy strict toutes cibles Studio. Le manifeste 013 diffère de 012 uniquement
sur `assistant_assets_native.rs`, exclu de WASM. Le desktop 004 réutilise donc le
frontend 003 et termine code 0 en 35,64 s ; le manifeste 014 est identique à 013
(2 153 entrées, 279 589 982 octets). SHA-256 du binaire natif 004 :
`c68ffb0fb93fde7b5baaf34770c5c574c9a8d1e00dbffb718ea94276c37db6f2`.
La réouverture réelle de la carte sauvegardée montre l’arbre conservé.

### Retouches humaines puis variante sélectionnée

Dans ce nouveau processus Studio (35644), l’auteur sélectionne la Porte du refuge,
change X de 4 à 4,5 m, examine puis applique avec le bus manuel commun. Il choisit
ensuite le Gardien et remplace sa réplique par : « Le refuge attend ta décision :
rapporte la preuve de la forêt ou écoute le témoin, puis reviens me voir. »
Cette seconde retouche est aussi examinée, appliquée puis sauvegardée.
`assistant-content-human-dialogue-ui-001.png` conserve le champ édité.

`assistant-content-human-retouches-001.failed.json` conserve le refus du premier
contrôle qui attendait exactement deux chemins modifiés : le helper manuel
`editor::edit` trie aussi les capacités requises via BTreeSet. L’inspection du
code et le reçu 002 vérifient une permutation sans ajout, retrait ni doublon de
capacité. Hormis cet ordre sérialisé, seuls la position X de cette porte et le
texte de cette réplique diffèrent ; tout autre contenu et toutes les règles
restent identiques. Les deux nouveaux reçus portent l’acteur humain. Le head
sauvegardé en révision 5 est
`b198736ad058ed3d60b8a8dfe3f4073ad0299e68f8ffcb8a0827652d8cbfed22`.
Le contrôle strict initial n’est pas réécrit en réussite.

L’arbre seul est ensuite sélectionné. Une nouvelle demande autorise son déplacement,
sans création, propriété générale, objet RPG, réplique ou référence de bibliothèque
supplémentaire, dans une région X/Y/Z ±10 m. La revue exacte montre uniquement
`move_placement` et exclut la réplique humaine. Brief : déplacer l’arbre de
(5,0,5) à (6,0,5), conserver ses autres propriétés et tout le module. Contexte :
`1f6fcf47f3802706654c38986b1def2970d525984fff9310c2bc099917b13a06` ;
job `5cf4cde6-289b-56a0-aaf0-db7605b7a112`. Raisonnement demandé sur le même
modèle local. L’envoi est actionné dans le Studio à 17:27:47,851 UTC. Aucun
résultat, conservation après variante ou nouvelle réouverture n’est anticipé.

La variante atteint Staged à la première tentative : 992 tokens d’entrée et
358 de sortie déclarés. Elle est observée à 17:28:28,770 UTC, 40,919 s après le
clic ; ce délai d’observation ne prétend pas mesurer exactement le temps HTTP.
Le diff natif affiche un seul changement : Position du modèle sélectionné · X,
Avant 5, Après 6. Il est approuvé puis appliqué et sauvegardé via Studio.

`assistant-content-local-variant-001.json` vérifie le head en révision 6 :
`438b61b0b62ab23fdea87edba5a8c7e3344324111fe4aebe2b0dbf3c68600ade`.
Seul `/placements/0/transform/translation_m/x` diffère du head humain précédent.
**Tout le gameplay est exactement identique**, y compris la porte déplacée,
la réplique humaine, les capacités dans leur ordre courant, les règles, les
quêtes, solutions et liens. Les reçus précédents restent identiques et un seul
reçu IA est ajouté. Le journal contient exactement Started, Staged, Applied.
`assistant-content-variant-diff-ui-001.png` conserve le diff relu.

Après « Enregistrer et quitter », un autre processus Studio (4000) rouvre la
carte. Le Gardien sélectionné affiche la réplique humaine exacte ; la Porte du
refuge sélectionnée affiche X=4,5, Z=4. Les observations sont conservées dans
`assistant-content-reopen-ui-001.json` et sa capture PNG, sans modification de
source pendant cette relecture. Le head du workspace reste celui de la variante,
identique à l’empreinte ci-dessus. Cette séquence établit la préservation des
retouches après variante réelle et réouverture dans le produit. Le jeu natif
des deux branches, la reprise hors fournisseur et la comparaison au parcours
manuel restent à qualifier avant clôture.

### Premier lancement natif hors fournisseur : refus d’export du modèle Vault

L’instance de recette est relancée avec `http://127.0.0.1:9/v1`, vérifié
inaccessible par une sonde GET sans données de projet ; le serveur Qwen du
propriétaire n’est pas arrêté ni modifié. La configuration et les empreintes
Studio/jeu sont dans `assistant-content-offline-ui-001.launch.json`.
Le bouton Lancer le jeu refuse l’export avant tout processus de jeu :
`No preserved imported asset ... with content pin ...`, pour l’arbre choisi.
`assistant-content-game-refused-001.json` et sa capture conservent ce résultat,
sans qualification native. Le rendu Studio savait lire le Vault, mais l’export
ne résolvait que le magasin des imports préservés : une liaison produit manquait.

La correction native complète les seuls pins non résolus des imports depuis le
Vault en lecture seule, y compris leur version historique vérifiée. Les imports
utilisateur et les sources d’auteur ne sont pas implicitement modifiés. Les
modèles sont validés avec le validateur média existant et capturés, avec leur
preuve de licence et métadonnées, dans le catalogue gouverné du package local.
Le total reste borné à 512 Mio ; les données déjà importées gardent leurs droits
et leur lignée. Cette voie supplémentaire refuse l’export public, sans inférer
de droit de redistribution. L’auteur absent reste explicitement non renseigné,
et la date de capture de la copie est distinguée d’une date d’achat inconnue.
La décision est consignée dans ADR-0035 avant qualification de cette correction.

`assistant-vault-export-native-check-001` passe. Le test 001 est refusé avant
exécution car le Studio de recette ouvert verrouillait le binaire ; il est
ensuite fermé proprement, sans fermer le Studio du propriétaire. Le test 002
détecte un JSON GLB non aligné dans une ancienne fixture de transport. La fixture
est corrigée ; aucun validateur n’est assoupli. Le test 003 passe : **213 tests
bibliothèque, 4 ignorés, puis 10 tests d’export des imports**, zéro échec.
Le contrôle `assistant-vault-export-native-clippy-001` passe. La version historique
et le modèle réel doivent encore être constatés par leurs contrôles dédiés,
puis le nouveau packaging et la reprise du vrai jeu restent nécessaires.


### Export privé Vault réel et lancement natif hors fournisseur

Les contrôles finaux natif/WASM Clippy passent. Le test historique passe ; le
contrôle réel `assistant-vault-export-live-001.json` exporte en mémoire le pin
exact de l’arbre : 9 419 256 octets de modèle, 9 428 788 octets au total, deux
fichiers, catalogue local valide, export public refusé et aucune écriture Vault.
Le build `assistant-content-desktop-005` passe en 1 min 04 s ; les manifestes
015/016 sont identiques (2 154 entrées, 279 612 697 octets). Le frontend 003 est
réutilisé : les sept fichiers modifiés sont natifs uniquement. SHA-256 du Studio :
`11a232a545d0764c0d94bfcbf9a040317baf112eb0a79feb483aaf1a539f9eb1`.

L’instance hors fournisseur 002 (PID 18524) lance depuis le bouton du Studio un
vrai jeu (PID 30144) sur le head `438b61b0...`. Le package contient le modèle et
sa preuve de licence. Le rapport de session
`gate6-content-studio-ui-001/native-game-sessions/module-game-1792692893591798500-18524/module-runtime-report.json`
constate la projection du modèle exacte, un primitive rendue/enregistrée, le
rendu prêt (46 pipelines, zéro échec) et le gameplay prêt. La partie ne passe
cependant pas : F5 refuse l’appui instable, aucune quête n’est terminée, aucun
save n’est écrit. `assistant-content-game-unstable-001.png` garde le refus.

La reproduction `native-game-sessions/input-diagnostic-001` sur le même package
immuable montre les clics reçus et le focus actif, mais une capsule bloquée à
(0,0,0), 20 collisions et une translation effective nulle malgré la gravité.
La fixture d’origine avait placé le centre du joueur dans le terrain. Le
formulaire normal initialise ce centre à 0,85 m au-dessus du terrain. La fixture
future est corrigée ; les données et reçus historiques ne sont pas réécrits.
Dans le Studio, la recette existante est retouchée, examinée, appliquée puis
sauvegardée : seul `/gameplay/actors/0/position/y` change de 0 à 0,85 (f32).
`assistant-content-player-spawn-retouch-001.json` prouve le nouveau head
`a8902b1b371e3aa839f633e0368a0cb4b773602ef1c9874731acca3cb6e33ae9`, tous les
anciens reçus et toutes les autres données identiques. Ce correctif de donnée
initiale est une intervention humaine supplémentaire, à compter dans la comparaison.
Les deux branches, la sauvegarde/reprise et la qualité finale restent à qualifier.

### Vérification explicite de présence du fournisseur

À la demande du propriétaire, `assistant-local-presence-001.json` constate un
nouvel appel au serveur Qwen exact à 18:12:52 UTC : HTTP 200 en 14,973 s,
106 tokens d’entrée et 417 de sortie, raisonnement explicitement activé et
reçu (1 180 caractères comptés puis écartés). Cette sonde courte ne remplace
pas les preuves de création et de variante dans le produit. Les phases de
compilation et de jeu hors fournisseur n’appellent pas le modèle.


### Solution du témoin et reprise native hors fournisseur

Après correction UI du centre du joueur, F5 sauvegarde sur le sol et les deux
PNJ sont accessibles. Le dialogue du Gardien présente exactement la réplique
humaine. Trois clics dans le panneau G acceptent la quête, écoutent le témoin
puis valident cette solution. La vraie sauvegarde est conservée dans
`assistant-content-native-witness-save-001.json` ; son vérificateur
`assistant-content-native-witness-001.json` constate : quête terminée par la
solution du témoin, Insigne quantité 1, porte du refuge ouverte, ancienne porte
fermée, preuve non consommée, trois reçus causaux. Le SHA-256 de sauvegarde est
`b0e8d75202dcfb30cf95df0496a04062cd73f2fd98acd51444fd7a2f4984af4a`, son état
`d0b9612ff765caf871b35f43a8f8cfa60913ec68ca6096fa9d354455256ea752`.

F9 demande la reprise ; Quitter ferme le processus 18812. Lancer le jeu depuis
Studio ouvre un nouveau processus 7076 : le HUD indique Sauvegarde reprise,
quête terminée et Insigne ×1. Les métadonnées de sauvegarde sont exactement
identiques dans les deux rapports, séquence 3, sans erreur de commande,
simulation ou reprise. `assistant-content-native-witness-resume-001.json` et
les deux captures UI conservent cette preuve. Le fournisseur demeure configuré
sur le port local inaccessible 9 ; aucun appel modèle pendant ces parties.
Le parcours collecte, la qualité visuelle du parcours de référence et les autres
critères restent ouverts. Le check de la fixture corrigée attend le verrou
Cargo occupé par une conversion du propriétaire, qui reste préservée.


### Collecte et reprise par commandes natives avec marche KCC

`native-collection-replay-001` réutilise le package exact exporté par Studio et
le binaire commun, sans copie de code ni modification des sources d’auteur.
Le scénario natif épinglé emploie les commandes normales et le contrôleur KCC ;
il ne pilote pas les clics de l’interface et ne téléporte pas le joueur. Les
14 étapes passent dans le processus 34472 : acceptation, marche vers (1;1,6),
ramassage réel à portée/visibilité revalidées, retour, validation par le Gardien,
capture, sauvegarde et demande de reprise. Le déplacement maximal observé est
1,68795 m, translations physiques non nulles et identité du backend stable.

`verification.json` vérifie les trois reçus causaux, la solution de collecte,
la preuve consommée, Insigne quantité 1, la porte du refuge ouverte et l’ancienne
porte fermée. La sauvegarde épinglée est
`be673df6689602695c9972a1eedb691bd2638c2dd3ad38791863f421220f70d1`, état
`e6c7ca11ba7de32d0bab173550d3e5594d8d11720bd06c6e20f2647f634b6eea`.
Le second processus 12232 passe les cinq étapes de reprise. Son événement de
boot contient **exactement** l’état sauvegardé ; séquence 3, une récompense,
aucun rejeu de commande et aucune erreur. Les captures de fin et de reprise sont
inspectées : HUD terminé, Insigne ×1 et réplique humaine visibles. Ce parcours
reste une preuve par scénario natif, distincte des clics du parcours témoin.

Les deux branches jouables et leurs reprises sont établies hors fournisseur sur
cette recette après correction de la donnée initiale. Le périmètre demeure une
scène de recette minimaliste : trois lieux nommés et un arbre ne prouvent pas
encore un village, une forêt et un intérieur exploitables au sens du scénario
canonique. R6-P03 et R6-P07 restent donc ouverts, ainsi que les qualifications
UI des conflits, reprises/quota et MCP durable étendu puis l’audit final.


### Préparation du scénario canonique au-delà de la recette minimale

Le check `assistant-fixture-grounded-check-001` passe après attente du verrou
Cargo partagé (6 min 58 s au total), sans interrompre la conversion concurrente.
Le manifeste 017 ne diffère du 016 que par la fixture corrigée ; le code produit
et les binaires qualifiés ne changent pas. La différence attendue fait sortir
le comparateur en code 1 ; elle est explicitement identifiée, pas ignorée.

La recherche dans le vrai panneau Assistant retrouve deux candidats convertis
`sm-housebuilding-001-a`, puis l’arbre déjà testé. Une maison et l’arbre sont
cochés, avec droit de placement séparé ; la région X/Z est bornée à ±24 m.
Aucune préparation, transmission ou mutation supplémentaire n’est effectuée
par cette seule sélection. Les métadonnées de trois packs de bâtiments sont
lues sans scan récursif ni lecture de modèles et épinglées dans
`reference-building-candidates-001.json`. Les trois maisons Viking sont des
fragments source-local convertis, métadonnées prêtes, runtime non qualifié,
licence EULA : leur nom ne prouve ni un assemblage complet ni un intérieur
praticable. Il faut inspecter le rendu et qualifier les collisions/accès, ou
assembler les éléments réellement disponibles, avant de retenir ces candidats.


### Environnement réellement proposé par Qwen et insuffisance du candidat maison

Le Studio de recette est relancé avec Qwen local et raisonnement activé. Le job
`2bb9d279-da81-56c1-be8b-855829c852ec` est préparé via l’UI : deux modèles vérifiés
par le natif, seule opération `place_asset`, région X/Z ±24 m, aucun objet,
dialogue ou placement existant partagé. Le clic d’envoi est à 18:52:44,097 UTC ;
la connexion établie du PID Studio 18592 vers 192.168.1.41:8080 est constatée à
18:52:50,300. Le panneau affiche l’appel en cours à 18:53:31 ; le staging est
constaté à 18:54:02. Ces observations bornent le délai, sans inventer un temps
HTTP exact. Une tentative, 1 590 tokens d’entrée et 1 834 de sortie déclarés.
Les tarifs locaux déclarés sont nuls ; électricité et coût matériel non mesurés.
`assistant-reference-environment-send-001.json` et la capture de fonctionnement
conservent cette preuve. Le raisonnement brut n’est jamais conservé.

Le plan propose exactement trois maisons candidates et cinq arbres selon le
brief, avec réserves explicites sur les maisons. Le diff natif est relu,
approuvé, appliqué puis sauvegardé dans le Studio. La vérification
`assistant-reference-environment-applied-001.json` prouve huit ajouts exacts,
ancien arbre et tous les reçus conservés, **tout le gameplay strictement
identique** (dont retouches humaines), et un seul nouveau reçu IA. Le head est
`c57a30c16ba1a2da131539ca217feee0efaf222c6aa6af124801695353f14b58`.

Le cadrage du Refuge révèle une ossature ajourée ; sa capture
`assistant-reference-house-candidate-partial-ui-001.png` est conservée. Ce modèle
source-local ne constitue pas une maison complète ou un intérieur accepté.
L’inspection du jeu générique révèle en outre que les bâtiments paramétriques
canoniques ne sont pas projetés, contrairement au Studio. Le raccord réutilise
`BuildingProjection` existant, sans nouvelle source de vérité ; ADR-0035 décrit
la barrière physique initiale et les nouvelles observations de bâtiment.
`assistant-module-buildings-check-001` passe en 3 min 42 s. Les tests physiques
`assistant-module-buildings-test-001` sont lancés ; aucune réussite de tests,
build du jeu, passage d’intérieur ou clôture n’est encore déduite de ce check.

L’UI actuelle présente l’ajout de placements comme une longue paire de tableaux
JSON sous « Donnée du projet ». Ce résultat est vérifiable, mais trop lourd à
relire pour l’auteur ; améliorer sa présentation en conservant l’accès au diff
exact fait partie des corrections restantes avant qualification finale.

### Pièce canonique, contrat natif et indisponibilité ultérieure de Qwen

Les deux tests physiques `assistant-module-buildings-test-001` passent : huit
éléments statiques enregistrés avant libération du joueur, conservation d’une
pause de récupération ultérieure et détection d’un collider manquant. Les cinq
tests `assistant-module-buildings-delivery-test-001` passent, dont le refus d’un
nombre, d’un hash, d’éléments, de registrations ou d’une readiness incorrects.
Le build `assistant-module-buildings-game-build-001` passe en 1 min 06 s ; SHA-256
du jeu commun : `265d8c053af3de8e6f5f36f82d01c8f173293e9be09ff6b3b8bbd66ab02793b9`.
Ces résultats ne prouvent pas encore un passage réel de porte dans cet intérieur.

Le panneau Construire ordinaire prépare, applique puis sauvegarde une pièce
8 × 6 × 3 m en (4,5 ; 0 ; 7), passage face à Z=4, sans fournisseur.
`assistant-reference-manual-room-001.json` vérifie que seule la collection
canonique des bâtiments change ; gameplay, neuf placements, retouches et anciens
reçus restent exactement identiques. Un reçu natif est ajouté avec le compte
d’auteur configuré `karen` ; les clics de recette sont pilotés par Codex.
Head révision 9 : `bc0571dde0927718b19ff752e678e5cb4fb1689a001edd91383c87e12b8c991f`.
L’annulation examinée/appliquée/sauvegardée retrouve exactement le head
`c57a30c…`, puis le rétablissement examiné/appliqué/sauvegardé retrouve exactement
`bc0571dd…`, sans fournisseur. Le journal
`assistant-reference-undo-redo-room-001.jsonl` épingle ces deux états persistés,
respectivement aux révisions 13 et 14 du workspace, sans duplication de pièce.
Le lancement depuis Studio refuse avant export le GLB de maison candidat :
attributs non reconnus et extension requise `OPENRPG_source_fragment` non prise
en charge par cette voie. Le refus reste un échec conservé ; ni retrait de
l’extension ni qualification artificielle du candidat. Le bilan ouvert masquait
son motif au profit de « aucun bilan » ; une correction conserve désormais le
motif dans les détails du refus. Sa validation graphique reste à effectuer.

Le diff des placements reçoit un résumé par ajout : noms, positions et échelles,
modèle/orientation et JSON exact accessibles dans les détails. Ce résumé ne
s’applique que si chaque ancienne valeur JSON est exactement conservée ; sinon
le diff complet reste affiché. Les trois tests de présentation passent, dont
suppression, modification, doublon, champ inconnu et changement numérique caché
par l’arrondi f32. Le contrôle UX relève seulement deux aides de configuration
préexistantes longues, conservées parce qu’elles expliquent où placer la
configuration et comment protéger les clés. Le frontend partagé 004 est lancé ;
son succès et sa recette visuelle ne sont pas encore établis ici.

À 19:23:46 UTC, une nouvelle sonde directe avec raisonnement demandé ne peut
établir la connexion à Qwen ; elle échoue après 21,08 s (WinError 10060).
`assistant-local-presence-002.json` conserve cet échec, sans raisonnement reçu.
Les contrôles TCP et `/v1/models` sans proxy échouent également à 19:24 puis à
19:26 UTC ; `assistant-local-reachability-003.json` conserve le dernier contrôle.
L’appel Studio réussi de 18:52–18:54 UTC reste attesté par ses propres reçus ;
il ne prouve pas une disponibilité permanente. Le serveur est actuellement
injoignable depuis le poste. Une question propriétaire demande s’il est toujours
démarré à cette adresse ; les travaux hors modèle continuent. Gate 6 reste ouverte.

Le premier Clippy strict des bâtiments refuse la fonction de vérification devenue
trop longue (108/100 lignes). La vérification des bâtiments est extraite dans
un helper dédié, sans désactivation de règle ; `assistant-module-buildings-clippy-002`
passe en 10,86 s. Les cinq tests du contrat passent de nouveau dans
`assistant-module-buildings-delivery-test-002`. Le build du jeu cité plus haut
précède cette extraction ; son hash reste historique jusqu’au rebuild suivant.
Le manifeste d’entrées 019 épingle 2 155 fichiers / 279 630 341 octets avant
l’extraction ; il ne doit pas être présenté comme un manifeste final de clôture.

### Builds courants, retrait du lot incompatible et récupération graphique

Le frontend partagé 004 passe, optimisation et hooks canoniques inclus ; le log
indique 3 000 fichiers d’import et 11 de kit vérifiés par SHA-256. Le jeu commun
est reconstruit dans `assistant-module-buildings-game-build-002` (2 min), hash
`1b7fe2ed5e6fecb4f709bb1cd244eb04bbc3fe706641ed6de627c54a3518e20e`.
Les builds natifs Studio 006 puis 007 passent. Le 007 porte
`ae973ff8035e02f5e3fc7e67846fd1023b0e64ec17b405141b159e288ce305ad`.
Les manifestes 020/021 sont identiques : 2 155 fichiers, 279 630 518 octets.
Leur seul changement depuis 019 est l’extraction du contrat de bâtiments.

La non-régression 002 est refusée avant tests par Windows : tentative de remplacer
le Studio de recette encore ouvert. Cette erreur est conservée, l’instance est
fermée normalement et la 003 passe : **713 tests, aucun échec, 11 ignorés sur 78
suites**. `assistant-common-regression-003.summary.json` liste chaque test ignoré ;
les essais fournisseur, Vault privé et données réelles restent des qualifications
séparées. Clippy WASM `assistant-placement-wasm-clippy-001` passe en 1 min 35 s.
Le warning futur de dépendance `proc-macro-error2 2.0.1` reste distinct du verdict.

Dans Studio, les deux annulations examinées, appliquées et sauvegardées retirent
d’abord la pièce puis le lot candidat IA entier. Le second retour retrouve
**exactement** `a8902b1b…`, un arbre d’origine, zéro bâtiment et toutes les retouches
RPG conservées, workspace révision 16. L’historique et les reçus de l’essai refusé
restent conservés ; les maisons ne sont pas qualifiées par leur présence passée.
Leur remplacement par des bâtiments natifs et la forêt manuelle restent à faire.

`gate6-recovery-studio-ui-001` est une copie de données isolée. Sa faute contrôlée
emploie les préfixes exacts du vrai journal Qwen historique : Started enregistré,
Started/Staged en pending, source contenant déjà le reçu accepté. La petite
archive smoke de quatre états ne contenait pas le blob initial encore référencé
par l’historique du workspace. Le premier lancement refuse ce fichier absent.
La recette est fermée puis le blob exact est rétabli depuis l’original avec son
SHA-256 ; le ZIP et les journaux historiques restent inchangés. Cette correction
de fixture est tracée dans `assistant-recovery-fixture-repair-001.json`.

Le lancement 002 ouvre correctement la copie. L’UI montre les deux versions,
le résultat Qwen et une seule tentative ; récupérer/conserver restent bloqués
avant la case de lecture. Après confirmation et récupération, le pending devient
**exactement** la version enregistrée et les deux versions sont archivées avec
leurs hashes. Workspace et archives de zone sont inchangés. Reprendre le brief
historique et préparer rapproche ensuite le reçu existant sans envoi ni Apply :
Started/Staged/Applied, une tentative, un reçu. Le journal final est identique
octet pour octet au journal historique d’origine. Les captures et
`assistant-recovery-ui-002.result.json` établissent ce parcours, y compris après
Enregistrer et quitter. Cette injection de faute ne constitue pas un nouvel
appel fournisseur réel.

Le parcours révèle un plafond affiché issu de la configuration actuelle
(16 384 tokens) au lieu du job historique (2 048). Le natif fournit désormais
les limites persistées du journal et leur délai associé lorsqu’il présente un
job chargé ; la configuration seule conserve les plafonds d’un futur job.
Les 15 tests de coordinateur et 8 tests de journaux natifs passent, dont reprise
Staged sous configuration modifiée et accusé perdu avec budget de sortie modifié.
Clippy strict `assistant-persisted-budget-clippy-001` passe en 13,29 s.
Le manifeste 022 identifie exactement quatre fichiers de ce correctif ; le
build natif 008 est lancé, sa recette graphique reste à faire. Le format des
documents et le frontend 004 ne changent pas.

`assistant-quota-fixture-001.json` prépare aussi un rejeu contrôlé du même résultat
de 718 tokens sous plafond d’entrée 128, avec pending ; il ne prétend pas avoir
appelé Qwen sous ce plafond. L’état avant la dernière commande réelle du workspace
est dérivé de son historique et vérifié contre le hash de base de cette commande.
Cette future recette graphique n’est pas encore un PASS. Le village/forêt/intérieur,
la comparaison complète, les conflits et le MCP durable étendus, l’audit et
l’archivage final restent ouverts ; Gate 6 n’est pas clôturée.

### Plafond historique corrigé et arrêt au quota exercés dans Studio

Le build natif 008 passe en 34,90 s, hash
`7a9667d5ae19e24feb357a02948cb3fd5099d10ba4d4fb6356cac05f021a498b`.
Les manifestes 022/023 sont identiques : 2 155 fichiers, 279 632 899 octets.
La recette `assistant-recovery-ui-003` reprend le même job déjà appliqué, sans
appel fournisseur. Studio affiche maintenant les **2 048 tokens et 120 s du job
historique**, malgré les 16 384 tokens de la configuration de lancement. Journal
et workspace restent identiques, une tentative, pas de nouvelle application.
Capture, arbre et `assistant-recovery-ui-003.result.json` établissent ce correctif.
Le contrôle direct `assistant-local-reachability-004.json` échoue encore à
20:02:55 UTC, avant toute génération.

La recette `assistant-quota-ui-001` ouvre l’état historique antérieur à la
proposition. L’UI compare Started et le pending BudgetExceeded, avec résultat
conservé ; Tab et Espace atteignent et cochent la lecture des versions.
Après récupération, le brief et la sélection exacts sont préparés ; le natif
accepte le hash de requête historique `b3062690…`. Le panneau montre une seule
tentative, 718/237 tokens et « Plafond dépassé ; résultat conservé, application
et reprises bloquées ». Même après avoir coché le partage, Générer reste grisé ;
Relire est grisé et aucune application n’est autorisée. Le résultat est conservé,
aucun nouvel événement Started/Applied, zéro reçu de ce job dans la source active.
Toutes les données et archives de source sont inchangées avant fermeture.
`assistant-quota-ui-001.result.json` et les captures distinguent explicitement ce
rejeu contrôlé d’un nouvel appel réel à Qwen et de son budget historique d’origine.

Après Enregistrer et quitter, le premier contrôle d’égalité de bytes du workspace
échoue. Le diagnostic conserve cet échec : la fixture Python avait trié ses clés
JSON, et Save les remet dans l’ordre du sérialiseur natif. Le document complet est
**strictement identique après parsing**, et le flux est exactement celui attendu
dans l’ordre natif ; tous les blobs de zone restent identiques octet pour octet.
Les deux archives de récupération sont intégralement vérifiées par SHA-256, le
résultat au plafond reste conservé, sans tentative ni application supplémentaire.
Le JSON de fixture avant Save et `assistant-quota-ui-001.closed.json` conservent
les deux hashes et cette distinction ; aucune preuve historique n’est réécrite.

### Environnement retouché sans fournisseur et accès Qwen à 22 h 46

Les reçus `assistant-reference-manual-buildings-001.json` et
`assistant-reference-manual-forest-001.json` établissent les opérations ordinaires
dans Studio : trois salles natives nommées Refuge, Maison est et Maison ouest,
puis déplacement et agrandissement de l'arbre existant et cinq copies. Les trois
salles mesurent 8 × 6 × 3 m ; le Refuge en (4,5 ; 0 ; 7) aligne son passage avec
la porte retouchée. Les six arbres partagent le même asset et son hash épinglé.
Les données de gameplay, la porte et le dialogue retouchés, ainsi que les reçus
antérieurs, restent strictement préservés. La source enregistrée devient
`51a5a5257c7e6917ee231a0a8532252e7edcc25a52a9f80846f27f6958826aaa`,
archive `81ffd7d763362f0e2f53bccf98d2e3ea4a89d6d0493d7fc93516c7dbd38b55a0`,
révision de zone 19 et de workspace 28.

Ces phases ne font **aucun appel fournisseur**. Leurs durées observées sont
262,714 s pour les salles et 534,563 s pour les arbres ; elles incluent les
inspections et la préparation par l'opérateur Codex. Elles ne constituent ni une
étude de vitesse humaine ni une comparaison contrôlée de l'aventure entière.
La projection de cet environnement complet dans le jeu, son intérieur et sa
reprise restent à qualifier ; ces reçus ne clôturent pas Gate 6.

Après le signalement du propriétaire, le contrôle direct
`assistant-local-presence-005.json` tente `/v1/models` sans proxy à
20:45:41,763 UTC (22:45:41 à Paris). La connexion expire après 21,058 s avec
WinError 10060. **Aucune génération n'est envoyée**. Le script prévoit le modèle
`qwen3.8-flash-next-iq4-xs`, `enable_thinking=true` et un délai de lecture de
960 s pour la génération, mais n'atteint pas cette étape. Ce défaut de connexion
ne prouve ni un refus du modèle ni un dépassement du temps de génération.
Le chat Codex, les builds et les retouches ordinaires ci-dessus ne sollicitent
pas Qwen ; aucune activité fournisseur nouvelle n'est revendiquée.

### Parcours complet jusqu'au refuge et reprise native — première recette

Depuis la source `51a5a525…`, le bouton Lancer le jeu de Studio crée l'export
`module-38072-1792702034179086400/module.orpmod`, SHA-256
`b030567478408aa654074dfdf68159276861594dd25eb3322ce96dc64252ca3c`.
Le processus 32312 démarre sur la bonne source, sans reprendre implicitement
l'ancienne sauvegarde d'une source différente, et quitte normalement. Sa
projection est prête ; les captures de départ et du Studio sont conservées.

`native-reference-interior-001` utilise ensuite cet export exact, le binaire
`1b7fe2ed…`, un cache immuable déjà extrait et une nouvelle racine de sauvegarde.
Le processus 15484 accomplit les 18 étapes en 20,005 s : accepter la quête,
aller chercher la preuve, revenir terminer la branche collecte, contourner la
porte ancienne, traverser le passage ouvert du Refuge, s'arrêter à l'intérieur,
capturer, sauvegarder et demander une reprise. Les déplacements passent par les
commandes natives et le KCC réel, sans téléportation ni édition d'état ; ce pilote
n'est pas une recette de déplacement par clics dans l'UI du jeu.

Le joueur est sauvegardé en (4,495257 ; 0,910000 ; 6,855342), dans le Refuge.
Déplacement maximal observé : 8,198 m ; identité du backend stable. La quête est
terminée par collecte, la preuve consommée, l'insigne présent une fois, trois
reçus causaux ; seule la bonne porte est ouverte. La projection native confirme
3 bâtiments / 24 éléments / 24 colliders enregistrés, les six arbres avec leurs
pins et transformations exacts, leurs six primitives enregistrées et un renderer
prêt sans pipeline en attente ou en échec. Workspace source inchangé en bytes.
Le reçu de sauvegarde est
`5a083ee7b498a76094b566072fe9eeb4c98ccfb3058a56f470abbfa02cd6930c`,
état `74271ef254b4ee81ef70c77b319a08756a0f9ac8954ac02c2cc1c116f3b3610c`.

Un second processus, 18480, accomplit les cinq étapes de reprise. Son état de
boot est exactement l'état sauvegardé, position intérieure incluse ; aucune
nouvelle action, aucun nouvel événement ni récompense supplémentaire. Le premier
vérificateur échoue car il suppose à tort que `last_action_receipt` doit être
vide après reprise. Les observations montrent le dernier reçu historique exact,
une séquence constamment 3 et `last_events` vide. Le vérificateur corrigé contrôle
ces invariants ; il ne rejoue pas le jeu et n'altère ni les observations ni le
pilote original. `resume/verification.json` conserve explicitement cet échec et
sa correction. Aucun fournisseur n'est sollicité.

La capture d'intérieur est presque noire et **sa lisibilité n'est pas acceptée**.
Ce défaut empêche d'assimiler la réussite fonctionnelle à une qualification
complète de l'intérieur. Le correctif en cours ajoute une lumière neutre dérivée
par salle dans l'adapter natif ; le manifeste source 024 capture les entrées
avant cette modification (2 155 fichiers, 279 632 899 octets). Tests, build et
nouvelle capture restent à terminer ; Gate 6 reste ouverte.

### Éclairage corrigé, deux solutions et leurs reprises dans l'environnement final

Les deux tests de bâtiments passent en 3 min 27 s de compilation ; Clippy strict
passe en 1 min 14 s. Le build partagé passe en 1 min 32 s, dont une attente du
cache détenu par une compilation de conversion d'assets, préservée. Le binaire
du jeu est maintenant
`7e200de1b9718daa10aa93e28ce4cc98a878b96acbeab77851288e3d61553797`.
Les reçus `assistant-interior-light-{test,clippy,game-build}-001.log.receipt.json`
conservent commandes, dates et verdicts. Formatage ciblé et diff check passent.

Le manifeste 025 diffère de 024 uniquement par `module_game/buildings.rs`.
026 ajoute la mise à jour documentaire du README assistant, qui décrit désormais
les opérations actuelles et le raccord MCP réellement implémenté. 027 est
identique à 026 : 2 155 fichiers, 279 634 616 octets. Aucun autre fichier produit
n'est modifié dans ce lot.

`native-reference-interior-002` rejoue les mêmes 18 étapes sur le même export,
avec le nouveau binaire et une nouvelle sauvegarde isolée. Le processus 30612
termine collecte et entrée dans le Refuge ; le processus 19864 réussit les cinq
étapes de reprise dans un processus neuf, état de boot exactement identique à
l'état sauvegardé. La sauvegarde et son hash sont même identiques en bytes à
ceux de la recette avant éclairage : ce correctif de présentation n'a pas changé
les conséquences du parcours.

Les captures avant/après et de reprise sont effectivement relues. Le sol, les
murs, le plafond et la sortie deviennent discernables ; le joueur reste fortement
ombré sous l'éclairage de plafond. La réserve de lisibilité presque noire est
levée pour cette salle procédurale simple, sans qualification esthétique générale
ni acceptation propriétaire. Le renderer observe 51 pipelines, zéro en attente,
zéro en échec. `assistant-reference-native-review-001.json` rattache cette revue,
ses limites et les hashes des captures aux builds et manifestes ; il complète le
champ de revue encore pending du reçu brut de reprise, sans le réécrire.

`native-reference-witness-001` vérifie également la deuxième approche sur la
source finale et le même binaire : processus 33392, 14 étapes, témoignage reçu,
quête terminée, insigne une fois, preuve non consommée, trois reçus causaux et
bonne porte seule ouverte, puis entrée KCC dans le Refuge. Sauvegarde
`63cb160f473e8af560f81a94c245cc0c85060a22bbb4e55686e2f0613b8862e5`,
état `3130136815ef03eef109db1e4ee2a59c546d5a628612617b3dae652945c8e1b4`.
Le processus neuf 2548 reprend exactement cet état intérieur en cinq étapes,
sans action ni récompense nouvelle. Les deux approches et leurs reprises
confirment les trois salles / 24 colliders et six arbres prêts. Aucun fournisseur
n'est sollicité par ces parcours ; aucun déplacement n'est une téléportation.
Ces pilotes natifs ne remplacent pas les preuves de clics dans Studio ou dans
le jeu, conservées séparément.

Restent notamment les parcours étendus de conflit/replanification et de reprise
MCP, la comparaison complète manuelle/IA, le rapprochement final de tous les
critères et l'archivage vérifié. Le dernier contrôle réseau Qwen demeure celui
de 22 h 46, sans génération envoyée ; aucune disponibilité nouvelle n'est inférée.
Gate 6 reste ouverte.

### Reprise MCP réelle après fermeture et nouvelle approbation dans Studio

La fixture `gate6-mcp-durable-studio-ui-001` copie les documents de la recette MCP
antérieure, en vérifiant leurs empreintes et sans copier les verrous. Le client
de qualification est synthétique : il utilise réellement le transport stdio du
Studio, mais n'appelle aucun modèle et ne possède aucune API d'autorisation ou
d'application. Le binaire Studio reste `7a9667d5…` ; aucun code produit n'est
modifié pour cette recette.

Le premier pilote échoue sur une assertion de format : avant autorisation,
`tools/call apply` reçoit un résultat MCP `isError: true`, et non une erreur
JSON-RPC `-32602`. Le refus « Authorize a selection in Studio first » est effectif,
zéro outil est exposé et aucune carte n'est modifiée. Le transcript et le pilote
`assistant-mcp-durable-ui-001` sont conservés ; ce Studio est fermé normalement.
Le pilote corrigé distingue ce refus d'une autorité accordée.

Phase 1 du pilote 002, processus 38264 : ouvrir la carte, sélectionner uniquement
ses propriétés générales, préparer et relire le contexte, cocher le partage puis
autoriser dans Studio. Le client dépose deux fois la même proposition de nom :
commande unique `f61aee89-2df4-596c-a96e-390cf0904d9d`, revue
`70b88cc809ce7842cfe8094e02f028facae3038fbc3c9e69bd780a88fc3016cc`.
Le diff montre « Village MCP corrigé » → « Village MCP repris ». Sa case
d'approbation est cochée et Appliquer devient disponible ; **aucune application
n'est effectuée**. Enregistrer et quitter ferme le processus avec succès.
Le workspace reste identique en bytes. La proposition persistée contient
seulement schéma, commande, empreinte de proposition, proposition et date ; aucune
permission, approbation ou revue sérialisée. SHA-256 du record :
`c4eb6382f6afdea500c90a897f12cd4440a2620a207db92e76525021754ae895`.

Phase 2, processus neuf 26052 : zéro outil exposé au démarrage, refus d'application
avant autorisation, UI « partage non autorisé », aucune proposition applicable.
Après nouvelle préparation et revue du même contexte puis autorisation UI,
la proposition conservée revient avec la même commande et une **nouvelle revue**
`250be079b4bba9fa5bbc65b38b6540d74d9c0131f06def551770b3c433ff93d9`.
La case d'approbation est décochée et Appliquer reste grisé. L'opérateur relit et
confirme de nouveau ce diff, applique puis sauvegarde dans Studio. Le client
vérifie un reçu canonique unique, un changement du seul nom, toutes les autres
corrections et la base conservées. La proposition persistée reste identique en
bytes ; les outils redeviennent indisponibles après changement de révision.
Source finale `b15837be13d7fb069ced6bd218911c5f5d2aa14bab6ae5c60408421ffca72433`.
Le processus quitte normalement. Les deux reçus `assistant-mcp-durable-ui-002-`
`phase{1,2}.result.json`, transcripts et quatre couples capture/arbre
`gate6-mcp-durable-*` établissent ces étapes. Ce parcours valide cette reprise
MCP avec nouvelle autorisation et nouvelle approbation ; il ne constitue ni une
génération Qwen ni la recette de conflit provoqué par une retouche humaine.

### Plan périmé par une vraie retouche de terrain, puis replanification dans Studio

La copie isolée `gate6-mcp-conflict-studio-ui-001` reprend la source `b15837be…`.
Le processus 30328 utilise le même Studio et un client synthétique sur le vrai
stdio. Après revue du contexte et autorisation UI, la proposition de nom reçoit
la commande `6c5a8bf8-1186-5689-b885-99c1f9ebc831` et sa revue
`66ecd991d9fbbc60b2cacb2d769461d28d2ea7f78972a9b2b605c8580aa4ae75`.
L'opérateur coche l'approbation sans appliquer, puis passe à Terrain, choisit
Placer dans la vue, prévisualise une hausse ponctuelle de relief, applique et
sauvegarde par les commandes ordinaires. Le journal montre 115 échantillons
authorés, un reçu Human supplémentaire et aucun reçu de l'ancien plan MCP.

La nouvelle source humaine est
`92065a3157a76b134d1d4c6d1b5c16cf8e50e5d6486fe73c415bbf85a96c865b`,
archive `97060920bc7aca2ab5e46c1276176282dae34cf7f0c81e431c4e5e91b0361e6e`.
Seul `height_deltas` a changé. Le client constate zéro outil disponible et reçoit
un refus MCP pour l'ancienne proposition. Dans Studio, le message de révision
changée apparaît ; même en recochant l'ancien accord, Appliquer reste désactivé.
La capture `gate6-mcp-conflict-old-blocked-001` conserve cet état.

Préparer à nouveau révoque l'ancien périmètre. Les données relues montrent la
révision 5 et le nouveau hash ; après une nouvelle autorisation UI, le client
propose le nom sur ce contexte. La commande devient
`cb9e9ec3-28c2-5619-a11d-ac9ae46ff6fb`, revue
`9beaea81f6fb693e08241d2984c5a201d9f31b8cabf42eee672c078307e564f7`.
La case est à nouveau décochée, Appliquer grisé. Après nouvelle revue et
approbation UI, appliquer puis sauvegarder crée un seul reçu pour cette nouvelle
commande ; la première n'est jamais appliquée. Les 115 hauteurs et toutes les
autres corrections humaines sont exactement conservées, ainsi que les reçus
précédents. Source finale
`f1d8ed36e5a596254b46d932c3d191666c9701447922ff8ed95d3d459963b284`.

Le reçu `assistant-mcp-conflict-ui-001.result.json`, son transcript et cinq
couples capture/arbre retracent le parcours. `assistant-mcp-ui-postclose-001.json`
revérifie les deux fixtures après fermeture normale, les commandes uniques,
l'absence de l'ancienne commande, les 115 retouches et les 18 fichiers de capture.
La source de l'aventure principale reste `51a5a525…`. Seul le Studio canonique
du propriétaire reste actif après les recettes. Ces propositions synthétiques
ne sont pas des générations Qwen. Aucun code produit n'a changé : le manifeste
028 diffère de 027 uniquement par le README assistant actualisé (2 155 fichiers,
279 634 921 octets).

À 21:23:06 UTC (23:23 à Paris), `assistant-local-presence-006.json` retente
`/v1/models` sans proxy. La connexion expire après 21,047 s, WinError 10060,
avant tout POST de génération. Le raisonnement et le délai de lecture de 960 s
sont prévus par le pilote, mais cette étape n'est pas atteinte. Aucun appel
réussi ni activité du modèle nouvelle n'est revendiqué.

La reprise durable MCP et le refus/replanification provoqués dans Studio ont
désormais leurs preuves. La comparaison complète manuelle/IA, le rapprochement
critère par critère et l'archivage vérifié restent à terminer ; les cas de jobs
et leurs preuves de faute contrôlée doivent être rapprochés explicitement de
R6-P05 dans cet audit. Gate 6 reste ouverte.

### Vérification de présence Qwen après signalement du propriétaire

Le 22 octobre à 21:44:40 UTC (23:44 à Paris), un GET direct sans proxy vers
`http://192.168.1.41:8080/v1/models` échoue après 21,063 s, WinError 10060.
Le contrôle est répété hors bac à sable à 21:45:15 UTC : même échec de connexion
après 21,028 s. Le reçu `assistant-local-presence-007.json` conserve
`generation_sent: false`. Le pilote prévoit le modèle
`qwen3.8-flash-next-iq4-xs`, `enable_thinking: true` et 960 s pour la génération,
mais aucun POST de génération n'est atteint. L'absence d'activité du modèle
pendant ces essais est donc cohérente avec les contrôles. Ces résultats ne
permettent pas de déterminer si la cause est le service, le réseau ou son accès.
Les compilations, tests hors ligne et propositions MCP synthétiques ne doivent
pas être présentés comme de l'inférence Qwen.

### Parcours manuel comparable : réglages de quête et état initial

La préparation de R6-P07 révèle que la création manuelle de quête fixe une
récompense absente, tandis que le panneau de solutions indique une récompense
partagée sans offrir son édition. Le parcours manuel ne peut donc pas encore
produire la récompense de l'aventure de référence. Le nouveau volet « Réglages
de la quête » expose nom, donneur, objet et quantité du parcours simple,
récompense et quantité. Il utilise le même document canonique, la prévisualisation
et les commandes ordinaires. Les solutions avancées et leurs consommations
restent indépendantes et explicitement conservées, ainsi que les dialogues.

Trois tests vérifient la retouche ciblée, le round-trip, les refus atomiques et
le retrait explicite de récompense. Clippy strict WASM passe ; le premier
Clippy natif signale un séparateur manquant dans un littéral de test du diff
assistant. Cette seule écriture est corrigée, sans changement numérique ;
`assistant-manual-quest-settings-native-clippy-002` passe. La régression
`assistant-manual-quest-settings-regression-001` termine à 21:40:38 UTC :
**717 PASS, zéro échec, 11 ignorés, 78 suites**. Les ignorés exigent fournisseur,
Vault, fixture ou benchmark explicites ; ils ne sont pas comptés comme PASS.
Le manifeste 030 épingle 2 157 fichiers / 279 648 972 octets et les six fichiers
de ce correctif depuis 029. La compilation frontend
`assistant-manual-quest-settings-frontend-001` est lancée ensuite ; ni la recette
graphique des réglages ni le benchmark manuel ne sont encore qualifiés.

La fixture `gate6-manual-reference-studio-ui-001` reprend le préfixe exact du
workspace avant la création IA : source `d66d4114…`, révision workspace 5,
hash de base vérifié contre la commande native suivante. Le reçu
`assistant-manual-reference-fixture-001.json` conserve les trois fichiers et
leurs SHA-256 (19 154 octets). Il n'y a ni quête, ni objet d'inventaire, ni lieu,
ni placement ou bâtiment ajouté, ni journal assistant copié. La comparaison
portera sur les deux approches de quête, les lieux, récompense et porte,
retouches, variante, forêt, salles et reprise jouable. Une seule observation
par Codex connaissant le résultat sera distinguée d'une étude humaine ; temps
d'inspection et d'outillage inclus, développement et builds séparés. Les temps
de simples retouches ou les appels métier hors UI ne remplaceront pas ce
parcours complet.

### Audit des critères et conservation des pilotes

La [matrice d'acceptation](GATE6_ACCEPTANCE_MATRIX_20261022.md) reprend les treize
fondations et les huit critères explicites de la roadmap. Le contrôle borné
`assistant-acceptance-audit-001.json` vérifie les reçus nommés, les hashes des
captures et de l'archive finale, les tests exécutés et le binaire natif. Les
manifestes 030/031 sont identiques ; depuis la revue native 027, aucun fichier
du jeu `forge_tidewater` n'a changé et son exécutable conserve `7e200de1…`.
Les preuves d'interruption/reprise sont des injections de faute contrôlées sur
le vrai stockage et un résultat fournisseur historique, complétées par les
recettes MCP et les tests. Elles ne sont pas de nouveaux appels réseau. Le
critère R6-P05 ne prescrit pas une coupure réseau fraîche supplémentaire.

`assistant-campaign-volume-001.json` inventorie séquentiellement la seule
campagne : 1 011 fichiers / 192 918 682 octets, zéro lien, sous les budgets
prévus. Le premier parcours échoue sur un chemin Windows long ; la reprise
utilise les chemins absolus étendus, sans suppression. Cet inventaire précède
les derniers reçus et ne constitue pas le manifeste final de clôture.

`assistant-comparison-provider-inputs-001.json` sépare les quatre journaux
Studio et les trois réponses stagées : 8 197 tokens d'entrée / 12 835 de sortie
déclarés, sans compter une consommation inconnue des appels sans réponse
comptabilisée. Les tarifs locaux configurés sont nuls, matériel et électricité
non mesurés. Lancement → première création sauvegardée : 768,538756 s observées,
distinctes des 318,634 s clic Générer → observation Staged. Le monde final,
les retouches et les candidats refusés à l'export sont des phases ultérieures ;
aucun gain complet sur le manuel n'est encore établi.

Les cinq pilotes exacts d'audit, volume, données fournisseur et fixtures sont
copiés dans la campagne avec contrôle SHA-256, sans écrasement. La copie
`gate6-quest-settings-studio-ui-001` contient seulement 4 365 869 octets de
workspace et archives référencées. La récompense Insigne a une pile maximale de
1 : la recette devra donc refuser 0 puis exercer le retrait explicite et la
restauration à 1, plutôt qu'essayer d'appliquer une quantité 2 invalide. Aucun
test graphique de ce nouveau volet, comparaison manuelle complète ou archivage
final n'est encore déclaré réussi. Le frontend commun reste en compilation,
puis il faudra reconstruire le desktop avant la recette, car les tests Cargo
ont remplacé l'exécutable de debug ordinaire.

Le lanceur frontend 001 termine avec code 1 : son invocation par Windows
PowerShell traite la sortie stderr de compilation comme `NativeCommandError`
et ne produit pas de reçu de build ; le log vide est conservé. Ce résultat
n'est ni un PASS ni une preuve d'erreur Rust. Après contrôle de terminaison
des processus enfants, le build 002 est lancé avec `pwsh` (PowerShell 7).
Il retrouve le cache Rust compilé (0,70 s) puis passe à l'optimisation WASM ;
son résultat et le packaging natif restent à vérifier. Le reçu
`assistant-manual-quest-settings-frontend-001.launch-failure.json` distingue
l'échec du lanceur du verdict applicatif non établi.

### Nouveau candidat et recette du formulaire manuel terminés

Le frontend 002 passe de 22:00:11 à 22:03:41 UTC, cache Rust réutilisé puis
optimisation WASM et hook canonique : 3 000 imports et 11 fichiers de kit vérifiés
par SHA-256. Le desktop 001 passe de 22:03:56 à 22:04:31 UTC, compilation 32,85 s.
Le binaire commun réel `debug/forge-studio.exe` porte
`2e16bbab5d0b89af88e2c0029c366a025ff8dd838a458d21e2d625862487cb40`.
Les manifestes 030 à 033 sont identiques ; le chemin underscore annoncé par
Tauri n'est toujours pas employé comme preuve.

Dans la copie de quête, processus 30180 : ouvrir la carte et « Quêtes et journal »,
choisir la quête existante puis déployer « Réglages de la quête ». Les valeurs
relues correspondent au Gardien, à Preuve et à Insigne ×1. Quantité 0 est refusée
via Tab puis Espace ; l'alerte française prend le focus, Appliquer reste grisé.
Le vérificateur confirme les bytes du workspace inchangés et zéro commande.
Choisir « Aucune récompense », examiner, appliquer et sauvegarder change seulement
`reward_item` et `reward_quantity` : null/0, un reçu manuel unique. Tous les autres
champs des corrections, la base et les anciens reçus restent identiques.

Après fermeture normale, le processus 23072 rouvre la même copie. « Aucune
récompense » est bien relue dans le formulaire. Sélectionner de nouveau Insigne,
garder 1, examiner au clavier, appliquer et sauvegarder retrouve exactement
**toutes les corrections initiales**, deux commandes manuelles distinctes et tous
les anciens reçus. Les deux solutions et les dialogues n'ont jamais changé.
Le head de cette copie est `5d7995a6…` (révisions et audit nouveaux), l'aventure
de référence reste `51a5a525…`. Le second processus quitte aussi normalement.

La première réouverture est refusée par l'auto-review qui confond le numéro de
fenêtre 19334062 avec le PID de lancement 30180. L'inventaire en lecture seule
des processus et fenêtres confirme ensuite la disparition des deux ; seul le
Studio propriétaire PID 15948 subsiste. La réouverture avec garde sur le chemin
exact de l'exécutable de recette est alors approuvée. Aucun double Studio de
recette ou arrêt du propriétaire n'est nécessaire. Une ancienne variable JS
locale masquait aussi l'objet global courant lors de la récupération de fenêtre ;
les références explicites à l'objet courant corrigent l'outillage avant saisie.

`assistant-quest-settings-ui-001.{invalid,removed,restored}.json` et le reçu
agrégé `assistant-quest-settings-ui-review-001.json` conservent ces étapes,
trois couples capture/arbre, hashes, binaire, manifeste et fermetures. Aucun
appel fournisseur n'a lieu. Les libellés, champs, erreur, focus et navigation
clavier de ce volet sont examinés dans le design existant ; le journal commun
de prévisualisation garde son libellé générique « Proposition terrain » même
pour cette retouche de gameplay. Cette limite de rédaction existante n'est pas
présentée comme un diff détaillé de quête.

Les contrôles locaux de dépendances passent : audit sur 970 packages, base
`640bf5a9…` mise à jour le 22 octobre ; zéro vulnérabilité dans sa liste, neuf
avis de maintenance et l'avis unsound GLib `RUSTSEC-2024-0429` conservés.
Deny valide advisories, bans, licences et sources selon la politique inchangée,
avec avertissements de versions multiples. `assistant-supply-review-001.json`
distingue ces résultats d'une qualification générale de sécurité/distribution.

Le correctif manuel est donc prêt pour la comparaison complète ; ce benchmark
n'a pas encore commencé. L'audit final, l'archivage et le verdict restent ouverts.

### Parcours manuel complet d'auteur, réouverture et refus du kit au lancement

Le parcours `gate6-manual-reference-studio-ui-001` part du préfixe commun de
quatre commandes antérieur à la création IA, head `d66d4114…`, sans copier le
résultat de Qwen. Le Studio `2e16bbab…` (PID 28740) crée par ses formulaires
ordinaires le Gardien, le Témoin, Preuve, Insigne, la quête et ses deux solutions,
la variable du témoignage, les effets de porte et les quatre liaisons dirigées
Village ↔ Forêt ↔ Refuge. Le joueur est centré à Y=0,85 ; la nouvelle porte est
retouchée à X=4,5, Z=4, et le Gardien reçoit la réplique humaine de référence.
Le formulaire de récompense qualifié auparavant permet de choisir Insigne ×1.
L'ancienne porte, l'ancien dialogue et les règles sont conservés.

Un arbre est placé, réglé à (5 ; 0 ; 5), puis déplacé seul à X=6 pour la variante.
La construction ordinaire ajoute trois pièces déverrouillées de 8 × 6 × 3 m,
aux positions (4,5 ; 0 ; 7), (9 ; 0 ; −6) et (−9 ; 0 ; −6). Les transformations
et duplications donnent les six positions finales de la forêt de référence,
échelle 0,75. Un choix initial de chêne est corrigé par remplacement groupé des
six références vers `vv-tree-pine-a`, sans changement d'identités, noms ou
transformations. Les noms de placement gardent donc `vv-tree-oak-a`.

Le pin du catalogue livré est `68534a54-c3cc-5c5c-b5d0-1b8141eb60ea`, hash
`c5c0ffe8676ab03b0d6dbb10a8e7deabee81cfd818997b364cebdf0cf09cc4a9`.
Il **diffère** du modèle Vault `9bbec162…` / `04471efb…` de la référence IA.
Les géométries et octets ne sont donc pas identiques. Les pièces manuelles
gardent aussi le nom par défaut « Pièce » ; les PNJ sont centrés à Y=0,97, la
pile Preuve vaut 24 et les dimensions physiques du ramassage/de la porte
proviennent des défauts manuels. La visibilité du retour par témoignage utilise
la variable seule ; la validation métier de solution exige encore la quête
active. Ces différences font partie du bilan de qualité et interdisent de
présenter l'essai comme une comparaison de modèles strictement identiques.

`assistant-manual-authoring-source-001.json` et son vérificateur contrôlent
41 nouvelles commandes humaines uniques, le reçu de fixture antérieur intact,
les solutions/conséquences, les quatre liens, la récompense, les pièces et arbres.
Chaque retouche d'environnement conserve exactement le gameplay. Le jeu de
capacités du Creator est identique, avec un ordre sérialisé différent. Le head
final est `458f0230361f0a5b53dc73d6cc7b8c651f69b090b890fe9be97678acf096808f`,
archive `dab55a55cdbc03411bdb80689fd2b6d4e5969ea41958eec3a498a6fab173973c`,
révision de zone 43 et de workspace 46. Aucun journal de job assistant n'est créé.
Les premières hypothèses erronées du vérificateur (révision initiale 1 au lieu
de 2, ordre des capacités, champs optionnels absents) sont corrigées dans
l'outillage ; les données historiques ne sont pas réécrites.

L'observateur en lecture seule, dont le pilote est conservé sous
`assistant-manual-comparison-ui-001.observer.py`, constate le lancement à
22:18:07 UTC et la dernière sauvegarde à 23:33:58 UTC : **4 550,633114 s**.
Ce temps inclut les inspections, latences des outils, corrections et interruption
pour diagnostiquer Qwen ; ce n'est ni une étude humaine aveugle ni une durée de
validation native complète. Les 768,538756 s historiques jusqu'à la première
création IA sauvegardée couvrent un périmètre plus court : aucun ratio de gain
n'est déduit de ces deux durées. La génération locale n'intervient pas dans le
parcours manuel. La sonde `assistant-local-qwen-live-008.json` à 23:10 UTC échoue
encore sur `/v1/models`, WinError 10060 après 21,054 s, `generation_sent=false`.

Après fermeture normale, le Studio PID 28104 rouvre la même copie : la réplique
exacte du Gardien et la porte X=4,5/Z=4 sont relues. Le bouton « Lancer le jeu »
refuse ensuite l'export avant processus de jeu : **« Modèle épinglé absent du
Vault et des imports conservés »**. Le catalogue gouverné déployé permet de
placer/afficher ses modèles, mais l'export natif ne résout actuellement que les
imports conservés et le Vault. Le refus n'est pas contourné par un changement
de pin ou une suppression du décor. Sa correction doit joindre les ressources
livrées vérifiées au package, avec provenance, licence et budgets, puis rejouer
les deux solutions natives et leurs reprises.

`assistant-manual-reopen-review-001.json`, son vérificateur et trois couples
capture/arbre conservent la réouverture et ce refus. Le head reste strictement
inchangé. Les deux processus de recette quittent normalement ; le Studio du
propriétaire PID 15948 est préservé. L'observateur se termine explicitement à
23:43:22 UTC (36 transitions de sauvegarde observées, certaines sauvegardes
regroupant plusieurs commandes). R6-P07, la correction d'export manuel, l'audit
final et l'archivage restent ouverts ; aucune clôture Gate 6 n'est déduite.

### Résolution du kit livré et deux parcours manuels natifs — 23 octobre UTC

Le correctif natif ajoute `governed_kit_export` et le raccordement au résolveur
de ressources Tauri. Après les imports conservés, les seuls pins manquants du
kit livré sont admis avant ceux du Vault. Le manifeste gouverné et les médias
restent validés ; pin modifié, modèle absent/invalide, doublon ou budget dépassé
sont refusés. Les octets UTF-8 exacts du manifeste, son hash, la licence et
l'auteur déclaré sont conservés dans la preuve du catalogue existant. La date
est celle de la copie locale, les droits restent privés. Aucun import utilisateur
ou document d'auteur n'est modifié implicitement. ADR-0035 décrit cette frontière.

Le test ciblé 001 échoue à compiler sur deux constantes privées de fixture ;
les quatre tests du lot 002 passent. Clippy 001 refuse un balisage documentaire,
puis 003 une fonction dépassant la limite de taille d'une ligne ; le code est
séparé sans exception et **Clippy natif 004 passe**. La régression 001 révèle une
collision de répertoires temporaires de tests concurrents. Un compteur propre
aux tests complète le timestamp, sans modifier le jeu de production. Après ce
correctif et la conservation de l'auteur déclaré, la régression finale 003
compte **721 PASS, 0 échec, 11 ignorés / 78 suites**. Clippy WASM 001 passe,
avec l'avertissement futur de `proc-macro-error2` conservé. Aucun échec historique
n'est réécrit comme une réussite.

Le frontend qualifié est réutilisé : les modifications compilées de cette
tranche sont natives uniquement. Le desktop `assistant-kit-export-desktop-001`
passe en 33,57 s. Les manifestes physiques 036, 037 et 038 sont identiques :
2 159 fichiers, 279 666 811 octets. Depuis 034, seules les entrées du résolveur
de kit, son raccordement, ses tests, la fixture native et le README Studio changent.
Le nouveau Studio a pour SHA-256
`f23467af0af841e626e2ab6a5a9760adca632d92b06990320dc5eb9aaffa591e` ;
le jeu conserve `7e200de1…`.

Le Studio PID 21020 rouvre le même head manuel `458f0230…`. Son bouton
« Lancer le jeu » exporte réellement le package `281beb0f…` et ouvre le jeu
PID 5316. Celui-ci termine normalement, prêt, sans erreur, séquence 0. Le
package contient un seul modèle de 10 177 164 octets, son pin exact et sa preuve
de provenance/licence. Les sept fichiers déclarés sont relus et vérifiés ; la
preuve contient le manifeste livré original, octet pour octet, auteur
« OpenRPG Forge project » et redistribution interdite. Le premier essai de lecture
Python du conteneur comme ZIP est refusé ; `.orpmod` utilise le conteneur Forge
existant, dont l'admission native et le répertoire source sont les autorités.

Le pilote conservé dans `native-manual-{collection,witness}-001/driver.py`
résout les identités depuis les définitions manuelles, sans copier les IDs IA.
Les deux chemins utilisent les commandes métier et le vrai KCC, puis entrent
dans le refuge ; trois pièces/24 éléments et six arbres sont prêts. La collecte
termine 18 étapes et consomme Preuve ; le témoignage termine 14 étapes sans la
consommer. Chacun attribue un Insigne, ouvre la nouvelle porte seulement et
conserve trois reçus d'action. Chaque reprise accomplit cinq étapes et retrouve
exactement l'état sauvegardé à l'intérieur, sans nouvel événement ou récompense.

Collecte : sauvegarde `1892b94c…`, état `2f8fb5ac…`. Témoignage : sauvegarde
`a8edf029…`, état `f43e8dd6…`. Les quatre captures sont relues pour leur
lisibilité fonctionnelle. Elles ne valent pas acceptation esthétique du
propriétaire. `assistant-kit-export-review-001.json` rapproche UI, package,
contrôles, captures et sources ; le head manuel reste inchangé. Toutes les
recettes ont quitté normalement, le Studio du propriétaire reste préservé.

`assistant-comparison-review-001.json` et
[le bilan comparatif](GATE6_COMPARISON_20261023.md) couvrent temps, corrections,
coût déclaré et qualité des deux parcours complets. Les intervalles observés
jusqu'aux deux reprises sont 14 794,361 s avec assistant et 6 442,844 s pour le
manuel, inspections/réparations comprises. La signature fonctionnelle est
identique, les modèles et certains défauts d'auteur diffèrent. Aucun ratio de
vitesse ni gain général de productivité n'est démontré. R6-P07 est comparé ;
l'audit de clôture et l'archive vérifiée/cataloguée restent à terminer.

### Clôture locale — 23 octobre 2026

L'audit final `assistant-final-acceptance-audit-001.json` rapproche les treize
fondations et huit parcours, vérifie les sources courantes et conclut les
21 critères sur leurs preuves locales. L'opération d'archivage suivante conserve
1 239 fichiers / 259 013 369 octets dans le ZIP64
`archives/gate6-ia-mcp-20261023.zip`, 198 331 004 octets, SHA-256
`8a90da709566f553b0c8a9836ec4f545dc941677c763f41f48de678633f5279b`.
Chaque membre est relu et vérifié, la source est revérifiée inchangée par
métadonnées et hashes, les catalogues JSON/TSV sont contrôlés ; les originaux
restent conservés. Le reçu de gestion central est
`rapports/gate6-archive-20261023.json`. Le
[rapport final](GATE6_CLOSURE_20261023.md) porte désormais le verdict
**clôturée localement** et conserve les réserves de disponibilité Qwen,
productivité non démontrée, esthétique, dépendances et distribution. Les reçus
historiques et leurs champs d'attente gardent leur portée datée. Aucune
publication Git ni ouverture de Gate 7 n'est effectuée.
