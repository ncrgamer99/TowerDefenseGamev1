# Phase 7 Roadmap – Wegbau-Auswahl & Verbau-Events V1

## Ziel

Phase 7 erweitert den spielbaren Baseline-Loop aus Phase 6 um bewusstere Wegbau-Entscheidungen und fairere, sichtbare Verbau-Events.

Der Spieler soll beim Wegbau mehr Kontrolle und Risikoabwägung erhalten, ohne bestehende Phase-6-Regeln zu brechen.

## Harte Designgrenzen

- Keine unfairen Sofortverluste.
- Boss und MiniBoss zerstören keine Tower.
- Tower-Zerstörung bleibt Elite-exklusiv.
- Chaos-Level gibt keine passiven Rewards.
- Chaos-Level erhöht Gegneranzahl nicht automatisch.
- Keine globale Speed-Erhöhung durch Chaos.
- Base-Schaden bleibt durch Chaos unverändert.
- Risiko-Modifikatoren müssen sichtbar und korrekt beschrieben sein.
- Keine Informationsverschleierung in Phase 7.
- Autopath, normales Elite-System und Chaos-Elite bleiben spätere Themen.

## Priorität 1 – Ist-Zustand absichern

### 1.1 Verbau-Loop dokumentieren
- Prüfen:
  - Wann `GameManager.HandleBaseBlocked()` ausgelöst wird.
  - Wann `BlockedEventManager.OpenBlockedEventSelection()` öffnet.
  - Wie `StartTimedBuildPhaseAfterBlockedEvent()` in die nächste Wave führt.
- Ziel:
  - Keine Logik ändern, nur Verhalten eindeutig dokumentieren.

### 1.2 Modal-Lock-Regeln für Phase 7 festlegen
- Wegbau-Auswahl darf nicht gleichzeitig offen sein mit:
  - Chaos/Gerechtigkeit
  - Verbau-Auswahl
  - Lexikon
  - Unlock-UI
  - TowerUI, falls Eingabe kollidiert
- Ziel:
  - Ein klarer Lock-Pfad über `GameManager`.

## Priorität 2 – Wegbau-Auswahl V1

### 2.1 Auswahlmodell definieren
- Aktuelle Richtungen:
  - Vorwärts
  - Links
  - Rechts
- V1-Ziel:
  - Diese Optionen sauber als Auswahlkarten/Buttons darstellen.
  - Ungültige Optionen sichtbar, aber nicht auswählbar machen.
  - Keine automatische Pfadwahl.

### 2.2 Vorschau verbessern
- Für jede Richtung anzeigen:
  - Richtung
  - gültig/ungültig
  - Grund bei ungültig
  - optional: erwarteter BuildTile-Bereich
- Keine versteckten Informationen.

### 2.3 Wegbau-Input konsolidieren
- Tastatur und UI sollen denselben Codepfad nutzen.
- Keine doppelten Wave-Starts.
- Rechtsklick/Escape schließen Auswahl ohne Seiteneffekte.

## Priorität 3 – Verbau-Events V1 sauber erweitern

### 3.1 Event-Datenmodell erweitern
Aktuelle Events:
- Weiter
- Goldreserve
- Notfall-Reparatur

Mögliche V1-Erweiterungen:
- Kleine Build-Hilfe
- Reparatur mit Nachteil
- Gold gegen Zeit
- Zeit gegen Sicherheit

Wichtig:
- Keine sofort tödlichen Events.
- Belohnungen klar anzeigen.
- Keine falschen Informationen.

### 3.2 Event-Auswahl fair machen
- Option „Weiter“ bleibt immer verfügbar.
- Mindestens eine sichere Option.
- Kein Event darf direkt Game Over erzwingen.
- Rewards müssen über RunStatisticsTracker erfasst werden.

### 3.3 Wiederholtes Verbau-Verhalten klären
- Wenn Spieler nach Event an gleicher Position weiterhin verbaut ist:
  - Keine Endlosschleife aus Eventauswahl.
  - Timed Buildphase bleibt verständlich.
  - UI-Text erklärt den Zustand.

## Priorität 4 – UI/UX

### 4.1 Einheitliche Top-Bar-Sprache
- PathBuildManager und BlockedEventManager sollen visuell konsistent bleiben.
- Texte:
  - kurz
  - eindeutig
  - keine falschen Versprechen

### 4.2 Debug-Anzeigen
- Aktuelle Base-Position
- mögliche Richtungen
- blocked/unblocked
- timed blocked build remaining
- letzter Verbau-Event-Typ

## Priorität 5 – Statistik und Result-Screen

### 5.1 Verbau-Event-Tracking erweitern
Tracken:
- Anzahl Verbau-Situationen
- gewählte Verbau-Events
- Gold aus Verbau
- Leben aus Verbau
- Anzahl timed blocked build phases

### 5.2 ResultUI erweitern
- Kurze Zusammenfassung:
  - „Verbau-Events: X“
  - „Gold aus Verbau: Y“
  - „Leben aus Verbau: Z“

## Priorität 6 – Testszenarien

### 6.1 Pflichttests
- Normaler Wegbau startet Wave.
- Ungültiger Wegbau startet keine Wave.
- Verbau öffnet Eventauswahl.
- Chaos/Gerechtigkeit und Verbau-Event öffnen nicht gleichzeitig.
- Nach Verbau-Event startet timed Buildphase.
- Nach timed Buildphase startet nächste Wave.
- Game Over verhindert weitere Auswahl.
- Lexikon/Unlock-UI blockieren Wegbau korrekt.

### 6.2 Regression
- BossChoice nach Boss-Wave funktioniert weiter.
- Rewards aus Justice funktionieren weiter.
- Chaos-Level erhöht nicht automatisch Gegneranzahl.
- Boss/MiniBoss zerstören keine Tower.
- Tower-Upgrades und BuildSelection funktionieren weiter.

## Nicht-Ziele für Phase 7

- Kein Autopath.
- Kein normales Elite-System.
- Keine Chaos-Elite.
- Keine Chaos 6/7.
- Keine Informationsverschleierung.
- Keine große UI-Neuarchitektur.
- Keine Umbenennung öffentlicher Inspector-Felder.