# TN-Watch
>Hinweis: Dieses Projekt ist Teil von Software Engineering an der DHBW Stuttgart

Gruppe: Two and a Half Men (Felix Teufel, Dennis Nikitin)

<img src="img/team_two_and_a_half_men.png" width="30%">

## 💡 1. Kontext & Problemstellung

### Ausgangssituation & Motivation
Ein Betreiber setzt zahlreiche mobile, batteriebetriebene Geräte ein (z. B. mobile Baustellenampeln, temporäre LED-Verkehrstafeln, Umweltsensorstationen). Diese Geräte verfügen bislang über keine integrierte digitale Zustandsüberwachung. Die Folge in der Praxis:
- **Ungeplante Geräteausfälle:** Entladene oder defekte Batterien führen zum plötzlichen Stillstand der Geräte im Einsatz.
- **Hohe Betriebskosten:** Unvorhergesehene Eileinsätze des Servicepersonals zur Störungsbeseitigung verursachen beträchtliche Fahrt- und Personalkosten.
- **Ressourcenverschwendung:** Um Ausfälle abzuwenden, werden Batterien häufig viel zu früh getauscht, was die wirtschaftliche Lebensdauer der Akkus verkürzt.

Unser System **TN-Watch** realisiert hierfür eine webbasierte Fernüberwachungs- und Koordinierungslösung.

### Systemkontext & Schnittstellenabgrenzung
Zur Erfassung der Messwerte dient eine nachrüstbare Telemetriebox (TrafficNode) an jedem Gerät. Diese misst kontinuierlich:
- Batteriespannung ($U$ in Volt)
- Entladestrom ($I$ in Ampere)
- Geografische Position (GPS-Koordinaten: Breitengrad, Längengrad)

Die TrafficNode übermittelt diese Messdaten per Funk an einen zentralen MQTT-Broker.
- **Startpunkt des Softwareprojekts:** Die Entwicklungsaufgabe beginnt an der Schnittstelle des **MQTT-Brokers**.
- **Out-of-Scope:** Hardware-Entwicklung, Sensorschaltungen und Firmware der TrafficNode sind **nicht** Bestandteil des Projekts. Für Test- und Demonstrationszwecke wird ein Software-Simulator eingesetzt.

### Stakeholder

| Stakeholder | Beschreibung | Ziel / Interesse |
|---|---|---|
| **Betriebsleiter** | Verantwortlich für Zuverlässigkeit, Verfügbarkeit und Wirtschaftlichkeit der Geräteflotte | Vermeidung von Ausfällen, Reduzierung teurer Notfalleinsätze, vollständige Flottenübersicht |
| **Servicetechniker (Außendienst)** | Tauscht und wartet Batterien vor Ort im Einsatzgebiet | Exakte GPS-Standortdaten, verlässliche Statusanzeigen, Vermeidung unnötiger Tauschvorgänge, Routenplaner |
| **Entwicklungsteam (Wir)** | Verantwortlich für Spezifikation, Architektur, Umsetzung und QS | Realisierung eines wartbaren, modularen und testbaren Softwaresystems nach SE-Standards |
| **Auftraggeber (Dozent)** | Fachliche Begleitung und Bewertung der Projektleistung | Einhaltung von Software-Engineering-Methoden, Anforderungs- und Codequalität, lückenlose Dokumentation |

### Personas & Nutzungsszenarien

#### Markus (48, Flottenmanager & Betriebsleiter)
- **Hintergrund:** Verwaltet über 80 mobile Verkehrsleitsysteme in einer weitläufigen Region.
- **Bedürfnis:** Möchte morgens auf einen Blick sehen, welche Geräte einwandfrei laufen und wo zeitnah Handlungsbedarf besteht.
- **Kernziel:** Vermeidung von Störfällen im laufenden Verkehr und Senkung der jährlichen Notdiensteinsatzkosten.

#### Kevin (29, Servicetechniker)
- **Hintergrund:** Fährt mit dem Service-Transporter Wartungsrouten ab.
- **Bedürfnis:** Benötigt auf seinem Tablet als auch auf seinem Handy den Standort der Stationen.
- **Kernziel:** Schnelles Auffinden der Boxen via Google Maps und transparente Historie, um defekte Batteriezellen von normaler Entladung zu unterscheiden.

