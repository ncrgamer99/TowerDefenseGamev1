# Meta-Hub UI Rebuild Spec

Quelle: `C:/Users/janni/Downloads/ChatGPT Image 23. Mai 2026, 20_43_40.png`

Referenzformat: 1602 x 982 px.

Status dieses Dokuments: Analyse, Datenmodell-Plan und Preview-Vorbereitung. Noch keine Unity-Integration.

## 1. UI-Hierarchie

```text
MetaHubScreen
- BackgroundLayer
  - dark textured blue-black base
  - subtle radial glows
  - faint horizontal/vertical panel noise
- TopBar
  - left resource strip
    - GoldResource
    - XpResource
    - ChaosResource
    - KeystoneOrSpecialResource
  - center title
  - center crest ornament
  - right account strip
    - AccountLevelLabel
    - AccountXpProgress
    - SettingsButton
    - CloseButton
- LeftSidebar
  - Header: Meta-Hub
  - NavigationButtons
    - Uebersicht selected
    - Allgemein
    - Tower Mastery
    - Chaos-Forschung
    - Verbau / Pfadtechnik
    - Elite-Jagd
    - Archiv
  - ActiveKeystonesPanel
    - Keystone slots 1-3
- MainContentFrame
  - PageTitle: Uebersicht
  - TopMetricCards row of 5
    - Tower Mastery
    - Chaos-Wissen
    - Risikokerne
    - Bauplaene
    - Elite-Jagd
  - MiddleRow
    - ProgressPanel
    - ChaosJusticePanel
    - NextGoalsPanel
  - BottomRow
    - ActiveBuffsPanel
    - ActiveRisksPanel
    - LastRunPanel
- BottomBar
  - TipText
  - OptionsButton
  - BackButton
```

## 2. Panels und Unterbereiche

| Bereich | Geschaetzte Position | Groesse | Inhalt |
| --- | ---: | ---: | --- |
| TopBar | x 12, y 20 | w 1576, h 55 | Ressourcen, Titel, Account, Icons |
| LeftSidebar | x 13, y 91 | w 273, h 823 | Navigation und Keystones |
| MainContentFrame | x 296, y 90 | w 1292, h 820 | Hauptdashboard |
| TopMetricCards | x 318, y 160 | w 1256, h 170 | 5 Karten |
| ProgressPanel | x 317, y 352 | w 405, h 314 | Account-Level-Kreis plus Werte |
| ChaosJusticePanel | x 733, y 352 | w 495, h 314 | Balance-Leiste und Scores |
| NextGoalsPanel | x 1244, y 352 | w 331, h 314 | Ziel-Liste |
| ActiveBuffsPanel | x 317, y 686 | w 390, h 207 | Buff-Liste |
| ActiveRisksPanel | x 721, y 686 | w 507, h 207 | Risiko-Liste |
| LastRunPanel | x 1244, y 686 | w 331, h 207 | Run-Werte |
| BottomBar | x 0, y 930 | w 1602, h 52 | Tipp und Buttons |

## 3. Sichtbare Texte

- TOWER DEFENSE
- 12.450
- 8.760
- 350
- 15
- Account Lv. 11
- 3.250 / 7.500 XP
- META-HUB
- UEBERSICHT
- ALLGEMEIN
- TOWER MASTERY
- CHAOS-FORSCHUNG
- VERBAU / PFADTECHNIK
- ELITE-JAGD
- ARCHIV
- AKTIVE KEYSTONES
- Lv. 1
- TOWER MASTERY
- 11
- Aktive Tower
- 11 / 17 Mastered
- CHAOS-WISSEN
- 0
- Forschungsstufen
- 0 / 25
- RISIKOKERNE
- Aktive Kerne
- BAUPLAENE
- Freigeschaltet
- Elite besiegt
- FORTSCHRITT
- ACCOUNT LEVEL
- 3.250 / 7.500 XP
- Freie Punkte
- Chaos-Level
- Gold-Gerechtigkeit
- XP-Gerechtigkeit
- CHAOS / GERECHTIGKEIT
- GERECHTIGKEIT
- BALANCE
- CHAOS
- SAFETY SCORE
- 6
- 100%
- Stabil
- CHAOS SCORE
- DETAILS ANZEIGEN
- NAECHSTE ZIELE
- Tower Mastery Level 12
- Chaos-Forschung Stufe 1
- Risikokern aktivieren
- Elite besiegen
- ALLE ZIELE ANZEIGEN
- AKTIVE BUFFS
- Gold-Boost I
- +10% Goldgewinn
- XP-Boost I
- +10% XP-Gewinn
- AKTIVE RISIKEN
- Mehr Runner I
- +20% Runner
- Schnellere Spawns I
- -15% Spawn-Delay
- LETZTER RUN
- Wellen ueberlebt
- Boss besiegt
- Chaos-Level erreicht
- Punkte verdient
- RUN-STATISTIKEN
- Tipp: Aktiviere Keystones im Tower Mastery und baue dein System strategisch auf.
- OPTIONEN
- ZURUECK

