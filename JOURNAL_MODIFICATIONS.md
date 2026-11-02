# Journal des modifications — IA de l'armée ROUGE

Ce fichier sert de base aux ReleaseNotes. Chaque modification a un statut :
- ✅ **Validée** : testée en jeu, effet mesuré.
- 🟡 **À valider** : implémentée et compilée, pas encore testée en jeu.
- ❌ **Abandonnée** : testée, sans effet ou négative (on garde la trace de la raison, utile pour la réflexion stratégique).

Seules les modifications ✅ sont à présenter comme « ce qui fonctionne » dans la livraison finale.

---

## 1. Focus fire (ciblage concentré) — 🟡 Partiellement validé

**Retour de test (2 nov.)**
- ✅ Tourelles : le comportement attendu est observé (elles concentrent leurs grenades sur la même tourelle verte, puis passent à la suivante).
- ❌ Drones : ils visaient un drone vert très éloigné (score basé sur les PV + distance au barycentre de l'armée, pas à l'unité). Corrigé dans la modification 2.
- ⚠️ Les tourelles rouges sont rapidement détruites par les drones verts. Traité dans la modification 3.

**Idée.** L'armée verte choisit ses cibles au hasard, donc sa puissance de feu est dispersée. Les unités rouges (moins nombreuses) se concentrent sur la même cible pour éliminer les ennemis un par un : une unité morte ne tire plus.

