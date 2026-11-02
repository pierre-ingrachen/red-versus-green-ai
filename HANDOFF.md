# Passation — IA de l'armée ROUGE (RED versus Green)

Document écrit pour **l'agent (ou l'humain) qui reprend le développement**. À lire après `CLAUDE.md` (contexte et règles du sujet). Le détail chronologique de toutes les modifications et de tous les résultats est dans `JOURNAL_MODIFICATIONS.md`.

Dernière mise à jour : 2 novembre 2026. État git : branche `main`, arbre propre, dernier commit « Journal : pistes testées pour le duel de drones ».

---

## 1. En une page : où on en est

**Objectif du projet** : l'armée ROUGE (13 drones + 6 tourelles) doit battre l'armée VERTE (19 drones + 9 tourelles, IA par défaut non modifiable). Contraintes strictes : ne pas modifier l'armée verte, les caractéristiques de l'Annexe A, le fonctionnement du jeu ni les capacités des unités ; uniquement des changements stratégiques (arbres de comportement, tâches Behavior Designer, chef d'état-major `ArmyManagerRed`).

**Résultat actuel** (configuration du commit `9f20583`, inchangée depuis) :
- Bataille complète : **1 victoire sur 2 batailles** (84 s, 7 drones rouges restants ; puis défaite de peu à 125 s avec 8 drones verts à 240 PV). Le taux réel est **inconnu** : il faut une série (voir §6, piste 1).
- Duel de drones isolé (13 contre 19, sans aucune tourelle) : **2 victoires sur 2**, environ 2 PV perdus côté vert pour 1 côté rouge.
- Les 9 tourelles vertes sont détruites vers 25 s ; ensuite c'est un duel de drones (nos ~13 drones à ~535 PV contre ~19 à ~870 PV) qui se joue à peu de chose. **C'est là qu'il faut gagner de la marge.**
- Départ (IA d'origine) : 0 victoire, défaite en ~52 s.

**Ce qui n'est PAS fait** (obligatoire pour la livraison, voir `CLAUDE.md` §Livraison) :
1. Le lot de **20 batailles** + fichier CSV (tabulations) au format de l'Annexe B. *Je (agent précédent) l'ai proposé à l'utilisateur, qui n'a pas encore donné son accord explicite : demande-le avant de lancer (≈ 25 à 40 min en basse priorité).*
2. La **fonctionnalité différenciante** (second objectif du sujet : nouvelle unité, soin, etc.) : **pas commencée**.
3. `ReleaseNotes`, **vidéo** d'un combat épique, **archive** `EPF_2026_2027_DecisionAI_UnityProject_<Noms>.zip` (sans `Library`).
4. **Vérification dans l'éditeur Unity** (voir §5) : tout a été testé via un exécutable construit en ligne de commande, jamais ouvert dans l'éditeur depuis les derniers changements.
5. Date limite : le PDF dit « dimanche 29 novembre **2025** », coquille probable pour 2026 : à confirmer avec l'enseignant.

---

## 2. Comment on a travaillé (méthode à reproduire)

Le cycle suivi tout au long de la session, et qui a bien fonctionné :

1. **Observer avant d'agir.** Chaque simulation écrit un suivi toutes les 2,5 s (unités et PV de chaque camp, vitesse moyenne des drones). On lit le **déroulé**, pas seulement le résultat final. Presque toutes les découvertes importantes sont venues d'une anomalie dans ce déroulé (ex. « nos drones vont à 1 m/s »).
2. **Formuler une hypothèse mécanique** (« les grenades touchent des drones groupés », « les drones sautent MySeek donc la vitesse reste à 0,1 »…) et la vérifier par **un test isolé** quand c'est possible : retirer des unités de la scène pour isoler un facteur (`SIM_NO_TURRETS=1`, `SIM_REMOVE="red:turret,green:drone"`). Ces scénarios de diagnostic ont été décisifs.
3. **Modifier le minimum**, compiler sans Unity (§3.3), lancer **une seule bataille** (préférence explicite de l'utilisateur, voir §7), lire le déroulé.
4. **Consigner** dans `JOURNAL_MODIFICATIONS.md` (statut ✅ / 🟡 / ❌, fichiers, résultat, ligne dans le tableau) **dans le même commit** que le code.
5. **Commiter** (français, sans `Co-Authored-By`), y compris les pistes abandonnées (on garde la trace de pourquoi).
6. **Ne jamais conclure sur une seule bataille** quand l'effet attendu est petit : l'armée verte est aléatoire et l'écart entre deux batailles identiques est du même ordre que la plupart de mes réglages (ex. 86 s / 75 s / 57 s pour trois variantes quasi identiques). Les seuls effets nets vus en une bataille étaient *gros* (mobilité corrigée, visée de zone, tourelles d'abord).

Principe de communication avec l'utilisateur : rapports en français, honnêtes (dire « défaite » quand c'est une défaite), courts, avec tableau avant/après quand c'est utile ; ne pas annoncer un succès non mesuré ; corriger ses propres erreurs de mesure (j'ai dû retirer une conclusion fausse sur la cadence de tir, voir §4).