## 4. Sichtbare Zahlen und Werte

- Gold: 12.450
- XP-Waehrung: 8.760
- Chaos-/Spezialwaehrung: 350
- Weitere Ressource: 15
- Account Level: 11
- Account XP: 3.250
- Account XP Max: 7.500
- Tower Mastery aktiv: 11
- Tower Mastery Maximum: 17
- Chaos-Wissen: 0 / 25
- Risikokerne: 0
- Bauplaene: 0
- Elite-Jagd / Elites besiegt: 0
- Freie Punkte: 0
- Chaos-Level: 0
- Gold-Gerechtigkeit: sichtbarer Wert nicht ausgeschrieben, Icon/Wertfeld vorhanden
- XP-Gerechtigkeit: sichtbarer Wert nicht ausgeschrieben, Icon/Wertfeld vorhanden
- Safety Score: 6
- Balance/Stabilitaet: 100%, Stabil
- Chaos Score: 0
- Ziele: 11/12, 0/1, 0/1, 0/1
- Buff-Dauer: 2 Wellen, 2 Wellen
- Risiko-Dauer: 3 Wellen, 3 Wellen
- Letzter Run: 10 Wellen, 1 Boss, Chaos-Level 0, Punkte 8.450

## 5. Buttons

- Sidebar-Navigation: 7 grosse vertikale Buttons, `Uebersicht` selected.
- Topbar: Settings-Button, Close-Button.
- Chaos/Gerechtigkeit: `DETAILS ANZEIGEN`.
- Ziele: `ALLE ZIELE ANZEIGEN`.
- Letzter Run: `RUN-STATISTIKEN`.
- BottomBar: `OPTIONEN`, `ZURUECK`.

## 6. Progressbars, Kreisanzeigen und Statusleisten

- Topbar Account XP: schmale horizontale Progressbar, blau-violett, ca. 155 x 6 px.
- Tower Mastery card: schmale goldene Progressbar, Wert 11/17.
- Chaos-Wissen card: schmale violette Progressbar, Wert 0/25.
- Fortschritt Panel:
  - grosser kreisfoermiger XP-Ring, ca. 190 px Durchmesser.
  - cyan Fortschrittssegmente bei ungefaehr 43%.
  - Zentrum: Level 11.
- Chaos/Gerechtigkeit Balance:
  - horizontale Leiste von Gold links nach Rot rechts.
  - Marker leicht rechts der Mitte.
  - Text-Mitte: Balance.
- Keystone-Slots:
  - kleine Level-Leisten unter jedem Keystone.

## 7. Icons, Ornamente, Rahmen und Dekoelemente

- Goldresource: goldener Wappen-/Coin-Icon.
- XP: cyan/blauer Diamant mit XP.
- Chaos: violettes Runen-/Kristall-Icon.
- Vierte Ressource: violettes Schloss/Token.
- Center crest: goldener gefluegelter Tower/Wappen-Ornament unter Titel.
- Sidebar-Icons:
  - Home/Overview
  - Shield/General
  - Tower Mastery
  - Chaos rune
  - Path/Verbau crystal
  - Skull/Elite
  - Book/Archive
