# Gate 5 — état de qualification du 20 octobre 2026

**Gate clôturée localement le 22 octobre 2026.** Voir
[le rapport de clôture](GATE5_CLOSURE_20261022.md) et ADR-0034. Cette matrice
conserve les preuves et les états intermédiaires de la campagne.

Le [rapprochement final du 21 octobre](GATE5_LOCAL_QUALIFICATION_20261021.md)
conclut au PASS local des treize domaines et R5-P01/P02. La CI distante du
candidat reste NON EXÉCUTÉE ; la décision propriétaire du 22 octobre retire
les contrôles GitHub des critères des gates et permet la clôture locale.
Le complément du 22 octobre examine le run publié `39664002787` : WASM et
supply-chain réussissent, Windows échoue par manque d'espace et Linux perd
la communication avec son runner. Ce résultat ne couvre pas les changements
locaux Gate 5. Voir le rapport final et le ZIP vérifié
`gate5-ci-review-20261022-e-001.zip` dans la campagne centrale.
Les états partiels et refus ci-dessous restent des jalons historiques lorsqu'ils
sont complétés par les reçus plus récents de la synthèse courante.

## Complément — 21 octobre, parcours de présentation

**Lisibilité demandée par le propriétaire : travail en cours.** Une vraie porte
de grange Villa Forge est importée par l'interface native, prévisualisée en 3D,
puis liée à la porte physique et sauvegardée. Le reçu central
`door-library-import-e-001.receipt.json` vérifie douze fichiers, le SHA-256 du
modèle de 65 562 396 octets, la conservation des six assets précédents et du
gameplay lors de cette liaison. Usage privé local, redistribution interdite ;
la date de copie dans la collection ne prouve pas la date d'achat originale.
Le modèle est ensuite ajusté par les nouveaux réglages génériques et observé
dans le jeu ; le reçu de placement ci-dessous complète ce reçu d'import
historique. Aucun verdict esthétique n'est acquis.
Les répétitions du script de test passent ensuite de 4/8 à 1/1 par le Studio,
sans changement des identifiants, permissions ou opérations, pour éviter les
32 messages pendant les prochains contrôles visuels. Les reçus des quotas
et du franchissement conservent leurs sources et verdicts historiques.

Les réglages génériques de position, échelle XYZ et rotation relative du modèle
lié sont ajoutés au parcours existant Examiner/Appliquer/Annuler/Sauvegarder,
sans modifier le corps physique. Trois tests ciblés réussissent ; Clippy natif
et WASM strict réussissent après extraction du sous-composant. Le candidat
`visual-model-controls-candidate-e-001` termine le 21 octobre à 18:56:57 UTC
avec le verdict `BUILD_PASS_RUNTIME_NOT_VERIFIED` : 2 498 tests workspace
réussis, zéro échec et 56 ignorés sur 262 cibles ; douze tests de présentation
native, Clippy natif/WASM, formatage et constructions frontend/jeu/Studio
réussis. Les entrées compilables sont stables entre les deux manifestes.
Le reçu central conserve les empreintes des trois artefacts et son verdict de
construction historique. Le Studio courant utilise ensuite ce candidat après
vérification des empreintes ; les preuves graphiques suivantes sont distinctes
du reçu de construction. La clôture de gate n'est pas acquise.

Un tonneau complet Dungeon Builder est également retenu après consultation de
son aperçu et vérification du GLB existant (3 380 044 octets, SHA-256
`be47d05b6a29178214041c2be8691789d3f45ec7465260719783e2157c0199ee`).
Le propriétaire effectue la sélection manuelle dans le dialogue Windows/WebView.
L'import privé natif est ensuite appliqué : sept fichiers vérifiés dans
`barrel-library-import-e-001.receipt.json`, sept anciens assets conservés.
Le tonneau est lié au conteneur existant et ajusté ; la bibliothèque source
reste inchangée et la redistribution demeure interdite.

`library-model-placement-e-001.receipt.json` conserve douze fichiers vérifiés
(3 401 133 octets) : échelle nulle refusée, deux aperçus sans cumul de l'angle,
Annuler/Rétablir du placement de porte, puis fermeture/réouverture de la carte
avec les décalages et échelles exacts des deux modèles. La rotation de porte
enregistrée reste à 90 degrés autour de Y ; l'angle relatif du formulaire
revient à zéro. Le gameplay, les corps physiques, les autres corrections,
les trois anciens modèles, les autres zones et modules restent identiques.
Les modèles sont réellement chargés dans le jeu, avec cinq liaisons visuelles
prêtes et aucun pipeline en échec ; le tonneau permet le transfert d'items
et le script ouvre la porte. La scène comporte encore des représentations
génériques pour les acteurs et cairns ; aucune acceptation visuelle n'est acquise.

`ui06-ui09-ui10-runtime-e-001.receipt.json` conserve 31 fichiers vérifiés
(2 988 361 octets) et quatre sorties normales sur ce candidat. UI09 et UI10
passent localement : sauvegarde à 96,333 secondes avant la première borne,
activation unique à 100 secondes après reprise, défaite des deux participants,
réarmement à 500 secondes et reprise sans doublon. Une nouvelle défaite avant
600 secondes ne réarme pas à cette borne, le délai de 25 minutes n'étant pas
écoulé depuis 500 secondes ; le réarmement suivant a lieu à 700 secondes.
Les cycles d'événement sont 0, 4 et 6, avec identités conservées et vitalités
50/60 restaurées. La solution alternative existante consomme ensuite deux
jetons et donne une seule marque ; la reprise conserve les reçus, la transition
et cette récompense sans nouveau choix de restitution. L'autre solution dans
une partie fraîche distincte est ensuite couverte par le reçu suivant. Les
sources sont inchangées par ces parties ; aucune quête n'est créée ou enrichie.

`ui06-two-paths-runtime-e-001.receipt.json` complète UI06 avec douze fichiers
vérifiés (509 064 octets). La nouvelle partie commence à séquence zéro,
inventaire vide et transition disponible. Le sceau ramassé ne suffit pas à
proposer la restitution avant la défaite du pillard ; après trois attaques,
le choix devient disponible, consomme un sceau et donne une seule marque.
La solution de restitution est enregistrée, variable alternative toujours
fausse et deux jetons restés au tonneau. Après reprise, les sept reçus,
la solution et la marque sont identiques, sans nouvelle proposition de
récompense. Deux sorties normales. Les deux chemins préexistants ont donc
chacun leur partie indépendante et leur reprise ; **UI06 passe localement**.

`ui03-danger-runtime-e-001.receipt.json` conserve quatorze fichiers vérifiés
(2 459 288 octets). Un objet fixe inflammable sans dégâts de chaleur est ajouté
par le formulaire physique. Après l'activation des deux acteurs à 100 secondes,
la sentinelle configurée pour fuir s'éloigne de 3,497 m ; le pillard reste sur
place malgré une observation de danger avec visibilité vraie. Les vitalités
restent à 60 et 50. Hors de portée, la sentinelle cesse de fuir ; la reprise
conserve sa position, le feu et l'unique reçu d'allumage. Deux sorties normales,
sources inchangées par le jeu, données narratives conservées. UI03 passe
localement ; les représentations des acteurs et de ce témoin restent génériques.

`ui07-perception-runtime-e-001.receipt.json` conserve dix-huit fichiers
vérifiés (2 575 268 octets), les définitions de deux factions, leurs
appartenances et une relation initiale orientée à +20. Le même témoin garde
son opinion initiale porte fermée, passe à +10 après l'agression visible
porte ouverte, puis reste à +10 lors d'une nouvelle agression porte refermée.
La victime réagit à chaque agression avec une cause distincte ; aucune opinion
n'est propagée à toute la faction. La sentinelle masquée par le joueur ne
réagit pas. La reprise conserve les opinions et les cinq reçus, sans coût
supplémentaire ; deux sorties normales sont vérifiées. Le runtime UI07 passe
localement ; la fermeture/réouverture des définitions reste à terminer.

`ui01-ui07-r5-persistence-e-001.receipt.json` conserve trente fichiers vérifiés
(3 139 197 octets). Les deux zones rouvertes affichent le même profil version 1
à 5 m, avec unité et aide ; ce rapprochement complète l'historique et le
contraste 4/5 m déjà prouvés pour UI01. Les factions et leur relation +20 sont
retrouvées après réouverture : UI07 passe localement. Le masquage de cette
surface est sauvegardé, relu dans les deux zones, puis réaffiché sans perte
de contenu ni des capacités requises. B conserve son profil à 3 m et masque
les scripts. Sa première configuration commune ajoute le champ Creator et
passe le schéma gameplay de 1 à 2 ; aucune autre donnée de contenu ne change.
Le même Studio et le même runtime jouent A, B, puis reprennent A avec les
reçus, objets et transitions propres à A. Le refus de reprise stricte après
changement de source est conservé ; la commande explicite de reprise compatible
réussit avec une empreinte fonctionnelle inchangée.