#### Anja (63, Servicetechnikerin)
- **Hintergrund:** Fährt mit dem Service-Transporter Wartungsrouten ab, hat aber kein Handy.
- **Bedürfnis:** Benötigt auf ihrem Tablet den Standort der Stationen sowie eine Adresse oder andere Möglichkeit mit der Sie hinfahren kann.
- **Kernziel:** Schnelles Auffinden der Boxen und transparente Historie, um defekte Batteriezellen von normaler Entladung zu unterscheiden.

## 🎯 2. Projektscope & MVP

In diesem Projekt entsteht zunächst ein **Minimal Viable Product (MVP)**, das den gesamten Datenfluss vom Broker bis zur Benutzeroberfläche abbildet.

### 🟢 Im Scope (MVP)
- **MQTT-Ingestion-Service:** Empfang, syntaktische Prüfung und Validierung von Telemetrienachrichten der TrafficNodes.
- **Persistenz:** Transaktionssichere Speicherung von Messreihen (Zeitreihendaten) und Gerätestammdaten in einer relationalen Datenbank.
- **Regelbasierte Statusbewertung:** Automatische Klassifikation in Normal, Warnung, Kritisch und Offline/Timeout.
- **Web-Dashboard:**
  - Flottenübersicht mit Gerätestatus.
  - Detailansicht pro Gerät mit Kenndaten und Liniendiagramm für Spannungs- und Stromverlauf.
  - Interaktive Kartenansicht mit farblich markierten Pins.
  - Benutzer- und Rechteverwaltung

### 🟡 Geplante Erweiterungen (Post-MVP)
- Automatischer Benachrichtigungsdienst (E-Mail) bei Eintritt eines kritischen Zustands.
- Routenplanung via Google Maps
- Vorhersagemodell (Predictive Maintenance) für die verbleibende Batterielaufzeit auf Basis von historischer Daten.

### 🚫 Out-of-Scope
- Entwicklung von Hardware, Platinenlayouts oder Sensorschaltungen der TrafficNode.
- Entwicklung von Firmware oder C-Treibern für Mikrocontroller.
- Bidirektionale Steuerung der Feldgeräte (z. B. Remote-Abschaltung mobiler Ampeln).
- ERP-, Inventar- oder Abrechnungsanbindung.

## 📐 3. Randbedingungen

### Technische Randbedingungen
- **Schnittstelle zur Datenerfassung:** Kommunikation erfolgt ausschließlich über standardisierte MQTT-Nachrichten mit JSON-Nutzlast über ein definiertes Topic-Schema.
- **Plattformunabhängigkeit:** Das Backend und die Weboberfläche müssen containerisiert (Docker) auf Standard-Linux- und macOS-Umgebungen lauffähig sein.
- **Webstandards:** Clientzugriff über moderne Standard-Webbrowser (Chrome, Firefox, Safari, Edge) ohne proprietäre Plugins.

### Organisatorische Randbedingungen
- **Projektrahmen:** Durchführung als DHBW-Studienprojekt im Rahmen der Lehrveranstaltung Software Engineering.
- **Teamressourcen:** Umsetzung durch das zweiköpfige Projektteam (Felix Teufel, Dennis Nikitin).
- **Tooling & AI-Unterstützung:** Der freigegebene Coding Agent darf für die Implementierung herangezogen werden; die methodische Verantwortung für Architektur, Datenmodell, Qualitätssicherung und Abnahmetests verbleibt vollständig beim Team.

### Rechtliche & Normative Randbedingungen
- **Datenschutz (DSGVO):** Es werden reine Maschinenzustands- und Gerätestandortdaten verarbeitet. Es werden keine personenbezogenen Fahrer- oder Bewegungsdaten erfasst.
- **Open-Source-Konformität:** Sämtliche eingesetzten Frameworks und Bibliotheken müssen freie Lizenzen (z. B. MIT, Apache 2.0, BSD) aufweisen.

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

## ⚙️ 5. Funktionale Anforderungen

Die Anforderungen sind nach MoSCoW priorisiert (*Must-Have*, *Should-Have*, *Could-Have*) und besitzen ein eindeutig beobachtbares und überprüfbares Akzeptanzkriterium.

