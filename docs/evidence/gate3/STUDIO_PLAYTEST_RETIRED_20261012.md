# Retrait du test intégré de Studio — 12 octobre 2026

Ce retrait concerne le jeu intégré au canvas de l'éditeur. Le bouton natif
« Lancer le jeu », ajouté ensuite, ouvre une fenêtre séparée ; voir
[le parcours courant et son retour sans rechargement](STUDIO_RESIDENT_GAME_RETURN_20261013.md).

Le propriétaire constate une partie injouable dans Studio, autour de 1 FPS,
et demande de corriger ou supprimer ce mode. Le retrait est retenu. Ce rapport
ne présente pas le chiffre signalé comme une mesure instrumentée de FPS GPU.

Le Studio ne propose plus Jouer, Jouer à cet endroit, Jouer devant cette porte,
le panneau de partie, les commandes de sauvegarde/reprise RPG ni les diagnostics
de jeu. Ces composants ne sont pas montés. Le backend refuse StartPlaytest et
StartPlaytestAt avant toute ouverture ou mutation d'une session ; la création
du renderer de jeu est également refusée. Il n'existe aucun réglage utilisateur
ou variable d'environnement pour réactiver le mode retiré.

La vue d'édition, les retouches, le départ enregistré, les données de quête et
la persistance des documents sont conservés. Les contrats moteur/domaines et
les sauvegardes antérieures ne sont pas supprimés. Les adaptateurs historiques
internes restent en source pour la traçabilité ; ils ne forment plus un parcours
produit accessible dans le Studio.

P05 reste ouvert. Le jeu séparé actuel permet une promenade native mais ne
raccorde pas encore dialogues, quête, inventaire, combat et sauvegarde RPG.
Ce raccordement doit réutiliser les mêmes documents et le moteur générique,
sans seconde version de projet. Le retrait ne qualifie aucun gain de fluidité,
ni aucune gate, CI distante ou nouvelle distribution.

Validation locale du retrait : **PASS**. Preuves locales :
`E:/OpenRPG_Forge/.gate3-evidence/20261012/embedded-playtest-retirement-001`.

| Contrôle | Résultat |
|---|---|
| Tests Studio, tous targets desktop | 252 PASS, 0 échec, 3 ignorés ; les deux anciens lancements sont refusés |
| Format et diff du périmètre | PASS |
| Clippy Studio natif strict | PASS, warnings traités en erreurs |
| Frontend WASM/worker/assets et EXE desktop | PASS ; même checkout et même cache communs |
| Interface de la nouvelle fenêtre | Aucun bouton de jeu/reprise ; carte ouverte et rendue en édition |
| Départ enregistré | X 40 / Z -115 / cap 0°, contrôles d'édition disponibles |
| Données de zone | 41 objets persistants byte-identiques après le contrôle |
| Fermeture du contrôle | Native PID 31452, sortie normale 0 à 16:59:17 UTC |

Les 2539 entrées produit restent identiques entre admission, build et contrôle
UI : `fa6a995a17746eed2c9893285c7ef81c5726d719100a32ec42c70cc62680e6c6`.
Seuls huit fichiers Studio diffèrent des entrées `019`. L'EXE commun reste au
même chemin `E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002/release/forge-studio.exe`,
SHA-256 `416cb7ccb2602232586a532d2f528ef73cf99cbb26aba100c761feca3c26ff03`.
Le reçu est `retirement-receipt.json`. Les sources des autres conversations
et les définitions RPG sont conservées ; aucune nouvelle copie produit.

Le build Web conserve quatre warnings moteur et cinq warnings Studio déjà
présents dans les sources non modifiées. Il n'est pas présenté comme un Clippy
WASM strict PASS. Ce contrôle local du retrait ne remplace pas une CI distante
sur les modifications, ni la qualification complète de Gate 3.
