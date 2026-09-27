# Configuration Codex locale

Installee le 19 septembre 2026. Les essais de delegation et Computer Use sont
reportes aux prochaines demandes, conformement a la demande du proprietaire.

## Portee et choix

Racine : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche `Rust`.
Le repertoire parent `E:/OpenRPG_Forge` n'est pas la racine de cette configuration.
Ouvrir une nouvelle tache Codex avec le depot Rust comme repertoire de travail :
changer seulement le repertoire d'une commande shell ne prouve pas le rechargement.
Les configurations de projet peuvent dependre de la confiance accordee au depot.
Aucune entree de confiance explicite OpenRPG observee dans le fichier global ;
cela ne prouve pas que l'application refuse ce depot. Verifier si le chargement echoue.

Le binaire du processus Codex actif est celui de l'application, version
`codex-cli 0.155.0-alpha.9` (bin/cdef5aaf3e41ab53), pas une CLI independante.
CODEX_HOME observe : `C:/Users/karen/.codex`.
Configuration globale inspectee sans modification : Astra, effort low,
service_tier default, pas de section agents ni profil selectionne dans ce fichier.
Les surcharges de session peuvent differer ; aucun changement du modele principal.
Le catalogue local et les outils de cette session annoncent Luna/max et Terra/medium.
Un catalogue n'est pas une preuve d'execution effective.

| Role | Modele | Raisonnement | Usage |
| --- | --- | --- | --- |
| code_mapper | gpt-5.6-luna | max | Exploration ciblee sans mutation |
| tester | gpt-5.6-luna | max | Validations et logs |
| visual_tester | gpt-5.6-luna | max | Parcours graphiques simples, experimental |
| rust_worker | gpt-5.6-terra | medium | Changements Rust delimites |

Defaut sans role : Luna/max. Maximum six sous-agents hors parent, sous reserve de la limite technique de la session.
Tous les roles demandent Standard (`service_tier = "default"`). Ne pas activer Fast.
La politique persistante est dans AGENTS.md ; elle autorise cette delegation dans
le perimetre du depot et impose max pour toute utilisation de Luna.
Si un outil ne sait pas selectionner un role, transmettre explicitement son modele,
son effort et ses instructions avec un contexte cible ; ne pas annoncer que son
fichier a ete charge sans preuve. Signaler toute limite de l'outil.
Les permissions reelles priment ; la lecture seule de code_mapper est une consigne,
pas une garantie de sandbox ajoutee par cette configuration.

## Verification effectuee

| Element | Configure | Charge | Teste | Preuve ou limite |
| --- | --- | --- | --- | --- |
| Defauts agents | Oui | Non verifie | Statique | TOML parse ; cles presentes au schema officiel |
| Quatre roles | Oui | Non verifie | Statique | Metadonnees requises ; Luna/max ; Terra/medium |
| Politique AGENTS.md | Oui | Lue pendant cette intervention | Contenu | Texte original conserve octet pour octet en prefixe |
| Standard | Oui pour les roles | Non verifie | Non | Global default observe ; aucune activation Fast |
| Modeles effectifs | Oui dans les fichiers | Non verifie | Differe | Aucun sous-agent lance |
| Computer Use du sous-agent | Role cree | Non verifie | Differe | API native desactivee dans l'outil expose a cette session |
| Moteur Rust | Sans changement | Sans objet | Non | Aucun build ni modification produit |

Les cinq fichiers TOML ont ete parses avec Python tomllib. Les noms de cles et
les types simples ont ete compares au schema officiel actuel ; ce n'est pas une
validation exhaustive JSON Schema ni une verification du schema embarque dans
le binaire. Le validateur jsonschema n'est pas installe ; rien n'a ete installe.
`git diff --check -- AGENTS.md` reussi. Fichiers .codex non ignores par Git.
Aucun rechargement a chaud, quota economise ou fonctionnement de bout en bout revendique.

Sources :
- https://learn.chatgpt.com/docs/agent-configuration/subagents
- https://learn.chatgpt.com/docs/config-file/config-reference
- https://developers.openai.com/codex/config-schema.json
- https://learn.chatgpt.com/docs/computer-use

## Prochain essai

Dans une nouvelle tache ouverte sur le depot Rust :

> Teste la configuration multi-agents locale : code_mapper localise un point
> d'entree sans modifier les fichiers, rust_worker analyse une fonction sans
> l'editer, tester execute une validation ciblee existante. Luna doit toujours
> etre en max, Terra en medium, avec six sous-agents maximum et Standard.
> Verifie separement visual_tester sur une scene existante si ses outils le
> permettent, puis le defaut Luna/max sans role si observable. Rapporte modele
> configure, modele observe, outils et preuves ; marque non verifie ce qui
> ne l'est pas. Ne remplace aucun modele silencieusement.

Reutiliser les scripts du depot selon le perimetre ; aucun nouveau harnais de
build n'est necessaire pour cette installation. Ne pas executer toute la suite
ni forcer toutes les features pour un simple test de configuration. Les futurs
essais conservent commandes, cibles, durees, codes de sortie et logs sur disque.
Comparer cout total, reprises et qualite sur des missions comparables avant
conclusion economique. Garder un seul operateur graphique a la fois.

## Retour arriere

Supprimer uniquement les cinq nouveaux fichiers sous .codex et cette note,
puis retirer la section `Delegation Codex autorisee — organisation economique`
ajoutee en fin de AGENTS.md. Ne pas utiliser git restore sur AGENTS.md : il etait
deja modifie avant cette intervention. Copie locale de securite ignoree par Git :
`artifacts/forge-runtime/codex-config-backup-20260919/AGENTS.md.before`.
Ne restaurer cette copie que si aucune modification ulterieure ne serait perdue.
Aucun fichier global, MCP, plugin, permission, authentification, source moteur ou
Cargo.lock modifie. Aucun commit ni push.
