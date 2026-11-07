# ADR-0061 — Surface et ressac explicites dans les modules, API 10

Date : 28 octobre 2026. Statut : transport et réexport qualifiés localement ; rendu en cours.

## Décision

L'API 10 étend les entrées d'océan de l'API 9 par deux paquets facultatifs mais
indissociables : surface et simulation de ressac. Ils sont choisis par zone,
sans recherche implicite dans une bibliothèque globale. L'API 9 conserve son
format et refuse les nouveaux champs, même lorsqu'ils sont explicitement nuls.
L'index et chaque ressource sont épinglés par taille et SHA-256, les rôles et
licences sont vérifiés, l'inventaire est fermé et la limite globale des ressources
uniques reste fixée à 256 Mio par module. Le résolveur refuse les désaccords
entre le champ de rivage et les paramètres partagés par les deux paquets.

Les lecteurs existants de ces deux formats sont déplacés vers `forge_assets` ;
ils reçoivent leurs octets d'un lecteur fourni par l'appelant. Le module installé
les lit depuis son inventaire autonome vérifié. Le réexport conserve les paquets,
leurs pins et leurs licences. Les identités historiques des formats source et
leurs contraintes de provenance restent en vigueur : cette étape n'admet pas
encore n'importe quel nouveau format de surface ou de simulation.

## Qualification et limites

Sur les sources 137, l'essai privé `shared-water-module-private-tests-001.log`
emballe et installe les paquets réels, vérifie leur lecture puis refuse une
incohérence de paramètres même lorsque les empreintes sont recalculées. Le
réexport de ces paquets et de leurs licences passe dans
`shared-water-module-export-private-001.log`. Les données sont copiées dans des
fixtures temporaires isolées sur E:, retirées à la fin des essais ; les paquets
réels comprennent 17 444 131 et 22 440 388 octets, index inclus.

Sur les sources 137 vérifiées inchangées par l'inventaire 138, Clippy strict
livraison et natif passe, ainsi que six tests du lecteur de stockage mutualisé,
deux tests des paquets d'effets et 29 contrôles de frontière. Les sources 139
ne changent ensuite que l'ordre d'imports ; Clippy strict Web passe avec un
avertissement d'incompatibilité future de `proc-macro-error2`.
Ces tests ciblés ne qualifient pas une migration de stockage concurrente en
cours. Les paquets distribués restent autonomes et n'exigent pas le Vault local.

Le jeu de module et le producteur Studio refusent toujours les entrées d'océan
tant que le rendu complet n'est pas raccordé. La simulation de ressac, l'optique,
le rendu et les interactions restent à intégrer au moteur commun et à qualifier
sur un autre module, avec images et performances. Ces résultats ne clôturent pas
Gate 7 et ne constituent pas une qualification Linux ou Windows vierge.