`r5-two-module-reexport-e-001.receipt.json` conserve dix-sept fichiers vérifiés
(141 067 605 octets). Les deux fichiers `.orpmod` sont créés par l'interface
native, en usage local privé ; le validateur admet les trois zones exportées
et rapproche leurs archives exactes, profils communs, assets et origines.
A conserve huit assets, B deux. La porte et le tonneau conservent leurs hashes
et leur interdiction de redistribution. R5-P01/P02 passent localement ; aucune
publication ni qualification distante n'est déduite de ces résultats.

Synthèse courante des parcours graphiques ; les contrats automatisés restent
des preuves distinctes. « Partiel » conserve tous les critères encore manquants.

L'audit `capture-dimensions-audit-e-001.json` vérifie les dimensions des 82
captures présentes directement dans la campagne F:, sans parcours récursif.
81 ont une taille de fenêtre exploitable ; la seule icône 16 × 16 est déjà
écartée par l'addendum du reçu de porte. Cet audit de fichiers ne valide pas
leur contenu ni les critères fonctionnels. Certaines captures sont encodées en
JPEG malgré leur extension `.png` ; leurs octets et empreintes sont conservés.

L'audit séquentiel `gate5-ui-receipt-integrity-e-001.json` contrôle ensuite les
manifestes `files` de 11 reçus graphiques : 160 références, 160 fichiers uniques,
21 013 431 octets, aucun écart de taille ou de SHA-256. Les 19 autres reçus,
sans ce manifeste, sont explicitement exclus. Rapport et script sont conservés
dans la campagne centrale F:. Ce contrôle ne valide ni le contenu des captures,
ni les comportements, ni l'équivalence des candidats ; aucun verdict fonctionnel
ou de clôture ne change.

Le rapprochement `gate5-candidate-current-inputs-e-001.json` compare ensuite
5 147 entrées des chemins compilables au manifeste final du candidat
`event-quota-remedy-candidate-e-001` : deux inventaires identiques, aucune
différence, trois empreintes binaires conformes (Studio, frontend WASM, jeu).
Le rapport et son script sont conservés sur F:. Les documents et données de
qualification ne font pas partie de cette équivalence des entrées compilables ;
les parcours graphiques restants demeurent exigés.

| Domaine | État local courant |
| --- | --- |
| UI01 — règles communes | PASS local ; historique, contraste en jeu, réouverture des deux zones et profil identique |
| UI02 — blueprints | PASS local |
| UI03 — acteurs/comportements | PASS local ; contact/dégâts, fuite différenciée face au feu et reprise |
| UI04 — items/conteneurs | PASS local |
| UI05 — dialogues conditionnels | PASS local |
| UI06 — transitions/journal/récompense unique | PASS local ; deux chemins dans des parties indépendantes, consommation, journal et récompense unique après reprise |
| UI07 — factions/perception | PASS local ; perception physique, conséquences personnelles, reprise et définitions retrouvées après réouverture |
| UI08 — capacités/effets | PASS local, expiration exercée |
| UI09 — calendrier | PASS local ; borne unique après sauvegarde/reprise |
| UI10 — rencontres | PASS local ; apparition, défaite, délai bloquant, réarmement et reprise sans doublon |
| UI11 — cinématiques | PASS local |
| UI12 — scripts | PASS local ; permissions, quotas CPU/événements, messages, répétitions, porte physique et reprise prouvés |
| UI13 — diagnostics | PASS local sur les refus de permission et de budget, lien vers le nœud, correction et replay |
| R5-P01/P02 — modules, surfaces et réexport | PASS local ; deux profils, masquage/réouverture sans perte, A→B→A, sauvegardes isolées, réexports privés admis |

**UI09 : reprise de l'horloge observée, preuve d'échéance encore partielle.**
Le complément d'édition `ui10-authoring-e-001.receipt.json` prépare le contrôle :
une seule rencontre est ajoutée par le Studio à l'événement existant, avec
les deux acteurs déjà présents, condition Toujours et réarmement après défaite,
délai de 25 minutes pour une période de 20 minutes. Trois fichiers vérifiés
conservent l'aperçu et la source ; l'égalité des autres données de zone, zones
et modules est contrôlée. Ce reçu ne prouve pas encore l'exécution en jeu.

La durée d'une minute passe de 1 000 à 5 000 ms via le Studio pour isoler les
bornes. La partie sauvegardée à 34,033 s reprend avant la première échéance
de 100 s ; les relevés suivants donnent 63,800 s puis 139,683 s, cohérents avec
l'affichage 08 h 06, 08 h 12 et 08 h 28. Deux sorties normales, séquence zéro,
aucune erreur de simulation ni refus de reprise. Le reçu
`ui09-clock-partial-e-001.receipt.json` centralise quatorze fichiers vérifiés
sur F:. Aucun PASS n'est prononcé : un événement de calendrier sans rencontre
liée ne laisse qu'une sortie transitoire de frame, absente des relevés tardifs.
Le champ `simulation.activations` conserve les activations de rencontres, pas
un historique des événements de calendrier. Le contrôle suivant doit lier une
rencontre minimale à l'événement pour prouver durablement la borne unique.

**UI12 : porte réelle ajoutée et ouverture par script observée ; parcours encore partiel.**
Le Studio ajoute une porte fixe minimale, initialement fermée, puis ajoute une
permission explicite et une seule instruction d'ouverture au programme existant.
Les autres objets et domaines de gameplay restent identiques ; aucune quête
n'est créée ou enrichie. Le contrôle du brouillon réussit. Dans le jeu, deux
avancées au contact du battant fermé donnent Z = 2,960111 puis 2,960110 m,
soit moins de 0,002 mm d'écart. Une exécution du script retire visiblement le
battant : 39 instructions, 32 messages dans 35 événements, dont un seul
`door_changed`, pour une transaction. Après reprise du contrôle du personnage,
une pression avance de 6,605 mm au-delà de la limite fermée.
`ui12-door-runtime-partial-e-001.receipt.json` centralise douze fichiers vérifiés
sur F:. Cette courte avancée ne prouve pas le franchissement complet : celui-ci,
la sauvegarde/reprise avec le même reçu et la sortie normale de cette partie
restent à vérifier. Aucun PASS UI12 n'est prononcé. Le reçu d'édition
`ui12-door-authoring-e-001.receipt.json` et son addendum conservent la source
et la capture complète ; l'image initiale de 16 × 16 pixels était une icône
d'application et ne sert pas de preuve visuelle.
Le complément `ui12-door-resume-e-001.receipt.json` apporte ensuite la reprise :
porte toujours ouverte, séquence 1 et reçu strictement identique, endurance 100,
aucun nouvel événement de script. Les deux parties sortent normalement, sans
erreur de simulation ni refus de reprise, avec projection prête. Huit fichiers
supplémentaires sont vérifiés sur F:. Seul le franchissement complet reste
manquant pour ce parcours ; le verdict UI12 demeure partiel.

Le contrôle `ui12-door-crossing-e-002.receipt.json` lève ensuite cette dernière
réserve. Dans une partie fraîche, le script ouvre la même porte une fois. Le
déplacement clavier du propriétaire conduit de (0 ; 3) à (0,287373 ; 2,379531),
puis (0,639897 ; 0,546702) en X/Z : le premier relevé passe la face éloignée
dans la largeur du battant, puis toute la capsule de rayon 0,35 m est au-delà
de cette face Z = 2,4 m. La reprise retrouve cette position, la porte ouverte,
la séquence 1 et le reçu strictement identique, endurance 100. Deux sorties
normales, projection prête, aucune erreur ni refus de reprise ; douze fichiers
sont vérifiés sur F:. Une tentative précédente, orientée latéralement par la
caméra, ne sert pas de preuve de franchissement. **UI12 passe localement** ;
Gate 5 reste ouverte. Le propriétaire demande ensuite une scène lisible avec
les assets de la bibliothèque ; la représentation géométrique actuelle ne
constitue aucune acceptation visuelle.

