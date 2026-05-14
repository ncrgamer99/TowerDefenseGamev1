Phase 6 V1 Step14 - Grund-Freischaltungen / Content-Pool V1

Import:
- Play Mode beenden.
- Alle .cs Dateien aus diesem Paket in deinen bestehenden Script-Ordner kopieren und vorhandene Dateien ersetzen.
- Neue Scripts in diesem Schritt: ChaosUnlockEntry.cs, ChaosUnlockManager.cs, ChaosUnlockUI.cs
- Bestehende Script-/Klassennamen wurden nicht geändert.
- RunStatisticsTracker aus Schritt 13 bleibt enthalten.

Unity Setup:
1. Neues leeres GameObject erstellen: ChaosUnlockSystem
2. Components hinzufügen:
   - ChaosUnlockManager
   - ChaosUnlockUI
3. ChaosUnlockManager Referenzen setzen:
   - Game Manager -> GameManager
   - Chaos Justice Manager -> ChaosJusticeManager
   - Enemy Spawner -> EnemySpawner
   - Lexicon Manager -> ChaosLexiconSystem / ChaosLexiconManager
   - Unlock UI -> ChaosUnlockSystem / ChaosUnlockUI
   - Target Canvas -> Canvas
4. ChaosUnlockUI setzen:
   - Manager -> ChaosUnlockManager
   - Target Canvas -> Canvas
   - Auto Create UI If Missing -> On
5. GameManager prüfen:
   - Chaos Unlock Manager -> ChaosUnlockSystem / ChaosUnlockManager
   - Chaos Unlock UI -> ChaosUnlockSystem / ChaosUnlockUI
   - Run Statistics Tracker bleibt wie in Schritt 13 verbunden oder wird automatisch erzeugt.
6. ChaosJusticeManager prüfen:
   - Chaos Unlock Manager -> ChaosUnlockSystem / ChaosUnlockManager
   - Use Unlock Gate For Risk Modifier Pool -> On
7. ChaosLexiconManager prüfen:
   - Unlock Manager -> ChaosUnlockSystem / ChaosUnlockManager

Test:
- Play Mode starten.
- F2 öffnet Freischaltungen.
- F1 Lexikon enthält den Eintrag Grund-Freischaltungen.
- F10 ResultUI enthält Freischaltungen und Wirtschaft/Tower-Progression.
- Beim ersten Bosskill werden erweiterte Rollenrisiken freigeschaltet.
- Nach erster Chaoswahl werden Belohnungsrisiken freigeschaltet.
- Ab Chaos 2/3/4 werden weitere Chaos-/Wave-Risiken freigeschaltet.

Reset für Tests:
- ChaosUnlockManager Inspector > Kontextmenü > Debug/Reset Chaos Unlocks

V1-Grenze:
- Dieses System erweitert Content-Pools.
- Es gibt keine permanenten Stärke-Boni.
- Es ist keine vollständige Meta-Progression.
