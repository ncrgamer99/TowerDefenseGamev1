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

## Phase 7 Vorbereitung
Phase 7 geplant:
Wegbau-Auswahl & Verbau-Events V1.

## Phase-7-Designregeln
- Wegbau-Auswahl muss klar zwischen gültigen und ungültigen Richtungen unterscheiden.
- Ungültige Wegbau-Optionen dürfen keine Wave starten.
- Ein erfolgreicher Wegbau darf nur einmal `GameManager.OnPathExtended()` auslösen.
- Verbau-Events müssen immer mindestens eine sichere/weiterführende Option enthalten.
- Verbau-Events dürfen keine unfairen Sofortverluste erzeugen.
- Verbau-Events müssen ihre Effekte vollständig und korrekt anzeigen.
- Wiederholter Verbau an derselben Position darf keine unendliche Event-Auswahl-Schleife erzeugen.
- Neue Verbau-Rewards müssen, wenn möglich, im RunStatisticsTracker erfasst werden.
- Neue UI-Panels müssen sich in die vorhandenen Modal-Locks einfügen.
- Wegbau-Auswahl, Towerbau, Chaos/Gerechtigkeit, Verbau-Auswahl, Lexikon und Unlock-UI dürfen keine widersprüchlichen Eingaben gleichzeitig erlauben.

## UI-/Textregeln
- UI-Texte müssen kurz, eindeutig und wahr sein.
- Keine versteckten Nachteile in Phase 7.
- Keine Informationsverschleierung in Phase 7.
- Debug-Texte dürfen ausführlicher sein, Gameplay-Texte sollen kompakt bleiben.

## Technische Änderungsregeln
- Bestehende public Inspector-Felder nicht umbenennen oder entfernen.
- Neue public Felder nur hinzufügen, wenn Inspector-Konfiguration sinnvoll ist.
- Bestehende Prefab- und Scene-Referenzen nicht löschen.
- Phase-7-Änderungen bevorzugt über kleine Erweiterungen an vorhandenen Systemen umsetzen.
- BlockedEventManager und PathBuildManager nicht durch neue Systeme ersetzen, sondern vorsichtig erweitern.