**UI12/UI13 : diagnostic de budget corrigé et rejoué dans le nouveau candidat.**
`event-quota-remedy-candidate-e-001` termine avec succès le 21 octobre à
15:14:54 UTC : 2 495 tests workspace réussis, zéro échec, 56 ignorés sur 262
cibles ; Clippy strict workspace et WASM, formatage et builds natif/WASM réussis.
Les douze tests natifs de présentation et les six contrôles de frontières
moteur/contenu passent aussi. Le manifeste avant/après confirme les entrées
compilées inchangées pendant la séquence. Ces contrôles restent des preuves
machine locales, distinctes du parcours graphique et de la CI distante.

Le Studio reconstruit reprend le même programme ; le dépassement d'événements
est maintenant classé `Budget`, avec cause précise et remède invitant à réduire
les répétitions. Le lien de retouche sélectionne le message fautif
`8ef67897-f95a-53f3-a6eb-8cdca038f81c`. Brouillon et diagnostic ne changent pas
les sources avant application. Le jeu natif confirme ce diagnostic et son refus
atomique : séquence zéro, aucun reçu et variable fausse après sauvegarde.
La correction de la seule boucle externe à quatre passe dans le Studio et le
jeu : 38 instructions, 32 messages dans 34 événements et une seule transaction.
Après reprise, le reçu reste strictement identique, sans réexécution ni coût.
Les trois sorties sont normales, sans erreur de simulation ; sources auteur et
autre module restent inchangés par le jeu. `ui12-event-e-002.receipt.json`
rapproche vingt-deux fichiers vérifiés sur F:. La réserve du remède générique
est levée ; UI12 reste partiel pour la porte physique, sans clôture de Gate 5.

**UI12 : quota d'événements et messages validés localement sur le candidat précédent.** Le formulaire
retouche le nœud terminal en message minimal, avec répétitions 16/8 : 128 messages
plus l'affectation et le reçu d'exécution dépasseraient les 128 événements permis,
alors que les 146 instructions prévues restent sous le quota CPU. Vérificateur
et jeu refusent la sortie ; sauvegarde avec variable fausse, séquence zéro et
aucun reçu. La correction ne change que la boucle externe, de 16 à 4 : le jeu
exécute 38 instructions et retient exactement 32 messages dans 34 événements,
pour une seule transaction. Après reprise, ce même reçu reste identique, sans
nouveau coût ni doublon. Trois sorties normales, sources et autre module
inchangés par les parties ; `ui12-event-e-001.receipt.json` rapproche dix-neuf
fichiers vérifiés sur F:. Le diagnostic de ce premier candidat classe ce refus
`GameApi`, avec un remède générique : la correction typée du code passe ensuite
les 127 tests gameplay et Clippy strict du composant sur toutes ses cibles
(`event-quota-remedy-targeted-e-001.receipt.json`). Le contrôle de formatage
workspace passe aussi. La reconstruction du candidat
`event-quota-remedy-candidate-e-001` est alors lancée avec contrôles workspace,
natif et WASM ; son parcours graphique restait à rejouer avant de lever cette
réserve ; le complément `e-002` ci-dessus
apporte maintenant cette preuve. La porte physique reste ouverte ; aucune
clôture de UI12 ou de Gate 5 n'est prononcée.
Les six contrôles de frontières moteur/contenu sont également rejoués avec
succès dans `event-quota-remedy-engine-boundaries-e-001.log` et son reçu.

**UI12 : quota CPU et répétitions validés localement.** Le même programme est
étendu via le formulaire avec deux répétitions imbriquées de 16 itérations autour
de la condition. Le vérificateur et le jeu refusent le dépassement du budget à
l'instruction conditionnelle : la sauvegarde conserve séquence zéro, aucun reçu
et variable fausse malgré l'affectation placée avant les boucles. La correction
via le Studio ne change que les nombres d'itérations, de 16/16 à 4/8, avec les
mêmes identités et permissions. Vérification réussie puis exécution réelle de
38 instructions, variable vraie, une seule transaction ; sauvegarde/reprise
conserve exactement ce reçu, sans réexécution. Les trois jeux se ferment avec
succès, sans erreur de simulation ; sources et autre module restent inchangés
pendant leur exécution. `ui12-cpu-e-001.receipt.json` rapproche dix-neuf fichiers
vérifiés sur F:. Le quota d'événements, le message et la porte physique restent
à qualifier ; UI12 et Gate 5 restent ouverts. Aucune quête n'est créée.

**UI13 : parcours local réussi ; UI12 reste partiel.** Le formulaire compose
un programme de deux instructions : affectation booléenne, puis comparaison de
la même variable. L'écriture est autorisée, la lecture volontairement absente.
Le vérificateur désigne l'instruction `8ef67897-f95a-53f3-a6eb-8cdca038f81c`,
explique le refus et son remède ; « Retoucher l'instruction signalée » sélectionne
la branche conditionnelle correspondante. Le runtime confirme le même refus :
après sauvegarde, séquence zéro, aucun reçu retenu et variable toujours fausse,
donc la première écriture n'a pas été conservée partiellement.
La seule correction ajoute la permission de lecture, sans changer les deux IDs
d'instructions ni leurs données. Le vérificateur réussit, puis le jeu accepte
une unique exécution : variable vraie et reçu `script_executed` de deux étapes.
Sauvegarde/reprise conserve ce même reçu, la séquence 1 et le choix conditionnel
absent, sans réexécution. Sources auteur et second module inchangés par le jeu.
Voir `ui12-ui13-diagnostic-e-001.receipt.json` et vingt et un fichiers vérifiés
sur F:. Les quotas CPU/événements, répétitions/messages et porte physique restent
à qualifier pour UI12 ; ce succès ne les remplace pas. Le collecteur a nécessité
une correction syntaxique PowerShell avant exécution, sans changement des
observations, archives ou sauvegardes testées.

Le saut et l'interruption cinématiques sont aussi rejoués chacun dans une partie
fraîche distincte, avec uniquement deux actions de présentation par partie,
endurance 100, inventaire vide, quête disponible et aucune récompense.
`ui11-independent-e-002.receipt.json` et ses huit fichiers vérifiés complètent
la fin naturelle et la reprise intermédiaire du premier reçu. Les deux sorties
réussissent sans erreur de simulation et les sources restent inchangées.

**UI08 : parcours local réussi après calibration et rejeu.** Le formulaire du
Studio retouche uniquement la durée de l'effet existant de 5 000 à 60 000 ms,
avec le même ID ; l'aperçu laisse les sources inchangées, puis Appliquer/Save
enregistre la retouche. La protection réduit une attaque de contact de 20 à
10 points. Le second usage est refusé (`effect is already active`), sans coût
supplémentaire ni progression de séquence. Après expiration, la même attaque
inflige de nouveau 20 points. Une première observation après reprise arrive
après expiration : elle est conservée comme telle. La seconde sauvegarde,
prise plus tôt, reprend avec le même effet actif, sa cause et son échéance
inchangés ; l'endurance reste à 80 et la séquence à 4 avant/après chargement,
puis l'effet disparaît après sa borne sans nouvelle consommation. Les sources
et l'autre module restent inchangés pendant ces parties. Le reçu
`ui08-runtime-e-002.receipt.json` rapproche dix-sept fichiers vérifiés sur F:.
Le cas exerce l'expiration ; aucune annulation explicite n'est revendiquée.
Cette preuve complète l'état partiel UI08 décrit plus bas.

La consigne propriétaire recentre explicitement la campagne sur la construction
et la validation des fonctionnalités génériques. Aucun enrichissement de quête
ou d'aventure n'est requis ; les données isolées servent uniquement de tests.

**UI02 : parcours local réussi sur le candidat final actuel.** Le reçu
`ui02-final-reopen-e-001.receipt.json` et ses captures rapprochent la réouverture
des deux instances du blueprint v2 : vitalité héritée 50 sans fuite, override
local 60 avec fuite. Les IDs, liaisons et sources restent inchangés par la sélection.

**UI11 : parcours local réussi.** Dans le jeu séparé lancé depuis le même Studio,
les deux plans apparaissent dans l'ordre retouché, avec caméras et locuteur
distincts. Une sauvegarde à 6,517 secondes du premier plan reprend en cours de
séquence. Fin naturelle, saut et interruption rendent la caméra et les actions
au jeu ; une attaque physique acceptée après interruption inflige 20 points.
Les reçus conservent les raisons distinctes `skipped` et `interrupted`.
Les acteurs, inventaires, objets, quêtes et variables sont identiques entre
la sauvegarde intermédiaire et celle après les contrôles, hormis le tassement
physique des positions inférieur à 0,1 mm après reprise, enregistré séparément.
Les sources auteur restent inchangées. Voir `ui11-runtime-e-001.receipt.json`,
ses quinze fichiers vérifiés et `gate5-ui11-retain-e-001.ps1` dans la campagne
centrale. Ce succès est local Windows, sans acceptation humaine implicite.

