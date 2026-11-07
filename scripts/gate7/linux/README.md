# Qualification Linux en conteneur

Le propriétaire a proposé Docker le 23 octobre 2026 et demandé de continuer.
Docker Desktop 4.96.3 est installé sur E:, avec Hyper-V et sans conteneurs Windows.
La nouvelle ouverture de session Windows a rendu le groupe `docker-users`
effectif ; le moteur Linux fonctionne. Aucune déconnexion ou aucun redémarrage
du poste ne sont déclenchés par les scripts.

## Périmètre

L'image Rust officielle Linux/amd64 est épinglée par digest ; les versions des
paquets système réellement installées sont enregistrées dans `environment.log`.
Les dépôts APT restent ceux de Debian Bookworm : le digest de l'image ne prétend
pas figer les futures réponses APT ou garantir un binaire identique octet pour octet.
Le SDK sert aux tests monde/moteur, Clippy strict, vrai jeu distribué, diagnostic
de parcours et launcher Aube de production. Aucune qualification de bureau,
de GPU, d'installateur ou de machine propre n'en découle.

Le snapshot ZIP est produit depuis le checkout commun par `package_vm_sources.py`
avec inventaire/hash et lecture intégrale. Il est copié dans le conteneur par
`docker cp` : aucun montage du workspace, du Vault ou des preuves. La copie dans
le conteneur est une entrée de qualification immuable, jamais un checkout de
développement. Les builds frontend et Cargo sont dérivés de cette copie ; chaque
fichier auteur inventorié est revérifié après réussite ou échec.

Le volume nommé `forge-gate7-linux-build` conserve le cache Linux sur le disque
Docker de E:. Le SDK tourne en UID 1000, sans privilèges hôte. La VM Docker est
bornée à 4 CPU, 16 Gio RAM et 64 Gio de disque. Le premier essai à 8 Gio et
deux jobs a été tué par manque de mémoire en release ; le second utilise un job
Cargo et un plafond conteneur de 15 Gio. Le profil release produit reste intact.
Estimation : snapshot 0,5 Gio / environ 6 200 fichiers, SDK et caches 10–50 Gio.
Les preuves utiles restent dans la campagne centrale, sous le budget de 20 Gio.

## Reprise après reconnexion Windows

1. Démarrer Docker Desktop installé sous
   `E:/OpenRPG_Forge/.build/tools/docker-desktop/Docker Desktop.exe` et vérifier
   le moteur avec `docker --context desktop-linux info`.
2. Réutiliser l'image SDK utile déjà construite si ses dépendances conviennent.
   Si une reconstruction est nécessaire, construire ce Dockerfile depuis ce
   seul répertoire de contexte, plateforme `linux/amd64`. Conserver log, ID et
   inspection dans les preuves ; un Dockerfile seul ne prouve pas son build.
   Un changement du runner seul peut être copié dans `/incoming` et exécuté par
   `--entrypoint python3` : épingler alors son SHA-256 dans le reçu hôte, en plus
   de l'ID de l'image et du snapshot. Ne pas multiplier les images pour ce cas.
3. Produire un nouveau ZIP source et lire son reçu. Créer un conteneur nommé
   `forge-gate7-linux-<run-id>` avec le volume `forge-gate7-linux-build` monté
   sur `/qualification`, plafonds `--cpus 2 --memory 15g --memory-swap 16g`,
   variable `--env CARGO_BUILD_JOBS=1`.
   Passer au runner `--run-id <run-id> --source /incoming/source.zip
   --source-sha256 <hash-du-reçu>` ; refuser tout nom de conteneur déjà utilisé.
4. Copier le ZIP vers `/incoming/source.zip`, puis démarrer avec attachement
   des logs. Le runner s'arrête au premier échec, conserve son reçu et revérifie
   les sources. Les commandes Cargo utilisent `--locked` ; aucune correction
   n'est faite dans cette copie. Corriger le checkout commun puis fournir un
   nouveau snapshot et un nouvel ID d'essai si nécessaire.
   Pour un échec de ressources sur des sources identiques, `--resume-from <id>`
   permet de reprendre au premier échec : SHA du même ZIP, sources inchangées,
   commandes du protocole et hashes des logs précédemment réussis sont vérifiés.
   Les anciens reçus et sources sont conservés ; les contrôles hérités sont
   explicitement identifiés. Une modification du produit exige un nouveau ZIP.
   `--suite shells` vérifie le launcher de référence. L'ancienne suite Hub
   est conservée avec la recette historique dans l'archive du retrait 048.
5. Après arrêt, copier `/qualification/results/<run-id>` vers les temporaires
   de qualification sur E:, vérifier hashes et exit code, puis centraliser les
   logs/reçus utiles sur F:. Conserver les binaires sur E: pour les essais
   suivants. Inspecter les essais graphiques séparément. Vérifier et conserver les
   preuves, dont le `source-manifest.json` exact et son hash dans le reçu, ainsi
   que le hash du transport revérifié en fin d'exécution. Supprimer ensuite le
   conteneur arrêté par son ID exact,
   sans `--force` ni suppression du volume. Retirer les anciennes images SDK
   remplacées ; conserver seulement l'image utile et le cache partagé pendant
   la campagne. Ne pas lancer de prune global ou supprimer une VM utile.

Le nettoyage propriétaire du 23 octobre retire les cinq conteneurs terminés et
les trois anciennes images SDK. Le reçu central `docker-cleanup-001.json`
conserve leurs identités, les résultats vérifiés et les inventaires avant/après.
L'image utile est `forge-gate7-linux-sdk:20261023-shells` ; le volume
`forge-gate7-linux-build` reste nécessaire aux compilations suivantes. Les quelque
2 Go de couches de conteneurs retirées sont un gain logique Docker ; le VHDX
de Docker ne se réduit pas automatiquement d'autant sur le lecteur Windows.

Contrôle local du transport, sans Docker :

```powershell
python -B -m unittest discover -s scripts/gate7/linux -p test_qualification.py -v
```

La VM `Forge-Gate7-Linux-Clean` est distincte du SDK Docker et de l'ancien disque
QEMU. Son image Ubuntu officielle et son état initial sans Rust/Cargo/GCC/Clang/Node
sont conservés dans la campagne. `prepare_clean_runtime.py` installe explicitement
les bibliothèques d'exécution, Vulkan logiciel et un serveur X virtuel ; il ne
constitue pas l'installateur produit. Leurs versions et les prérequis réellement
présents doivent accompagner tout verdict sur cette machine. Le rendu logiciel
de cette VM ne qualifie pas les performances du GPU cible.

`run_clean_delivery.py` accepte aussi le paquet Studio privé Linux de la
campagne. Transférer `forge-delivery` et `forge-studio.forgepack` depuis le
même candidat vérifié. Dans l'invité sans SDK, utiliser des chemins absolus
et les deux SHA-256 du reçu du candidat.

```sh
python3 -B run_clean_delivery.py --run-id linux-clean-studio-001 \
  --binary "$delivery_cli" --sha256 "$delivery_sha256" \
  --studio-package "$studio_pack" --studio-package-sha256 "$studio_sha256"
```

Ce mode valide le paquet avec le service de livraison, applique le plan
sur une installation isolée, vérifie l'éditeur installé, puis le désinstalle
en contrôlant le fichier témoin conservé. Le parcours auteur, la reprise
d'un jeu et la mise à jour ou restauration entre deux versions exigent
leurs propres observations. Le mode sans paquet conserve le scénario
historique avec fixture.
