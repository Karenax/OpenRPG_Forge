# Tidewater — ressources de surface d'eau vérifiées dans Studio

Le rendu natif utilise les deux sorties de la FFT mais aussi le shader optique,
les uniformes, la mousse à onze mips, le champ de rive, les détails de mer et
la dentelle de rive à dix mips. Ces entrées manquaient au transport Studio.
Le paquet `forge.tidewater.water-surface-package` v1 contient **34 fichiers**
et **17 439 139 octets**. Il exclut les déplacements et dérivées calculés par
le GPU. La source est le bundle Tidewater figé
`c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335`,
révision `1438b1abfcaee3267092b75573014f4d9b4a983c` ; chaque payload a
été comparé au manifeste de cette source, et la licence MIT est incluse.
L'empreinte SHA-256 du nouveau manifeste est
`307f7af005da3ba4c4b35e3268880ce4443ae19c363daa73a54beb0057abcd93`.

Le chargeur `WaterSurfacePackage` refuse un manifeste différent, un ensemble
de fichiers incomplet, des tailles non conformes, les non-finis dans le champ
de rive, la mousse et les détails, les routes de shader non prises en charge
et un payload falsifié.
Les tests synthétique et acquis passent ; le test acquis altère aussi un octet
de l'uniforme de surface et vérifie le refus. La préparation du site vérifie
les 34 entrées avant copie. Studio télécharge et valide séquentiellement ce
paquet avant de rendre la bibliothèque importée disponible.
Le contrôle Clippy de `forge_assets` passe avec les sept catégories de lints
déjà signalées dans les fichiers partagés existants neutralisées pour le
diagnostic. Le contrôle strict sans ces exceptions reste ouvert ; il ne faut
pas présenter cette exécution ciblée comme une validation Clippy globale.

Le candidat isolé final
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-water-candidate-024/forge-studio-candidate.exe`
a le SHA-256
`937e93a4a5f879528b47f486864f63e70830244d7c83fd6bb94f88411d3d4e5e` ;
son WASM a le SHA-256
`297052a2d4bca7872d3214068e010d3a6b5c433ffdbcfd4f57108ca5c28e4851`.
Le parcours WebView complet sur copie isolée passe avec le nouveau paquet :
[reçu](../../../../.tidewater-integration/studio-tidewater-water-smoke-024/fish-stand-studio-smoke-ledger.json),
zone rouverte, jeu actif, marche à 317 ticks, nuages à 115 images, FFT à
l'étape 233 après progression vérifiée de 131 à 139, zéro erreur navigateur et
fixture source inchangée. Le reçu vérifie la bibliothèque publiée après
chargement ; il n'est pas une preuve de rendu de la surface.

Reste à brancher ces ressources et les textures FFT au maillage et au shader
optique dans le viewport, à dériver le champ de rive après retouche du relief,
puis à mesurer les images d'eau avec la référence. L'eau est encore invisible
dans le candidat 024.
