# Tidewater — résolution de la population importée

`forge_world::imported_vegetation::ImportedPopulation` résout les blocs du
document canonique avec un callback de stockage recevant le hash et la taille
attendue. Aucun accès disque ni objet GPU dans ce module. Le décodeur vérifie
chaque bloc ; une erreur ne retourne aucune population partielle.

Les instances conservent leur identité, transformation f64 et paramètres de
prototype. Un index dérivé les répartit en secteurs de 64 m. Le bord positif
appartient au dernier secteur, sans secteur supplémentaire hors de la carte.
Cet index n'est pas une seconde source de données éditables.

Preuve : `E:/OpenRPG_Forge/.tidewater-integration/imported-population-verification-001.json`.
Les dix blocs de `imported-vegetation-001` sont chargés : **65383 instances**,
**65383 appartenances uniques aux secteurs**, aucune instance perdue ou dupliquée.
Le hash du document reste
`964761707a8e135fa0ef471e31b5a1a88a57414c9a3e83d55ff0b11e8043e0d7`.

Deux tests vérifient les frontières exactes, la conservation des transformations,
le rejet des blocs absents/corrompus et l'absence de génération procédurale de
secours. Clippy du module et de l'exemple passe avec `-D warnings`.

Le résultat est la population **de base**. Les exclusions d'auteur, promotions,
densités, collisions et la projection des prototypes restent à raccorder. Le
callback de démonstration lit les fichiers préparés ; il ne constitue pas encore
le magasin de blobs des modules Studio. Aucune nouvelle parité visuelle ni
qualification Studio/WebGPU n'est revendiquée par cette étape.
