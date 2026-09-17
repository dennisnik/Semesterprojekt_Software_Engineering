# 🔋 BatteryGuard – Telemetrie- & Batterieüberwachungssystem

Gruppe: Two and a Half Men (Felix Teufel, Dennis Nikitin)

<img src="team_two_and_a_half_men.png" width="30%">

> **Hinweis:** Dieses Projekt ist Teil der Lehrveranstaltung *Software Engineering* an der DHBW.

---

## 🧭 1. Kontext & Problemstellung

### a. Ausgangssituation & Motivation
Ein Betreiber setzt im Außeneinsatz zahlreiche mobile, batteriebetriebene Geräte ein (z. B. mobile Baustellenampeln, temporäre LED-Verkehrstafeln, Umweltsensorstationen). Diese Geräte verfügen bislang über keine integrierte digitale Zustandsüberwachung. Die Folge in der Praxis:
- **Ungeplante Geräteausfälle:** Entladene oder defekte Batterien führen zum plötzlichen Stillstand der Geräte im Einsatz.
- **Hohe Betriebskosten:** Unvorhergesehene Eileinsätze des Servicepersonals zur Störungsbeseitigung verursachen beträchtliche Fahrt- und Personalkosten.
- **Ressourcenverschwendung:** Um Ausfälle abzuwenden, werden Batterien häufig viel zu früh getauscht, was die wirtschaftliche Lebensdauer der Akkus verkürzt.

Das System **BatteryGuard** realisiert eine zentrale, webbasierte Fernüberwachungslösung. Die gesammelten Telemetriedaten bilden zudem die historische Basis, um künftig optimale Wechselzeitpunkte datengestützt vorherzusagen (Predictive Maintenance).

### b. Systemkontext & Schnittstellenabgrenzung
Zur Erfassung der Messwerte dient eine nachrüstbare Telemetriebox (**TrafficNode**) an jedem Gerät. Diese misst kontinuierlich:
- Batteriespannung ($U$ in Volt)
- Entladestrom ($I$ in Ampere)
- Geografische Position (GPS-Koordinaten: Breitengrad, Längengrad)

Die TrafficNode übermittelt diese Messdaten per Funk an einen zentralen MQTT-Broker.
- **Startpunkt des Softwareprojekts:** Die Entwicklungsaufgabe beginnt an der Schnittstelle des **MQTT-Brokers**.
- **Out-of-Scope:** Hardware-Entwicklung, Sensorschaltungen und Firmware der TrafficNode sind **nicht** Bestandteil des Projekts. Für Test- und Demonstrationszwecke wird ein Software-Simulator eingesetzt.

---

### c. Stakeholder

| Stakeholder | Beschreibung | Ziel / Interesse |
|---|---|---|
| 🧑‍💼 **Flottenmanager / Betriebsleiter** | Verantwortlich für Zuverlässigkeit, Verfügbarkeit und Wirtschaftlichkeit der Geräteflotte | Vermeidung von Ausfällen, Reduzierung teurer Notfalleinsätze, vollständige Flottenübersicht |
| 🧑‍🔧 **Servicetechniker (Außendienst)** | Tauscht und wartet Batterien vor Ort im Einsatzgebiet | Exakte GPS-Standortdaten, verlässliche Statusanzeigen, Vermeidung unnötiger Tauschvorgänge |
| 👩‍💼 **Disponent / Tourenplaner** | Plant und koordiniert Routen und Ressourcen des Servicepersonals | Frühzeitige Vorwarnung bei schwachen Batterien zur Einplanung in reguläre Wartungsfahrten |
| 🧑‍💻 **Entwicklungsteam (Two and a Half Men)** | Verantwortlich für Spezifikation, Architektur, Umsetzung und QS | Realisierung eines wartbaren, modularen und testbaren Softwaresystems nach SE-Standards |
| 🧑‍🏫 **Auftraggeber / Dozent (DHBW)** | Fachliche Begleitung und Bewertung der Projektleistung | Einhaltung von Software-Engineering-Methoden, Anforderungs- und Codequalität, lückenlose Dokumentation |

---

