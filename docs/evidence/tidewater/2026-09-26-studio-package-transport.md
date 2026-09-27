# Transport du paquet dans Studio

Le panneau Fiche du catalogue contient maintenant une commande explicite de
vérification/chargement de la bibliothèque de bâtiments importés. Elle lit
`./forge-imports/buildings.json` (4 Kio maximum), puis le manifeste épinglé sous
`./forge-imports/<sha256>/package.json` et ses payloads via le lecteur partagé
asynchrone. Aucun chemin ni domaine distant n'est fourni par le candidat.

Le transport réutilise le flux HTTP borné de Studio, son refus des redirections
et son délai avec abandon de 20 secondes par requête. Le chargement vérifie une
génération avant chaque payload et avant adoption en mémoire ; démonter le
composant invalide la publication d'un résultat tardif. Masquer un panneau ne
le démonte pas dans Studio et laisse donc ce chargement se terminer. La dernière bibliothèque
entièrement validée est conservée dans le cache du composant. Aucun placement
ni fichier de projet n'est modifié par ce bouton.

Le script `prepare-building-library-site.py` prépare un nouveau répertoire local
de déploiement après contrôle des tailles et hashes. Exécution sur le paquet
002 : 821 payloads copiés dans `building-library-site-001`, sous la racine
locale de qualification. Le vérificateur Rust rouvre cette copie et passe ses
contrôles synchrones et asynchrones sur les 23 bâtiments.

Le check WASM Studio réussit. Le clic réel dans le navigateur, le statut rendu,
les erreurs HTTP et l'acquisition réseau de cette nouvelle route restent à
qualifier. Le panneau indique explicitement que l'aperçu et le placement ne
sont pas encore disponibles ; la création des matériaux et la projection dans
le viewport restent à raccorder au paquet résident.

Clippy strict Studio ne passe pas : `atelier_ui.rs:122`, `bind_main`, dépasse
le seuil de longueur (103 lignes pour 100). Ce fichier n'a pas été modifié par
ce lot. Le check WASM réussi ne vaut donc pas verdict Clippy global.

Qualification navigateur réalisée ensuite : build release WASM réussi (7 min
12 s), wasm-bindgen 0.2.127, site local isolé servi sur loopback. Depuis Studio
en mode navigateur autonome, sans zone ouverte : Objets → Fiche → Bibliothèque
de bâtiments importés → Vérifier et charger. Le statut passe par la vérification
puis affiche « 23 bâtiments vérifiés et chargés en mémoire », avec la limite
d'aperçu/placement toujours visible. Capture inspectée :
`studio-library-loaded-001.png`.

Le journal HTTP confirme exactement 823 requêtes importées réussies (candidat,
manifeste, 821 payloads), toutes HTTP 200, sans payload omis. Rapport local
`studio-package-http-proof-001.json`. Hash du frontend servi :
`3928eba2d138d9cd436d20acdc66e5939b1643b58acef7eea899681b70303cab`.
Ce résultat valide le parcours HTTP nominal et le statut rendu. L'injection
d'erreurs HTTP, le rendu des bâtiments et leur manipulation restent ouverts.