| ID | Anforderung | Beschreibung | Quelle | Priorität |
|---|---|---|---|---|
| **F01** | **MQTT-Telemetrieingestion** | Das System nimmt eingehende JSON-Messdaten von der TrafficNode über den MQTT-Broker entgegen und validiert diese syntaktisch. | Aufgabenstellung | 🟢 Must-Have |
| **F02** | **Automatische Statusbewertung** | Auswertung der aktuellen Batteriespannung und des Empfangszeitpunkts zur Einstufung in die Statusklassen *Normal*, *Warnung*, *Kritisch* oder *Offline*. | Aufgabenstellung / Flottenmanager | 🟢 Must-Have |
| **F03** | **Flotten-Dashboard (Listenansicht)** | Übersichtliche tabellarische Darstellung aller bekannten Geräte mit Node-ID, aktuellem Status, Spannung, Strom, Standort und Zeitstempel der letzten Messung. | Flottenmanager / Betriebsleiter | 🟢 Must-Have |
| **F04** | **Kartenvisualisierung** | Anzeige der Standorte aller TrafficNodes auf einer interaktiven Landkarte. | Servicetechniker / Disponent | 🟢 Must-Have |
| **F05** | **Detailansicht & Historie** | Detaillierte Einzelansicht eines Geräts mit Stammdaten und Zeitreihendiagramm für Spannungs- und Stromverlauf. | Aufgabenstellung / Betriebsleiter | 🟢 Must-Have |
| **F06** | **Ereignis- & Alarmhistorie** | Chronologische Protokollierung aller aufgetretenen Statuswechsel (z. B. Übergang von Warnung zu Kritisch). | Flottenmanager | 🟠 Should-Have |
| **F07** | **Routenplanung via Google Maps** | Automatische Routenplanung durch anklicken der kritischen Nodes und weiterleitung an Google Maps | Servicetechniker | 🟠 Should-Have |
| **F08** | **CSV-Datenexport** | Export historischer Messreihen eines Geräts zur weiteren wissenschaftlichen oder statistischen Auswertung. | Aufgabenstellung | 🟡 Could-Have |
| **F09** | **Benachrichtigung** | Auslösen einer Benachrichtigung beim Eintritt des Status „Kritisch“. | Servicetechniker / Disponent | 🟡 Could-Have |

## 🧱 6. Nicht-funktionale Anforderungen

| ID | Kategorie | Beschreibung | Quelle | Priorität | Messbares Prüfkriterium |
|---|---|---|---|---|---|
| **NFA01** | **Performance** | Durchsatz und Verarbeitungszeit der Ingestion | Architektur / Lastprofil | 🟢 Must-Have | Bei einer Last von 20 eingehenden MQTT-Nachrichten pro Sekunde beträgt die Latenz vom Eintreffen am Broker bis zum DB-Commit im 95. Perzentil weniger als 300 ms. |
| **NFA02** | **Performance** | Antwortzeiten der Weboberfläche | Usability / UI-Standard | 🟢 Must-Have | Die Dashboard-Übersicht lädt bei 100 registrierten Geräten in unter 1,5 Sekunden. Filter- und Suchaktionen im Frontend reagieren in unter 200 ms. |
| **NFA03** | **Zuverlässigkeit** | Resilienz bei Verbindungsabbrüchen | Systemarchitektur | 🟢 Must-Have | Bei einem Neustart oder temporären Netzausfall des MQTT-Brokers stellt der Ingestion-Service die Verbindung innerhalb von 10 Sekunden nach Wiederverfügbarkeit automatisch wieder her. |
| **NFA04** | **Datenintegrität** | Transaktionssicherheit & Persistenz | Aufgabenstellung | 🟢 Must-Have | Bei abruptem Abbruch oder Neustart des Backend-Services bleiben alle bis dahin quittierten Datensätze in der Datenbank konsistent erhalten (ACID-Prinzip, keine verwaisten Teilzustände). |
| **NFA05** | **Usability** | Intuitive Statuserfassung | Servicetechniker / Flottenmanager | 🟢 Must-Have | Die Bedeutung der Statusfarben (Grün, Gelb, Rot, Grau) erschließt sich ohne Schulung; kritische Geräte werden durch visuelle Hervorhebung innerhalb von 5 Sekunden wahrgenommen. |
| **NFA06** | **Wartbarkeit** | Modulare Architektur & Code-Qualität | SE-Lehrveranstaltung | 🟢 Must-Have | Klare Entkopplung in Ingestion-, Business-Logic-, API- und Frontend-Komponenten. Alle automatisierten Linter (z. B. Flake8/ESLint) melden 0 Fehler; Einhaltung einheitlicher Formatierungsregeln. |
| **NFA07** | **Testbarkeit** | Automatisierte Testabdeckung | SE-Lehrveranstaltung | 🟢 Must-Have | Die Geschäfts- und Bewertungslogik (Parser, Validierung, Statusermittlung) weist eine automatisierte Unit-Testabdeckung von mindestens 80 % (Code Coverage) auf. |
| **NFA08** | **Sicherheit** | Schnittstellenabsicherung & Robustheit | Best Practice | 🟠 Should-Have | Sämtliche REST-Endpunkte validieren Eingabeparameter typensicher (z. B. gegen SQL-Injections). Keine ungesicherten Standardpasswörter in Produktionskonfigurationen. |

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