### d. Personas & Nutzungsszenarien

#### 👨‍💼 Markus (48, Flottenmanager & Betriebsleiter)
- **Hintergrund:** Verwaltet über 80 mobile Verkehrsleitsysteme in einer weitläufigen Region.
- **Bedürfnis:** Möchte morgens auf einen Blick sehen, welche Geräte einwandfrei laufen und wo zeitnah Handlungsbedarf besteht.
- **Kernziel:** Vermeidung von Störfällen im laufenden Verkehr und Senkung der jährlichen Notdiensteinsatzkosten.

#### 👨‍🔧 Kevin (29, Servicetechniker)
- **Hintergrund:** Fährt mit dem Service-Transporter Wartungsrouten ab.
- **Bedürfnis:** Benötigt auf seinem Tablet den exakten Standort der Stationen sowie den genauen Spannungsverlauf vor Ort.
- **Kernziel:** Schnelles Auffinden der Boxen via GPS und transparente Historie, um defekte Batteriezellen von normaler Entladung zu unterscheiden.

#### 👩‍💼 Sarah (35, Disponentin)
- **Hintergrund:** Plant wöchentliche Inspektions- und Wartungstouren.
- **Bedürfnis:** Benötigt eine Frühwarnliste („Warnstufe Gelb“), damit Batteriewechsel mit mindestens 2–3 Tagen Vorlaufzeit in bestehende Touren integriert werden können.
- **Kernziel:** Keine Sonderfahrten für einzelne leere Batterien.

---

## 🎯 2. Projektscope & MVP-Abgrenzung

Das Projekt wird nach agilen Prinzipien inkrementell entwickelt. Zunächst entsteht ein **Minimal Viable Product (MVP)**, das den gesamten Datenfluss vom Broker bis zur Benutzeroberfläche abbildet.

### 🟢 Im Scope (MVP)
- **MQTT-Ingestion-Service:** Empfang, syntaktische Prüfung und Validierung von Telemetrienachrichten der TrafficNodes.
- **Persistenz:** Transaktionssichere Speicherung von Messreihen (Zeitreihendaten) und Gerätestammdaten in einer relationalen Datenbank.
- **Regelbasierte Statusbewertung:** Automatische Klassifikation in *Normal*, *Warnung*, *Kritisch* und *Offline/Timeout*.
- **Web-Dashboard:**
  - Tabellarische Flottenübersicht mit Filterung nach Gerätestatus und Suchfunktion.
  - Detailansicht pro Gerät mit Kenndaten und Liniendiagramm für Spannungs- und Stromverlauf.
  - Interaktive Kartenansicht (OpenStreetMap / Leaflet) mit farblich markierten Pins.
- **Telemetrie-Simulator:** Tool zur Generierung realistischer Test-Datenströme (inkl. Simulation von Entladezyklen und Verbindungsabbrüchen).

### 🟡 Geplante Erweiterungen (Release 2 / Post-MVP)
- Automatischer Benachrichtigungsdienst (E-Mail / Webhook) bei Eintritt eines kritischen Zustands.
- CSV- und JSON-Export von Messreihen für externe Analyseprogramme.
- Vorhersagemodell (Predictive Maintenance) für die verbleibende Batterielaufzeit auf Basis von Trendextrapolation.
- Benutzer- und Rechteverwaltung (Rollen: Administrator, Disponent, Techniker).

### 🚫 Explizit Out-of-Scope
- Entwicklung von Hardware, Platinenlayouts oder Sensorschaltungen der TrafficNode.
- Entwicklung von Firmware oder C-Treibern für Mikrocontroller.
- Bidirektionale Steuerung der Feldgeräte (z. B. Remote-Abschaltung mobiler Ampeln).
- Routenoptimierungs- und Navigationssoftware für Servicefahrzeuge.
- ERP-, Inventar- oder Abrechnungsanbindung.

---

## 📐 3. Randbedingungen (Constraints)

