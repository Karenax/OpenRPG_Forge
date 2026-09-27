# Regroupement autonome des entrees du prototype natif

package-preview-scene.py rassemble les sept groupes existants : scene,
workspace auteur, batiments, ocean, rivage, embruns et nuages. Aucun document
Forge n'est transforme. Le manifeste enregistre taille et SHA256 de chaque
fichier, commit source, zone et pin du paquet batiments. Les liens, repertoires
de sortie existants, changements de source pendant copie et quotas depasses
sont refuses. La verification requiert un pin externe du manifeste et refuse
les groupes manquants, fichiers inattendus, absents ou modifies.

Sortie locale : E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-001.
1539 fichiers, 291477343 octets. Manifeste :
6c365047e7cb1e0e9995e934b4ecb60d7053c52e15145854261aff91806d9690.
Configuration d'entree conservee : preview-scene-inputs-001.json.

Le binaire natif deja qualifie b3303572d7b0b063b011c698f4b928c3446cca78968831789dc47aa0f959d139
est lance avec tous ses chemins de donnees diriges vers ce nouveau dossier.
Il termine sans erreur et produit native-bundled-scene-001.png. Une seconde
execution dirigee vers les dossiers d'origine produit native-original-replay-001.png :
images strictement identiques, hash PNG
789d82671aff9387d3082c1f582d289dbe3a8033d15be71e20a45f3bdf3984b9.
Les ombres de relief et profondeurs d'objets sont identiques a la baseline.

Le rapport bundled-scene-proof-001.json conserve la comparaison. Les deux executions different de
l'ancienne native-shared-cloud-scene-001.png sur 14 pixels, ecart maximal 2/255,
dans le rectangle [1742,453]-[1750,460] situe sur l'eau. Ce n'est pas un effet du
regroupement ; la cause precise de la variation de baseline reste non demontree.
L'image complete a ete inspectee : village, ponton, plantes, rivage, vagues et ciel.

Tests negatifs : mauvais pin refuse avant lecture des payloads ; premier octet
de sky.wgsl altere refuse ; restauration puis verification complete reussie ;
reutilisation du repertoire de sortie refusee. Les sources n'ont pas ete alterees.
Rapports bundled-scene-proof-001.json et preview-scene-bundle-negative-proof-001.json.

Limites : paquet du prototype a vue/temps fixes, pas un module distribuable ni
une carte Studio entierement integree. Les textures acquises qui existaient dans
les entrees restent presentes. Les anciens dossiers existent encore ; la preuve
porte sur le deplacement de tous les chemins explicites, sans trace exhaustive
des acces filesystem du processus. Ce binaire precede les derniers ajouts
Studio ; il n'est pas presente comme une nouvelle qualification du code courant.