**UI08 : partiel.** L'utilisation de la capacité consomme exactement cinq points
d'endurance et enregistre un effet expirant 5 000 ms après son application.
L'endurance reste à 95 après reprise, sans nouvelle consommation. Le calcul
des dégâts protégés, le refus atomique/stacking et la reprise d'un effet encore
actif restent à exercer ; les données ne suffisent pas à qualifier ce domaine.
**UI03 reste partiel** : contact et dégâts prouvés, comportement de fuite encore
à observer face à un danger physique approprié. Une attaque ne prouve pas la fuite.
Les autres réserves restent ouvertes ; Gate 5 n'est pas clôturée.

## Dernier état — 21 octobre, après 11:30 UTC

Le candidat `container-feedback-candidate-e-001` termine à **11:10:34 UTC**,
code 0, avec entrées compilées inchangées. Tests workspace : **2 495 PASS,
0 FAIL, 56 ignorés, 262 cibles** ; Clippy strict natif/WASM et format passent.
Frontend/worker, runtime et Studio sont construits. Les six contrôles Python
des frontières moteur/contenu passent également. Studio SHA-256
`47e266177db38a00e9dcdb588c654b7a04fbfdce3cc026f08b5fb917506fc3f8`,
runtime `e527ba258f9fd2a03eb5807916b64c2da167c0a53fe61467da74f9015c4e8c2b`.
Le lancement vérifie tous les artefacts et les **3 025 pins frontend**.

**UI04/UI05 : parcours locaux réussis** sur ce candidat. Le jeu affiche
`action refused: item excluded by container filter`, conserve le sceau et les
deux jetons, accepte retrait de deux jetons puis dépôt d'un, et reprend un jeton
dans chaque inventaire. La variable de reconnaissance passe à vrai ; le choix
conditionnel disparaît et reste absent après reprise. Reçu commun
`ui04-ui05-final-replay-e-001.receipt.json`, données sources inchangées.

**UI06 : solution alternative réussie, parcours global encore ouvert.**
Deux jetons consommés, une seule marque attribuée, journal terminé et solution
`0d1a3cf5-64ca-527a-91b3-717408253cdc` enregistrée ; après reprise, séquence 7,
une seule marque, aucun nouveau choix de récompense et sauvegarde inchangée.
Voir `ui06-accord-e-001*` et `ui06-accord-resume-e-001*`. L'autre solution,
dans une partie fraîche distincte, reste à exécuter.

**UI03 : ouvert.** Les cibles à environ 1,8 m sont à portée mais sans contact
physique ; l'attaque est correctement refusée. La capture
`ui03-contact-refused-e-001.png` conserve ce résultat. Une pression Z normale
déplace le joueur de 0,0067 m dans la sauvegarde : aucun blocage de commande
n'est conclu sur l'absence de changement visible à cette échelle. La calibration
du contact doit être poursuivie sans affaiblir les gardes physiques.

Capacités, horloge et cinématique ont leurs préparations graphiques sauvegardées
et leurs reçus F:, sans qualification complète de leur comportement en jeu.
UI01/UI02 conservent leurs preuves précédentes et les réserves de rejeu final ;
UI07 à UI13 et R5-P01/P02 restent ouverts selon les détails ci-dessous.
Les paragraphes suivants retracent les étapes historiques, avec leurs verdicts
à la date de chaque tentative. Les nouvelles preuves sont physiquement sur F:.

## Historique de la campagne