### a. Technische Randbedingungen
- **Schnittstelle zur Datenerfassung:** Kommunikation erfolgt ausschließlich über standardisierte MQTT-Nachrichten (MQTT 3.1.1 / 5.0) mit JSON-Nutzlast über ein definiertes Topic-Schema.
- **Plattformunabhängigkeit:** Das Backend und die Weboberfläche müssen containerisiert (Docker) auf Standard-Linux- und macOS-Umgebungen lauffähig sein.
- **Webstandards:** Clientzugriff über moderne Standard-Webbrowser (Chrome, Firefox, Safari, Edge) ohne proprietäre Plugins.

### b. Organisatorische Randbedingungen
- **Projektrahmen:** Durchführung als DHBW-Studienprojekt im Rahmen der Lehrveranstaltung *Software Engineering*.
- **Teamressourcen:** Umsetzung durch das zweiköpfige Projektteam *Two and a Half Men* (Felix Teufel, Dennis Nikitin).
- **Tooling & AI-Unterstützung:** Der freigegebene Coding Agent darf für die Implementierung herangezogen werden; die methodische Verantwortung für Architektur, Datenmodell, Qualitätssicherung und Abnahmetests verbleibt vollständig beim Team.

### c. Rechtliche & Normative Randbedingungen
- **Datenschutz (DSGVO):** Es werden reine Maschinenzustands- und Gerätestandortdaten verarbeitet. Es werden keine personenbezogenen Fahrer- oder Bewegungsdaten erfasst.
- **Open-Source-Konformität:** Sämtliche eingesetzten Frameworks und Bibliotheken müssen freie, unbedenkliche Lizenzen (z. B. MIT, Apache 2.0, BSD) aufweisen.

---

## 📊 4. Mengengerüst

| Parameter | Planwert (MVP) | Skalierungsziel (Ausbaustufe) | Begründung / Annahme |
|---|---|---|---|
| **Verwaltete Geräte (TrafficNodes)** | 50 – 100 Geräte | bis zu 1.000 Geräte | Typische Flottengröße eines regionalen Verkehrssicherungsdienstleisters |
| **Sendeintervall pro Gerät** | 60 Sekunden | 15 – 300 Sekunden (dynamisch) | Standardintervall für zeitnahes Condition Monitoring bei moderatem Datenvolumen |
| **Nachrichtenrate (Ingestion)** | ~1 bis 2 Msg/s (Peak: 10 Msg/s) | ~20 bis 50 Msg/s | Gleichmäßige Verteilung der Sendezyklen über die Minute |
| **Nutzlastgröße (Payload)** | ca. 200 – 300 Bytes / Nachricht | ca. 300 Bytes | Kompaktes JSON mit ID, Zeitstempel, Spannung, Strom und Koordinaten |
| **Tägliches Datenvolumen** | ca. 20 – 30 MB / Tag (100 Geräte) | ca. 300 MB / Tag | Entspricht ca. 144.000 Datensätzen pro Tag bei 100 Geräten |
| **Datenhaltezeitraum (Retention)** | 90 Tage hochauflösend | 12 Monate aggregiert | Ermöglicht Langzeituntersuchung von Entladekurven zur Wechselzeitpunkt-Analyse |
| **Gleichzeitige Benutzer (UI)** | 2 – 5 Personen | bis zu 25 Personen | Interne Nutzung durch Disponenten und Betriebsleitung |

---

## ⚙️ 5. Funktionale Anforderungen (Requirements)

Die Anforderungen sind nach MoSCoW priorisiert (*Must-Have*, *Should-Have*, *Could-Have*) und besitzen ein eindeutig beobachtbares und überprüfbares Akzeptanzkriterium.

