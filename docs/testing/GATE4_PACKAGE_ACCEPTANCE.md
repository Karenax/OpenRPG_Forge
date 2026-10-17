# Acceptation Gate 4 — assets et packages

Protocole du 15 octobre 2026, portée réconciliée le 16 octobre.
État de la campagne au 16 octobre 2026 : **clôturée localement** selon le
[rapport final](../evidence/gate4/GATE4_CLOSURE_20261016.md), lot `0.1.0-preview.20261016.019`.
Le protocole seul n'attribue aucun PASS ; les preuves et réserves sont celles
du rapport. La base du 15 octobre décrite ci-dessous reste historique.

Ce protocole précise les preuves des critères de [Gate 4](../planning/MIGRATION_ROADMAP.md#gate-4--assets-gouvernés-et-orpmod-v2) :
catalogue et imports, provenance, dérivés et vignettes, validation de publication,
`.orpmod` v2, protections d'archive, installation et jeu, second module,
remplacement gouverné et import expérimental Solaren.
L'exécution utilise le checkout Rust commun et conserve les travaux concurrents.
GitHub reste une information séparée ; aucune attente de validation Git n'est
ajoutée aux critères de ce protocole.

## Base existante et portée de sa réutilisation

Le Setup du 15 octobre constitue une base d'essai, pas la preuve anticipée de Gate 4 :

- `E:/OpenRPG_Forge/OpenRPG-Forge-Setup.exe`, version
  `0.1.0-preview.20261015.013`, 1 170 618 151 octets,
  SHA-256 `0eca18e3e632ca108b841feb2d540f1ee9bded5fa01dc6cd1686561b6ea17ad0` ;
- contenus séparés dans `E:/OpenRPG_Forge/Forge-Contenus-20261015`, avec
  `reference-assets.forgepack`, `reference-module.forgepack`, leurs manifestes
  et `CONTENUS.json` ;
- sources consommées Source013 :
  `4178ac3b3f7d028cb5b94ca0d870d67c245fc0418a595f375e6a8eb75027b163`.

Les reçus historiques se trouvent sous `E:/OpenRPG_Forge/.gate3-evidence/20261015` :
`closure-classic-verify-013/verification.json`,
`closure-installed-studio-ui-013-audit-001/AUDIT.md`,
`closure-content-export-013-001/result.json` et
`closure-main-promotion-013-001/result.json`.
Ils prouvent leurs installations locales, fichiers et parcours respectifs.
Ils ne prouvent pas que le runtime était empêché d'accéder aux données de développement.

Selon [ADR-0021](../ADR/ADR-0021-detached-delivery-installation.md), `.forgepack` v1
est le transport existant, distinct du contrat sémantique `.orpmod` v2 dont
[ADR-0024](../ADR/ADR-0024-orpmod-v2-semantic-admission.md) décrit la première admission.
Renommer une archive ne satisfait pas ce contrat. Les trois packs Source013 sont
marqués `private_local_only` : leur intégrité n'autorise pas une redistribution
publique. Le lot sélectionné doit avoir ses droits vérifiés avant tout envoi.
Un essai du Setup013 peut qualifier son autonomie locale ; la qualification du
futur `.orpmod` devra porter sur les véritables octets de ce format.

L'admission de ce format cible doit rendre vérifiables l'identité et la version du
module, le schéma, les versions compatibles du runtime et du profil de règles,
les cartes et données de gameplay, les dépendances exactes, les crédits et droits,
ainsi que l'inventaire fermé des fichiers avec tailles, hashes et limites de lecture.
Les tests doivent lire ces valeurs dans le package réel et exercer leur refus ;
un manifeste descriptif extérieur à une archive opaque ne suffit pas.

## Dossier d'une campagne

Créer un dossier de preuves neuf par campagne, hors des entrées compilées.
Conserver les éléments suivants, avec taille et SHA-256 des fichiers :

| Élément | Contenu attendu |
| --- | --- |
| Identité | Date UTC, branche/commit, modifications consommées et empreinte des sources, binaires, packages, outils et paramètres effectivement utilisés. |
| Environnement | OS et architecture du client, GPU/pilote, prérequis présents ou installés, configuration VM, partages et réseau. |
| Admission | Inventaire fermé, licences, dépendances, versions compatibles, résultats de validation et refus. |
| Exécution | Commandes exactes exécutées, entrées utilisateur observées, PID/création/fin des processus possédés, journaux et codes de sortie. |
| Résultat métier | Identités module/carte/objets/assets, révisions, rapports de jeu, état de quête et sauvegardes avant/après. |
| Observation | Captures originales du client installé, légendes de ce qui a réellement été vu et limites de l'observation. |
| Conservation | Empreintes des données authorées, sauvegardes et fichiers étrangers avant/après mise à jour et désinstallation. |
| Verdict | Par critère : `PASS`, `FAIL` ou `NON EXÉCUTÉ`, avec liens aux preuves et réserves explicites. |

Ces intitulés décrivent le contenu de la preuve, pas un nouveau schéma produit ou
des commandes déjà disponibles. Réutiliser les commandes existantes lorsqu'elles
couvrent le parcours ; consigner toute capacité manquante avant implémentation.
Une modification d'entrée pertinente invalide les seules preuves affectées ;
relier explicitement les composants inchangés sans réétiqueter leurs anciens reçus.

## R4-P01 — installer et jouer sans les sources de développement

### Isolation réelle

Utiliser une VM Windows distincte ou un autre ordinateur, avec un profil utilisateur
neuf. `WindowsSandbox.exe` n'a pas été trouvé sur le poste de préparation le
15 octobre ; son activation n'est pas un prérequis. Documenter la VM effectivement
utilisée et son accélération graphique : un GPU virtuel ne qualifie pas les
performances du poste natif.

Une autre isolation Windows peut servir au même critère d'accès si la campagne
en démontre effectivement les propriétés. Le prototype AppContainer local est
une méthode candidate, et non un PASS acquis : profil neuf, SID propre, aucune
capacité réseau, entrées reçues physiquement copiées et épinglées, environnement
reconstruit et processus descendants attribués au même confinement. Les contrôles
source/Vault/réseau doivent réellement échouer avant et après le parcours, avec
des témoins positifs exécutés hors confinement ; l'absence de variables ne suffit
pas. Les permissions de la source, du Vault et des autres ancêtres préexistants
ne sont pas modifiées. Une permission d'un objet de test neuf doit rester limitée
à cet objet et être consignée avec son nettoyage.

Cette méthode utilise le pilote et le GPU de l'hôte : elle ne prouve ni un autre
ordinateur ni un Windows vierge. Si l'enveloppe NSIS ne peut pas démarrer dans
ce contexte, qualifier séparément son installation classique et l'installation
CLI confinée des **mêmes packages authentifiés**, en nommant précisément ces deux
preuves. Une installation CLI ne devient pas une exécution NSIS confinée ; la
clôture du critère d'autonomie exige installation et jeu sans accès aux données
de développement dans le client effectivement utilisé. Le parcours auteur Studio
peut être exécuté séparément sur DATA dans le contexte ordinaire : R4-P01 n'ajoute
pas un confinement de l'éditeur. Ses véritables exports doivent être reliés aux
packages admis, installés et joués par leurs octets et empreintes exacts.

Le parcours réel du launcher installé peut également être acquis dans le contexte
ordinaire, puis rapproché de l'installation et du jeu confinés. Ce rapprochement
exige le même launcher livré, les mêmes modules/cartes et packages, ainsi que la
preuve que son bridge natif et la commande publique `module-launch` utilisent
le même `installed_modules::start` pour replanifier, admettre et démarrer le jeu.
Il ne dispense pas du véritable clic dans Aube : un témoin IPC simulé ne suffit
pas. Nommer séparément le launcher ordinaire et le client de jeu confiné, sans
attribuer au launcher le SID ou les refus d'accès observés seulement dans ce client.

1. Fournir uniquement le Setup et les packages admis dans un répertoire d'entrée
   dédié, monté en lecture seule. Les applications et leurs données personnelles
   seront installées sur le disque du client.
2. Désactiver le réseau, les partages automatiques des lecteurs de l'hôte et les
   échanges de fichiers par presse-papiers. Un éventuel dossier de sortie dédié
   aux preuves ne doit exposer ni le workspace, ni le Vault, ni le profil de l'hôte.
3. Vérifier depuis le client que le checkout
   `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, le Vault `F:/OpenRPG_AssetVault` et les
   racines de données de développement épinglées dans la campagne sont absents ou
   inaccessibles. Conserver ces contrôles négatifs et l'inventaire des partages.
4. Vérifier l'absence de profil développeur `forge-studio.local.json`, de préférence
   vers un Vault externe et de configuration fournisseur IA. Consigner les variables
   Forge pertinentes. L'absence de variables seule ne prouve pas l'isolation.

La copie d'auteur créée par l'installation dans les données utilisateur est
autorisée : elle est le workspace du client. Le workspace source interdit dans
ce parcours est celui du développement, ainsi que toute donnée externe nécessaire
qui n'aurait pas été transportée dans les packages.
Ne pas déplacer les répertoires partagés de développement, ni changer leurs ACL,
pour simuler cette absence.

### Parcours positif

1. Vérifier les hashes des octets reçus. Installer dans une destination choisie,
   contenant espaces et caractères Unicode. Conserver le journal et les prérequis
   réellement satisfaits, dont WebView2 ; aucun téléchargement ne doit être nécessaire.
2. Ouvrir le launcher installé et démarrer l'aventure de référence par son parcours
   normal. Vérifier les identités de l'exécutable, du module, de la carte et des assets.
   Une simple survie du processus ou une promenade ne prouve pas la boucle RPG.
3. Recevoir la quête, parler au PNJ, suivre une approche authorée, utiliser le décor
   dans le combat, récupérer l'objet et terminer l'objectif. Conserver les événements
   et conséquences persistantes, en plus des captures.
4. Sauvegarder par l'action publique. Avec le comportement Source013, F5 sauvegarde
   et F9 prépare une reprise au prochain lancement : fermer normalement, relancer
   et vérifier l'état de quête, l'inventaire et la conséquence conservés.
   Utiliser les contrôles réellement présents si le candidat évolue.
5. Relancer une nouvelle partie sans nouvelle demande de reprise. Vérifier son état
   neuf et l'absence d'écrasement de la sauvegarde précédente.
6. Dans le parcours auteur séparé, ouvrir le Studio installé et la copie utilisateur du module, effectuer une
   retouche gouvernée simple, sauvegarder et rouvrir. Lancer le jeu séparé depuis
   cette carte et retrouver la retouche. Conserver les révisions et le rapport de retour.
   Studio peut fonctionner dans le contexte ordinaire ; relier ensuite son export
   exact au parcours client isolé. Cette étape couvre les critères auteur et R4-P02.
7. Réinstaller ou mettre à jour le candidat, puis le désinstaller selon son protocole.
   Les données personnelles et fichiers étrangers doivent conserver leurs octets.
   Fermer normalement les applications possédées et vérifier leur terminaison.

**Acceptation R4-P01** : le parcours est réussi sur le client isolé, avec les mêmes
packages authentifiés, sans accès possible aux données de développement, au Vault
ou au fournisseur IA. Tout accès externe requis, téléchargement implicite ou
donnée manquante constitue un échec. Une trace d'accès peut compléter la preuve ;
un chemin absolu conservé comme texte de provenance n'est pas à lui seul une lecture
runtime. Distinguer les chemins documentaires des fichiers réellement ouverts.

### Refus et récupération du package

Sur des copies de test, présenter successivement un package corrompu, tronqué,
avec fichier supplémentaire ou chemin sortant de la racine, puis une dépendance
manquante/incompatible. Exercer aussi une limite de taille, un conflit de noms
portables et un lien non autorisé. L'admission doit refuser avec un diagnostic
actionnable, sans publier de tête partielle ni altérer les données utilisateur.

Interrompre une installation possédée en staging, puis la reprendre. La version
précédente doit rester active jusqu'à une promotion complète ; la reprise doit être
idempotente et laisser les fragments non activés identifiables. Vérifier également
un plan périmé et le retour à la version précédente. Les tests de `.forgepack`
existants constituent une base ; rejouer les protections affectées avec le vrai
format `.orpmod` v2, ses limites et son lecteur. Aucun exécutable ou script importé
ne doit être lancé depuis un package de données.

## Second module de régression

Construire une petite aventure distincte : identités propres de module, carte,
objets, PNJ, dialogue, quête et récompense ; disposition et texte authorés propres.
Une pièce, une porte/interaction et un objectif court suffisent. Des assets déjà
gouvernés peuvent être partagés si leurs dépendances sont explicites et épinglées.
Un renommage de l'archive Tidewater ou une fixture unitaire seule ne suffit pas.

1. Créer et rouvrir ce contenu avec les commandes partagées ; conserver sa source
   authorée et son export. Aucun slug, chemin ou branche spécifique au module ne
   doit être ajouté au moteur, au launcher ou à un nouvel éditeur.
2. Valider et empaqueter le module au format cible, puis l'installer à côté de
   l'aventure de référence dans le même client isolé.
3. Le charger, accomplir son objectif, sauvegarder et reprendre. Revenir à l'aventure
   de référence : sa quête, ses sources et sa sauvegarde doivent rester inchangées.
4. Remplacer ou retirer une dépendance uniquement dans la copie de test du second
   module. Le diagnostic doit citer cette dépendance et préserver l'autre module.

**Acceptation** : les deux aventures fonctionnent avec les mêmes binaires et outils,
avec des documents, sauvegardes et dépendances attribuables sans ambiguïté. Conserver
les deux inventaires, identités et rapports de jeu. Une limite du renderer sur un
terrain différent reste une limite à résoudre ou à documenter ; partager des assets
ne doit pas cacher un chargement forcé de la zone de référence.

## R4-P02 — variante gouvernée et liens gameplay conservés

Choisir un asset visible attaché à une interaction de la quête : porte, objet à
récupérer ou objet utilisable dans le combat. Préparer une variante avec source,
licence, hash, dérivation et paramètres explicites. Épingler les IDs de placement,
liens gameplay, pose et corrections authorées avant le remplacement.

1. Importer et retrouver la variante dans le catalogue avec sa vignette, ses
   métadonnées et ses fichiers vérifiés. Exercer les imports GLB, texture et audio
   sur un petit lot gouverné, avec réouverture et refus des entrées invalides ;
   ne pas déduire ces trois capacités de la seule présence des fichiers sur disque.
2. Depuis le parcours public du Studio, préparer le remplacement sur la révision
   courante, examiner son aperçu/diff puis appliquer. Conserver command ID, base,
   nouvelle révision, audit et inverse/snapshot. La politique d'identité doit être
   explicite : liaison vers un nouvel asset ou nouvelle révision d'un asset existant.
3. Enregistrer la variante et les retouches dans les documents authorés ou une
   recette reproductible. Fermer et rouvrir le Studio ; invalider ou reconstruire
   les seuls caches dérivés de cette copie de test. Retrouver les mêmes corrections.
4. Exporter, installer et rejouer la quête avec la variante dans le client isolé.
   Vérifier interaction, collisions pertinentes, récompense et références d'objet.
   Si une reprise est déclarée compatible, la demander explicitement et vérifier
   l'état conservé ; sinon produire un refus motivé sans altérer la sauvegarde.
5. Exercer Undo/Redo et une proposition devenue périmée. Une origine, licence,
   dérivation ou dépendance absente doit empêcher l'admission/publication concernée.

**Acceptation R4-P02** : variante visible après réouverture et installation, liens
gameplay préservés, retouches reconstructibles hors d'un cache Blender, provenance
fermée et refus atomiques. Conserver source/recette avant/après, graphe des dérivés,
diff des références fonctionnelles, captures et rapport de quête. Une apparence
correcte seule ne démontre pas la conservation des liens.

## Import expérimental Solaren sélectionné

La [matrice de reprise](../migration/LEGACY_REUSE_MATRIX.md#6-solaren-et-crypte-des-cendres)
garde la migration générale de Solaren différée ; Gate 4 demande seulement une
expérience bornée sur des données sélectionnées.

1. Déclarer un lot fini, avec dépôt/commit source, chemins exacts, hashes, motif,
   licences et différences attendues. Choisir par exemple un dialogue et ses
   références, ou un petit ensemble de placements lié à des assets vérifiés ;
   ne pas annoncer toute Solaren migrée.
2. Extraire les seules données admises vers des documents Forge versionnés. Aucun
   C#, exécutable, PCK, cache ou scène Godot ne devient une dépendance du runtime.
   Un extracteur éventuel est offline et ne fait pas du format historique la source
   canonique du module Rust. Refuser les champs et références non supportés sans
   les supprimer silencieusement.
3. Ouvrir le résultat dans le Studio Rust, retoucher un élément, sauvegarder et
   rouvrir. Comparer au lot source selon les invariants déclarés : texte, identités,
   références et poses applicables. Documenter les pertes et exclusions.
4. Exporter le lot admis dans un module de test et le charger dans le client isolé.
   Observer au moins le comportement transféré ; les anciennes captures Godot ne
   prouvent aucun comportement Rust.

**Acceptation de l'expérience** : lot traçable, droits vérifiés, conversion bornée,
réouverture éditable, comportement effectivement observé et limites explicites.
Des données non acquises ou sans droits vérifiables restent `NON EXÉCUTÉ` ou refusées,
jamais remplacées par une fixture synthétique présentée comme import Solaren.

## Verdict de campagne

Rapprocher les résultats de chaque parcours et chaque ligne de la roadmap avec
les véritables preuves. Une interface observée, un test de contrat, un build et un
essai sur VM conservent leurs portées différentes. Le transport et les essais
Source013 ne ferment pas les critères du futur format par anticipation.

Les réserves de licence et de distribution, dont GLib, restent explicitement
suivies ; ce protocole ne déclare aucune release générale ou plateforme non testée.
Ne pas ajouter de condition d'attente GitHub. Aucun commit, push, envoi public,
refonte du launcher ou verdict de clôture n'est autorisé par la seule rédaction
de ce protocole.