État des preuves complété après la maintenance du stockage : le replay global
002 est interrompu sans reçu terminal, ses processus étant absents. La reprise
`gate5-native-workspace-tests-e-001.log` termine à 22:19 UTC, code 0 :
**2 489 PASS, zéro échec, 56 ignorés**, sur 262 cibles. Les builds graphiques
et leurs parcours restent à acquérir.
Le checkout, le cache partagé et les temporaires sont sur E:. Les nouvelles
preuves vont dans `Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
Les journaux anciens conservent leurs chemins et hashes. Aucun push n'est implicite.

Clippy natif E: 002 passe à 22:33 UTC sur son inventaire. Le séquenceur est
ensuite arrêté pendant Clippy WASM pour reproduire une divergence Windows
distante de bibliothèque d'assets. Aucun build de candidat n'a encore commencé.
Les deux arrêts E: 001/002 conservent leurs causes et preuves séparées.

Actualisation du 21 octobre (heures UTC du 20 octobre) : le replay global
`gate5-native-workspace-tests-e-002.log` passe à 23:10:16 UTC, code 0 :
**2 490 tests réussis, zéro échec, 56 ignorés, 262 cibles**. Il inclut le
correctif des alias de racine de bibliothèque d'assets. Clippy natif strict
`native-workspace-clippy-e-003.log` passe à 23:10:28 UTC. Le séquenceur 003
travaille ensuite sur Clippy WASM ; ses builds et parcours graphiques restent
non qualifiés tant que leurs reçus et observations ne sont pas acquis.

Le correctif conserve la racine physique canonique et accepte également
l'orthographe absolue configurée, puis rebase uniquement les composants normaux
validés sur cette racine physique. Les contrôles physiques de confinement restent
actifs. Une vraie jonction locale reproduit le refus avant correction ; après
correction et emploi d'une fixture GLB valide, la bibliothèque Studio passe
à 198 succès, zéro échec et quatre ignorés (`asset-library-root-alias-green-e-002`).
Les essais rouges et le premier replay avec fixture GLB invalide sont conservés.
Ce succès local ne remplace pas un replay distant sur la révision corrigée.

### Parcours graphique commencé le 21 octobre

Le séquenceur `candidate-sequence-e-003.receipt.json` termine le 21 octobre
à 00:28:16 UTC, code 0 : Clippy natif/WASM, frontend et worker, jeu séparé,
Studio natif, producteur des deux modules, tests du transport et six contrôles
des frontières moteur. Son verdict est `BUILD_PASS_RUNTIME_NOT_VERIFIED`.
Les empreintes des artefacts ont été revérifiées avant lancement.

Le premier lancement refuse une racine vide héritée de l'environnement du
lanceur : dans cette version de PowerShell/.NET, `SetEnvironmentVariable`
avec une valeur nulle laisse une chaîne vide dans le processus enfant.
`Remove-Item Env:FORGE_AUTHORING_ROOT` supprime effectivement la variable.
Le second lancement fixe explicitement les racines : les deux modules sont
listés et Les Trois Cairns s'ouvre avec rendu GPU. Le profil WebView2 est
vérifié par la ligne de commande du processus sur E:, sans nouveau cache C:.
La capture et le texte du premier refus restent sur F:.

Le premier cas UI01 montre la règle commune, son unité et son aide, puis prépare
et applique une portée d'interaction de 3 à 4 mètres. Les sources enregistrées
restent identiques pendant le brouillon et l'aperçu. Le bouton global Annuler
révèle ensuite un défaut de routage : il demande une annulation de zone qui
contredirait la définition commune du module ; le contrôle de cohérence la
refuse. La reproduction est conservée dans `ui-history-red-e-001.png/.txt`.
La retouche appliquée est ensuite sauvegardée par le Studio et celui-ci fermé.
**UI01 reste incomplet**, sans succès inventé pour Undo/Redo ou reprise en jeu.

Le correctif de routage fait examiner l'historique de l'autorité du module,
puis le confirme par Appliquer ; les boutons, raccourcis et commandes du panneau
Creator partagent le même préparateur. Les anciens documents autonomes gardent
leurs commandes de zone/terrain. La disponibilité des boutons suit l'historique
du module, y compris pour Rétablir, sans autoriser d'effet sur un autre module.
`studio-history-routing-tests-e-001.log` passe à 06:54:05 UTC : **16 tests,
zéro échec**, dont modification de règle, aperçu sans mutation, Undo/Redo,
refus d'une base périmée, Save/Open et autre module inchangé.
`studio-history-routing-wasm-clippy-e-001.log` passe à 06:55:40 UTC.

Le séquenceur `history-candidate-e-001` reprend les tests globaux, Clippy et
la construction du frontend/Studio corrigés depuis le même checkout et cache.
L'ancien candidat fermé est conservé ; la nouvelle sortie de qualification est
`.build/frontend/studio-native-history` sur E:. Les données auteur sauvegardées
restent dans le workspace isolé existant, les preuves dans la campagne F:.
Le replay graphique du correctif et les douze autres domaines restent à acquérir.

Le replay global du correctif termine à **07:25:08 UTC**, code 0 :
**2 491 succès, zéro échec, 56 ignorés, 262 cibles**
(`history-workspace-tests-e-001.log`). Clippy natif refuse ensuite le nouveau
test d'intégration pour sa longueur de 121 lignes, limite 100. Les lectures
répétées du profil sont factorisées dans un helper de test ; aucun code produit
n'est changé. La tentative 002 épingle l'ancien et le nouveau fichier de test,
vérifie toutes les autres entrées compilées contre ce replay global, puis
rejoue les tests des modules, Clippy et les builds. Le refus initial est conservé.
Cette reprise passe les 16 tests des modules à 07:30:17 UTC, Clippy natif
du workspace à 07:30:20 UTC et Clippy WASM du Studio à 07:30:25 UTC,
tous code 0. Le frontend corrigé est ensuite en construction ; ces reçus
ne constituent pas encore un replay graphique de l'annulation corrigée.

### Reprise UI01 : deux zones et refus de projection historique

`history-candidate-e-002.receipt.json` termine le 21 octobre à 07:47:37 UTC,
code 0. Le Studio corrigé est lancé depuis `studio-native-history`, empreinte
`825046ac56df828064b8943cadf8a15e87f43847465dbc25fbdb5d943202649b`.
Une seconde zone vide, `d05bc390-2ed3-526d-bc70-cc83d37c6101`, est créée
par les commandes natives sur les seules données isolées de qualification.
Cette préparation externe ne qualifie pas un parcours de création dans le Studio.

Dans le Studio, l'activation du gameplay est prévisualisée, appliquée puis
sauvegardée. Le profil hérite bien de la portée commune de 4 m. Une retouche
à 5 m annonce les deux cartes concernées, Les Trois Cairns et G5 — Seconde zone
de règles, puis s'applique. L'annulation globale prépare maintenant l'historique
du module et remet la valeur à 4 m après confirmation. La projection du terrain
refuse cependant le snapshot restauré : elle attend `Revision(4)` et reçoit
`Revision(2)`. La restauration métier ne suffit donc pas à qualifier le parcours.

Preuves dans la campagne centrale F: : `gate5-ui01-propagation-e-001.png/.txt`
et `gate5-ui01-projection-red-e-001.png/.txt`. La reproduction moteur ciblée est
en cours ; **UI01 reste incomplet**, rétablissement graphique et calcul en jeu
encore non validés. Les contrôles stricts des mutations terrain ne doivent pas
être affaiblis pour accepter une restauration de snapshot.

Le test `authoring_history_restores_exact_snapshots_and_rapier_height` reproduit
le refus avant correction (`projection-history-red-e-001`, code 101,
08:11:13 UTC). Le correctif ajoute une projection explicite des snapshots
déjà acceptés, sans réécrire leurs révisions ni hashes et sans modifier le
contrat de révision suivante des mutations ordinaires. Les trois tests terrain
passent à 08:14:29 UTC (`projection-history-world-green-e-001`), puis les
**11 tests moteur de projection passent à 08:19:22 UTC**, dont le nouveau cas
Undo/Redo avec hauteurs vérifiées dans le backend Rapier. Les bases périmées,
changements d'identité, corruptions et restaurations transactionnelles restent
couverts. Le séquenceur `projection-history-candidate-e-001` poursuit Clippy,
le replay global et les builds ; aucun nouveau verdict graphique n'est acquis.
Clippy natif du workspace passe à **08:21:05 UTC** et Clippy WASM du Studio
à **08:22:24 UTC**, code 0 (`projection-history-workspace-clippy-e-001` et
`projection-history-studio-wasm-clippy-e-001`). Le replay global démarre ensuite
sur les mêmes entrées épinglées. Le futur avertissement tiers sur
`proc-macro-error2 2.0.1` reste visible dans le journal WASM ; il ne constitue
pas un échec du contrôle strict exécuté.

La baseline de reprise `ui01-projection-history-e-001.baseline.json` vérifie
en lecture seule les deux modules et trois zones enregistrées : autorité
révision 10, SHA-256 `66fa21ed7ac0192473a82a18c89b8de47cbbb68f4778192aaadc2b69b03e4f4d`,
archives immuables conformes à leurs références et profil commun à 4 m dans les
deux zones de A. B garde son profil distinct. Cette baseline ne qualifie pas UI01.

Le replay global `projection-history-workspace-tests-e-001.log` termine le
21 octobre à **08:46:59 UTC**, code 0 : **2 493 succès, zéro échec, 56 ignorés,
262 cibles**. Le reçu et le journal sont épinglés respectivement à
`bce95173cbbbf53b6445cb162c07afd89fd2d3f5d064dace28dce9eb3420ce1b` et
`6b8cc227d0b4b8cfd8f0bf0a67b887129d87bf9c664c696faf10c801d535f8f4`.
`cargo fmt --all --check` passe à 08:47:06 UTC. Le séquenceur termine à
09:14:35 UTC, code 0, `BUILD_PASS_RUNTIME_NOT_VERIFIED`, après frontend,
jeu séparé et Studio natif. Les entrées de compilation avant/après sont
inchangées. Le nouveau Studio est épinglé à
`6c84f90e9f3c99cf451659b5446a7ffe55c0f43ecee963a46ca6dd29aa00e00e`,
le frontend à `6455894e0de04fe3d77377e9c8f09c8ba08003533a2387b4560b45bfdd0d2d2e`
et le jeu à `4f7f5334f21cc5a49e1c8938de49422babad78531607c5296f1eae8a9a174456`.
Le lancement PID 3476 à 09:18:42 UTC vérifie les artefacts et tous les pins
frontend ; ses journaux sont sur F:, son profil WebView et ses temporaires sur E:.

Dans ce candidat, le rejeu UI01 modifie 4 → 5 m dans la première zone et
retrouve 5 m dans la seconde. Annuler puis Appliquer restaure 4 m avec
projection CPU/Rapier acquittée ; Rétablir puis Appliquer restitue 5 m et
son hash de projection. Aucun refus de révision historique n'est observé.
Les captures `ui01-projection-history-undo-green-e-001*` et
`ui01-projection-history-redo-green-e-001*`, la baseline et le reçu
`ui01-projection-history-replay-e-001*` sont sur F:. Après Save, les deux
archives vérifiées contiennent 5 m, même identité/version de profil ; B est
inchangé. **UI01 reste incomplet** jusqu'à réouverture et calcul observé en jeu.

### UI02 : données de blueprint créées dans le Studio

Pendant le replay global, le Studio déjà construit est relancé pour avancer
les données isolées, sans modification des entrées compilées. Son exécutable
reste celui de `history-candidate-e-002`, SHA-256 `825046ac56df828064b8943cadf8a15e87f43847465dbc25fbdb5d943202649b`
(`studio-domain-authoring-e-001.launch.json`). Aucun jeu n'est lancé dans ce lot.

Les formulaires créent le blueprint « G5 — Sentinelle des cairns », y lient
le pillard et une seconde instance « G5 — Sentinelle locale », puis définissent
pour celle-ci une vitalité locale de 60 et la fuite au danger. Une révision
du blueprint de 40 à 50 produit sa version 2 : le pillard hérite de 50, tandis
que la seconde instance conserve 60 et sa fuite locale. Aperçus, applications
et sauvegardes passent par les commandes normales du Studio. Les noms, poses,
patrouilles et liens de dialogue des anciens acteurs sont vérifiés inchangés ;
B, la seconde zone de A et les règles communes sont préservés.

`ui02-blueprint-authoring-e-001.png/.txt`, `.zone.json` et `.receipt.json`
conservent la capture et les données enregistrées. Blueprint :
`2dde7c60-363c-56e5-bd10-dc3a2d51ff81` ; nouvelle instance :
`5ce34f7c-81d8-5e5c-b491-bd1b7565483a`. Cette instance n'a pas de nouveau
modèle importé accepté. **UI02 et UI03 restent incomplets** : réouverture dans
le candidat corrigé et comportements en jeu restent à observer.

### UI04 : item et conteneur préparés dans le Studio

Le Studio précédent crée l'item `a2730089-2ed4-573d-902a-b4c77a20da53`
« G5 — Jeton de garde » (pile 20), puis convertit l'objet physique
`a5f192cd-c7f6-5b33-8f1c-748ab5db8478` en coffre par le formulaire normal.
Sa position (1,5 ; 0,5 ; 1), ses dimensions et sa physique sont conservées :
capacité trois unités, deux jetons initiaux, sans serrure, filtre jetons seuls.
Save synchronise le workspace révision 19. Les preuves
`ui04-container-authoring-e-001*` sont sur F: ; le reçu SHA-256
`9d7d02d615d5bcd0cfe49229499cd246313137abbabd83cef3b214cff2333497`
confirme les quantités et références enregistrées. **UI04 reste incomplet** :
réouverture, transferts acceptés/refusés sans perte et reprise restent à exercer.

### Rejeu graphique du 21 octobre : portée, variable et transferts

Le candidat `6c84f90e…` rouvre la première zone après Save et retrouve les
données d'auteur. La gardienne est ensuite placée par le formulaire normal à
(0 ; 0,9 ; −1,5), face au départ (0 ; 0,95 ; 3), pour comparer la portée.
Deux parties fraîches, sans entrée de déplacement, utilisent le même jeu
`4f7f5334…` : à 4 m, seules les actions du coffre sont proposées ; à 5 m,
la réplique et les choix de la gardienne sont accessibles. Les documents
gameplay comparés ne diffèrent que par `profile.interaction_range_m`.
Les rapports terminaux passent ; les sources auteur restent inchangées et
l'ancienne sauvegarde n'est pas écrasée pendant le premier essai.
Les preuves `ui01-range-4m-e-001*` et `ui01-range-5m-e-001*` sont sur F:.

Le dialogue retouché conserve son nœud `64d782b4-e2df-598f-bb5e-61d24c556802`
et son choix de quête. Une variable booléenne « G5 — Passage reconnu »
`61ea63df-257e-5f85-b424-be6894dd367f`, initialement fausse, contrôle le
choix `9d1702f1-98da-5f58-8396-4a89b55e48b9` : son exécution la passe à
vrai et retire ce choix, sans accepter la quête. Dans cette même partie,
le retrait de deux jetons puis le dépôt d'un jeton laissent un jeton dans
chaque inventaire. Save/F9/fermeture/relance affichent « Sauvegarde reprise »,
les mêmes quantités, la quête disponible et le choix conditionnel absent.
La sauvegarde épinglée est `54aaf5f42c1286fea8486d840b7a60c2b9433bcb20cc5d09732bcdf7f533cd41`,
séquence 3, avec les trois reçus réels de changement de variable et transferts.
Voir `ui04-ui05-transfer-variable-e-001.save.json`, `ui04-ui05-resume-e-001*`,
`ui05-variable-after-e-001*` et `ui05-variable-resume-e-001*` sur F:.

Le parcours de refus révèle toutefois une limite de présentation : les menus
masquent les transferts exclus par filtre/capacité et le panneau natif masque
le retour d'action lorsque les données du menu ne changent pas. UI04 reste
incomplet. Le correctif prépare des demandes de transfert explicites sans
changer `container_actions`, ni les validations atomiques d'exécution ; les
présentations native et WASM réutilisent ce préparateur commun. Le retour
d'action participe au rafraîchissement et au texte du panneau natif.
Le premier nouveau test échoue sur son attente textuelle « capacity » au lieu
de l'erreur réelle « inventory full » ; ce reçu est conservé. Après correction
de cet oracle, `container-feedback-domain-e-002` passe à 09:53:54 UTC :
13 succès, zéro échec, avec filtre, capacité, absence de mutation et reprise.
Un test natif vérifie aussi que le menu propose la pile entière lorsque seule
une unité tient. Le séquenceur `container-feedback-candidate-e-001` relance
contrôles et builds. Les preuves graphiques précédentes restent historiques ;
le candidat de ce nouveau correctif doit être exercé avant qualification finale.
Quatre sorties de candidats sont conservées, estimées ensemble à moins de 4 Go
et 14 000 fichiers, sans nouveau checkout ni cache Cargo.

Le premier contrôle natif du nouveau lot passe : 12 tests de présentation/
persistance, zéro échec, 559 filtrés ; Clippy natif strict passe ensuite avant
entrée en Clippy WASM à 10:02:48 UTC. Pendant ce contrôle, le Studio de projection
rouvre les deux instances : définition v2 à 50 et lien intégralement hérité
pour le pillard ; vitalité locale 60 et fuite cochée pour la sentinelle.
Leurs données et blueprint sont comparés inchangés aux archives de création.
`ui02-blueprint-reopen-e-001.receipt.json` et les captures inherited/local
sont sur F:. Ce rejeu qualifie cette réouverture sur ce binaire précis,
sans anticiper le candidat du correctif de conteneurs ni le comportement UI03.
Le sceau existant est ensuite conservé à son ID et déplacé par le formulaire
à (0 ; 0,3 ; 1), pour exercer le dépôt filtré à portée du coffre lors du rejeu.

### Jeu séparé : sauvegarde et isolation graphique A–B–A

`game-smoke-resume-e-001.receipt.json` et
`game-smoke-isolation-e-001.receipt.json` rapprochent cinq parties réellement
lancées depuis le même Studio, avec le même jeu natif épinglé à
`0be501b4ab36076c196c8ef99f255669616845b79c01cccbb5617c82607bc121`.
Dans A, le dialogue accepte la quête, F5 sauvegarde, F9 demande la reprise,
puis la relance affiche « Sauvegarde reprise » et la quête en cours, séquence 1.
B affiche sa propre quête à découvrir et sauvegarde séparément en séquence 0.
Après B, une nouvelle demande F9 dans A reprend sa sauvegarde en séquence 1,
sans écraser sa partie lors du lancement frais intermédiaire.

Les captures sont sur F:, avec les rapports terminaux et les deux sauvegardes.
Les cinq sorties sont réussies ; chaque projection a ses trois modèles prêts,
ses placements manuels disponibles et zéro pipeline de rendu en échec.
Les hashes des sources auteur et de la sauvegarde A restent identiques pendant
ce replay. La sauvegarde B a un autre module, une autre zone, une autre quête
et un autre hash. Les personnages ont ici les représentations simples gouvernées
des modules de qualification ; aucune acceptation de fidélité visuelle n'est déduite.

Ce contrôle utilise le Studio précédent pendant la reconstruction du correctif
d'historique ; il n'est pas une validation du futur frontend corrigé.
Il prouve le démarrage, la sauvegarde/reprise et l'isolation de ces parties.
**R5-P02 reste ouvert** : configurations de surfaces distinctes, masquage et
restauration de données, réexport des deux modules et rapprochement du candidat
final restent à qualifier. Les treize domaines étendus ne deviennent pas PASS.

## Preuves récentes

- `gate5-native-workspace-tests-e-001.log` et son résumé : **PASS** complet
  `--workspace --all-targets --locked --offline -j2 -- --test-threads=1`,
  fin à 22:19 UTC. 2 489 tests réussis, 56 ignorés et 262 cibles. Les ignorés
  sont explicitement listés : sondes CPU, replays locaux et fixtures d'assets
  externes/privés ; ils ne deviennent pas des PASS. Les contrats Creator passent
  à 10/10, temporels à 11/11, factions à 10/10 et packages à 27/27 dans cette suite.
  Les autres totaux restent dans le résumé, sans addition aux anciens résultats.
- Le frontend E: 001 est refusé avant compilation : Trunk exige un booléen
  pour `NO_COLOR`, hérité à `1`. La tentative 002 utilise `true` et conserve
  le refus 001. Clippy natif puis WASM sont enchaînés avant les builds ;
  aucune qualification graphique n'en est déduite.

- Reprise E:, `gate5-supply-chain-check-e-001.log` : **PASS**, fin à 21:30 UTC,
  code 0, `advisories ok, bans ok, licenses ok, sources ok`. Configuration de
  politique équivalente à `deny.toml`, seul le chemin de base est déplacé sur E:.
  Les avertissements autorisés restent dans le journal ; aucune liste d'exceptions
  ni aucun filtre n'est ajouté. Preuve locale du graphe, pas exécution Linux.
- `gate5-rustsec-audit-e-001.log` et `gate5-rustsec-policy-e-001.json` : **PASS**
  à 21:29 UTC, 954 dépendances, zéro entrée de vulnérabilité et le multiensemble
  exact des dix avertissements attendus. Base propre de 1 348 avis, commit
  `e6f94f3760d5e6c54c1b05f204002263bc025524`. Le blocage de distribution
  `RUSTSEC-2024-0429` / GLib reste explicitement conservé. Ces nouveaux fichiers
  résident dans la campagne centrale `Preuves-et-analyses/actives/gate5/20261019/`.

- `presentation-rapier-tests-002.log` : **18/18 PASS**, fin à 05:55 UTC.
  Caméra et commandes restituées après reprise/fin, saut et interruption ;
  deux requêtes identiques produisent un seul reçu et une seule promotion.
  Les anciens scénarios physiques, factions et temporels passent également.
  Manifeste `lot-e-physics-012/source-inputs.json`. Preuve headless.

- `presentation-engine-boundaries-001.log` : **6/6 PASS**, contrôles Python
  des frontières moteur/contenus et des entrées de compilation. Le reçu conserve
  les empreintes du test et du journal. Aucun build ni GPU lancé par ces contrôles.

- `temporal-presentation-studio-engine-wasm-clippy-003.log` : **PASS**, moteur
  et bibliothèque Studio WASM/WebGPU, warnings projet traités comme erreurs,
  fin à 05:36 UTC. Manifeste `lot-de-studio-010/source-inputs.json`.
  Formulaires D/E et diagnostic vers l'instruction compilés ; parcours graphique
  non exercé. L'avertissement de compatibilité Rust future de `proc-macro-error2`
  est conservé, distinct du résultat strict du projet.

- `presentation-host-save-tests-001.log` : **9/9 PASS**, fin à 05:29 UTC,
  sauvegardes v1 à v6. Save/Close/Open/Load v6 conserve le script et reprend
  la séquence avant de la sauter, sans écriture des sources d'auteur.
  Manifeste `lot-e-host-009/source-inputs.json`.

- `presentation-gameplay-tests-001.log` : **126 tests gameplay réussis**, aucun
  échec, fin à 05:18 UTC. Manifeste `lot-e-gameplay-006/source-inputs.json`.
  Il inclut les anciens formats, treize tests unitaires E et onze scénarios
  de session E. Les 126 tests constituent un total, pas trois totaux à additionner.
- `presentation-domain-project-clippy-004.log` : **PASS**, tous targets
  gameplay/projet, warnings traités comme erreurs, fin à 05:14 UTC.
  Manifeste `lot-e-domain-005/source-inputs.json`. Les trois refus antérieurs
  restent conservés avec leurs diagnostics et corrections.
- `temporal-rapier-tests-001.log` : **2/2 PASS**, fin à 04:59 UTC.
  Protection/expiration après reprise ; activation d'une rencontre sur le même
  corps, contact réel, sauvegarde/reprise sans doublon. Manifeste
  `lot-d-physics-006/source-inputs.json`. Preuve headless.
- `temporal-host-save-tests-001.log` : **8/8 PASS**, fin à 04:46 UTC.
  Save/Close/Open/Load v5, coûts conservés et expiration après reprise.
- `factions-retry-green-001.log` : **2/2 PASS**, fin à 04:15 UTC.
  Occlusion réelle des témoins et absence de nouvelle promotion sur retry exact.
  La reproduction rouge reste dans `factions-retry-red-001.log`.

Les journaux et manifestes antérieurs à la reprise E: sont relatifs à
`F:/OpenRPG_Forge/.gate5-evidence/20261019/`. Les dates UTC des reçus font foi ;
le nom historique du répertoire ne date pas chaque exécution du 19 octobre.
Un HEAD seul n'identifie pas ce checkout modifié : chaque candidat conserve
ses entrées suivies, non suivies, modifiées, supprimées ou inaccessibles.

## Matrice des treize domaines

| Domaine | Contrat et preuve locale acquis | Qualification encore nécessaire |
| --- | --- | --- |
| Dialogues avancés | Variables typées, conditions composées bornées, graphe et retouche stable ; suite gameplay complète PASS | Création/retouche/reprise par les formulaires du candidat graphique final |
| Quêtes et journal | Solutions alternatives, échec, journal, récompense unique après reprise ; conservation pendant une cinématique | Parcours auteur puis joueur, deux solutions et récompense dans le même build |
| Factions | Relations locales et reçus causaux ; vrais témoins occultés/visibles et retry physique PASS | Formulaires puis conséquences observées dans une partie graphique |
| Blueprints | Révision partagée et overrides d'instance conservés ; persistance et historique testés | Deux instances retouchées et jouées dans le parcours auteur |
| Objets et conteneurs | Transferts atomiques, capacité/filtres/clefs, conservation d'items et reprise testées | Cas acceptés/refusés exercés par l'interface et le runtime final |
| Créatures | Système commun d'acteur et de blueprint ; aucun moteur par slug ajouté | Deux créatures configurées, présentées et jouées, sans branche spécifique |
| Capacités et effets | Coûts atomiques, stacking/exclusivités, annulation/expiration ; host v5 et Rapier PASS | Formulaires, explications des refus et parcours graphique |
| Cinématiques | Session et host v6 PASS ; caméra/entrées physiques restituées après reprise/fin, saut et interruption PASS | Parcours graphique auteur/joueur |
| Calendrier | Temps entier injecté, franchissement de bornes et reprise sans répétition testés | Horloge et événements configurés puis observés dans l'interface |
| Rencontres | Absence/présence, réarmement après défaite et délai, refus atomiques ; première activation physique PASS | Récurrence et conditions exercées dans les parcours graphiques |
| Profils de règles | Sept descripteurs communs, unités/calculs/validation, capacités et configuration commune du module testés | Inspecteur/aide/contenus concordants dans les deux modules du Studio final |
| Scripting sandboxé | AST typé, permissions distinctes, 256 étapes/appel, refus sans mutation, sorties et reprise vérifiées ; host v6 et retry physique PASS | Diagnostic/correction/replay et exécution dans les parcours graphiques |
| Validation et diagnostics | Sources/références bornées, refus typés et journaux réempreintés falsifiés rejetés | Lien vers la donnée corrigible, correction et replay vérifiés dans l'interface |

Le profil est explicitement partiel et versionné. Ce tableau ne déclare aucune
conformité intégrale au manuel de règles de référence.

## R5-P01 et R5-P02

**R5-P01 ouvert.** Les définitions et registres sont communs au calcul, à la
validation et aux formulaires. Une capacité future inconnue est refusée par le
codec, et masquer une surface conserve ses données et exigences. Le rapprochement
visuel inspecteur/aide/contenu du candidat final reste à exercer.

**R5-P02 ouvert.** Le backend commun de module et le producteur passent leurs
preuves ciblées antérieures, dont 24 tests du producteur/export à deux modules.
Deux packages privés existants et leurs empreintes sont préparés sous
`qualification-module-inputs-001/`. Aucun import ni parcours graphique des deux
modules sur le nouveau candidat n'est encore qualifié. Les mêmes exécutables,
les sauvegardes isolées et les surfaces propres au module restent à prouver.

## Contrôles en cours et limites du verdict

Le premier contrôle WASM strict du moteur et des formulaires D/E, incluant le
diagnostic de script vers l'instruction, est conservé dans
`temporal-presentation-studio-engine-wasm-clippy-001.log` avec son refus
`match_same_arms`. Les deux branches capacité/attaque partageant la même cible
sont regroupées, sans changement de comportement. Le deuxième contrôle est
conservé dans `temporal-presentation-studio-engine-wasm-clippy-002.log`, manifeste
`lot-de-studio-008/source-inputs.json` (6 160 entrées).
Le deuxième contrôle WASM conserve ses deux refus de lint dans le panneau de
scripts : point-virgule manquant et argument d'erreur passé par valeur. Ils sont
corrigés sans désactiver de contrôle. Le replay strict réussi est
`temporal-presentation-studio-engine-wasm-clippy-003.log`, manifeste
`lot-de-studio-010/source-inputs.json`.
Les 18 tests physiques natifs sont lancés dans `presentation-rapier-tests-001.log`,
manifeste `lot-e-physics-011/source-inputs.json`, dont les deux nouveaux scénarios
caméra/entrées physiques. Aucun verdict n'est acquis par leur lancement.

Le premier replay physique E termine à 05:48 UTC avec **17/18 tests réussis**
et un verdict global **FAIL**. Le test fautif lisait le dernier lot non vide
comme s'il représentait un nouvel événement. L'oracle corrigé envoie deux
requêtes identiques dans le même pas et exige un seul reçu et une seule promotion
du message ; le moteur reste inchangé. Le replay complet courant est
`presentation-rapier-tests-002.log`, manifeste `lot-e-physics-012/source-inputs.json`,
et passe ensuite **18/18** à 05:55 UTC. Ce résultat reste une preuve headless.

Les trois fichiers de libellés du panneau de scripts sont corrigés après le
contrôle WASM003 (reçu `script-label-encoding-repair-001.json`). Leurs accents
doivent donc être inclus dans le prochain build graphique ; WASM003 ne prouve
pas leur affichage. Trois anciens tests projet sont également adaptés à la
capacité de script désormais disponible, en conservant le refus des versions et
capacités futures inconnues. Leur replay reste à acquérir.

Clippy strict natif du workspace, tous targets, est lancé dans
`gate5-native-workspace-clippy-002.log`, manifeste `lot-g-native-014/source-inputs.json`.
La première tentative s'est arrêtée avant compilation sur une archive CLI absente
du cache hors ligne. Le reçu `registry-prepare-cli-001.json` vérifie les 62 paquets
de cette fermeture, dont 13 extraits sur F:, sans changement du lockfile.

Le deuxième contrôle natif termine à 06:30 UTC avec quatre refus de construction
de chaînes dans le HUD temporel/cinématique. Ils sont corrigés sans modifier les
règles métier ou désactiver de lint. Le replay actif est
`gate5-native-workspace-clippy-003.log`, manifeste `lot-g-native-015/source-inputs.json`.
Son journal UTF-8 est désormais flushé ligne par ligne ; les essais du wrapper
conservent correctement succès et code d'échec. Le replay termine à **07:02 UTC
le 20 octobre**, code **0**, après 28 min 52 s : Clippy strict natif du workspace,
tous targets, **PASS**. Ce résultat vérifie les targets sans exécuter leurs tests.

Le contrôle `fmt --all -- --check` passe à **07:25 UTC**, code **0**, dans
`gate5-workspace-fmt-final-001.log`, sur les mêmes sources compilées.
Il n'a modifié aucun fichier et n'est pas une exécution de tests.

L'audit RustSec termine à **08:39 UTC**, code **0**, dans
`gate5-rustsec-audit-001.log`, avec base explicitement sur F:.
`gate5-rustsec-policy-001.json` confirme **PASS** pour la politique exacte :
954 dépendances, `vulnerabilities=0`, 10 avertissements attendus, aucun avis
ignoré ni filtre de plateforme. Les empreintes du lockfile et de la politique
sont celles du manifeste de la suite workspace. La base de 1 338 avis est au
commit `999ec89d5d3f10f20be14750ec8c2d8b44e0ac9b` (mise à jour déclarée le
17 octobre). La réserve `RUSTSEC-2024-0429` sur `glib 0.18.5` reste **bloquante
pour la distribution** ; Linux et la distribution ne sont pas qualifiés.
Le contrôle licences/bans/sources était alors à acquérir ; sa reprise E: passe
à 21:30 UTC, comme décrit dans les preuves récentes ci-dessus.

Les six contrôles Python d'architecture passent à **09:09 UTC**, code **0**, dans
`gate5-engine-boundaries-final-001.log`. Le manifeste
`lot-g-engine-boundaries-final-001/source-inputs.json` (6 161 entrées) confirme
des sources compilées inchangées depuis le lancement des tests workspace :
seuls trois documents ont changé. Ce résultat **6/6 PASS** couvre les gardes de
frontière moteur/contenu et d'entrées de compilation ; il ne prouve pas les
parcours graphiques à deux modules.

La suite globale `gate5-native-workspace-tests-001.log` termine à **09:57 UTC**,
code Cargo **101**, sur un test de `forge_project --lib` (**101 PASS, 1 FAIL**).
La fixture d'export injectait une session gérée sans workspace/module ; l'export
réussit, puis l'inspection échoue sur l'absence de propriétaire. Le frontend
conditionnel n'a pas démarré (`frontend-release-001.receipt.json`). Le scénario
est déplacé dans les tests d'intégration du Creator avec un vrai module et des
oracles de non-écriture conservés ; aucun changement de règle produit. Son
replay ciblé passe ensuite à **10:10 UTC**, code **0**, dans
`gate5-managed-export-fixture-test-001.log` : **10/10 tests d'intégration PASS**,
manifeste `lot-g-export-fixture-019/source-inputs.json`. Les oracles d'archive
exacte, de non-écriture et d'état modifiable sont conservés avec un module réel.
Le formatage corrigé passe à **10:05 UTC** (`gate5-workspace-fmt-final-002.log`).
La suite globale suivante reste à acquérir.

Le candidat corrigé passe ensuite Clippy strict natif, workspace et tous targets,
à **10:37 UTC**, code **0**, dans `gate5-native-workspace-clippy-004.log`, manifeste
`lot-g-native-020/source-inputs.json`. Ce PASS conserve le premier échec global
comme preuve historique ; il n'est pas une exécution de tests. Le nouveau replay
global est `gate5-native-workspace-tests-002.log`, manifeste
`lot-g-workspace-tests-021/source-inputs.json`, avec verdict encore à acquérir.

Restent ensuite les contrôles natifs du candidat complet, les non-régressions
workspace, les builds puis lancements/captures du même Studio et runtime,
la matrice de parcours et le rapport final. Les preuves graphiques et humaines
ne sont pas remplacées par des tests de domaine.

ADR-0006 couvre les gates 0–4 et ADR-0023 la décision spécifique à Gate 3.
Aucune exception de CI distante n'est prolongée à Gate 5. Les résultats distants,
Linux/Windows vierge, distribution et acceptation propriétaire restent séparés
des résultats locaux acquis ici. Aucune gate suivante n'est ouverte.

### Reprise du 21 octobre — solutions UI06 et candidat conteneur

Le formulaire du Studio sauvegarde deux solutions de la quête : restitution
consommant un sceau, ou accord conditionné par « Passage reconnu », consommant
deux jetons. Les deux choix du dialogue référencent leurs solutions exactes ;
les quatre textes du journal sont enregistrés. Le choix de restitution conserve
son identité et sa condition de défaite du pillard. Le reçu
`ui06-two-solutions-authoring-e-001.receipt.json` et son archive source sont dans
la campagne centrale F:. Les acteurs, objets, items, règles et récompense de base
sont inchangés par cette retouche. **Les deux parties fraîches, la reprise et
la récompense unique restent NON EXÉCUTÉES sur ces sources.**

La séquence `container-feedback-candidate-e-001` passe les tests natifs de
présentation (12), Clippy strict natif et WASM, puis la suite workspace :
**2 495 PASS, 0 FAIL, 56 ignorés, 262 cibles**. Le contrôle de formatage passe ;
la compilation du frontend est en cours. Ces contrôles ne qualifient pas encore
le refus graphique du dépôt, ni les parcours UI03 à UI13 restants.

La préparation graphique UI08 enregistre l’effet « G5 — Garde des cairns »
(10 points, 5 000 ms, cumul refusé sans coût), la capacité « G5 — Protection
ciblée » (personnage vivant à 5 m, coût de 5 points d’endurance) et son acquisition
par le Voyageur. Sources et capture vérifiées sur F: dans
`ui08-ability-authoring-e-001.receipt.json` et `ui08-ability-acquired-e-001.png`.
Le calendrier UI09 est également sauvegardé : jour 1 à 08 h 00, 1 000 ms/minute,
événement « G5 — Relève des gardes » après 20 minutes puis toutes les 20 minutes
(`ui09-calendar-authoring-e-001.receipt.json`). Ces préparations ne valident pas
les coûts, l’expiration ou le franchissement de borne avec reprise en jeu.

UI11 : la séquence « G5 — Les voix des cairns » contient deux plans de 10 000 ms,
avec caméras (0,2,-4) et (4,2,-4) regardant (0,0,0), champs 60° et 65° ;
la gardienne parle dans le plan des cairns. L’ordre est ensuite retouché par
Descendre, examiné, appliqué et sauvegardé : le plan du Voyageur précède celui
de la gardienne. Le reçu `ui11-cinematic-reorder-e-001.receipt.json` confirme
les mêmes identités et données de segments, avec aperçu sans écriture des
sources. Les captures et archives sont centralisées sur F:.
Fin, saut, interruption et reprise en cours de plan restent NON EXÉCUTÉS.