| ID | Anforderung | Beschreibung | Quelle | Priorität | Beobachtbares Prüfkriterium (Akzeptanzkriterium) |
|---|---|---|---|---|---|
| **F01** | **MQTT-Telemetrieingestion** | Das System nimmt eingehende JSON-Messdaten von der TrafficNode über den MQTT-Broker entgegen und validiert diese syntaktisch. | Aufgabenstellung | 🟢 Must-Have | Trifft eine valide JSON-Nachricht auf Topic `trafficnode/{id}/telemetry` ein, wird sie innerhalb von < 500 ms in der Datenbank abgelegt. Ungültige Nachrichten (z. B. ungültiges JSON oder fehlende Pflichtfelder) werden verworfen und im Error-Log protokolliert. |
| **F02** | **Automatische Statusbewertung** | Auswertung der aktuellen Batteriespannung und des Empfangszeitpunkts zur Einstufung in die Statusklassen *Normal*, *Warnung*, *Kritisch* oder *Offline*. | Aufgabenstellung / Flottenmanager | 🟢 Must-Have | Liegt die geglättete Spannung unter 11,8 V, wechselt der Status des Geräts auf „Kritisch“. Bleibt eine Nachricht länger als 30 Minuten aus, wechselt der Status auf „Offline“. Die Statusänderung wird sofort in der Datenbank reflektiert. |
| **F03** | **Flotten-Dashboard (Listenansicht)** | Übersichtliche tabellarische Darstellung aller bekannten Geräte mit Node-ID, aktuellem Status, Spannung, Strom, Standort und Zeitstempel der letzten Messung. | Flottenmanager / Betriebsleiter | 🟢 Must-Have | Das Dashboard zeigt alle registrierten Geräte an. Die Liste kann nach Status (Normal, Warnung, Kritisch, Offline) gefiltert und per Freitextsuche nach der Node-ID durchsucht werden. |
| **F04** | **Kartenvisualisierung** | Anzeige der Standorte aller TrafficNodes auf einer interaktiven Landkarte. | Servicetechniker / Disponent | 🟢 Must-Have | Auf einer interaktiven Karte (Leaflet/OSM) wird jedes Gerät mit einem farbcodierten Marker entsprechend seinem Status angezeigt. Ein Klick auf den Marker öffnet ein Popup mit ID, Spannung und aktuellem Status. |
| **F05** | **Detailansicht & Historie** | Detaillierte Einzelansicht eines Geräts mit Stammdaten und Zeitreihendiagramm für Spannungs- und Stromverlauf. | Aufgabenstellung / Betriebsleiter | 🟢 Must-Have | In der Detailansicht wird für ein gewähltes Gerät der zeitliche Verlauf von Spannung ($U$) und Strom ($I$) als interaktives Liniendiagramm für konfigurierbare Intervalle (24h, 7 Tage, 30 Tage) dargestellt. |
| **F06** | **Telemetrie-Simulator** | Ein eigenständiges Software-Skript zur Erzeugung realistischer MQTT-Messdaten für mehrere virtuelle Geräte. | Entwicklungsteam / Dozent | 🟢 Must-Have | Der Simulator kann mindestens 10 virtuelle TrafficNodes parallel simulieren und zyklisch Daten publizieren. Szenarien wie lineare Entladung, Tiefentladung und Sendeunterbrechung können per Parameter gezielt ausgelöst werden. |
| **F07** | **Konfigurierbare Schwellenwerte** | Zentrale Konfigurationsmöglichkeit für Spannungsgrenzwerte und Timeout-Zeiten. | Betriebsleiter | 🟠 Should-Have | Schwellenwerte für Warn- und Alarmgrenzen sowie das Offline-Intervall können in einer Konfigurationsdatei oder über Umgebungsvariablen angepasst werden, ohne den Programmcode zu verändern. |
| **F08** | **Ereignis- & Alarmhistorie** | Chronologische Protokollierung aller aufgetretenen Statuswechsel (z. B. Übergang von Warnung zu Kritisch). | Flottenmanager | 🟠 Should-Have | Jeder Statuswechsel wird mit Vorher-/Nachher-Zustand, Auslösemesswert und Zeitstempel in einer Ereignistabelle abgelegt und kann im UI als Alert-Historie eingesehen werden. |
| **F09** | **CSV-Datenexport** | Export historischer Messreihen eines Geräts zur weiteren wissenschaftlichen oder statistischen Auswertung. | Aufgabenstellung | 🟡 Could-Have | In der Detailansicht existiert ein Button „CSV-Export“, der eine gültige CSV-Datei mit den Spalten `timestamp`, `node_id`, `voltage`, `current`, `latitude`, `longitude` für den gewählten Zeitraum herunterlädt. |
| **F10** | **Alarm-Benachrichtigung (Alerting)** | Auslösen einer Benachrichtigung beim Eintritt des Status „Kritisch“. | Servicetechniker / Disponent | 🟡 Could-Have | Sobald ein Gerät in den Status „Kritisch“ wechselt, wird ein konfigurierter Webhook aufgerufen oder eine E-Mail-Nachricht an den Disponenten versendet. |

