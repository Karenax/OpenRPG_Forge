# ADR-0041 — Réglages d'affichage du jeu de module

Date : 24 octobre 2026. Périmètre : Gate 7, réalisation autorisée.
Statut : implémentée et vérifiée sur le module raster privé ; qualification du
candidat complet restant ouverte.

## Besoin et autorité

Un joueur doit pouvoir appliquer Faible, Moyen, Élevé et Ultra, puis retrouver
son choix au lancement suivant. Le jeu de module n'installe pas les sept domaines
du contrôleur historique de la vallée. Un sélecteur qui acquitte ce contrôleur
avec des ressources fictives ne prouverait aucun effet réel.

Les documents auteur, populations, collisions et sauvegardes de partie restent
les autorités existantes. Cette décision ajoute une préférence d'affichage, sans
créer de document de monde ou de version concurrente du produit.

## Politique et application

Le jeu compose une politique identifiée par hash, liée au terrain effectif et
aux quatre budgets. Il réutilise les valeurs de la table existante pour les
ombres et le post-traitement. Sur raster, la finesse cellules/HLOD passe par
la transaction bornée d'ADR-0036. La topologie proche de collision reste complète.
Sur terrain procédural, cette politique ne prétend pas changer sa topologie.
Faible utilise un échantillon et Moyen le MSAA à deux échantillons. Élevé et
Ultra utilisent la reconstruction temporelle, sans MSAA : les ombres de contact
de Bevy emploient un échantillonnage stochastique, dont le bruit reste visible
sur les surfaces courbes avec le seul MSAA. Le prépass des plantes reconstruit
déjà leurs véritables déformations précédentes pour les vecteurs de mouvement.
Les paramètres de contact par défaut restent conservés ; l'essai d'épaisseur
réduite et de pas supplémentaires n'a pas corrigé l'artefact et reste historique.

Les populations et niveaux source de la végétation restent conservés ; leurs
matériaux participent au rendu, aux ombres et au post-traitement du profil.
Les domaines absents, dont eau, météo et atmosphère auteur, ne sont pas simulés
pour acquitter un profil global. L'identité de cette politique reste distincte
de celle des sept domaines de la vallée. Qualifier le candidat complet et ses
domaines supplémentaires reste nécessaire avant de fermer R7-05.

L'application exige une caméra 3D active unique, le terrain admis, les réglages
observés des lumières/cascades et de la caméra, puis les ressources GPU. Une
demande possède une séquence propre, y compris une nouvelle application du même
profil ; un acquittement précédent ne vaut pas succès du nouvel essai.

## Ressources GPU indépendantes

`render_inventory` conserve son propriétaire principal exclusif, utilisé par
l'hôte de plantes. Il accepte au plus quatre inventaires supplémentaires,
chacun identifié par propriétaire, source, génération, durée de vie et vue.
Le jeu en utilise au plus trois : terrain, modèles et végétation gouvernée.
Un inventaire supplémentaire ne remplace jamais le reçu principal.

Les limites existantes par inventaire restent applicables. Les sources et
familles de matériaux demeurent typées ; des dépendances inconnues sont refusées.
L'absence d'un inventaire demandé, une ressource remplacée, une génération ou
vue périmée, une nouvelle époque du renderer ou un retrait invalident le reçu.

Les inventaires d'affichage vérifient aussi les prérequis GPU publics des effets
demandés, en réutilisant l'observateur existant : atlas à la résolution requise,
cascades, uniforms d'ombres de contact, bloom et pipeline/LUT de tonemapping.
Les profils temporels exigent également les composants de pipeline et d'historique
réellement préparés, les textures depth/mouvement mono-échantillon de la bonne
taille et l'absence de MSAA. Les champs internes de l'historique et de son id de
pipeline étant privés dans Bevy, l'observation ne prétend pas les lire ; la
stabilité globale des pipelines et la fence restent exigées avec ces prérequis.
Huit observations stables des pipelines et une fin de travail de la queue
postérieure à l'application sont nécessaires. La même exigence de fraîcheur
couvre le propriétaire principal des plantes lorsqu'elles sont présentes.
Ces observations ne constituent pas une lecture des pixels ou des timings GPU.

Le jeu suspend ses actuateurs tant qu'un adapter nécessaire n'est pas prêt.
Les deux barrières des réglages ne peuvent que maintenir une suspension ; elles
ne lèvent pas celle d'un autre adapter. Un timeout d'application est explicite.

## Préférences et interface

`graphics.json`, sous la racine de sauvegarde utilisateur, possède son schéma
et sa version. Sa lecture est bornée à 4 Kio. L'absence utilise Élevé ; après
application effective ce choix peut être enregistré. Une entrée invalide reste
intacte, ouvre les réglages et affiche l'échec de lecture. Seule une application
explicite autorise son remplacement. L'écriture prépare un fichier créé
exclusivement dans le même répertoire, le synchronise puis le renomme ; un échec
retire uniquement ce staging et conserve l'erreur visible.

