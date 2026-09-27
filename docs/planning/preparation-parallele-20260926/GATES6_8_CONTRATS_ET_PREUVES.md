# Gates 6 à 8 — préparations P09 à P11

Date : 26 septembre 2026. Conception proposée fondée sur la roadmap actuelle et les contrats inspectés ; aucune bibliothèque, API produit ou dépendance ajoutée. [Index](README.md).

## P09 — Gate 6 : IA et MCP gouvernés

### Base constatée et écart

Le [protocole d'auteur](../../../crates/forge_project/src/authoring.rs), les révisions/hashes et les reçus de [ZoneEditSession](../../../crates/forge_project/src/zone_authoring/session.rs) fournissent des invariants réutilisables. Ils ne constituent pas déjà un serveur MCP produit, un moteur de jobs IA ou une approbation de plan multi-documents. Le workspace Cargo inspecté n'annonce pas de crate serveur MCP dédiée.

### Parcours produit proposé

1. Inspecter les capacités et la sélection autorisées ; chercher les assets réellement disponibles avec leur état de préparation et leurs droits.
2. Transformer le brief en plan borné ; chaque opération cible des IDs connus et une révision.
3. Présenter diff, ressources à transmettre, fournisseur éventuel, plafond et limites. Le dry-run ne modifie aucun document d'auteur.
4. L'approbation porte sur ce diff précis. La replanification changeant son contenu invalide l'ancienne approbation.
5. Produire en staging, puis appliquer via les commandes communes ; vérifier encore révision, permissions et verrous.
6. Conserver résultat, audit et inverse ; rendre le contenu réouvrable sans le fournisseur.

Contrat de plan proposé : ID et version, révision/hash de base, capacités requises, sélection/région autorisées, commandes typées, ressources et droits, coût/plafond, politique de retry, diff, approbation liée au hash, statut et reçus. Les noms exacts et le stockage sont à adopter après stabilisation des contrats Gate 3/4.

### États de job proposés

Préparé → relu → approuvé → en staging → prêt à appliquer → appliqué. Échec, interruption et annulation sont des états explicites. La reprise d'un même ID ne duplique pas le contenu ; un job ancien ne s'applique pas sur une révision changée. Les transactions multi-documents devront préciser leur atomicité ou leur compensation avant exposition.

### Menaces et oracles

| Risque | Barrière attendue | Test futur |
|---|---|---|
| Instruction cachée dans un document/asset | Données externes traitées comme contenu, commandes autorisées séparément | Texte demandant une commande shell ne provoque aucune exécution |
| Écriture hors sélection | Validation des IDs, propriétés et région dans le domaine | Opération valide syntaxiquement mais hors portée refusée |
| Approbation périmée | Hash du diff et révision recontrôlés à l'application | Retouche manuelle entre approbation et Apply préservée |
| Téléversement indu | Droits et données sortantes affichés avant envoi | Asset privé exclu d'un envoi non autorisé |
| Coût ou retries sans borne | Plafond et compteur autoritaires | Arrêt au plafond, résultats intermédiaires conservés |
| Double livraison après interruption | IDs idempotents et reçus | Reprise après réponse perdue sans doublon |
| Dépendance permanente au fournisseur | Documents et assets locaux gouvernés | Ouvrir/éditer/jouer fournisseur absent |

### Correspondance officielle

| Critère | Fixture/oracle préparé |
|---|---|
| R6-P01 | Brief de l'aventure Gate 3 ; capability manquante signalée avant promesse |
| R6-P02 | Sélection d'une porte et région limitée ; diff et commandes ne débordent pas |
| R6-P03 | Retoucher porte + dialogue, demander variante locale, rouvrir ; overrides inchangés |
| R6-P04 | Changer la révision après preview ; plan refusé puis nouvelle approbation requise |
| R6-P05 | Interrompre avant/après reçu ; reprise unique, promotion liée aux permissions |
| R6-P06 | Job atteignant plafond, fournisseur affiché, données sortantes identifiées |
| R6-P07 | Comparaison même brief manuel/IA : temps actif, attente, corrections, coût et qualité ; fournisseur réel distingué des fixtures |
| R6-P08 | Réouvrir et jouer sans compte ou modèle ayant produit le contenu |

Inspection, recherche, carte, plan, dry-run, approbation, transaction, Undo, tests, captures, audit, quotas et absence de shell sont couverts par le parcours et les oracles ci-dessus. Aucun tarif, modèle ou protocole fournisseur n'est figé prématurément.

## P10 — Gate 7 : monde étendu et distribution

### Base constatée

Le [workflow Rust](../../../.github/workflows/rust-gate.yml) décrit des jobs Windows/Linux/WASM et construit le Studio avec `--no-bundle`. La [configuration Tauri](../../../apps/forge_studio/tauri.conf.json) porte `bundle.active=false` lors de l'inspection. La configuration d'un job ne prouve pas son exécution ni l'existence d'un installateur qualifié.

### Plan de mesure préparé

| Capacité | Fixture à construire après prérequis | Mesures/oracles |
|---|---|---|
| Streaming de cellules | Trajet aller/retour traversant plusieurs frontières | Attente, pop visible, mémoire stable, cellules libérées, absence d'état perdu |
| HLOD | Même scène proche/loin et transitions répétées | Silhouette, matériau, changement temporel, frame times et mémoire |
| Sauvegardes multi-zones | Objet déplacé et quête avancée dans deux zones | Reprise et changements de zone sans doublon ni remise à zéro |
| Packaging runtime | Copie propre sans workspace/Vault/upstream | Lancement hors ligne et dépendances explicites |
| Profils graphiques | Même trajet Low/Medium/High/Ultra disponibles | Paramètres réellement appliqués, stabilité et délais de changement |
| Lanceur | Installer, choisir module, jouer, gérer erreur de compatibilité | Parcours compréhensible et reprise après échec |
| Windows/Linux | Builds et smokes sur vrais environnements | Pas d'assimilation de Windows à preuve Linux ; hashes exacts |
| Licences agrégées | Module/code/assets avec provenance complète | Notices concordantes et absence de ressource de droit inconnu |
| Installateur | Installation, mise à jour interrompue, désinstallation | Sources/sauvegardes utilisateur préservées ; installation cohérente |
| Machine propre | Aucun cache ni outil de développement préalable | Démarrage, partie et reprise reproductibles |

Fiche de budget future : matériel, OS/pilote, résolution, profil, dimensions et densité de carte, nombre d'agents, taille des cellules, temps de chargement, moyenne/p95/1 % low, VRAM/RAM, pic de reconstruction, I/O et taille du package. Les cibles héritent des budgets de [PROJECT_CONTEXT](../../../PROJECT_CONTEXT.md) ; les nouveaux seuils seront fixés avant la mesure de la fixture.

Tests de panne : ressource absente, cache périmé, disque plein, chargement annulé, installation interrompue, module incompatible, retour dans une cellule modifiée. Le cache est régénérable ; la sauvegarde et le contenu d'auteur ne doivent pas l'être par supposition.

### Dépendances et exception existante

Gate 3 apporte la simulation et le delta ; Gate 4 apporte le module autonome. Les preuves visuelles/performance précédentes servent de baseline, avec revalidation des portions affectées. L'extension maritime possède désormais une autorisation spécifique [Tidewater](../TIDEWATER_INTEGRATION.md) et [ADR-0017](../../ADR/ADR-0017-tidewater-fidelity-integration.md). Le texte historique qui la disait différée ne doit pas rouvrir cette décision ; l'autorisation ne qualifie toutefois ni Gate 7 ni les assets du chantier pour tous les usages.

Cette préparation ne duplique aucun export, océan, shader ou packaging Tidewater déjà en cours.

## P11 — Gate 8 : autorité et réseau

### Proposition de responsabilités

| État/opération | Autorité proposée | Rôle du client | Preuve attendue |
|---|---|---|---|
| Admission et session | Serveur | Demande d'entrée et affichage du refus | Identité/compatibilité vérifiées avant simulation |
| Présence | Serveur | Affichage, demande de sortie | Déconnexion/reconnexion sans avatar dupliqué |
| Mouvement | Serveur pour état validé | Entrées et présentation ; prédiction éventuelle à décider | Corrections cohérentes et limites physiques |
| Combat et impacts | Simulation autoritaire | Intention et rendu | Aucun dégât accepté depuis une valeur inventée par client |
| Inventaire/transferts | Serveur transactionnel | Demande d'action | Deux prises simultanées d'un objet unique : un seul propriétaire |
| Quêtes/récompenses | Serveur | Choix et journal | Une récompense après retry/reconnexion |
| Changement de zone | Serveur | Demande, chargement et acquittement | État transféré une fois ; reprise en cas de chargement échoué |
| Sauvegarde campagne | Serveur | Demande si rôle autorisé | Snapshot cohérent ; aucun écrasement par client non autorisé |

Le client ne reçoit que l'information nécessaire à sa vue. Le transport ne doit pas contenir les règles de dégâts ou de quête. Les IDs et causalités Gates 3/5 doivent survivre à la réplication, sans persister des handles Bevy/Rapier.

### Matrice de tests préparée

| ID | Situation | Oracle |
|---|---|---|
| N01 | Deux clients et un serveur, processus distincts | Présence, mouvement et interaction concordants |
| N02 | Deux réseaux distincts | Admission et reconnexion réellement observées ; pas de promesse WAN fondée sur localhost |
| N03 | Latence, jitter, pertes, duplication et réordre | Aucun effet métier doublé ; corrections visibles et bornées |
| N04 | Deux clients ramassent le même objet | Un succès, un refus ; inventaires cohérents |
| N05 | Client modifie dégâts/vitesse/quantité annoncés | Serveur rejette ou recalcule selon ses règles |
| N06 | Déconnexion pendant récompense/transfert | Reprise unique, reçus conservés |
| N07 | Reconnexion avec état client ancien | Resynchronisation depuis autorité, pas d'écrasement du monde |
| N08 | Transition de zone interrompue | Une présence valide, inventaire/quête conservés |
| N09 | Version/module incompatible ou message malformé | Refus explicite avec consommation bornée |
| N10 | Arrêt et reprise du serveur | Campagne cohérente, conséquences persistantes |
| N11 | Saturation de requêtes et droits insuffisants | Quotas/permissions appliqués côté serveur |

Décisions encore ouvertes : mode d'hébergement, taille de groupe cible, transport, tick/réplication, budget bande passante, stratégie de prédiction, reprise de session et gestion des secrets. Elles seront choisies sur une tranche coopérative mesurée après stabilité solo. Aucun fournisseur réseau n'est sélectionné par ce dossier.

## Livraison

P09–P11 : responsabilités, scénarios, mesures et prérequis préparés pour tous les domaines des Gates 6/7/8. Aucun job IA, installateur, serveur, benchmark ou session réseau n'a été lancé ; ces résultats restent des spécifications prêtes à être utilisées lors de l'implémentation.