- Metric Cards:
  - Tower statue
  - Chaos star
  - red risk gem
  - blue blueprint cards
  - red skull
- Chaos/Gerechtigkeit Panel:
  - goldene Waage links
  - rotes Chaos-Symbol rechts
- Ziele/Buffs/Risiken:
  - kleine farbige Icons links pro Row.
- Rahmen:
  - duenne goldene Linien
  - abgeschraegte Ecken
  - kleine Corner-Ornamente
  - Glow bei aktivem Sidebar-Item

## 8. Farbpalette

| Rolle | Hex geschaetzt | Verwendung |
| --- | --- | --- |
| Background deep | `#061014` | Hauptgrund |
| Background blue black | `#081820` | Panels |
| Panel dark | `#0B1518` | Karten |
| Panel inner | `#101B1E` | Row-Hintergrund |
| Gold primary | `#D6A24A` | Rahmen, Titel, Selected |
| Gold bright | `#F3C66A` | Highlights/Text |
| Text main | `#E8D7B5` | Primaertext |
| Text muted | `#A99B84` | Sekundaertext |
| Cyan | `#38BFE6` | XP, Pfadtechnik, Ring |
| Purple | `#A460F2` | Chaos-Forschung |
| Red | `#E9574E` | Elite/Risiken/Chaos |
| Green | `#6FDD65` | Buffs |
| Blue dark | `#102B37` | Blueprint-Bereich |

## 9. Abstaende, Groessen und Ausrichtung

- Bildschirm hat ca. 12 px Aussenrand.
- Topbar beginnt bei y 20, Hauptcontainer bei y 90.
- Linke Sidebar: 273 px breit, 12 px links.
- Zwischen Sidebar und MainContent: ca. 11 px.
- MainContent Padding: ca. 20-24 px.
- TopMetricCards: 5 Karten mit ca. 10-12 px Gap.
- Kartenhoehen: Topkarten ca. 170 px, mittlere Panels ca. 314 px, untere Panels ca. 207 px.
- Sidebar Items: ca. 64 px hoch, 8-10 px vertikaler Gap.
- Buttons unten rechts: ca. 180 x 38 px.
- Typografie:
  - Titel: Serif, gold, ca. 34-40 px, breite Laufweite.
  - Paneltitel: Serif, gold/hell, ca. 22-25 px.
  - Body: Sans/Condensed, ca. 14-16 px.

## 10. Dynamische Datenfelder

```csharp
public sealed class MetaHubData
{
    public ResourceSummary Resources;
    public AccountProgress Account;
    public MetaOverview Overview;
    public JusticeChaosState JusticeChaos;
    public List<GoalItem> NextGoals;
    public List<EffectItem> ActiveBuffs;
    public List<EffectItem> ActiveRisks;
    public List<KeystoneItem> ActiveKeystones;
    public LastRunStats LastRun;
}

public sealed class ResourceSummary
{
    public int Gold;
    public int XpCurrency;
    public int ChaosCurrency;
    public int SpecialResource;
}

public sealed class AccountProgress
{
    public int Level;
    public int CurrentXp;
    public int RequiredXp;
    public int FreePoints;
    public int ChaosLevel;
    public int GoldJustice;
    public int XpJustice;
}

public sealed class MetaOverview
{
    public int TowerMasteryActive;
    public int TowerMasteryMax;
    public int ChaosKnowledgeProgress;
    public int ChaosKnowledgeMax;
    public int RiskCores;
    public int Blueprints;
    public int ElitesDefeated;
}

public sealed class JusticeChaosState
{
    public int SafetyScore;
    public int ChaosScore;
    public float Balance01;
    public string StabilityLabel;
}

public sealed class GoalItem
{
    public string Id;
    public string Label;
    public int Current;
    public int Required;
    public string IconKey;
}

public sealed class EffectItem
{
    public string Id;
    public string Label;
    public string Description;
    public int WavesRemaining;
    public string IconKey;
    public EffectTone Tone;
}

public sealed class KeystoneItem
{
    public string Id;
    public string Label;
    public int Level;
    public string IconKey;
}

public sealed class LastRunStats
{
    public int WavesSurvived;
    public int BossesDefeated;
    public int ChaosLevelReached;
    public int PointsEarned;
}
```