---

## 3. Outillage (à connaître absolument)

### 3.1 Simulation hors éditeur — `sim-tools/` (volontairement **hors git**)
Dossier `sim-tools/` à la racine du projet, listé dans `.gitignore` (demande explicite de l'utilisateur : « ne l'inclus pas dans le git »). Il existe sur le disque mais **n'est pas versionné** : si le dossier a disparu, le recréer d'après la spécification ci-dessous.

- `sim-tools/run_sim.sh [N=1] [étiquette] [révision git ou ""] [timescale=1] [fps=60]` — chaîne complète :
  1. copie le projet dans `/private/tmp/rvg_sim/proj` (variable `SIM_DIR`), **avec `Library`** la première fois (≈ 1 Go) pour éviter un réimport complet ; ensuite `rsync` de `Assets`, `Packages`, `ProjectSettings` (ou d'une révision git extraite avec `git archive <rev>` : permet de tester **n'importe quel commit**, ex. `053d013` = jeu d'origine) ;
  2. y ajoute `Assets/_Sim/SimRunner.cs` et `Assets/_Sim/Editor/SimBuild.cs` ;
  3. compile un exécutable Mac (`Unity -batchmode -nographics -quit -executeMethod SimBuild.Build`, journal `/private/tmp/rvg_sim/build.log`), **seulement si des fichiers ont changé** ;
  4. lance l'exécutable en `-batchmode -nographics -job-worker-count 2` ; tout (compilation et simulation) tourne sous `nice -n 19 taskpolicy -b` (priorité minimale, cœurs économes : l'utilisateur s'était plaint que l'ordinateur chauffait) ;
  5. affiche un résumé (`summarize.py`).