---

## 🧱 6. Nicht-funktionale Anforderungen (Quality Requirements)

| ID | Kategorie | Beschreibung | Quelle | Priorität | Beobachtbares / Messbares Prüfkriterium |
|---|---|---|---|---|---|
| **NFA01** | **Performance** | Durchsatz und Verarbeitungszeit der Ingestion | Architektur / Lastprofil | 🟢 Must-Have | Bei einer Last von 20 eingehenden MQTT-Nachrichten pro Sekunde beträgt die Latenz vom Eintreffen am Broker bis zum DB-Commit im 95. Perzentil weniger als 300 ms. |
| **NFA02** | **Performance** | Antwortzeiten der Weboberfläche | Usability / UI-Standard | 🟢 Must-Have | Die Dashboard-Übersicht lädt bei 100 registrierten Geräten in unter 1,5 Sekunden. Filter- und Suchaktionen im Frontend reagieren in unter 200 ms. |
| **NFA03** | **Zuverlässigkeit** | Resilienz bei Verbindungsabbrüchen | Systemarchitektur | 🟢 Must-Have | Bei einem Neustart oder temporären Netzausfall des MQTT-Brokers stellt der Ingestion-Service die Verbindung innerhalb von 10 Sekunden nach Wiederverfügbarkeit automatisch wieder her. |
| **NFA04** | **Datenintegrität** | Transaktionssicherheit & Persistenz | Aufgabenstellung | 🟢 Must-Have | Bei abruptem Abbruch oder Neustart des Backend-Services bleiben alle bis dahin quittierten Datensätze in der Datenbank konsistent erhalten (ACID-Prinzip, keine verwaisten Teilzustände). |
| **NFA05** | **Usability** | Intuitive Statuserfassung | Servicetechniker / Flottenmanager | 🟢 Must-Have | Die Bedeutung der Statusfarben (Grün, Gelb, Rot, Grau) erschließt sich ohne Schulung; kritische Geräte werden durch visuelle Hervorhebung innerhalb von 5 Sekunden wahrgenommen. |
| **NFA06** | **Wartbarkeit** | Modulare Architektur & Code-Qualität | SE-Lehrveranstaltung | 🟢 Must-Have | Klare Entkopplung in Ingestion-, Business-Logic-, API- und Frontend-Komponenten. Alle automatisierten Linter (z. B. Flake8/ESLint) melden 0 Fehler; Einhaltung einheitlicher Formatierungsregeln. |
| **NFA07** | **Testbarkeit** | Automatisierte Testabdeckung | SE-Lehrveranstaltung | 🟢 Must-Have | Die Geschäfts- und Bewertungslogik (Parser, Validierung, Statusermittlung) weist eine automatisierte Unit-Testabdeckung von mindestens 80 % (Code Coverage) auf. |
| **NFA08** | **Sicherheit** | Schnittstellenabsicherung & Robustheit | Best Practice | 🟠 Should-Have | Sämtliche REST-Endpunkte validieren Eingabeparameter typensicher (z. B. gegen SQL-Injections). Keine ungesicherten Standardpasswörter in Produktionskonfigurationen. |

---

## 🚦 7. Fachliche Statusregeln & Schwellenwerte

Für den Einsatz mit standardmäßigen 12V Blei-Vlies- (AGM/Gel) bzw. LiFePO4-Pufferbatterien gelten folgende betriebliche Regeln:

