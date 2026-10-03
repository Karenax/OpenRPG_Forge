# ADR-0019 — Domaine gameplay et autorité d'auteur Gate 3

Date : 2026-10-01
Statut : décision d'implémentation dans la portée Gate 3 autorisée (ADR-0018)

## Décision

Une crate `forge_gameplay` contient les définitions et les règles moteur-neutres.
Elle ne dépend que des primitives Forge et des codecs déjà présents. Ni Bevy,
Rapier, Tauri, Leptos ni JavaScript ne définissent une règle de simulation.

Le `GameplayDocument` d'une zone appartient à `ZoneCorrections.gameplay`.
La présence de ce champ adopte la version 13 de la tête ; son absence préserve
les anciens hashes. Création, édition de répliques, objets, quêtes et lieux
utilisent le protocole `ZoneRequest` existant : Preview/Apply, révision/hash,
inverse, audit et retry. Il n'existe pas de second fichier gameplay d'auteur.
Les identités persistent via les types Forge, jamais par indices ou handles ECS.

L'état de partie est séparé : ressources, inventaire, objets consommés,
progression, portes, destruction, combustion et journal d'actions dédupliquées.
Chaque session est liée au hash de la source de playtest approuvée. La simulation
ne modifie ni la carte Tidewater originale ni son historique d'édition.

Le moteur convertit les positions, visibilités et contacts physiques observés
en observations et événements domaine. Une commande de poussée/lancer produit
une intention d'impulsion ; seul l'adapter Rapier applique la physique. Un
contact confirmé demeure un contact, même si la protection absorbe ses dégâts.
Les journaux et réactions ont des quotas explicites ; leur dépassement refuse
l'opération atomiquement au lieu d'abandonner silencieusement la déduplication.

## Profil minimal et sauvegarde

Le profil Gate 3 expose explicitement portée, dégâts de base, coût et maximum
d'endurance, protection et vitalité, quotas d'inventaire et d'événements. Il ne
revendique pas la couverture complète du ruleset systémique ni du manuel proposé.
Les valeurs sont des données du module. Aucun test ou règle ne branche sur le
nom de Tidewater ou sur les IDs de la quête de démonstration.

La sauvegarde JSON versionnée lie module, zone, hash source et signature
fonctionnelle. Une reprise exacte est la valeur par défaut. La reprise après
retouche compatible est explicite : texte et pose initiale d'une porte peuvent
changer sans perdre choix, inventaire, quête ou état ouvert/fermé ; changer ou
supprimer une identité/règle/référence refuse la reprise avec diagnostic.
Les contrôles spatiaux du moteur restent requis avant activation réelle.
Une retouche compatible invalide l'ancienne observation physique ; commandes
et nouvelle sauvegarde attendent un frame de la source courante. Les reprises
exactes restaurent les orientations quaternion des objets. Une sauvegarde est
refusée pendant un mouvement linéaire ou angulaire, ou tant qu'une commande ou
une conséquence moteur est en attente, afin de ne pas perdre une impulsion.
La sauvegarde native utilise le stockage atomique existant sur un répertoire
de sauvegarde distinct de l'archive d'auteur.

## Limites initiales

Les contrats et tests de domaine ne sont ni un parcours Studio ni une preuve
de navigation, physique, rendu ou revue humaine. La navigation reste soumise
aux conditions ADR-0009 jusqu'à décision fournisseur et benchmark valide.
Le passage de Gate 3 exige les preuves de chaque critère, suivant le plan et
ADR-0006 ; cette décision structurelle ne coche aucun critère runtime.
