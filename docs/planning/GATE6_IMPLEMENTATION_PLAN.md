# Gate 6 — IA et MCP gouvernés

Ouverte par le propriétaire le 22 octobre 2026, **clôturée localement le 23 octobre**
selon [le rapport final](../evidence/gate6/GATE6_CLOSURE_20261023.md), la matrice
des 21 critères et l'archive vérifiée/cataloguée. Gate 5 reste clôturée localement. Le checkout commun est l'autorité ;
les changements présents sont conservés. Pas d'ouverture de Gate 7 ou de
publication Git implicite. Les contrôles GitHub restent arrêtés selon ADR-0034.

## Réalisation et preuves attendues

| Lot | Travail | Critères | État |
| --- | --- | --- | --- |
| A | Gouvernance commune au-dessus des commandes existantes : portée, révision, diff, approbation exacte, audit et undo | R6-A ; R6-P02/P04 | Fondations/hôte/sélection testés ; partage/diff/application prouvés dans l'UI ; plan MCP périmé par une retouche de terrain refusé, nouvelle autorisation et nouveau diff exigés, retouche préservée après application unique du nouveau plan |
| B | Inspection ciblée du projet et recherche des assets réellement disponibles ; brief vers plan et capacités absentes | R6-A ; R6-P01 | Création Qwen avec raisonnement et lieux à clés natives : appel métier hors UI de 152,61 s, distinct de la recette Studio dont l'attente observée est de 318,634 s ; deux solutions jouées et reprises hors fournisseur ; recette Studio précédente refusée sans mutation sur identité invalide, recette avec le Vault réel appliquée et sauvegardée, retouches et variante ciblée vérifiées |
| C | Jobs, staging, reprise sans doublon, fournisseur, données transmises, coûts et retries bornés | R6-P05/P06 | Jobs et propositions MCP persistés sans autorité sauvegardée ; tests de perte d’application, corruption et reprise réussis ; UI de récupération pending, rapprochement du reçu sans doublon et quota conservant le résultat exercés par rejeu contrôlé ; plafond historique corrigé et relu dans Studio ; reprise MCP réelle après fermeture avec nouvelle autorisation et nouvelle revue, commande unique et proposition immuable vérifiées. Preuves rapprochées de R6-P05 dans la matrice finale ; une nouvelle interruption réseau réelle n'est pas un critère supplémentaire de la roadmap |
| D | Assistant dans le Studio et transport MCP borné, mêmes commandes et validations ; aucun shell ou chemin arbitraire | R6-A ; R6-P02 | Parcours fournisseur et client MCP réel prouvés sur renommage, sauvegarde/révocation ; compteur, nom hérité et textes de reprise relus ; reconnexion durable et conflit de révision exercés dans le Studio actuel, avec les mêmes commandes et sans outil MCP d'application directe |
| E | Parcours de référence, porte et dialogue retouchés puis variante locale ; réouverture, jeu et indépendance du fournisseur | R6-P03/P08 | Création Qwen avec Vault appliquée/sauvegardée dans Studio ; porte et réplique retouchées à la main, variante réelle sur l’arbre seul, gameplay strictement préservé et valeurs relues après redémarrage ; trois salles et six arbres créés par les outils ordinaires ; collecte et témoignage terminés dans le jeu sur l'environnement final, entrée KCC dans le refuge et reprises exactes à l'intérieur sans fournisseur ; éclairage neutre corrigé et captures relues pour la lisibilité fonctionnelle ; les parcours natifs commandés restent distincts des clics UI antérieurs et d'une acceptation esthétique |
| F | Comparaison manuelle/IA réelle, temps, corrections, coût et qualité ; tests, builds, captures et rapport final | R6-P07 ; ensemble des critères | État initial commun et signature fonctionnelle rapprochés ; 41 commandes manuelles, 4 550,633114 s jusqu'au décor sauvegardé, deux solutions jouées et reprises exactement. Export du kit livré corrigé et qualifié sur le même head. Comparaison complète avec différences de modèles/défauts et coûts inconnus explicités, sans gain statistique démontré. Régression finale 721 PASS / 11 ignorés, Clippy natif/WASM et build réussis ; audit 21 critères, rapport final et archive ZIP64 vérifiée/cataloguée |