| Status | Farbcode | Technische Bedingung | Fachliche Bedeutung & Handlungsanweisung |
|---|---|---|---|
| **Normal / OK** | 🟢 Grün | $U \ge 12{,}4\text{ V}$ und letzter Kontakt $< 30\text{ min}$ | Batterie ist ausreichend geladen. Regelbetrieb ohne Wartungsbedarf. |
| **Warnung** | 🟡 Gelb | $11{,}8\text{ V} \le U < 12{,}4\text{ V}$ und letzter Kontakt $< 30\text{ min}$ | Batterie nähert sich der Entladeschwelle. Wechsel sollte für die nächste planmäßige Servicefahrt eingeplant werden. |
| **Kritisch** | 🔴 Rot | $U < 11{,}8\text{ V}$ und letzter Kontakt $< 30\text{ min}$ | Akute Gefahr von Tiefentladung und Geräteausfall. Sofortiger Batteriewechsel erforderlich! |
| **Offline** | ⚪ Grau | Letzter Kontakt $\ge 30\text{ min}$ (unabhängig von $U$) | Gerät sendet nicht mehr (Funkloch, Hardwarefehler oder Batterie vollständig entleert). Prüfung vor Ort notwendig. |

### Hysterese & Messwertglättung
Um ein „Flattern“ des Status (z. B. ständiges Springen zwischen Warnung und Kritisch) durch kurzzeitige Spannungsabfälle bei Lastspitzen (z. B. Sendeimpulse des Funkmoduls) zu verhindern, wird die Statusbewertung über einen **gleitenden Mittelwert über die letzten 3 Messungen** durchgeführt.

---

## ❓ 8. Offene fachliche Fragen & Klärung mit dem Auftraggeber

Zur Schärfung der Anforderungen wurden die folgenden fachlichen Fragestellungen identifiziert und bezüglich ihres Klärungsstands dokumentiert:

| ID | Thema / Fragestellung | Relevanz für das System | Getroffene Annahme / Lösungsvorschlag | Klärungsstatus |
|---|---|---|---|---|
| **Q01** | **Batterietechnologie & Nennspannung:** Werden ausschließlich 12V Blei-Säure/AGM-Akkus überwacht oder auch andere Typen (z. B. 24V oder Lithium-Systeme)? | Beeinflusst die Spannungs-Schwellenwerte und Entladekurven. | Standardmäßig wird von 12V-Systemen ausgegangen. Schwellenwerte werden konfigurierbar hinterlegt, um zukünftige Akkutypen zu unterstützen. | 🟡 In Klärung mit Auftraggeber |
| **Q02** | **Toleranzzeitraum für Offline-Erkennung:** Ab welchem Sendeausfall soll ein Gerät verbindlich als „Offline“ markiert werden? | Verhindert Fehlalarme bei kurzzeitigen Funklöchern oder Netzüberlastung. | Als Standardzeitfenster werden **30 Minuten** angesetzt (entspricht 30 verpassten Paketen bei 60s Intervall). | 🟢 Mit Auftraggeber vorabgestimmt |
| **Q03** | **Struktur der MQTT-Nutzlast:** Liegt bereits ein fixes JSON-Schema der TrafficNode vor? | Notwendig für die Implementierung des Ingestion-Parsers. | Festlegung eines robusten Schemas: `{"node_id": "string", "timestamp": "ISO8601", "voltage": float, "current": float, "lat": float, "lon": float}`. | 🟢 Abgestimmt & bestätigt |
| **Q04** | **Mobilitätsverhalten der Geräte:** Bewegen sich die Geräte kontinuierlich während des Betriebs oder verweilen sie an Einsatzorten? | Bestimmt die Art der Kartendarstellung (Echtzeit-Tracking vs. statische Marker mit Wechselhistorie). | Geräte sind semimobil (Verbleib an Baustellen über Tage/Wochen). Anzeige als feste Pins mit Verlaufsübersicht bei Umplatzierung. | 🟢 Durch Auftraggeber bestätigt |
| **Q05** | **Datenaufbewahrung & Granularität:** Müssen Sekunden- bzw. Minutendaten unbegrenzt aufbewahrt werden? | Beeinflusst Dimensionierung und Speicherbedarf der Datenbank. | Rohdaten werden 90 Tage gespeichert. Ältere Daten können auf Stundenmittelwerte verdichtet werden. | 🟡 Zur Freigabe eingereicht |

