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

## 9. Drones : esquive des grenades ennemies — 🟡 Effet possible, dans le bruit
Le chef d'état-major voit les grenades en vol : `ArmyManagerRed.TryGetIncomingImpact()` intègre leur balistique (vitesse + gravité jusqu'au sol, via `TerrainManager`) pour prédire leur point d'impact, recalculé tous les 0,1 s. Les drones en déplacement circulaire (`StrafeMovement.Dodge()`) sortent de la zone de 7 m (rayon de dégâts 5 m + marge) dans les 2 dernières secondes avant l'impact, avec `EscapePoint()`. Aucune caractéristique des unités n'est modifiée.
- Test complet (v10) : 575 PV de drones rouges à 25 s (270 dans la v9 au même moment). Mais la bataille suivante (v11, même esquive) n'en a que 375 : **l'écart entre deux batailles est du même ordre que l'effet recherché**. Il faudrait plusieurs batailles par version pour conclure.

## 10. Drones : combat contre les drones ennemis quand nos tourelles sont mortes — 🟡 Non démontré
`ArmyManagerRed.ShouldFightEnemyDrones()` : quand toutes les tourelles rouges sont détruites et qu'il reste des drones ennemis, `SelectFocusEnemyTurret` échoue pour les drones, qui passent à `SelectFocusEnemyDrone` (achever les plus blessés). Idée : à ce stade les tourelles vertes ne ciblent plus nos tourelles mais nos drones, qui esquivent leurs grenades. Test (v11) : défaite à 39 s, aucun drone vert détruit ; l'effondrement des drones rouges a commencé avant la bascule.

## Pistes 2 et 3 (évaluées sans développement)
- **Débit de destruction des tourelles vertes** : mesuré à ~30 PV/s, soit à peu près le débit théorique de 13 drones (une salve de 10 PV toutes les ~3,5 s : Wait de 1 à 2 s imposé). Pas d'amélioration possible par le ciblage. Détruire les 900 PV de tourelles vertes avant la mort de nos tourelles (~15 à 20 s) demanderait le double.
- **Retarder la mort de nos tourelles** : leurs positions sont fixes et plusieurs sont à moins de 5 m les unes des autres (une grenade verte en touche plusieurs). Pas de levier stratégique identifié.

