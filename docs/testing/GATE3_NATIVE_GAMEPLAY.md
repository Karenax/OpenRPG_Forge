# Jeu RPG séparé et qualification Gate 3

Le Studio commun exporte la carte courante et démarre le jeu configuré dans une
fenêtre séparée. Les commandes de jeu utilisent le même document RPG et le même
noyau de simulation que les contrats de gameplay. La carte authorée reste
immuable pendant la partie ; la sauvegarde de jeu est une donnée distincte.

## Parcours utilisateur

- **G** ouvre les actions et les dialogues accessibles à la position courante.
  La simulation revalide la portée, la visibilité et les conditions de chaque
  commande. Fermer le panneau rend la main au déplacement.
- **F5** sauvegarde la partie quand l'état physique est stabilisé. Un refus
  explique la condition à résoudre ; il ne remplace pas une sauvegarde existante.
- **F9** demande explicitement de reprendre cette sauvegarde au prochain
  lancement. Fermer le jeu puis le relancer depuis Studio effectue la reprise
  dans un nouveau processus. Sans demande de reprise, le lancement est une
  nouvelle partie et conserve la sauvegarde précédente.
- Après une retouche de texte ou de pose compatible dans Studio, le panneau
  permet une reprise compatible explicite. Une reprise refusée ouvre les choix
  de récupération en suspendant joueur et gameplay. La nouvelle partie et la
  reprise compatible nécessitent une action explicite ; Quitter préserve la
  demande et la sauvegarde. Une modification des règles ou des références
  fonctionnelles reste incompatible.
- Après fermeture du jeu, le Studio résident reprend sa carte et sa caméra.
  Le bilan du jeu conserve les diagnostics et l'identité de la reproduction.
  Un rapport absent, invalide ou issu d'une autre session reste signalé.

Le point d'entrée commun reste `E:/OpenRPG_Forge/Lancer-Editeur.cmd`. Le profil
voisin de `release/forge-studio.exe` choisit le jeu et ses données. Le backend
place les sauvegardes sous `game.session_root/saves`, par module et carte.

## Preuves automatisées

Le protocole [du checkout commun](GATE3_SHARED_CHECKOUT.md) construit les trois
exécutables, authentifie les entrées et conserve les reçus natifs, WebGPU et
supply-chain. Les contrats nommés sont lus par
`scripts/Validate-Gate3NativeGameplay.py headless` ; leur réussite ne constitue
pas une observation du jeu, du GPU ou du parcours humain.

Le helper `crates/forge_project/examples/prepare_native_gameplay_fixture.rs`
prépare des **données de test** depuis un export réel du Studio. Il vérifie ses
blocs, refuse une référence manquante sans appliquer la proposition et peut
retoucher un texte et une porte via `prepare_gameplay`, Preview et Apply. Les
reçus annoncent explicitement que cette voie SDK ne prouve pas l'interface Studio.

`scripts/Run-Gate3NativeGameplay.py` démarre le véritable jeu commun avec la
recette Studio, une copie de données authentifiée et un scénario épinglé. Le
hook de qualification est facultatif : il soumet les commandes normales de jeu
et de déplacement, attend leurs confirmations exactes, capture le rendu Bevy
et ferme normalement son processus. Il ne téléporte pas le joueur et n'écrit
pas directement l'état simulé. Les captures demandent une inspection visuelle.

Le lecteur strict conserve les événements causaux : commandes commises, contacts,
chaleur, dégâts, extinction, déplacement KCC et reprise dans un second processus.
La garde de retrait après ignition reste de **1 000 ms**. Les délais, les échecs
historiques et les capacités absentes ne sont pas remplacés par une case verte.

P01 (lieux et deux approches), P02 (retouche et reprise), P03 (diagnostic), P04
(état choisi et isolation), acceptation propriétaire et CI restent des preuves
distinctes. La clôture exige leur rapprochement sur la version effectivement
qualifiée ; un build, un rapport de sortie ou un processus fermé ne suffit pas.