---

## 🔍 9. Anforderungsreview

Vor Beginn der Architektur- und Implementierungsphase wurden alle Anforderungen einem strukturierten Peer-Review nach bewährten Software-Engineering-Praktiken unterzogen.

### a. Review-Methodik & Kriterien
- **SMART-Kriterien:** Sind Anforderungen spezifisch, messbar, erreichbar, relevant und terminiert formuliert?
- **INVEST-Prinzip:** Sind funktionale Stories unabhängig, verhandelbar, wertbringend, schätzbar, angemessen klein und testbar?
- **Widerspruchsfreiheit & Eindeutigkeit:** Schließen sich Regeln (z. B. Schwellenwerte) nicht gegenseitig aus? Gibt es undefinierte Randbereiche?
- **Prüfbarkeit:** Verfügt jedes Requirement über ein objektiv nachprüfbares Akzeptanzkriterium?

### b. Review-Ergebnisse & eingesteuerte Maßnahmen
1. **Finding 1 (Feste Schwellenwerte):** Ursprünglich waren Spannungs-Grenzwerte fest im Code vorgesehen. Dies hätte eine Umrüstung auf 24V oder LiFePO4 unmöglich gemacht.  
   *Maßnahme:* Anforderung F07 (Konfigurierbarkeit) aufgenommen; Schwellenwerte werden aus Konfigurationsdateien bezogen.
2. **Finding 2 (Fehlender Timeout-Status):** Anfangs gab es nur die Zustände Grün, Gelb, Rot. Geräte im Funkloch blieben auf ihrem letzten Messwert stehen.  
   *Maßnahme:* Einführung des expliziten Zustands *Offline / Timeout* (Grau) bei Ausbleiben von Messwerten > 30 Minuten.
3. **Finding 3 (Gefahr von Alarm-Flattern):** Kurzzeitige Stromspitzen führen zu vorübergehenden Spannungseinbrüchen, was Fehlalarme erzeugt hätte.  
   *Maßnahme:* Definition einer Messwertglättung über gleitende Durchschnitte ($N=3$) in den Statusregeln verankert.
4. **Finding 4 (Scope-Präzisierung):** Unklarheit, ob Schnittstellen zu Firmware oder Hardware programmiert werden müssen.  
   *Maßnahme:* Explizite Abgrenzung formuliert: Systemverantwortung beginnt am MQTT-Broker; Bereitstellung eines Simulators für Tests.

---

## 📖 10. Glossar (Dictionary)

Zur Vermeidung von Missverständnissen zwischen Stakeholdern, Entwicklern und Prüfern gilt folgendes Begriffsverzeichnis:

| Fachbegriff | Englischer Begriff / Code-Symbol | Definition & Erläuterung | Abgrenzung / Hinweis |
|---|---|---|---|
| **TrafficNode** | `traffic_node` / TrafficNode | Nachrüstbare Hardware-Telemetriebox an den mobilen Endgeräten, welche Sensorwerte erfasst und über Funk per MQTT publiziert. | Die physische Box selbst wird im Softwareprojekt nicht entwickelt. |
| **Telemetriedaten** | `telemetry_data` | Messwert-Tupel bestehend aus Zeitstempel, Batteriespannung ($U$), Entladestrom ($I$) und GPS-Koordinaten. | Nicht zu verwechseln mit reinen Status- oder Fehlermeldungen. |
| **Klemmenspannung** | `voltage` ($U$ in Volt) | Die an den Batteriepolen anliegende elektrische Potenzialdifferenz; Hauptindikator für den Ladezustand. | Bei Belastung geringer als die Leerlaufspannung (Lastabhängigkeit). |
| **Entladestrom** | `current` ($I$ in Ampere) | Der momentan von der mobilen Einheit aus der Batterie entnommene elektrische Strom. | Positives Vorzeichen entspricht Entladung, negatives Vorzeichen Ladung (z. B. via Solar). |
| **Ladezustand (SoC)** | `state_of_charge` (SoC in %) | Prozentualer Füllstand der Batterie bezogen auf die Nennkapazität. | Kann im MVP nur näherungsweise über die Spannung abgeschätzt werden. |
| **MQTT-Broker** | `mqtt_broker` | Zentraler nachrichtenorientierter Server (Publish/Subscribe-Architektur), an den TrafficNodes Daten senden und von dem das Backend liest. | Kommunikationsschnittstelle zwischen Außenwelt und Backend. |
| **Ingestion-Service** | `ingestion_service` | Softwarekomponente im Backend, welche die MQTT-Topics abonniert, Nachrichten entgegennimmt, validiert und speichert. | Reine Datenerfassung ohne Frontend-Funktionalität. |
| **Condition Monitoring** | Condition Monitoring | Kontinuierliche Zustandsüberwachung von Maschinenparametern zur Feststellung des aktuellen Anlagenzustands. | Basisstufe vor der vorausschauenden Instandhaltung. |
| **Predictive Maintenance** | Predictive Maintenance | Vorausschauende Wartung basierend auf historischen Trenddaten und Ausfallprognosen. | Zielperspektive für Post-MVP-Phasen. |
| **Hysterese** | Hysteresis | Verzögertes Umschalten zwischen Zuständen bzw. Schwellenwert-Pufferung zur Vermeidung von Signalsprüngen bei Grenzwerten. | Technische Maßnahme gegen Fehlalarme. |