## 11. Correction du déplacement des drones — ✅ Vérifié (c'est la découverte la plus importante)
Les simulations mesurent la vitesse moyenne des drones : **nos drones ne bougeaient pas** (~1 m/s au lieu de 10). `MySeek` règle la distance d'arrêt de l'agent à 6 m et sa vitesse à 10 m/s ; mes destinations de déplacement circulaire étaient à moins de 6 m, donc l'agent se croyait déjà arrivé. Corrigé dans `StrafeMovement` : destinations lointaines (14 m), distance d'arrêt à 0, et déplacement **perpendiculaire à la ligne de tir du drone vert le plus proche** (l'IA verte tire toujours vers la position actuelle de la cible, sans anticiper).
- **Duel de drones sans tourelles (13 rouges contre 19 verts) : VICTOIRE**, 9 drones rouges restants (avant : défaite en 15 s). Pertes rouges 440 PV, pertes vertes 950 PV.

## 12. Espacement entre drones, groupes limités sur les tourelles — 🟡
Les grenades font des dégâts de zone de 5 m : chaque drone s'écarte du drone rouge le plus proche à moins de 14 m ; 4 drones au maximum par tourelle ennemie (`k_MaxDronesPerTurret`). Test isolé 13 drones contre les 9 tourelles vertes : pertes ~19 PV/s (avant ~23), peu d'écart.

## 13. Esquive des grenades v2 — 🟡 Effet marginal
Le chef d'état-major prédit les points d'impact de toutes les grenades en vol ; le drone calcule où il sera à chaque impact s'il continue et, si l'une le touche, choisit parmi 16 directions celle qui l'éloigne le mieux de tous les impacts. Activée (`k_DodgeGrenades`). Sans elle : défaite en 29 s ; avec : 38 s (une bataille chacune).

## 14. Tirer sans se rapprocher si la cible est à moins de 16 m — 🟡
Dans l'arbre : `Selector(Sequence(IsTargetInRange 16 m), MySeek)`. `MySeek` est inchangé quand il est utilisé. Comme `MySeek` est aussi ce qui règle la vitesse de l'agent (le prefab la laisse à 0,1), `StrafeMovement` applique la vitesse (10 m/s) et la vitesse angulaire (180°/s) de l'Annexe A.
(Mesure de cadence de tir de la simulation : faussée, elle compte les missiles en vol à l'instant de l'échantillon ; ne pas s'y fier.)

## 15. Tourelles d'abord avec toute la nouvelle mobilité — 🟡 Meilleur résultat à ce jour (défaite)
Ordre des cibles des drones : tourelles ennemies, puis drones (et bascule sur les drones quand nos tourelles sont mortes). Bataille v18 : **86 s, 9 tourelles vertes détruites à 30 s**, 12 drones rouges encore en vie à 30 s, puis duel de drones : 9 drones verts détruits, défaite avec **9 drones verts (250 PV) restants**.

## 16. Tourelles rouges : viser le point qui touche le plus de tourelles ennemies — 🟡 Première victoire (1 bataille)
Les tourelles vertes sont espacées de 6 à 11 m seulement et la grenade fait des dégâts de zone de 5 m (le collider d'une tourelle ajoute environ 0,5 m de portée). Une grenade bien placée en touche deux. On l'avait déjà observé : 4 tourelles vertes détruites en 10 s par nos 6 tourelles, bien au-dessus de leur cadence théorique.
- `ArmyManagerRed.GetSplashAimPoint()` : pour une tourelle rouge, parmi les positions des tourelles ennemies et les milieux de celles qui sont à moins de 11 m l'une de l'autre, on choisit le point qui inflige le plus de dégâts utiles (10 PV par tourelle touchée, moins ce qui est déjà réservé par des tirs en route), puis on réserve les dégâts sur chaque tourelle de la zone. Le point de visée est un `GameObject` invisible (comme pour le tir anticipé), donc `TurretSeekTarget` et `TurretShoot` ne sont pas modifiés.
- Les drones rouges, eux, gardent la répartition par groupes (`AssignTarget`).
- **Bataille v20 : VICTOIRE en 84 s, 7 drones rouges et 145 PV restants.** Les 9 tourelles vertes sont détruites à 25 s (30 à 35 s avant), avec 12 drones rouges encore en vie à 530 PV ; le duel de drones qui suit est gagné.
- **Reproductibilité : 1 victoire sur 2 batailles.** La seconde (v20b) est une défaite de peu après 125 s : les 9 tourelles vertes sont détruites à 25 s (13 drones rouges encore à 595 PV), mais le long duel de drones qui suit est perdu avec 8 drones verts (240 PV) restants. Les trois batailles précédentes de la même configuration (sans la visée de zone) étaient des défaites de peu (86 s, 75 s, 57 s). Il faut une série pour connaître le taux de victoire réel.

## 17. Drones : préférer les drones ennemis à l'arrêt, réévaluer la cible à chaque tour — 🟡 Effet non démontré
Pénalité de 40 sur le score d'un drone ennemi qui court (plus de 2 m/s) : un drone vert immobile (en train de tirer ou d'attendre) est une cible facile. Les tourelles ennemies restent gardées jusqu'à leur destruction. Bataille v19 : défaite en 57 s (dans le bruit).

## 18. Pistes testées pour renforcer le duel de drones — ❌ Aucune ne bat la configuration actuelle
Toutes mesurées sur une seule bataille ; les écarts restent dans le bruit, aucune n'a été conservée (code remis à l'état du commit v20).
- **5 drones seulement sur les tourelles, les autres sur les drones dès le début** (v21) : défaite en 68 s ; les tourelles vertes survivent plus longtemps (3 encore à 25 s) et les 8 drones envoyés sur les drones n'en détruisent presque aucun.
- **Distance de combat plus grande** (tir sans approche à 24 m au lieu de 16, orbite à 12-24 m au lieu de 6-18 m), mesurée sur le duel de drones sans tourelles : victoire en 85 s avec 5 drones restants, contre 60 s et 8 drones restants pour la référence (même ordre de grandeur, un peu moins bon).
- **Ouverture de 7 s sur les drones verts avant de partir vers les tourelles** (v22) : défaite en 59 s ; un seul drone vert détruit pendant l'ouverture et les tourelles tombent plus tard.
- Constat commun : le duel de drones isolé (13 contre 19) se gagne de façon reproductible (2 victoires sur 2 avec le code actuel, environ 2 pour 1 en PV), la bataille complète est à environ 1 victoire sur 2 (20 batailles nécessaires pour le taux réel).

---

## 19. Tourelles rouges : grenade de zone sur les groupes de drones verts — 🟡 À valider (6/6 contre 5/6, dans le bruit)
Nouvelle méthode `ArmyManagerRed.GetDroneSplashAimPoint` (appelée par `SelectFocusEnemyDrone` pour une tourelle) : on vise le point d'arrivée prévu (rotation + 4 s de vol) qui touche le plus de drones verts (rayon 4,5 m). Remplace le tir sur le drone le plus proche.
Mesure par séries de 3 batailles complètes, graines communes (`SIM_SEED`, outil hors git) : graine 100 → 3/3 (avant 3/3), graine 200 → 3/3 (avant 2/3). PV rouges restants : 157 en moyenne contre 139 avant. Une victoire de justesse (1 drone, 10 PV). Écart non significatif sur 6 batailles ; retenu car il ne dégrade rien.

## 20. Conformité : distance d'arrêt des drones rétablie à 6 m (Annexe A) — ✅ Appliquée (coût mesuré)
`StrafeMovement` mettait `stoppingDistance = 0` ; l'Arrive Distance de l'Annexe A (6 m) est désormais respectée. Les destinations sont placées 6 m plus loin pour que le pas parcouru reste de 14 m.
Séries de 3 batailles : graine 100 → 2/3 (3/3 avant), graine 200 → 2/3 (3/3 avant). Total 4/6 contre 6/6 : retenu malgré la baisse, la conformité prime.
Point encore ouvert (décision utilisateur) : les drones rouges se déplacent pendant le `Wait` (`ParallelSelector` Wait + `StrafeAround`) et pendant la visée (`StrafeAim`), alors que les drones verts restent immobiles.

## 21. Conformité stricte : plus aucun déplacement hors `MySeek`, `Wait` d'origine rétabli — ✅ Appliquée (décision de l'utilisateur, coût mesuré)
Règle : aucun avantage injuste sur l'armée verte, on ne gagne que par la stratégie et le chef d'état-major. Retirés : `StrafeMovement`, `StrafeAim`, `StrafeAround` (déplacement latéral, esquive des grenades, espacement pendant le `Wait` et la visée). Le `ParallelSelector` est remplacé par le `Wait` seul (1 à 2 s, immobile). Nouvelle tâche `PredictiveAim` : le drone pivote sur place (même loi que `MySlerpRotateAroundY`, 4 et 5°) vers la position anticipée de la cible.
Séries de 3 batailles : graine 100 → 1/3, graine 200 → 1/3. Total 2/6 (4/6 avant, 6/6 avec l'arrêt à 0 m). Défaites typiques : tourelles vertes détruites mais 11 à 15 drones verts restants (290 à 365 PV).

## 22. Pistes testées en configuration stricte — ❌ Aucune retenue (tout dans le bruit ou pire)
Séries de 3 batailles (graines 100 et 200), base stricte = 2 victoires sur 6 (PV rouges restants 22 en moyenne).
| Variante | Victoires / 6 | PV rouges (moy.) | Décision |
|---|---|---|---|
| `k_ExpectedDamagePerDrone` 5 (groupes plus gros) | 3 | 90 | dans le bruit, non retenu |
| `k_ExpectedDamagePerDrone` 10 | 2 | 58 | non retenu |
| Tourelles : drones d'abord, tourelles ensuite | 1 | 6 | ❌ nettement pire (défaites vers 45 s) |
| `IsTargetInRange` 30 m au lieu de 16 m | 2 | 10 | non retenu |
Analyse du déroulé : à la fin de la phase d'artillerie (20 à 27 s) Rouge a ~440–565 PV contre ~775–940 PV ; le duel de drones qui suit se fait à environ 2 PV pour 1 : on est au seuil, d'où 1 sur 3 à 1 sur 2. Le code est revenu à l'état de la section 21.

## 23. Un seul drone à la fois par tourelle verte (`k_MaxDronesPerTurret` 4 → 1) — 🟡 6/6 sur les graines de réglage, à confirmer sur d'autres graines
Les drones s'étalent sur les tourelles vertes au lieu de se grouper autour de la même : sans esquive ni espacement (retirés par la conformité stricte), un tas de drones prend les grenades de zone d'un coup. Tendance monotone mesurée (victoires sur 6) : 8 par tourelle → 0, 4 → 2, 2 → 3, **1 → 6**. PV rouges restants 236 en moyenne (22 avant). Écartés au passage : choix par valeur du tir de zone des tourelles (1/6), groupes de 8 (0/6).
Réglé sur les graines 100 et 200. **Validation sur des graines neuves (300 et 400) : 5 victoires sur 6** (2/3 et 3/3 ; la défaite : 5 drones verts restants, 95 PV). Total 11/12 en configuration stricte. Statut : 🟡 solide mais pas encore 20/20.

## 24. Groupes de tir plus petits sur les drones verts (`k_ExpectedDamagePerDrone` 7 → 10) — 🟡 Une seule série (3/3), à confirmer
Une salve de drone compte 10 PV attendus au lieu de 7 : moins de drones rouges assignés à chaque drone vert (5 au plus au lieu de 8), donc le feu se répartit sur plus de cibles. Série de 3 batailles, graine 300 : 3/3 (avant : 2/3), PV rouges restants 170 (avant 112), durée 65 s. Un seul jeu de graines : écart d'une bataille, non démontré. À confirmer sur d'autres graines avant de passer en ✅.

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
| 10. + esquive des grenades | 40 s | défaite | 19 / 0 / 875 | drones rouges à 575 PV à 25 s |
| 11. + combat de drones en phase 2 | 39 s | défaite | 19 / 2 / 940 | drones rouges à 375 PV à 25 s (même esquive : bruit) |
| Duel de drones, déplacement corrigé | 52 s | **victoire** | 0 / – / 0 (rouge : 9 drones, 210 PV) | 13 contre 19, sans tourelles |
| 12. Mobilité corrigée, tourelles d'abord | 43 s | défaite | 16 / 3 / 760 | |
| 14. Drones d'abord, tir sans approche | 53 s | défaite | 18 / 6 / 1100 | |
| 15. Tourelles d'abord + mobilité (v18) | 86 s | défaite | 9 / 0 / 250 | |
| 15b. Même configuration (v18b) | 75 s | défaite | 12 / 0 / 355 | reproductible |
| 17. + drones à l'arrêt en priorité (v19) | 57 s | défaite | 12 / 2 / 325 | |
| **16. + tourelles rouges : visée de zone (v20)** | **84 s** | **VICTOIRE** | **0 / 0 / 0** (rouge : 7 drones, 145 PV) | première victoire en bataille complète |
| 16b. Même configuration (v20b) | 125 s | défaite | 8 / 0 / 240 | tourelles vertes détruites à 25 s, duel de drones perdu de peu |
| Duel de drones, référence (code v20) | 60 s | victoire | 0 / – / 0 (rouge : 8 drones, 175 PV) | sans tourelles |
| Duel, distance de combat 24 m | 85 s | victoire | 0 / – / 0 (rouge : 5 drones, 150 PV) | pas mieux |
| 18a. 5 drones sur les tourelles (v21) | 68 s | défaite | 12 / 2 / 355 | écarté |
| 18b. Ouverture de 7 s sur les drones (v22) | 59 s | défaite | 11 / 1 / 295 | écarté |
| Référence v20, graine 100 (3 batailles) | 85 s | 3/3 | rouge : 7,7 drones, 188 PV | séries |
| Référence v20, graine 200 (3 batailles) | 95 s | 2/3 | rouge : 90 PV | |
| **19. Tourelles : zone sur drones, graine 100** | 109 s | 3/3 | rouge : 115 PV | |
| **19. Tourelles : zone sur drones, graine 200** | 83 s | 3/3 | rouge : 198 PV | |
| **20. Arrêt à 6 m (conformité), graine 100** | 81 s | 2/3 | rouge : 160 PV | |
| **20. Arrêt à 6 m (conformité), graine 200** | 79 s | 2/3 | rouge : 172 PV | |
| **21. Stricte (immobile pendant Wait et visée), graine 100** | 76 s | 1/3 | rouge : 37 PV | |
| **21. Stricte, graine 200** | 56 s | 1/3 | rouge : 7 PV | |
| **24. Groupes de tir de 10 PV par drone, graine 300** | 65 s | 3/3 | rouge : 170 PV | avant : 2/3, 112 PV |

---

## Idées restantes (backlog)
Voir **`HANDOFF.md` §6** : 16 pistes classées (mesure par séries et graines communes en premier, fonctionnalité de soin, abris et ligne de vue, réglage fin des constantes, esquive des missiles, nettoyage du code mort, etc.).
