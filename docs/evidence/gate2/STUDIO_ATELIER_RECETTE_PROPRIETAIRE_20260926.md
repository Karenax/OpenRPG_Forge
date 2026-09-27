# Atelier — recette courte sur les écrans disponibles

Le test sur écran 4K physique est exempté à votre demande. Cette recette ne
demande ni nouveau matériel, ni compilation, ni modification d'une vraie carte.
Elle complète les essais automatisés ; elle ne clôt pas à elle seule Gate 2.

## Ouvrir la bonne version

Utiliser la fenêtre du candidat si elle est déjà ouverte. Sinon, depuis
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust`, lancer :

```powershell
./scripts/Start-StudioGoldReview.ps1
```

Ce lanceur utilise l'espace de test isolé, sans compiler. Ne pas lancer directement
l'exécutable sans son environnement de revue. Candidat vérifié le 26 septembre :
SHA-256 F3431207451D4D681E75B5AE7FB24D95420662C624E5117B61D9151EF0C46603.

## Parcours principal — quelques minutes

1. Dans Projet, choisir **R2-004 Final B**, puis **Ouvrir la zone choisie**.
   Ignorer l'ancienne archive signalée comme indisponible.
2. Dans la scène à gauche, sélectionner **Asset placé · vv-tree-oak-a**.
   Le panneau Objets doit montrer position, rotation et échelle sans devoir
   parcourir un long formulaire. Ne pas cliquer sur Appliquer ou Sauvegarder.
3. Ouvrir **Ancrer**, choisir **À gauche**, puis rouvrir le menu et choisir
   **À droite**. Réduire/déployer avec le bouton −/+ ; l'objet reste sélectionné.
4. Cliquer **Deux écrans**. La fenêtre secondaire doit montrer le même objet
   et les mêmes valeurs. La déplacer sur l'autre écran si nécessaire.
5. Dans le secondaire, remplacer seulement la position Y par **7.1**.
   Attendre **Brouillon de session conservé · non appliqué**, puis fermer le
   secondaire avec sa croix. Le principal doit conserver 7.1, sans erreur.
6. Cliquer **Reprendre les valeurs de la carte** : Y revient à **7**.
   La carte n'a pas été modifiée. Fermer sans enregistrer si vous avez terminé.

À signaler : texte trop petit, bouton coupé/inaccessible, panneau perdu,
valeur perdue, retour impossible ou message d'erreur. Une capture et le nom du
bouton suffisent ; inutile de rédiger un rapport technique.

## Essais matériels distincts, uniquement lorsque vous êtes disponible

- Indiquer résolution et mise à l'échelle Windows de chaque écran utilisé.
  Ne pas confondre zoom de l'application et mise à l'échelle Windows.
- Avec le secondaire sur l'autre écran et un brouillon de test conservé,
  déconnecter cet écran seulement si cela ne perturbe aucun autre travail.
  Vérifier que le secondaire redevient accessible ou que Rapatrier permet le
  retour sans perte de valeur. Rebrancher puis reprendre les valeurs de la carte.
- Pour la réduction des mouvements, utiliser votre préférence d'accessibilité
  habituelle et indiquer si elle est active. Aucun changement système n'est
  requis pour le parcours principal ci-dessus.

Ces essais ne demandent pas de provoquer artificiellement des écritures
simultanées : le conflit de brouillons conserve son statut de preuve automatique,
avec parcours graphique non acquis dans le bilan.

## Retour suffisant

« Écran 1 : …, … %. Écran 2 : …, … %. Mono/dual/retour : OK ou problème … .
Lisibilité et apparence : … . Déconnexion : non testée / OK / problème … . »

Une validation visuelle n'est pas interprétée comme validation des essais
matériels non réalisés. Aucun résultat n'est prérempli ni présumé réussi.
