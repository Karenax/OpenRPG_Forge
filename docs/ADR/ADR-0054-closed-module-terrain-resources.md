# ADR-0054 — Ressources terrain explicites des modules, API 8

Date : 26 octobre 2026. Statut : admission, réexport et transport natif qualifiés localement ; raccord renderer en cours.

Le moteur possède désormais le terrain source (ADR-0053), mais les modules ne
déclarent pas encore ses textures/programmes. Déduire ces ressources du nom ou
de la géométrie d'une carte reproduirait l'héritage implicite à retirer.

Ajouter un compagnon `terrain_surfaces` à l'API de module 8. Chaque liaison
désigne une zone existante, le hash de son descripteur raster canonique complet
et un paquet fermé de fichiers épinglés. Les rôles, tailles, fermeture du paquet
et conservation de sa licence sont contrôlés avant admission. Le codec partagé
reste l'autorité sur le format du paquet ; aucun second décodeur n'est créé.

L'absence de liaison retourne explicitement aucune ressource, sans lecture ni
bibliothèque globale. Le descripteur inclut pin des échantillons, grille, origine,
pas, encodage et hauteur extérieure ; un même pin avec un autre repère est refusé.
Les corrections auteur gardant cette source peuvent conserver les ressources.
Le réexport depuis une origine admise préserve la liaison et sa fermeture.

Les API 1–7 restent admises et leurs encodages vides inchangés. Elles refusent la
présence du nouveau champ, même vide. Les consommateurs anciens refusent l'API 8.
Le budget du compagnon est borné à 512 Mio de fichiers uniques par module, avec
au plus sept ressources par paquet pour le profil actuellement pris en charge.

La consommation par le viewport/jeu communs doit conserver peinture, relief,
collisions et barrières GPU. Le transport seul ne qualifie pas leur affichage
ni la clôture de Gate 7. Les essais réutilisent
les paquets immuables ; nouvelles preuves sous la campagne centrale existante,
préfixe `module-terrain-resources`, sans copie de checkout ou de cache Cargo.

Qualification initiale : `module-terrain-resources-validation-001.json` dans la campagne
centrale. Le workspace principal passe 2 751 tests ; le workspace séparé de
livraison passe ses 126 tests, puis un essai privé complet sur le paquet réel.
Celui-ci admet l'API 8, refuse une ressource altérée, retouche le document et
réexporte la même liaison fermée. Les cas sans compagnon, repère/pin différents,
fermeture et licence sont couverts. Les ressources d'origine sont revérifiées.
Clippy natif/Web et livraison, format et 25 frontières passent. Les essais
privés produisent 58 fichiers / 285 667 155 octets, sous 512 Mio / 1 024 fichiers.
Ces preuves ne qualifient ni transport Studio, ni rendu, ni performances.

Le bilan suivant `module-terrain-transport-validation-001.json` qualifie l'autorité
native du Studio, le protocole par morceaux, l'acquisition commune compilée aussi
pour Web et `LoadedModuleZone::terrain`. Le codec reste unique. Le paquet réel
requiert 713 requêtes initiales, puis une seule description au cache et après
retouche, avec la même allocation immuable ; annulation et ancienne tête sont
refusées. Le workspace passe 2 752 tests, la livraison 126 ; l'essai privé passe.
Cette acquisition exécutée en natif ne prouve ni WebView réel ni adoption GPU.
