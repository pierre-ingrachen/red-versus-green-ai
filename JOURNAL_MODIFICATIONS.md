# Journal des modifications — IA de l'armée ROUGE

Ce fichier sert de base aux ReleaseNotes. Chaque modification a un statut :
- ✅ **Validée** : testée en jeu, effet mesuré.
- 🟡 **À valider** : implémentée et compilée, pas encore testée en jeu.
- ❌ **Abandonnée** : testée, sans effet ou négative (on garde la trace de la raison, utile pour la réflexion stratégique).

Seules les modifications ✅ sont à présenter comme « ce qui fonctionne » dans la livraison finale.

---

## 1. Focus fire (ciblage concentré) — 🟡 À valider

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

## Résultats de simulation

| Version | Batailles | Victoires | Drones vivants (moy.) | Tourelles vivantes (moy.) | PV restants (moy.) | Durée (moy., s) |
|---|---|---|---|---|---|---|
| Base (arbres d'origine) | _à mesurer_ | | | | | |
| 1. Focus fire | _à mesurer_ | | | | | |

---

## Idées restantes (backlog)
Esquive des grenades, abris/ligne de vue, tir avec avance, point d'impact optimal des grenades, rôles et phases dans `ArmyManagerRed`, outil de simulation accélérée avec journal CSV. Voir la discussion initiale.