Le gouverneur ne devient pas une seconde source de vérité : le document Forge,
les commandes transactionnelles et leurs reçus restent canoniques. Les données
d'un fournisseur sont des propositions non fiables. Une réponse synthétique ne
prouve ni une intégration fournisseur réelle, ni l'accessibilité dans le Studio.
Les permissions et l'approbation sont établies par l'application de confiance,
jamais accordées par le modèle ou par un argument MCP.

Fournisseur de qualification choisi par le propriétaire le 22 octobre : OpenAI,
avec clé configurée localement. Ne jamais inscrire la clé dans les documents,
plans, reçus ou captures. L'inspection initiale n'a trouvé aucune variable
`OPENAI_API_KEY` dans les environnements Process/User/Machine accessibles ; cela
ne prouvait pas l'absence d'une autre configuration locale. À cette étape initiale,
l'appel fournisseur restait à qualifier ; les générations Qwen réelles suivantes
et leurs budgets sont rapprochés dans le rapport final.

Le propriétaire fournit ensuite un serveur local pour les essais : API compatible
OpenAI `http://192.168.1.41:8080/v1`, modèle exact
`qwen3.8-flash-next-iq4-xs`. Accès vérifié le 22 octobre : modèles HTTP 200,
modèle chargé, génération courte HTTP 200 et réponse `ACCESSIBLE` en 11,17 s.
Un second appel `chat/completions` avec `response_format=json_schema` strict
renvoie `{"status":"ready"}` en 5,40 s. Ces sondes n'envoient aucune donnée du
projet et ne qualifient pas encore le schéma complet du plan ni le parcours produit.
Le serveur local peut servir aux tests réels ; la configuration OpenAI cloud
et ses éventuels identifiants restent séparés.

Consigne propriétaire suivante : activer le raisonnement lors des essais locaux.
Les requêtes réelles de test portent explicitement
`chat_template_kwargs.enable_thinking=true`, paramètre inclus dans le hash des
données approuvées. Une sonde HTTP 200 a confirmé la présence d'un champ de
raisonnement ; son contenu n'est ni affiché ni conservé. L'appel de plan Forge
avec ce réglage passe aussi par le journal durable et la transaction canonique.

Dernier appel Studio d’environnement réussi le 22 octobre, de 18:52 à 18:54 UTC :
huit ajouts appliqués et sauvegardés, 1 590 tokens d’entrée / 1 834 de sortie.
Les contrôles ultérieurs de 19:24 et 19:26 UTC ne peuvent plus joindre le serveur,
y compris en TCP direct sans proxy et via `/v1/models`. La disponibilité courante
reste à rétablir ; cela ne réécrit pas les appels réussis précédents. Les maisons
candidates de cet essai sont refusées à l’export sur extension glTF requise non
prise en charge ; elles ne constituent pas un intérieur qualifié. La pièce
canonique créée dans Studio et ses undo/redo sont sauvegardés. Les recettes
ultérieures sur les trois salles natives et les six arbres vérifient les deux
solutions, l'entrée KCC dans le refuge et leurs reprises ; elles sont détaillées
dans le rapport courant. La lisibilité fonctionnelle de l'intérieur éclairé est
examinée séparément d'une qualification esthétique.

## Stockage et validation

Campagne stable : `Preuves-et-analyses/actives/gate6-ia-mcp/` depuis la racine
OpenRPG Forge, stockage physique F:. Rapports de qualification versionnés sous
`docs/evidence/gate6/`. Budget initial : moins de 2 Gio et 2 000 fichiers de
preuves, sans copie de code ou de binaire ; réestimer avant dépassement.
Le cache Cargo et les temporaires partagés restent sur E: ; utiliser
`../scripts/Invoke-ForgeCargo.ps1 -Campaign gate6-ia-mcp ...`.

Inventaire Git et empreintes des sources avant modification, puis manifeste des
entrées de chaque candidat. Tests de refus atomique et de préservation des
retouches, tests ciblés, Clippy/fmt, non-régressions et builds natif/WASM adaptés.
Les essais UI et fournisseur restent distincts des tests de domaine. La clôture
exige une matrice critère/preuve, les réserves exactes et l'archivage vérifié des
données froides conformément au catalogue central.
