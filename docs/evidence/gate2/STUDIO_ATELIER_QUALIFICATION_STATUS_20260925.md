# Atelier ivoire/champagne — audit de qualification, 25 septembre 2026

Objectif non clôturé. Ce bilan ne clôt pas Gate 2 et ne remplace pas le rapport
chronologique `STUDIO_ATELIER_INSPECTOR_20260924.md` ni le plan
`docs/planning/STUDIO_ATELIER_IMPLEMENTATION.md`.

Mise à jour du 26 septembre : le propriétaire ne possède pas d'écran 4K et
demande explicitement de passer cette vérification. Les essais sur écran 4K
physique sont **non réalisés — matériel indisponible, exception acceptée** et
ne sont plus bloquants pour cette refonte. Les preuves géométriques existantes
restent partielles ; aucune validation visuelle 4K ni performance n'est déduite.
Cette exception ne s'étend pas aux autres contrôles restant ouverts.

## Candidat remis à disposition

Exécutable unique de revue :
`output/studio-gold-20260924/candidate/forge-studio-candidate.exe`.
SHA-256 `F3431207451D4D681E75B5AE7FB24D95420662C624E5117B61D9151EF0C46603`.
Configuration normale 1280 × 720, minimum 1100 × 600, redimensionnable ;
aucun maximum 4K forcé ni raccourci de zoom expérimental.

Dernier correctif : retour du focus au déclencheur Ancrer après changement ou
réinitialisation de disposition. Compilé et rejoué graphiquement dans F343120.
WASM Clippy strict PASS ; build WASM release PASS ; build natif PASS.
Suite Studio relancée après ce correctif : 108 tests bibliothèque et 9 natifs
PASS, 1 benchmark local ignoré. Ce résultat n'est pas une qualification workspace.

## Confrontation aux exigences

| Exigence | Évidence et portée | Verdict |
|---|---|---|
| Structure claire mono-écran | Coque, huit sujets, scène, inspecteurs et bibliothèque observés ; preuves compactes et WQHD dans le rapport | Implémenté, revue esthétique finale propriétaire non acquise |
| Direction ivoire/champagne | CSS rendu concordant, captures inspectées, contrastes ciblés et focus corrigés | Preuve visuelle locale ; pas certification générale d'accessibilité |
| Panneaux dockables | Gauche/droite/bas/flottant, réduction, restauration observées ; préférences séparées et tests de repli petite hauteur | Implémenté ; dernier défaut clavier corrigé et rejoué |
| Secondaire optionnel | Fenêtre locale sans renderer dupliqué, sélection et bibliothèque partagées, inspecteur de transformation synchronisé | Observé ; autres formulaires métier restent dans le principal |
| Retour mono sûr | Fermeture par croix et bouton, sélection et brouillons conservés dans les parcours documentés | Observé ; débranchement physique non vérifié |
| Protection des saisies concurrentes | Compare-and-swap refusant un numéro périmé ; test préservant le brouillon accepté et réessayant avec nouvelle version | Test automatique PASS ; parcours graphique de conflit non acquis |
| Full HD et WQHD | F45BDC5 : principal 1280 × 720 puis 2560 × 1369, secondaire 1920 × 1009 ; neuf champs et captures complètes | PASS sur ces conditions ; pas toute la matrice de densités |
| 4K | 0550930 : vrai client natif 3840 × 2160 DPR 1 après redimensionnement, géométrie PASS | Preuve géométrique partielle ; essai physique non réalisé, exception propriétaire acceptée le 26 septembre, non bloquante |
| DPI Windows mixtes | Zoom WebView mesuré et défaut Projet corrigé ; dimensions/DPR distingués | Non qualifié : zoom différent du DPI Windows |
| Commandes et persistance | Parcours aperçu/appliquer/undo/redo/sauvegarder/réouvrir documenté AA5AF2B ; bâtiment B51183D | Preuves antérieures identifiées, pas présentées comme rejeu F343120 |
| Animations réduites | Règle prefers-reduced-motion présente, couvre les deux surfaces authoring-shell | Source vérifiée ; préférence OS non changée, pas de preuve visuelle dédiée |
| Périmètre de travail | Branche Rust, aucun sous-agent, aucun commit/push, cache et candidat uniques | Respecté pour cette réalisation |

Les contrôles Node de palette/géométrie/capture sont des contrôles ciblés,
pas une simulation de tous les parcours utilisateur. Un test de rectangles
de moniteurs ne prouve pas un hotplug physique ; un PNG tronqué ne prouve pas 4K.

## Suite nécessaire

Parcours propriétaire court et sans mutation de carte :
[recette du 26 septembre](STUDIO_ATELIER_RECETTE_PROPRIETAIRE_20260926.md).
Le SHA du candidat F343120 a été revérifié le 26 septembre ; il est inchangé.

1. Compléter les essais de densités sur le matériel Full HD/WQHD disponible,
   sans modifier implicitement les réglages Windows. Ne plus demander d'écran 4K.
2. Compléter les essais physiques de déplacement/retour après disparition
   d'écran, et les cases non exemptées de la matrice du plan.
3. Compléter la revue graphique des situations de conflit et de réduction du
   mouvement ; conserver les limites des tests actuels jusqu'à preuve directe.
4. Faire valider la réalisation visuelle et son usage par le propriétaire.

La configuration matérielle a été demandée au propriétaire. Le candidat est
laissé ouvert sur l'accueil. Aucun build ne reste lancé par cette tâche après
la suite Studio ; aucun nouvel historique de compilation n'a été créé.

## Tentative graphique de concurrence, F343120

Zone isolée ouverte, objet sélectionné, deux fenêtres actives. Saisie Y=7.1
dans le secondaire conservée comme brouillon non appliqué. La tentative
suivante depuis le principal a été refusée par l'automatisation (index UIA
devenu indisponible), avant toute saisie : aucun conflit applicatif provoqué.
Ne pas qualifier ce cas comme conflit résolu ni comme échec de l'application.
Pas de répétitions chronométrées non déterministes pour en déduire une preuve.

Reprendre les valeurs de la carte ramène Y=7, vérifié dans le secondaire puis
le principal. `draft-recovered-after-race-attempt` : secondaire 1920 × 1009,
géométrie et capture complète PASS. Fermeture Alt+F4 du secondaire et retour
à une seule fenêtre vérifiés. Aucun aperçu, application ou sauvegarde ; archive
SHA-256 A46FDC1E…21A8 inchangée. Principal laissé sur la zone, sans retouche.