## 11. Assets, die rekonstruiert oder extrahiert werden sollen

- Gold/currency icon
- XP diamond icon
- Chaos shard icon
- Special resource token
- Gold crest under title
- Navigation icons
- Tower Mastery statue icon
- Chaos-Wissen star icon
- Risk core gem icon
- Blueprint card stack icon
- Elite skull icon
- Balance scale icon
- Chaos sun/skull icon
- Keystone gem icons
- Panel frame/corner ornaments
- Button frame variants
- Circular progress ring segments
- Small row icons for goals, buffs and risks

## 12. Listen-/Array-basierte Bereiche

- Resources: 4 entries.
- Sidebar navigation: 7 entries.
- Top overview metric cards: 5 entries.
- Progress side stat rows: 4 entries.
- Next goals: variable list, currently 4 rows.
- Active buffs: variable list, currently 2 rows.
- Active risks: variable list, currently 2 rows.
- Active keystones: variable list, currently 3 slots.
- Last run stat rows: 4 rows.
- Bottom actions: 2 main buttons plus optional future actions.

## 13. Risiko- und Unsicherheitsliste

- Font ist vermutlich Trajan/Cinzel/klassische Serif; ohne Originalfont nur angenaehert.
- Feine Panel-Textur/Noise kann nur angenaehert werden, solange kein Original-Texture-Asset existiert.
- Icons sind im Screenshot klein und teilweise stilisiert; fuer Unity sollten sie als separate SVG/PNG-Assets neu rekonstruiert werden.
- Rahmen haben sehr feine abgeschraegte Ornamentlinien; erste Preview rekonstruiert sie ueber CSS/SVG, spaetere Unity-Version braucht ggf. 9-slice Sprites.
- Glow/Blend wirkt im Screenshot wahrscheinlich mit mehreren Overlays; Preview naeherungsweise per CSS box-shadow.
- Exakte Werte fuer Farbabstufungen und Schatten sind geschaetzt, da keine Design Tokens vorliegen.

## Trennung der Arbeitsbereiche

### Statischer visueller Aufbau

- Gesamtlayout, Panelrahmen, Topbar, Sidebar, Card-Anordnung, Hintergrundstruktur.
- Dekorative Linien, Corner-Ornamente, Crests und Glow-Schichten.
- Default-Positionen und responsive Regeln.

### Dynamische Datenfelder

- Alle Zahlen, Progresswerte, Labels in Listen, Dauerangaben, Run-Stats, Ressourcen, Account-Level.
- Ziele, Buffs, Risiken, Keystones und Metrics sind Collections.
- Fortschrittsanzeigen werden aus aktuellen und maximalen Werten berechnet.

### Rekonstruierte Einzelassets

- Icons und Ornamente werden nicht als komplettes Screenshot-Hintergrundbild verwendet.
- Fuer die Preview werden Icons/Rahmen als CSS/SVG-artige Formen nachgebaut.
- Fuer Unity nach Freigabe: Export in separate Assets unter `Assets/Art/Generated/UI/MetaHub/`.

### Spaetere Unity-Implementierung

- Erst nach Freigabe.
- Bevorzugt Unity UI Toolkit.
- Geplante neue Ordner:
  - `Assets/UI/Generated/MetaHub/`
  - `Assets/Art/Generated/UI/MetaHub/`
- Geplante Dateien:
  - `MetaHubScreen.uxml`
  - `MetaHubScreen.uss`
  - `MetaHubController.cs`
  - `MetaHubData.cs`
  - `MetaHubMockData.cs`
  - optionale Row-/Binding-Hilfsklassen
  - `README.md`
- Keine dynamischen Werte hardcoded im UXML/USS.

## Preview-Plan vor Freigabe

- Erzeuge eine lokale, nicht in Unity importierte HTML/CSS/JS-Preview unter `CodexScreenshots/MetaHubPreview/`.
- Daten in einer MockData-Struktur.
- Screenshot der gerenderten Preview als Vergleichsgrundlage.
- Danach STOPP: keine Unity-Integration ohne Freigabe.