- **Pas de temps fixe** : `Time.captureFramerate = 60` — chaque image fait avancer le jeu d'exactement 1/60 s quelle que soit la charge. Résultats reproductibles et indépendants de la priorité. (Sans cela, ralentir le processus augmentait le dt et faussait le comportement.)
- Variables d'environnement de diagnostic : `SIM_NO_TURRETS=1` (supprime toutes les tourelles des deux camps = duel de drones) ; `SIM_REMOVE="red:turret,green:drone"` (liste `camp:type`, types `turret` ou `drone`).
- Sorties dans `/private/tmp/rvg_sim/` : `results_<étiquette>.tsv` (colonnes `INDEX_BATTLE IS_VICTORY DURATION ALIVE_DRONES ALIVE_TURRETS REMAINING_CUMULATED_HEALTH_POINTS STATUS GREEN_DRONES GREEN_TURRETS GREEN_HEALTH`, `STATUS` ∈ VICTORY/DEFEAT/DEADLOCK ; une impasse n'est pas comptée et la bataille est relancée) et `run_<étiquette>.log` (lignes `[SIM] t=…s  rouge Nd/Nt/PV   vert …   vitesse moy. …`).
- **Détails de `SimRunner.cs`** (pour le recréer) : script `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, actif seulement si l'argument `-simBattles N` est présent ; lit `-simTimeScale`, `-simFps`, `-simTimeout` (360 s de jeu = impasse), `-simOut`, `-simNoTurrets`, `-simRemove` ; retrouve les tags des armées par réflexion sur le champ privé `ArmyManager.m_ArmyTag` ; compte les unités vivantes par tag (`ArmyElement` avec `Health > 0`, `Turret` vs reste = drones) ; attend que les PV soient initialisés ; détecte la fin quand une armée est à 0 ; recharge la scène entre deux batailles ; `Application.Quit()` à la fin.
- **Détails de `SimBuild.cs`** : `BuildPipeline.BuildPlayer` pour `Assets/Scenes/RedVersusGreenBattle.unity`, `StandaloneOSX`, **stripping désactivé** (Behavior Designer retrouve les tâches par leur nom : le stripping les supprimerait), `EditorApplication.Exit(code)`.
- Prérequis : Unity 6000.6.0f1 dans `/Applications/Unity/Hub/Editor/`, **Unity Hub ouvert** (service de licence), `rsync`. L'éditeur Unity peut rester ouvert ou être fermé (la simulation travaille sur une copie).
- Durée : quelques dizaines de secondes à environ 2 min par bataille (+ 1 à 2 min de compilation si le code a changé).

### 3.2 Pièges rencontrés avec l'outil
- **La mesure de cadence de tir est fausse** (elle compte les missiles encore en vol à l'instant de l'échantillon ; un missile vit moins d'une seconde, donc sous-estimation d'un facteur ~3). Ne pas s'y fier (les grenades, qui volent 4 s, sont mieux comptées).
- Dans cette version d'Unity, `Object.GetInstanceID()` est obsolète (erreur CS0619) : utiliser les objets eux-mêmes dans un `HashSet<UnityEngine.Object>`.
- `Tooltip` est ambigu entre Unity et Behavior Designer : écrire `[BehaviorDesigner.Runtime.Tasks.Tooltip(...)]`.
- `grep` renvoie parfois un code d'erreur quand il n'y a pas de correspondance : ne pas s'arrêter à `exit code 1` sur un `grep -c`.

### 3.3 Vérifier la compilation sans Unity
Unity ne recompile qu'au retour au premier plan (et pas en mode Play). Pour vérifier sans lui : copier `Assembly-CSharp.csproj` dans un dossier temporaire, remplacer les chemins relatifs (`Compile Include="Assets/…"` et `HintPath` commençant par `Assets/`, `Library/`, `Packages/`) par des chemins absolus, ajouter les nouveaux `.cs` dans un `<ItemGroup>`, supprimer les `ProjectReference`, puis :
`…/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet build Check.csproj -nologo` (avec `DOTNET_CLI_HOME` dans un dossier temporaire). Résultat attendu : `0 Error(s)`. C'est fiable (les erreurs de la compilation de l'exécutable ont toujours été détectées là d'abord, sauf l'API obsolète du simulateur).

### 3.4 Éditer les fichiers sans les casser
- **`ArmyManagerRed.cs` est en Windows-1252.** `Edit`/`Write` refusent ce fichier : l'éditer avec un script Python (`open(p, encoding="cp1252", newline="")`) en insérant du texte avec des accents compatibles cp1252. Les autres fichiers (`MyBehaviorTrees/*.cs`) sont en UTF-8.
- **Arbres Behavior Designer** (`DroneRedBehavior.asset`, `TurretRedBehavior.asset`) : YAML contenant `JSONSerialization: '<json>'` replié sur plusieurs lignes. Recette fiable : `re.search(r"JSONSerialization: '(.*?)'\n", t, re.S)` → remplacer `\n\s+` par un espace → `json.loads` → modifier (`ID` uniques, listes `Children`, `Variables`) → `json.dumps(separators=(",",":"), ensure_ascii=False)` → doubler les `'` → réinjecter. Copier la forme JSON d'un nœud existant du même type. Un nouveau champ public `SharedFloat x` d'une tâche s'écrit `"SharedFloatx":{"Type":"BehaviorDesigner.Runtime.SharedFloat","Name":null,"SinglemValue":v}` ; un `SharedTransform` : `{"Type":"BehaviorDesigner.Runtime.SharedTransform","Name":"Target","IsShared":true}`.
- **Nouveau script C#** : créer aussi son `.cs.meta` (GUID aléatoire, modèle `MonoImporter`), sinon Unity en génère un autre chez chaque personne. Les tâches sont référencées **par nom de classe** dans les arbres, pas par GUID.
- **Garde-fou de conformité** (à relancer avant toute livraison) : `git diff --stat 053d013 HEAD -- Assets/Prefabs Assets/Scenes ProjectSettings Packages Assets/Scripts/Army/ArmyManagerGreen.cs Assets/Scripts/Army/Drone.cs Assets/Scripts/Army/Turret.cs Assets/Scripts/Army/Missile.cs Assets/Scripts/Army/Rocket.cs Assets/Scripts/Army/Health.cs Assets/Scripts/Army/ArmyManager.cs "Assets/Scripts/MyBehaviorTrees/*Green*"` doit être **vide**. Il l'est au dernier commit ; `053d013` est le commit d'état initial.

### 3.5 Git
- Dépôt local, branche `main`, pas de remote, **ne pas pousser**. Commits en français, **sans ligne `Co-Authored-By` ni mention de Claude** (consigne explicite de l'utilisateur, prioritaire sur l'attribution par défaut). Un commit par modification cohérente + mise à jour du journal.
- Commits clés : `053d013` jeu d'origine · `cf8c8dc` v1 focus fire · `ac236e2` drones sur les tourelles d'abord · `334fcbb` déplacement corrigé (v18) · **`9f20583` visée de zone des tourelles (v20, 1ʳᵉ victoire)** · `82be04f`, `8fb7207` journaux.
- Versionné : `Assets/`, `Packages/`, `ProjectSettings/`, `.gitignore`, `CLAUDE.md`, `JOURNAL_MODIFICATIONS.md`, `HANDOFF.md`. Ignoré : `Library/ Temp/ Logs/ UserSettings/`, `*.csproj`, `*.sln*`, `.vscode/`, `.claude/`, `sim-tools/`.

---

## 4. Ce qu'on a appris sur les mécaniques du jeu (faits mesurés ou lus dans le code)

**Drone** : 50 PV, **un seul collider sphérique de 0,4 m** (les missiles doivent passer très près). `NavMeshAgent` du prefab : `m_Speed = 0.1` (!), accélération 8. C'est **`MySeek.OnStart` qui règle la vitesse (10 m/s), la vitesse angulaire (180°/s) et la distance d'arrêt (6 m)** ; ces réglages persistent ensuite sur l'agent. `MySeek.OnEnd` appelle `Stop()`.
**Boucle verte** : choix aléatoire d'un drone ou d'une tourelle rouge (rayon 0 à 1000), `MySeek` vers la position de la cible **au moment du choix** (jamais mise à jour), arrêt à 6 m, rotation lente vers la position **actuelle** de la cible (Slerp 4, 5°), tir de 2 missiles **sans anticiper ni vérifier la ligne de vue**, `Wait` 1 à 2 s. Un drone vert est donc **immobile ~2 à 3 s par cycle**, et tire toujours là où la cible *est*.
**Missile** : part à la vitesse de l'agent, +40 m/s² jusqu'à 20 m/s, vit 3 s (≈ 55 m), 5 dégâts, explose sur le **premier collider d'un autre tag** (terrain, caisses, unités de l'autre camp), suit le relief à hauteur constante. 6 m → ~0,4 s de vol ; 24 m → ~1,2 s.
**Tourelle** : 100 PV ; vol de la grenade **toujours 4 s** quelle que soit la distance ; vise la position de la cible **au moment du tir** ; `Physics.OverlapSphere` de 5 m : touche **tout collider d'un autre tag** dans le rayon (pas de tir ami), d'où les **dégâts de zone** ; anneau de sélection 5 à 50 m (ne peut pas viser à moins de 5 m) ; cycle ≈ 2,5 s (sélection + rotation à 90°/s + `Wait` 1 à 3 s). **Les tourelles vertes visent d'abord les tourelles rouges, puis seulement les drones** : tant qu'il reste des tourelles rouges, nos drones sont relativement à l'abri des grenades.
**Géographie** (coordonnées x/z de la scène) : tourelles vertes x ∈ [-29, -17], z ∈ [-22, 28] ; tourelles rouges x ∈ [17, 26] ; drones verts au départ x ∈ [-11, -3] (groupés, ~15 m des nôtres) ; drones rouges x ∈ [6, 12]. **Les tourelles d'un même camp sont espacées de 6 à 11 m** : une grenade de 5 m en touche souvent deux. Distances rouge↔vert entre tourelles : 35 à 63 m (certaines paires sont hors de portée 50 m).
**Chronologie typique** : nos tourelles meurent entre 15 et 25 s (grenades vertes + drones verts) ; avant cela, nos drones prennent peu de dégâts.

**Découvertes qui ont tout changé (par ordre d'importance)** :
1. **Nos drones ne bougeaient pas** (~1 m/s mesuré, au lieu de 10) : mes destinations de déplacement circulaire étaient à moins de 6 m, et `MySeek` avait laissé `stoppingDistance = 6`. Correction : destinations à 14 m, `stoppingDistance = 0`, déplacement **perpendiculaire à la ligne de tir** du drone vert le plus proche. Le duel de drones est passé de défaite à victoire.
2. **Sauter `MySeek` (tir sans approche) saute aussi le réglage de vitesse** (0,1 m/s dans le prefab) : `StrafeMovement` applique donc explicitement 10 m/s et 180°/s (valeurs de l'Annexe A).
3. **Visée de zone des tourelles** : viser le point (une tourelle ou le milieu de deux) qui touche le plus de tourelles ennemies, en tenant compte des tirs déjà en route. 4 tourelles vertes détruites en 10 s, et toutes à ~25 s.
4. **Drones sur les tourelles ennemies d'abord** (puis sur les drones) : les tourelles vertes sont la principale source de pertes rouges.
5. L'esquive des grenades n'est utile que modérément : il y a ~10 grenades en l'air en permanence face à 9 tourelles.
6. Une bataille de 60 à 125 s est normale ; les pertes sont déterminées par la phase d'artillerie (0 à 25 s) puis par un long duel de drones.

---

## 5. Architecture actuelle de l'IA rouge (v20)

**Arbre drone** (`DroneRedBehavior.asset`) : `Repeater(∞) → Sequence[ Selector(SelectFocusEnemyTurret, SelectFocusEnemyDrone), Selector « Move only if needed »[ Sequence(IsTargetInRange 16 m), MySeek ], StrafeAim, DroneShoot, ParallelSelector(Wait 1–2 s aléatoire, StrafeAround) ]`.
**Arbre tourelle** (`TurretRedBehavior.asset`) : `Repeater → Sequence[ Selector(SelectFocusEnemyTurret 5–50 m, SelectFocusEnemyDrone 5–50 m), TurretSeekTarget, TurretShoot, TestEPFTask, Wait 1–3 s ]` (les nœuds `Turret*` d'origine ne sont pas modifiés).

| Fichier | Rôle |
|---|---|
| `Army/ArmyManagerRed.cs` (cp1252) | Chef d'état-major. `AssignTarget<T>` (répartition des drones en groupes : drones ennemis par PV + distance + pénalité s'ils courent ; tourelles ennemies les plus avancées, 4 drones max par tourelle) ; `GetSplashAimPoint` (visée de zone des tourelles + réservations de dégâts) ; `GetNearestEnemy` + `GetAimPoint` (tourelle contre drone : tir anticipé) ; prédiction des impacts de grenades (`RefreshImpacts`, `GetIncomingImpacts`) ; positions des drones amis/ennemis ; `ShouldFightEnemyDrones` (phase 2). **Code mort** : `GetFocusEnemy`, `ReserveRocketShot` (plus appelés). Constantes réglables en tête (`k_MaxDronesPerTurret`, `k_ExpectedDamagePerDrone`, `k_MovingTargetPenalty`, `k_SplashAimRadius`, `k_FleeMaxDelay`…). |
| `MyBehaviorTrees/SelectFocusEnemy.cs` | `SelectFocusEnemyDrone` / `SelectFocusEnemyTurret` (+ base). Comportement différent selon que l'appelant est un drone ou une tourelle. Phase 2 : quand nos tourelles sont mortes, les drones passent directement aux drones ennemis. |
| `MyBehaviorTrees/StrafeMovement.cs` (+ `StrafeAim`, `StrafeAround`) | Déplacement : perpendiculaire à la ligne de tir du drone ennemi le plus proche, destinations à 14 m, espacement 14 m entre drones rouges, orbite autour de la cible à 6–18 m, esquive des grenades (16 directions, évaluation de tous les impacts prévus). `StrafeAim` pivote vers le point anticipé (même loi que `MySlerpRotateAroundY` : 4 et 5°) et réussit quand il est aligné. Constantes en tête (`k_DodgeGrenades`, `k_SpacingDistance`, `k_StepLength`…). |
| `MyBehaviorTrees/DroneAim.cs` | Temps de vol du missile et position anticipée d'une cible mobile. |
| `MyBehaviorTrees/IsTargetInRange.cs` | Condition : cible à moins de N mètres (on ne se rapproche pas pour tirer). |

**Vérification dans l'éditeur (à faire en premier si on rouvre Unity)** : ouvrir le projet (arrêter le mode Play), vérifier que la console est vide, ouvrir les deux arbres rouges dans Behavior Designer et vérifier qu'ils s'affichent (nœuds `Strafe Aim`, `Parallel Selector`, `Move only if needed`, `Already in range`, `Is Target In Range`), lancer une bataille en jeu. La simulation utilise un exécutable construit : les différences éventuelles avec l'éditeur n'ont pas été vérifiées.

---

## 6. Pistes à explorer (classées par intérêt estimé)

1. **Mesurer proprement avant d'optimiser.** Lancer les 20 batailles de livraison (aussi le CSV), puis comparer chaque variante sur des **séries de 5 à 10 batailles**. Écrire un petit `run_batch` (boucle sur `-simBattles N` : l'outil gère déjà N batailles et les impasses) et un comparateur de séries (moyenne et écart-type de la durée, du taux de victoire, des PV restants). C'est le prérequis de toutes les autres pistes.
2. **Graines aléatoires communes** (« common random numbers ») : initialiser `Random.InitState(index_bataille)` au début de chaque bataille pour que deux variantes affrontent les mêmes tirages au départ. Réduit la variance des comparaisons.
3. **Fonctionnalité différenciante = soin** (second objectif du sujet, pas commencé). Un drone soigneur ou une zone « camp de base » derrière nos lignes : le duel de drones se joue à ~10 % de PV près (~535 PV contre ~870 PV), des drones blessés qui se replient et reviennent soignés seraient décisifs. Attention à ne pas modifier les caractéristiques existantes (créer un nouveau composant, de nouveaux prefabs).
4. **Repli des drones blessés** (dépend de la piste 3) : un drone à moins de ~15 PV est presque mort ; le soustraire du combat vers la zone de soin au lieu de le laisser servir de cible.
5. **Abris et ligne de vue.** Les missiles explosent sur tout collider d'un autre tag (caisses du centre, relief) et les drones verts **tirent sans vérifier la ligne de vue** : combattre derrière une caisse (raycast) ne coûte rien à nos tirs bien placés et fait gaspiller les leurs.
6. **Réglage fin par séries** (une fois la piste 1 prête) : `IsTargetInRange` (16), rayon d'orbite (6–18), `k_SpacingDistance` (14), `k_MovingTargetPenalty` (40), `k_MaxDronesPerTurret` (4), `k_SplashAimRadius` (5,5), intervalle d'inversion du déplacement (2–3,5 s), `k_FleeMaxDelay`, `k_RotationDelay`, durée de la phase 1.
7. **Esquive des missiles** (et pas seulement des grenades) avec la vision du chef d'état-major sur les missiles en vol (trajectoires rectilignes, donc prévisibles) ; ne fuir une grenade que si elle vise réellement notre zone et pas quand l'esquive nous détourne de la cible (voir le journal : l'esquive v2 pouvait nous faire perdre la cible).
8. **Tourelles rouges après la destruction des tourelles vertes** : viser les groupes de drones verts immobiles avec visée de zone et prédiction (aujourd'hui : drone le plus proche avec tir anticipé).
9. **Discipline de tir** : ne tirer que sur une cible à l'arrêt ou prévisible, sinon rechoisir une cible (tester sans créer de boucle à chaque image : ajouter une attente à l'échec).
10. **Réservation de dégâts pour les salves de drones** (comme pour les grenades) afin de réduire le sur-ciblage, ou une affectation plus fine (problème d'assignation) à la place des groupes de capacité `PV/7`.
11. **Ouverture et placement initial** : les drones verts démarrent groupés et lents ; une grenade lancée à t=0 n'arrive qu'à t=4 (ils seront partis), mais le placement des drones rouges avant le premier ordre ou une formation initiale pourrait réduire les pertes de la phase d'artillerie.
12. **Exploiter la portée des tourelles ennemies** (anneau 5–50 m) : zones hors de portée de certaines tourelles vertes (x > +28 pour celles d'extrême gauche) ; angle mort à moins de 5 m ; choisir les cibles de tourelles à abattre en priorité selon *qui peut atteindre nos tourelles* (certaines paires sont hors de portée).
13. **Stratégie adaptative selon le rapport de forces** (le chef d'état-major voit tout) : par exemple, si le ratio de PV à 25 s est mauvais, changer de tactique (kiting hors de portée des dernières tourelles, regroupement…).
14. **Simplification et qualité du code** (critère d'évaluation « qualité des scripts C# ») : supprimer le code mort (`GetFocusEnemy`, `ReserveRocketShot`) ; mesurer par séries si le tir anticipé de `StrafeAim` et l'esquive de grenades servent réellement, sinon les retirer ; commenter et regrouper les constantes.
15. **Outillage** : exécuter plusieurs batailles en parallèle (plusieurs exécutables, 2 threads chacun), garder en mémoire un tableau des versions testées, générer automatiquement le CSV de l'Annexe B depuis les `results_*.tsv`.
16. **Vérifier l'éditeur vs l'exécutable** : mêmes résultats en jeu dans Unity (timeScale 1, dt variable) qu'en simulation (dt fixe 1/60) ? Si non, comprendre pourquoi avant de livrer.

---

## 7. Préférences de l'utilisateur et consignes explicites

- Langue : **français**.
- Il laisse une **très grande autonomie** sur la stratégie (« libre à 100 % ») tant que les règles du sujet sont respectées (rien d'autre que des changements stratégiques) ; il veut de la progression visible et a réagi quand il a eu l'impression que l'agent « abandonnait ».
- **Une seule simulation à la fois** : il a trouvé inutile de lancer 10 batailles (et l'ordinateur chauffait). Les séries (piste 1) sont désormais nécessaires pour conclure ; **expliquer pourquoi et demander son accord avant de lancer un lot**.
- **Commits sans co-auteur**, outils de simulation **hors git**.
- Il veut tout ce qui fonctionne consigné dans un fichier `.md` pour pouvoir le montrer (`JOURNAL_MODIFICATIONS.md`) et un contexte à portée de main (`CLAUDE.md`, ce fichier).
- Son dernier message avant la passation (« …explore la possibilité d'être encore plus fort dans le duel entre drones pour être sûr de l'emporter à la fin. Ce ») était **tronqué** ; il n'a donc pas encore répondu sur le lot de 20 batailles.
- Il ne touche pas au projet pendant que l'agent travaille (« Moi je ne vais rien toucher ») ; Unity peut être fermé ou ouvert.

---

## 8. Liste de contrôle pour reprendre

- [ ] Lire `CLAUDE.md`, ce fichier, puis `JOURNAL_MODIFICATIONS.md` (tableau de résultats).
- [ ] `git status` (propre ?) et `git log --oneline` ; relancer le garde-fou de conformité (§3.4).
- [ ] Vérifier que `sim-tools/` existe (sinon le recréer, §3.1) et qu'Unity Hub est ouvert ; lancer une bataille de contrôle : `sim-tools/run_sim.sh 1 controle ""`.
- [ ] Demander à l'utilisateur son accord pour le lot de 20 batailles ; sinon continuer par pistes à effet attendu **gros** testées en une bataille.
- [ ] Avant toute livraison : vérification dans l'éditeur (§5), conformité, `ReleaseNotes`, CSV, vidéo, archive sans `Library`.
