# Tidewater — préférences graphiques locales du Playtest Studio

Les quatorze réglages graphiques du Playtest sont maintenant mémorisés dans le
stockage local de la WebView, sous une clé versionnée. Leur modification ne
change ni le document de zone ni les sources Tidewater. Au démarrage, Studio
accepte uniquement un profil JSON complet dont les valeurs numériques sont
finies et dans les bornes des commandes ; une entrée absente ou invalide
redonne les valeurs par défaut. « Réinitialiser les effets » enregistre aussi
ces valeurs par défaut.

Le [parcours isolé du candidat 067](../../../../.tidewater-integration/studio-tidewater-graphics-persistence-smoke-067/fish-stand-studio-smoke-ledger.json)
est `passed`. Dans un profil WebView2 neuf, il règle l'exposition à −0,75 EV,
vérifie la valeur sérialisée, recharge la page, retrouve −0,75 dans le
curseur, réinitialise et constate 0,40 EV à l'écran et dans le stockage.
Il ouvre ensuite la bibliothèque et la zone, place/sauvegarde/rouvre
l'échoppe, entre en Playtest et marche jusqu'au tick 911. Océan FFT et
ShoreSim atteignent l'étape 480 ; les réglages d'AO, flare, brume et flou
avec l'océan restent actifs. Aucune erreur navigateur n'est capturée et
l'archive source garde le même SHA-256 avant/après. Le parcours prouve le
rechargement de page dans le même profil ; un redémarrage complet de
l'exécutable avec ce profil n'a pas été exercé séparément.

Candidat local :
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-graphics-persistence-candidate-067/forge-studio-candidate.exe`,
SHA-256 `eb9d1c0bf9b9470ae11a6f6895dd95bd895bac218a4283d0466d3ec0a67c5d15` ;
WASM release du frontend SHA-256
`bfeae304ee0cca1906a48d0a5d445fa4b55154cfae7e15b84df3978b3ed98b94`.
`cargo check` WASM, build release, `wasm-bindgen`, self-test du parcours et
`git diff --check` ont réussi. Le candidat n'est pas publié et ne prouve pas
la parité visuelle avec la démo publique.
