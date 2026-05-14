# AGENTS.md

## Projekt
Unity Tower Defense Roguelike Hybrid.

## Sprache
Antworten und Erklärungen auf Deutsch.
Unity-Menübegriffe dürfen auf Englisch genannt werden.

## Arbeitsweise
- Erst analysieren, dann ändern.
- Kleine, sichere Änderungen bevorzugen.
- Keine großen Reworks ohne ausdrücklichen Auftrag.
- Bestehende Systeme nicht ungefragt umbauen.
- Nach Änderungen genau erklären, welche Dateien geändert wurden.
- Keine Änderungen an Library, Temp, Obj, Build, Builds, Logs oder UserSettings.
- Keine öffentlichen Unity-Inspector-Felder umbenennen, wenn es nicht ausdrücklich nötig ist.
- Keine Inspector-Referenzen löschen.
- Keine Systeme beschädigen, die nicht Teil der aktuellen Aufgabe sind.

## Aktueller Stand
Phase 6 abgeschlossen:
Chaos/Gerechtigkeit V1 playable baseline.

## Wichtige Systeme
- GameManager
- BuildManager
- BuildSelectionUI
- PathBuildManager
- Tower
- TowerUI
- Enemy
- EnemySpawner
- ChaosJusticeManager
- ChaosJusticeChoiceUI
- ChaosUnlockManager
- ChaosUnlockUI
- ChaosLexiconManager
- ChaosLexiconUI
- RunStatisticsTracker
- WaveData
- WaveCompletionResult
- WaveHistory

## Wichtige Designregeln
- Boss und MiniBoss zerstören keine Tower.
- Tower-Zerstörung bleibt Elite-exklusiv.
- Chaos-Level gibt keine passiven Rewards.
- Chaos-Level erhöht Gegneranzahl nicht automatisch.
- Keine globale Speed-Erhöhung durch Chaos.
- Base-Schaden bleibt durch Chaos unverändert.
- Rewards entstehen über Gerechtigkeit, Risiko-Modifikatoren oder normale Wave-/Boss-Rewards.
- Risiko-Modifikatoren sind sichtbar und dürfen keine falschen Informationen erzeugen.
- Keine unfairen Sofortverluste.
- Chaos 6/7 und Informationsverschleierung sind spätere Themen.
- Autopath, normales Elite-System und Chaos-Elite sind spätere Themen.

## Unity-Hinweise
- Das Projekt nutzt MonoBehaviour-Skripte.
- Öffentliche Felder können im Unity Inspector verbunden sein.
- Änderungen an public fields, enum-Namen oder Klassennamen nur mit Vorsicht.
- Keine automatisch generierten Unity-Dateien bearbeiten.