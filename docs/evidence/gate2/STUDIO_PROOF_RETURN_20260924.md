# Retour des scènes de preuve au Studio

Le bandeau des scènes techniques n'offrait aucun lien vers `?authoring=1`.
Ajout d'un lien permanent « Retour à l'éditeur », avec focus clavier visible.
Le script de rafraîchissement du candidat copie désormais aussi la feuille CSS
source, afin de ne pas embarquer une ancienne copie.

Vérifications locales du 24 septembre 2026 :

- `cargo fmt -p forge_studio --check` et vérification WASM : réussite.
- Compilation WASM release et candidat natif : réussite.
- Computer Use, surface 1920 × 1080 : clic sur « Scènes de preuve », affichage
  de Gate 0 et du lien de retour ; clic sur le lien ; retour observé au Studio,
  avec dock et panneau Projet. Fenêtre laissée ouverte pour le propriétaire.
- Exécutable testé SHA-256 :
  `347B889AB49AB2EC4D11CF780E9C12E518A92578B783B37269D326B8DEF2EFAD`.
- Document de référence inchangé :
  `62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

Limite : navigation avec rechargement, sans restauration des brouillons ni
réouverture automatique de la zone. Ce contrôle ne clôture pas la Gate 2.