## 📖 8. Glossar

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

## 🏛️ 9. Architektur- & Technologievorschau

Das Softwaresystem folgt einer entkoppelten, 4-schichtigen Service-Architektur:

1. **Datenerzeugung:**
   - Echte TrafficNodes publizieren Messpakete im JSON-Format.
2. **Ingestion & Business-Logik (Backend):**
   - **MQTT Ingestion Worker:** Nimmt Datenströme asynchron entgegen, parst und validiert die Nutzlast.
   - **Rules Engine:** Führt die Glättung durch, prüft Schwellenwerte und ermittelt den aktuellen Status (Normal, Warnung, Kritisch, Offline).
   - **REST API:** Stellt strukturierte Endpunkte für Dashboard, Historienabfragen, Filter und Kartendaten bereit.
3. **Datenhaltung:**
   - Relationale Datenbank zur persistenten Speicherung von Gerätestammdaten, Messreihen und Event-Logs.
4. **Präsentationsschicht (Web-Frontend):**
   - Moderne, responsive Single-Page-Anwendung mit dynamischer Flottentabelle, Diagrammen und Kartenintegration.

## 🧰 10. Tools & Technologien

Die detaillierten Architekturentscheidungen und deren Begründungen sind in den [Architecture Decision Records (ADR)](docs/adr/ADR.md) dokumentiert:

- **Architektur-Stil:** Modularer Monolith (ASP.NET Core Solution) ([ADR-01](docs/adr/ADR.md#adr-01-architektur-stil-modularer-monolith-vs-microservices))
- **Protokoll & Broker:** MQTT (Eclipse Mosquitto) & .NET `MQTTnet` Worker ([ADR-04](docs/adr/ADR.md#adr-04-telemetrie-ingestion-protokoll-mqtt-vs-http-post))
- **Backend & Ingestion:** ASP.NET Core (.NET 8/9), Event-driven Verarbeitung mit In-Memory-Events (MediatR/Channels) ([ADR-01](docs/adr/ADR.md#adr-01-architektur-stil-modularer-monolith-vs-microservices), [ADR-07](docs/adr/ADR.md#adr-07-interne-datenverarbeitung-event-driven-architektur))
- **Frontend:** ASP.NET Core Blazor (Interactive Server / Razor Components) mit SignalR & Leaflet/Charts ([ADR-02](docs/adr.md#adr-02-client---schnittstellen-kommunikation-rest--signalr-vs-reines-messaging), [ADR-05](docs/adr/ADR.md#adr-05-frontend-architektur-aspnet-core-blazor))
- **Datenbank & ORM:** PostgreSQL mit Entity Framework Core & Npgsql (Shared DB mit Schematrennung `core`, `telemetry`, `alerting`) ([ADR-03](docs/adr/ADR.md#adr-03-persistenztechnologie-postgresql-vs-mongodb), [ADR-09](docs/adr/ADR.md#adr-09-datenbank-architektur-gemeinsame-vs-getrennte-datenbanken))
- **Authentifizierung:** OpenID Connect (OIDC) mit RBAC ([ADR-06](docs/adr/ADR.md#adr-06-authentifizierung--autorisierung-openid-connect--oidc))
- **Betrieb & Deployment:** Containerisiert via Docker & Docker Compose ([ADR-08](docs/adr/ADR.md#adr-08-betriebs---deployment-modell-containerisiert-on-premises--docker-compose))
- **Test- & Simulationswerkzeuge:** xUnit / Moq, Software-Simulator für TrafficNodes
- **Qualitätssicherung & CI/CD:** GitHub Actions, SonarQube / .NET Code Analysis