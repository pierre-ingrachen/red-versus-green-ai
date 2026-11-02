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

## 2. Drones rouges : cible la plus proche, conservée jusqu'à sa mort — 🟡 À valider

**Problème.** Avec le focus fire global, les drones allaient vers un ennemi lointain (la cible était choisie par rapport au barycentre de l'armée, pas à la position du drone).

**Changement**
- `ArmyManagerRed.GetNearestEnemy<T>()` : renvoie l'ennemi le plus proche **du drone qui demande** (dans l'anneau `[minRadius, maxRadius]`). Si la cible précédente du drone est encore vivante, elle est conservée : le drone tire dessus jusqu'à ce qu'elle meure.
- `SelectFocusEnemyDrone` et `SelectFocusEnemyTurret` (`SelectFocusEnemy.cs`) utilisent cette règle quand l'appelant est un **drone**. Pour une **tourelle** qui cherche une tourelle, le focus fire de la modification 1 reste en place.
- Arbre `DroneRedBehavior` inchangé par rapport à la v1 (`Selector` : drone d'abord, sinon tourelle).
- Effet attendu : moins de trajet sous le feu, et un focus naturel (les drones voisins choisissent le même ennemi proche).

## 3. Tourelles rouges : défense contre les drones qui s'approchent, tir avec anticipation — 🟡 À valider

**Problème.** Les tourelles ne ciblaient que des tourelles ennemies (ou des drones lointains) et se faisaient détruire par les drones verts qui s'arrêtent à 6 m d'elles pour tirer.

**Changement**
- `TurretRedBehavior` : nouveau premier enfant du `Selector`, **`Select Threatening Drone`** (`SelectFocusEnemyDrone`, rayon min 5 m, max **20 m**). Si un drone ennemi est à moins de 20 m, la tourelle le vise en priorité ; sinon elle reprend le comportement précédent (focus sur les tourelles, puis drones à 5–50 m). Les deux nœuds `Select Enemy` d'origine gardent leurs rayons de l'Annexe A (5/50).
- `ArmyManagerRed.GetAimPoint()` : la grenade met 4 s à tomber et le drone bouge. Un point de visée invisible (un `GameObject` vide enfant du manager, un par tourelle) est placé à la **position anticipée** du drone : sa position + vitesse du `NavMeshAgent` × (temps de rotation + 4 s), limitée à la distance restante jusqu'à son point d'arrivée. `TurretSeekTarget` et `TurretShoot` (partagés avec le vert, non modifiés) visent ce point.
- Limite connue : on ignore ce que le drone fera ensuite (le chef d'état-major ne connaît pas les intentions) ; un drone arrêté qui repart avant l'impact échappe à la grenade.

**Vérifications faites (2 et 3)** : compilation indépendante sans erreur, JSON de l'arbre relu. Pas encore testé en jeu.

**À valider**
- [ ] Les drones rouges attaquent le drone vert le plus proche et le gardent jusqu'à sa mort.
- [ ] Les tourelles rouges réagissent aux drones qui approchent (à vérifier : les grenades tombent bien là où le drone s'arrête).
- [ ] Les tourelles rouges survivent plus longtemps qu'avant.
- [ ] Le nœud ajouté s'affiche bien dans Behavior Designer.

---

## Résultats de simulation

| Version | Batailles | Victoires | Drones vivants (moy.) | Tourelles vivantes (moy.) | PV restants (moy.) | Durée (moy., s) |
|---|---|---|---|---|---|---|
| Base (arbres d'origine) | _à mesurer_ | | | | | |
| 1. Focus fire | _à mesurer_ | | | | | |
| 2+3. Plus proche + défense des tourelles | _à mesurer_ | | | | | |

---

## Idées restantes (backlog)
Esquive des grenades, abris/ligne de vue, tir avec avance des drones, point d'impact optimal des grenades (viser le centre d'un groupe), rôles et phases dans `ArmyManagerRed`, outil de simulation accélérée avec journal CSV. Voir la discussion initiale.
