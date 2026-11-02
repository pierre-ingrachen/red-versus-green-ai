# RED versus Green — contexte projet

Projet TP EPF 5A, module « Real Time Decision Making in Video Game » (David Bilemdjian), année 2026-2027.
Unity 6000.6.0f1 (tous les membres de l'équipe sur la même version), C#, plugin **Behavior Designer** (Opsive).
Groupes de 4. Mini-jeu « RTS Wargame » : deux armées IA s'affrontent, le joueur ne contrôle rien.

## Objectifs
1. **Principal (en cours)** : développer l'IA de l'armée **ROUGE** pour battre la VERTE (qui est plus forte au départ).
   - arbre de comportement du drone : `DroneRedBehavior`
   - arbre de comportement de la tourelle : `TurretRedBehavior`
   - éventuelles nouvelles tâches Behavior Designer (actions, conditions, décorateurs)
   - chef d'état-major : `ArmyManagerRed`. Il voit tout (positions/trajectoires des unités, missiles, grenades des deux camps, vie, obstacles, terrain) mais **ne connaît pas les intentions** de l'ennemi.
2. **Secondaire (plus tard)** : une fonctionnalité différenciante (nouvelle unité, nouvelle arme, unité soigneuse, zone « camp de base » de soin, etc.).

## Règles à respecter (important)
- **Ne jamais modifier l'armée VERTE** (arbres `*Green*`, `ArmyManagerGreen`, prefabs Green).
- **Ne pas modifier les caractéristiques de l'Annexe A** ni les dimensions des Colliders.
- Effectifs de départ : ROUGE 13 drones + 6 tourelles ; VERTE 19 drones + 9 tourelles.

## Annexe A — caractéristiques imposées
Drone : My Seek (Speed 10 m/s, Angular Speed 180°/s, Arrive Distance 6 m, NavMeshAgent accélération 8 m/s²) ; My Slerp Rotate Around Y (Slerp Coef 4, Arrival Angle 5°) ; Wait en fin de boucle (random 1–2 s) ; Health 50.
Tourelle : Select Enemy (Min Radius 5 m, Max Radius 50 m, drone et tourelle) ; Rotation Speed 90°/s ; Rocket Travel Duration 4 s ; Wait en fin de boucle (random 1–3 s) ; Health 100.
Missile (drone) : durée de vie 3 s, accélération 40 m/s², vitesse max 20 m/s, dégâts 5 (impact direct uniquement).
Grenade/Rocket (tourelle) : vie max 10 s, rayon de dégâts 5 m, dégâts 10 (dégâts de zone à l'atterrissage, trajectoire parabolique).

## Évaluation
Réflexion stratégique/tactique, complexité des arbres, efficacité de l'armée (Annexe B), complexité et qualité de la fonctionnalité différenciante, qualité des scripts C#.

## Annexe B — statistiques (20 batailles contre l'IA par défaut)
Critères par ordre d'importance : victoire (1/0) > drones vivants > tourelles vivantes > PV cumulés restants > durée (s).
Impasse (« deadlock », ex. il ne reste que des tourelles hors de portée l'une de l'autre) : bataille non comptée, à relancer.
Fin de bataille : `ArmyManagerRed` écrit dans le presse-papiers `victoire\tdurée\tdrones\ttourelles\tPV` (à coller dans le CSV, séparateur tabulation).
Colonnes CSV : `INDEX_BATTLE IS_VICTORY DURATION ALIVE_DRONES ALIVE_TURRETS REMAINING_CUMULATED_HEALTH_POINTS`.
Note : en cas de défaite le presse-papiers contient `0\tdurée\t0\t0\t0`.

## Livraison
- **Date limite : dimanche 29 novembre, 21h00** (le PDF écrit « 2025 », très probablement une coquille pour 2026 — à confirmer).
- Archive `EPF_2026_2027_DecisionAI_UnityProject_<Nom1Nom2Nom3Nom4>.zip`, partagée via Drive (sans notification) à dbilemdjian@gmail.com ; le mail ne contient que le lien de l'archive.
- Contenu : `ReleaseNotes` (membres, réflexion stratégique/tactique, ce qui a été développé : fonctionnalités / arbres / scripts, limites et bugs connus, répartition du travail), CSV des 20 batailles, vidéo HD d'un combat épique, projet Unity **sans le dossier `Library`**.
- Dernière séance d'accompagnement : 04/11 (PM).

## Architecture du code (`Assets/Scripts/`)
| Fichier | Rôle |
|---|---|
| `Army/ArmyManager.cs` | Classe abstraite du chef d'état-major. Recense les unités de son tag, HUD (drones/tourelles/vie), `GetRandomEnemy<T>(pos, minR, maxR)` (ennemi aléatoire dans un anneau), `ArmyElementHasBeenKilled`, événement `m_OnArmyIsDead`. |
| `Army/ArmyManagerRed.cs` / `ArmyManagerGreen.cs` | Variantes par camp. Red écrit les stats dans le presse-papiers. **Point d'entrée pour la stratégie rouge.** |
| `Army/ArmyIElement.cs` (`ArmyElement`), `IArmyElement.cs` | Base des unités : `ArmyManager`, `Health`, `Die()`. |
| `Army/Drone.cs` | Drone terrestre (NavMeshAgent), `Shoot()` tire des missiles. |
| `Army/FlyingDrone.cs` | Drone volant, tire des balles raycast (`RaycastBullet`) ; utilisé côté vert (`FlyingDroneGreenBehavior`), absent du sujet. |
| `Army/Turret.cs` | Tourelle fixe, `RotateTowards(pos)` puis `Shoot(pos)` ; calcul balistique avec `m_RocketTravelDuration`. |
| `Army/Missile.cs`, `Rocket.cs`, `RaycastBullet.cs` | Projectiles. Missile suit le relief via `TerrainManager`, dégâts à l'impact ; Rocket dégâts de zone (`OverlapSphere`). Les projectiles portent le tag de leur camp. |
| `Army/Health.cs` | PV, barre de vie, `InflictDamage`, événement de mort. |
| `TerrainManager.cs`, `ExplosionManager.cs`, `Timer.cs`, `Restart.cs` | Terrain, explosions, chronomètre, relance de la scène. |
| `MyBehaviorTrees/*.cs` | Tâches Behavior Designer personnalisées (ci-dessous). |
| `xTests/` | Scripts d'essai du cours (pas le jeu). |

### Tâches Behavior Designer personnalisées (`Assets/Scripts/MyBehaviorTrees/`)
- Sélection de cible : `SelectEnemyDrone`, `SelectEnemyTurret` (SharedTransform `target`, `minRadius`, `maxRadius` ; appellent `ArmyManager.GetRandomEnemy<T>`, renvoient Success si une cible existe, sinon Failure).
- Déplacement : `MySeek` (NavMesh), `MyFlySeek`, `TurretSeekTarget` (orientation de la tourelle).
- Rotation : `MySlerpRotateAroundY`, `MyFlyRotateAroundTarget`.
- Tir : `DroneShoot`, `FlyingDroneShoot`, `TurretShoot`.
- `TestEPFTask` : exemple.

## Arbres de comportement (fichiers texte modifiables)
Les arbres sont des assets `ExternalBehavior` YAML contenant un champ `JSONSerialization` (JSON des tâches), **lisibles et éditables directement** :
- `Assets/Scripts/MyBehaviorTrees/DroneRedBehavior.asset` ← à améliorer
- `Assets/Scripts/MyBehaviorTrees/TurretRedBehavior.asset` ← à améliorer
- `DroneGreenBehavior.asset`, `TurretGreenBehavior.asset`, `FlyingDroneGreenBehavior.asset` ← NE PAS TOUCHER

Arbre drone actuel (basique) : Repeater(∞) → Sequence[ RandomSelector(SelectEnemyDrone | SelectEnemyTurret), MySeek, MySlerpRotateAroundY, DroneShoot, Wait ].
Arbre tourelle actuel : Select Enemy (priorité tourelles) → TurretSeekTarget → TurretShoot → Wait.

Pour modifier un arbre sans l'éditeur : éditer le JSON dans `.asset`, puis ouvrir Unity (réimport). Garder les `ID` uniques et la structure `Children` cohérente ; de préférence demander à l'utilisateur de vérifier l'arbre dans la fenêtre Behavior Designer après modification. Si l'utilisateur a modifié l'arbre dans l'éditeur, relire le fichier avant de l'éditer (Unity sauvegarde dans le `.asset` seulement après Ctrl+S / Save Project).

## Autres dossiers utiles
- `Assets/Scenes/RedVersusGreenBattle.unity` : scène unique du jeu (UI, managers, placement des unités). Gros fichier YAML, à lire avec précaution.
- `Assets/Prefabs/` : `DroneRed/Green`, `TurretRed/Green`, `FlyingDroneGreen`, `MissileRed/Green`, `GrenadeRed/Green`, `Health`, explosions, terrain.
- `Assets/Behavior Designer/` : plugin tiers (ne pas modifier).
- `Assets/Gfx/`, `Assets/JMO Assets/`, `Assets/Materials/`, `Assets/Meshes/` : assets graphiques.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` : générés par Unity (ignorer ; retirer `Library` de l'archive finale).

## Pistes stratégiques (à explorer pour l'IA rouge)
Hors-nombre (13+6 contre 19+9), il faut compenser par la tactique : focus-fire sur une cible commune plutôt que cibles aléatoires, exploitation des terrasses et de l'abri des obstacles, priorité aux tourelles ennemies (dégâts de zone, 100 PV) ou aux drones isolés, tourelles qui visent des zones où se regroupent les ennemis (la grenade met 4 s à arriver : anticiper le mouvement), drones qui restent hors du Min Radius des tourelles ennemies, repli des unités blessées, utilisation de `ArmyManagerRed` pour coordonner (attribution des cibles, évitement du sur-ciblage).

## Suivi du travail
- **`JOURNAL_MODIFICATIONS.md`** : à mettre à jour à chaque modification de l'IA rouge (statut ✅ validée / 🟡 à valider / ❌ abandonnée, fichiers touchés, résultats). Sert de base aux ReleaseNotes. Ne marquer ✅ qu'après un test en jeu.
- **Git** (dépôt local, branche `main`, pas de remote) : Claude peut commiter. **Ne jamais ajouter de ligne `Co-Authored-By` ni de mention de Claude dans les commits** (consigne explicite de l'utilisateur, prioritaire sur l'attribution par défaut). Un commit par modification cohérente, message en français, et mettre à jour `JOURNAL_MODIFICATIONS.md` dans le même commit. Ne pas pousser.
  - Versionné : `Assets/`, `Packages/`, `ProjectSettings/`, `.gitignore`, `CLAUDE.md`, `JOURNAL_MODIFICATIONS.md`, et plus tard les ReleaseNotes/CSV. Ignoré : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`/`*.sln*`, `.vscode/`, `.claude/` (voir `.gitignore`). `Library` est de toute façon à retirer de l'archive livrée.
  - Le premier commit contient l'état d'origine du projet fourni (`git diff <premier commit>` donne tout ce qu'on a changé).
- Fichiers `.cs` anciens en **Windows-1252** (ex. `ArmyManagerRed.cs`) : les éditer avec un script qui conserve l'encodage, pas avec Edit/Write.
- Vérifier la compilation sans Unity : copier `Assembly-CSharp.csproj` dans le scratchpad, passer les `Compile Include`/`HintPath` relatifs en chemins absolus, y ajouter les nouveaux fichiers, puis `…/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet build`. Unity ne recompile qu'au retour au premier plan (et pas en mode Play) ; son journal est `Logs/Editor.log` dans le projet.
- Unity est ouvert sur le projet pendant le travail : impossible de lancer un second Unity dessus (verrou). Pour des simulations automatiques, il faudrait travailler sur une copie du projet en `-batchmode`.

## État d'avancement
- [x] Lire/analyser les arbres actuels
- [~] Focus fire (`SelectFocusEnemy.cs`, `ArmyManagerRed`, arbres rouges) : tourelles OK en jeu ; drones corrigés (plus proche, gardé jusqu'à la mort) et défense des tourelles ajoutée : compilé, **à tester en jeu** (voir journal)
- [ ] Autres améliorations de `DroneRedBehavior` / `TurretRedBehavior` / `ArmyManagerRed`
- [ ] Simuler 20 batailles et remplir le CSV
- [ ] ReleaseNotes + vidéo + archive