---

## 🏛️ 11. Architektur- & Technologievorschau

Das Softwaresystem folgt einer entkoppelten, 4-schichtigen Service-Architektur:

1. **Datenerzeugung:**
   - Echte TrafficNodes im Feld oder der integrierte **Telemetrie-Simulator** publizieren Messpakete im JSON-Format auf Topics des Schemas `trafficnode/{node_id}/telemetry`.
2. **Ingestion & Business-Logik (Backend):**
   - **MQTT Ingestion Worker:** Nimmt Datenströme asynchron entgegen, parst und validiert die Nutzlast.
   - **Rules Engine:** Führt die Glättung durch, prüft Schwellenwerte und ermittelt den aktuellen Status (Normal, Warnung, Kritisch, Offline).
   - **REST API:** Stellt strukturierte Endpunkte für Dashboard, Historienabfragen, Filter und Kartendaten bereit.
3. **Datenhaltung:**
   - Relationale Datenbank (SQLite für lokale Entwicklung, PostgreSQL für den Regelbetrieb) zur persistenten Speicherung von Gerätestammdaten, Messreihen und Event-Logs.
4. **Präsentationsschicht (Web-Frontend):**
   - Moderne, responsive Single-Page-Anwendung (z. B. React / TypeScript mit Tailwind CSS) mit dynamischer Flottentabelle, Diagrammen (Chart.js) und OpenStreetMap/Leaflet-Kartenintegration.

---

## 👥 12. Team & Rollenverteilung

| Rolle | Teammitglied | Verantwortungsbereiche |
|---|---|---|
| **Product Owner & System Analyst** | Felix Teufel | Fachliche Spezifikation, Requirements Engineering, Abstimmung mit Stakeholdern, Definition von Abnahmekriterien, Backlog-Pflege |
| **Lead Developer & Software Architect** | Dennis Nikitin | Systemarchitektur, Ingestion-Pipeline, Schnittstellendesign, Datenbankmodellierung, CI/CD-Pipeline, Test-Automatisierung |

---

## 🧰 13. Tools & Technologien

- **Protokoll & Broker:** MQTT 3.1.1 / 5.0 (Eclipse Mosquitto)
- **Backend & Ingestion:** Python (FastAPI, Paho-MQTT, SQLAlchemy) oder TypeScript / Node.js
- **Frontend:** React / TypeScript, Tailwind CSS, Leaflet (OSM-Karten), Chart.js
- **Datenbank:** SQLite (Entwicklung) / PostgreSQL (Produktivbetrieb)
- **Test- & Simulationswerkzeuge:** Pytest, Custom Telemetrie-Simulator
- **Qualitätssicherung & CI/CD:** Git, GitHub, GitHub Actions, Flake8 / ESLint