**Fichiers modifiés / créés**
| Fichier | Changement |
|---|---|
| `Assets/Scripts/Army/ArmyManagerRed.cs` | Ajout de `GetFocusEnemy<T>()` (désigne la cible commune) et `ReserveRocketShot()` (anti sur-ciblage des grenades). Le code existant (statistiques dans le presse-papiers) est inchangé. |
| `Assets/Scripts/MyBehaviorTrees/SelectFocusEnemy.cs` (nouveau) | Tâches Behavior Designer `SelectFocusEnemyDrone` et `SelectFocusEnemyTurret`, utilisées à la place des `SelectEnemy*` aléatoires. |
| `Assets/Scripts/MyBehaviorTrees/DroneRedBehavior.asset` | `RandomSelector(drone, turret)` remplacé par `Selector(SelectFocusEnemyDrone, SelectFocusEnemyTurret)` : les drones visent d'abord un drone ennemi, sinon une tourelle. |
| `Assets/Scripts/MyBehaviorTrees/TurretRedBehavior.asset` | `SelectEnemyTurret/Drone` remplacés par `SelectFocusEnemyTurret/Drone` (même ordre : tourelles d'abord). |

Rien n'a été modifié côté vert ni dans les caractéristiques de l'Annexe A (rayons min/max, vitesses, Wait, etc. identiques).

**Fonctionnement**
- Pour chaque candidat ennemi dans l'anneau `[minRadius, maxRadius]` de l'unité qui demande, le score est :
  `score = PV restants + 1 × distance au barycentre de l'armée rouge − 15 si c'est déjà la cible focalisée`
  La cible au score le plus bas est choisie. Comme le barycentre est commun à tous, toutes les unités tombent sur la même cible tant qu'elle est à leur portée ; un ennemi blessé devient prioritaire (il finit de mourir). Le bonus de 15 évite de changer de cible à chaque appel.
- **Anti sur-ciblage (tourelles).** Quand une tourelle rouge choisit une tourelle ennemie (fixe, donc la grenade touche à coup sûr), elle « réserve » 10 PV de dégâts pendant `temps de rotation + 4 s de vol`. Les tourelles suivantes soustraient ces dégâts réservés aux PV de la cible : quand elle est déjà condamnée, elles passent à la suivante. Pas de réservation sur les drones (mobiles, la grenade peut les manquer).
- Constantes réglables en tête de `ArmyManagerRed` : `k_DistanceWeight`, `k_FocusStickiness`.

**Vérifications faites**
- Compilation indépendante (SDK .NET fourni avec Unity, csproj du projet + nouveau fichier) : 0 erreur.
- Relecture du JSON des arbres après modification : structure et paramètres corrects.
- **Pas encore testé en jeu** : à faire dans Unity (voir ci-dessous).

**À valider**
- [ ] Unity recompile sans erreur à l'ouverture (console vide).
- [ ] Les arbres rouges s'affichent correctement dans la fenêtre Behavior Designer.
- [ ] En jeu, les drones rouges convergent vers un même drone ennemi.
- [ ] Les tourelles rouges concentrent leurs grenades sur une même tourelle verte, puis passent à la suivante.
- [ ] Résultats sur 20 batailles (voir tableau ci-dessous).

**Risques connus**
- Tous les drones sur la même cible peuvent se gêner sur le NavMesh (arrêt à 6 m de la cible) et se regrouper : plus vulnérables aux grenades vertes (zone de 5 m).
- Le barycentre de toute l'armée (drones + tourelles fixes) est un repère grossier.
- Les drones ciblent les drones d'abord : à comparer avec « tourelles d'abord ».

---

## 2. Drones rouges : cible la plus proche, conservée jusqu'à sa mort — ❌ Remplacée (voir 5)

**Problème.** Avec le focus fire global, les drones allaient vers un ennemi lointain (la cible était choisie par rapport au barycentre de l'armée, pas à la position du drone).

**Changement**
- `ArmyManagerRed.GetNearestEnemy<T>()` : renvoie l'ennemi le plus proche **du drone qui demande** (dans l'anneau `[minRadius, maxRadius]`). Si la cible précédente du drone est encore vivante, elle est conservée : le drone tire dessus jusqu'à ce qu'elle meure.
- `SelectFocusEnemyDrone` et `SelectFocusEnemyTurret` (`SelectFocusEnemy.cs`) utilisent cette règle quand l'appelant est un **drone**. Pour une **tourelle** qui cherche une tourelle, le focus fire de la modification 1 reste en place.
- Arbre `DroneRedBehavior` inchangé par rapport à la v1 (`Selector` : drone d'abord, sinon tourelle).
- Effet attendu : moins de trajet sous le feu, et un focus naturel (les drones voisins choisissent le même ennemi proche).

## 3. Tourelles rouges : défense contre les drones qui s'approchent, tir avec anticipation — ❌ Abandonnée

**Problème.** Les tourelles ne ciblaient que des tourelles ennemies (ou des drones lointains) et se faisaient détruire par les drones verts qui s'arrêtent à 6 m d'elles pour tirer.

**Changement**
- `TurretRedBehavior` : nouveau premier enfant du `Selector`, **`Select Threatening Drone`** (`SelectFocusEnemyDrone`, rayon min 5 m, max **20 m**). Si un drone ennemi est à moins de 20 m, la tourelle le vise en priorité ; sinon elle reprend le comportement précédent (focus sur les tourelles, puis drones à 5–50 m). Les deux nœuds `Select Enemy` d'origine gardent leurs rayons de l'Annexe A (5/50).
- `ArmyManagerRed.GetAimPoint()` : la grenade met 4 s à tomber et le drone bouge. Un point de visée invisible (un `GameObject` vide enfant du manager, un par tourelle) est placé à la **position anticipée** du drone : sa position + vitesse du `NavMeshAgent` × (temps de rotation + 4 s), limitée à la distance restante jusqu'à son point d'arrivée. `TurretSeekTarget` et `TurretShoot` (partagés avec le vert, non modifiés) visent ce point.
- Limite connue : on ignore ce que le drone fera ensuite (le chef d'état-major ne connaît pas les intentions) ; un drone arrêté qui repart avant l'impact échappe à la grenade.

**Bilan (simulation hors éditeur)** : avec ces deux changements, l'armée rouge est détruite en 29 s en moyenne (10 batailles), contre 52 s avec l'IA d'origine. Les tourelles qui se détournent vers les drones perdent le duel d'artillerie, et les drones qui attaquent chacun l'ennemi le plus proche se dispersent comme les verts. Remplacés par les modifications 4 à 6.

**À valider**
- [ ] Les drones rouges attaquent le drone vert le plus proche et le gardent jusqu'à sa mort.
- [ ] Les tourelles rouges réagissent aux drones qui approchent (à vérifier : les grenades tombent bien là où le drone s'arrête).
- [ ] Les tourelles rouges survivent plus longtemps qu'avant.
- [ ] Le nœud ajouté s'affiche bien dans Behavior Designer.

---

## Outil de simulation (hors dépôt)
Pour mesurer sans lancer Unity à la main : `sim-tools/run_sim.sh [N] [étiquette] [révision git]` (dossier ignoré par git). Il copie le projet dans le dossier temporaire, compile un exécutable Mac, lance N batailles sans fenêtre, en basse priorité et avec un pas de temps fixe de 1/60 s, puis écrit un fichier de résultats et un suivi toutes les 5 s (unités et PV de chaque camp). Une seule bataille par test : c'est bruité, on lit surtout le déroulé.

## Ce que les simulations ont appris (IA d'origine, 54 s, défaite)
- Les **tourelles vertes (9) détruisent toute l'artillerie rouge en 15 à 25 s** : le duel d'artillerie 6 contre 9 est perdu d'avance.
- Les **drones rouges ne meurent en masse qu'après la destruction des tourelles rouges** : les tourelles vertes visent d'abord les tourelles, puis les drones, et leurs grenades (zone de 5 m) font des ravages sur des drones groupés.
- Les drones rouges de l'IA d'origine ne détruisent presque aucun drone vert (1 à 3 en 54 s).

## 4. Drones : répartition en groupes, tirs anticipés, déplacement circulaire — 🟡 Effet non démontré
- `ArmyManagerRed.AssignTarget<T>()` : les drones sont répartis en groupes sur les ennemis les plus avancés vers le centre de notre armée, chaque ennemi recevant juste assez de drones pour être détruit (PV / dégâts d'une salve). Chaque drone garde sa cible jusqu'à sa mort.
- `DroneAim` (visée anticipée : vitesse de la cible + temps de vol du missile), `StrafeMovement` / `StrafeAim` / `StrafeAround` : le drone tourne autour de sa cible au lieu de rester immobile, pivote vers le point anticipé (même loi que `MySlerpRotateAroundY` : coefficient 4, angle d'arrivée 5°) et tire. Pendant l'attente de 1 à 2 s (inchangée) il continue de bouger (`ParallelSelector` Wait + `StrafeAround`). Vitesse et vitesse angulaire de l'agent inchangées (fixées par `MySeek`, Annexe A).
- Arbre `DroneRedBehavior` : `Selector(cible) → MySeek → StrafeAim → DroneShoot → ParallelSelector(Wait, StrafeAround)`.
- **Bilan** : dans un duel de drones (sans tourelles), le rouge et le vert ont la même efficacité par drone (~2,2 PV/s chacun) : le déplacement circulaire et le tir anticipé ne donnent pas d'avantage mesurable pour l'instant. Conservés (sans effet négatif visible) en attendant mieux.

## 5. Drones : attaquer d'abord les tourelles ennemies — 🟡 Très prometteur
**Idée.** Les tourelles vertes sont la principale source de dégâts contre nous, elles sont fixes, ne visent pas les drones tant qu'il reste des tourelles rouges, et ne peuvent pas cibler à moins de 5 m. Les éliminer en premier avec les drones.
- Arbre `DroneRedBehavior` : le `Selector` essaie `SelectFocusEnemyTurret` avant `SelectFocusEnemyDrone`. Capacité d'un groupe sur une tourelle : 10 PV par drone (tous les missiles touchent une cible fixe).
- Tourelles rouges : retour au focus sur les tourelles vertes (modification 1).
- **Résultat (1 bataille)** : les 9 tourelles vertes sont détruites à 35 s (l'IA d'origine en laisse 3 après 54 s) ; les 13 drones rouges sont encore tous vivants à 20 s. Défaite finale (38 s) face aux 19 drones verts intacts : il reste à gagner le combat de drones.


## 6. Drones : achever les plus blessés en priorité — 🟡 Prometteur
Dans `AssignTarget`, pour les drones ennemis, l'ordre des cibles est `PV + distance au drone qui demande` (au lieu de la distance au centre de l'armée) : on transforme les dégâts en destructions. **Test de duel (sans tourelles, 13 drones rouges contre 19 verts, 1 bataille)** : 7 drones verts détruits et 640 PV infligés pour 650 PV perdus, contre 2 détruits et 420 PV avant ce changement. Défaite quand même (24 s), mais proche de l'équilibre.

## 7. Drones : « légitime défense » (combattre un drone ennemi à moins de 12 m) — ❌ Abandonnée
Test complet (v8) : détourne les drones des tourelles vertes sans tuer assez de drones (3 détruits) ; 6 tourelles vertes survivent et massacrent les drones rouges dès que nos tourelles sont mortes. Retiré.

## 8. Drones et tourelles rouges se partagent les tourelles ennemies par groupes — 🟡 Neutre
Les tourelles rouges utilisent maintenant `AssignTarget<Turret>` comme les drones (capacité : 10 PV par unité), pour éviter que toute l'armée vise la même tourelle. Test complet (v9) : pas d'accélération mesurable de la destruction des tourelles vertes (1 restante à 35 s, comme avant). Les méthodes `GetFocusEnemy` et `ReserveRocketShot` de `ArmyManagerRed` ne sont plus utilisées par les arbres.

## Constats des simulations (v6 à v9)
- Détruire les tourelles vertes en premier avec les drones est la seule piste qui a nettement amélioré les choses (9 tourelles détruites en ~38 s, drones rouges encore presque intacts à 20 s).
- Mais le rythme de destruction (~25 PV/s, environ la moitié du débit théorique de 13 drones + 6 tourelles) est trop lent : **tant qu'il reste des tourelles vertes quand les nôtres meurent (vers 15-20 s), elles s'acharnent sur nos drones**, qui s'effondrent en 10 à 15 s.
- Une fois les tourelles vertes détruites, il reste 19 drones verts intacts face à quelques drones rouges blessés : défaite.
- Pistes non encore testées : esquive des grenades à partir de leur trajectoire (visible par le chef d'état-major), attaque des tourelles verte par leur flanc/angle mort, retarder la mort des tourelles rouges, mieux comprendre pourquoi le débit de destruction est à ~50 % du théorique (trajets, portée, alignement).

---

## Résultats de simulation

| Version | Batailles | Victoires | Drones vivants (moy.) | Tourelles vivantes (moy.) | PV restants (moy.) | Durée (moy., s) |
|---|---|---|---|---|---|---|
Une bataille par ligne (bruité). `vert restant` = ce qu'il reste à l'armée verte à la fin (plus c'est bas, mieux c'est).

| Version | Durée | Résultat | Vert restant (drones / tourelles / PV) | Remarque |
|---|---|---|---|---|
| Base (arbres d'origine) | 54 s | défaite | 16 / 3 / 870 | référence |
| 3. Plus proche + défense des tourelles (10 batailles) | 29 s moy. | 0/10 | — | pire que la base |
| 3b. Groupes de drones, tourelles focus | 26 s | défaite | 18 / 7 / 1180 | drones se regroupent |
| 4. + tir anticipé | 21 s | défaite | 18 / 6 / 1425 | |
| 5. + déplacement circulaire | 23 s | défaite | 19 / 6 / 1370 | |
| 6. Drones sur les tourelles d'abord | 38 s | défaite | 19 / 0 / 800 | toutes les tourelles vertes détruites |
| Duel de drones (sans tourelles), avant 6 | 15 s | défaite | 17 / – / 530 | 2 drones détruits |
| Duel de drones, achever les blessés | 24 s | défaite | 12 / – / 310 | 7 drones détruits |
| 7. Tourelles d'abord + blessés en priorité | 48 s | défaite | 19 / 0 / 870 | tourelles détruites à 40 s, drones rouges épuisés |
| 8. + légitime défense | 37 s | défaite | 16 / 6 / 1050 | abandonné |
| 9. Partage par groupes (tourelles aussi) | 39 s | défaite | 19 / 1 / 960 | équivalent à 7 |

---

## Idées restantes (backlog)
Esquive des grenades, abris/ligne de vue, tir avec avance des drones, point d'impact optimal des grenades (viser le centre d'un groupe), rôles et phases dans `ArmyManagerRed`, outil de simulation accélérée avec journal CSV. Voir la discussion initiale.