L'état rendu et la persistance sont distincts : une sauvegarde de préférence
refusée n'invente ni un succès d'enregistrement ni une panne du rendu. Le rejeu
de qualification d'un profil exige néanmoins application et persistance exactes.

F2 ouvre les réglages. Les quatre choix, Appliquer et Fermer sont accessibles
au clavier (Tab/Shift+Tab, flèches, Entrée/Espace, Échap) et au pointeur. Les
boutons utilisent les rôles Bevy et des noms accessibles. Le panneau annule les
commandes joueur en attente ; sa fermeture ne reprend pas le contrôle sans
nouveau clic. Une récupération de partie refusée conserve sa priorité.

## Qualification

Les tests CPU portent sur le stockage, les refus, le clavier, les identités et
la révocation des reçus ; ils ne qualifient pas le renderer. Le protocole natif
doit rejouer les quatre profils sur le module installé, observer simultanément
les inventaires supplémentaires et principal, inspecter les captures, relancer
le même binaire pour relire la préférence, puis observer une entrée invalide
et sa réparation explicite. Les rapports portables rapprochent politique,
génération, inventaires, effets et fences sans dépendance du vérificateur vers
Bevy. La persistance et les performances restent des vérifications séparées.

Le rapprochement `module-quality-validation-001.json` passe sur les entrées 007 :
1 153 exécutions de tests, contrôles stricts natif/livraison/WASM, quatre rejeux
natifs retenus, onze captures inspectées et entrées stables. La correction du
bruit de contact est mesurée dans `module-quality-visual-003.json`. Cela qualifie
ce raccord sur le module privé ; le candidat complet, ses transitions et ses
performances restent dus.

Campagne unique : `../Preuves-et-analyses/actives/gate7-monde-distribution/`,
préfixe `module-quality-*`, budget initial de 32 Mio / 192 fichiers, étendu à
64 Mio / 192 fichiers pour préserver les premières captures et rejouer la
correction du bruit de contact observé visuellement en Élevé/Ultra.
Réutiliser le module installé et le cache commun ; aucune nouvelle VM ou copie
de module n'est requise par cette tranche. Gate 7 reste ouverte.

## Extension du 26 octobre — Objets source et capacité des inventaires

L'ajout des matériaux source des objets API 7 porte à quatre les familles
d'affichage possibles. L'environnement et l'herbe conservent leurs propriétaires
indépendants : la capacité initiale de quatre inventaires supplémentaires est donc
portée à six. Les limites par inventaire, la séparation du propriétaire principal,
les générations, les refus de dépassement et les fences restent exigés. Le premier
rejeu refusé et le rejeu corrigé sont conservés sous `module-object-runtime-*`.
Les erreurs graphiques bloquantes terminent aussi l'automatisation en échec.
Cette extension ne qualifie ni la fidélité globale ni les performances.

## Extension du 3 novembre — Réglages communs personnalisés

Les quatre profils et les 20 contrôles employés par Tidewater sont définis dans
`forge_engine::source_graphics`. Le jeu de module les propose dans son panneau
F2 et applique une demande commune au moteur. Les domaines absents de la zone
sont indiqués sans effet ici. La désactivation de l'anticrénelage temporel
désactive aussi les ombres de contact qui en dépendent.

Une personnalisation utilise `graphics-custom.json`, distinct de l'ancien
`graphics.json`. Le nouveau fichier référence le hash de l'ancien : si une
autre version le modifie, les deux fichiers sont conservés et le joueur doit
appliquer un choix pour résoudre le conflit. Un profil choisi explicitement
après cette alerte peut remplacer la personnalisation dans le nouveau fichier.
Sans personnalisation préexistante, les profils continuent d'utiliser l'ancien
format. Les reçus historiques limités aux profils ne certifient pas les valeurs
personnalisées ; la vérification du rendu réel et de l'interface reste à faire.

Le runtime prépare aussi par petits lots des tuiles terrain et eau dans la
direction de déplacement, sans modifier la scène visible. Le moteur limite
chaque lot et conserve un inventaire GPU distinct. Un remplacement peut retirer
des tuiles périmées seulement si les sources encore utilisées sont couvertes,
que la publication courante est revérifiée et qu'une clôture GPU ultérieure
est disponible. Une demande remplacée est annulée sans retirer son successeur.
Le contrôle strict existant, qui exige de conserver toutes les anciennes sources,
reste disponible pour les autres consommateurs. La mesure d'un seul parcours de
développement ne qualifie ni les performances générales ni la parité Tidewater.

La règle d'exclusion entre FXAA et TAA lors d'un changement interactif appartient
aussi à `forge_engine::source_graphics::Control` : le dernier mode activé
désactive l'autre, dans Tidewater comme dans le jeu de module. La lecture des
valeurs persistées n'est pas modifiée par ce transfert.
