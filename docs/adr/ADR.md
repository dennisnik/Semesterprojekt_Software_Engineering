# Architecture Decision Records (ADR) – TN-Watch

> **Projekt:** TN-Watch – Webbasierte Fernüberwachung & Koordinierung für mobile Verkehrsleitsysteme  
> **Modul:** Software Engineering (DHBW Stuttgart)  
> **Autoren:** Two and a Half Men (Felix Teufel, Dennis Nikitin)  
> **Datum:** Oktober 2026  
> **Status:** Genehmigt / Aktiv  

---

## Inhaltsverzeichnis

1. [ADR-01: Architektur-Stil (Modularer Monolith vs. Microservices)](#adr-01-architektur-stil-modularer-monolith-vs-microservices)
2. [ADR-02: Client- & Schnittstellen-Kommunikation (REST & SignalR vs. reines Messaging)](#adr-02-client---schnittstellen-kommunikation-rest--signalr-vs-reines-messaging)
3. [ADR-03: Persistenztechnologie (PostgreSQL vs. MongoDB)](#adr-03-persistenztechnologie-postgresql-vs-mongodb)
4. [ADR-04: Telemetrie-Ingestion-Protokoll (MQTT vs. HTTP-POST)](#adr-04-telemetrie-ingestion-protokoll-mqtt-vs-http-post)
5. [ADR-05: Frontend-Architektur (ASP.NET Core Blazor)](#adr-05-frontend-architektur-aspnet-core-blazor)
6. [ADR-06: Authentifizierung & Autorisierung (OpenID Connect / OIDC)](#adr-06-authentifizierung--autorisierung-openid-connect--oidc)
7. [ADR-07: Interne Datenverarbeitung (Event-Driven Architektur)](#adr-07-interne-datenverarbeitung-event-driven-architektur)
8. [ADR-08: Betriebs- & Deployment-Modell (Containerisiert On-Premises / Docker Compose)](#adr-08-betriebs---deployment-modell-containerisiert-on-premises--docker-compose)
9. [ADR-09: Datenbank-Architektur (Gemeinsame vs. Getrennte Datenbanken)](#adr-09-datenbank-architektur-gemeinsame-vs-getrennte-datenbanken)

---

## ADR-01: Architektur-Stil (Modularer Monolith vs. Microservices)

### Status
**Akzeptiert**

### Kontext
Für das Projekt *TN-Watch* muss eine Plattform realisiert werden, die Telemetriedaten von 50 bis 1.000 mobilen Geräten (TrafficNodes) empfängt, validiert, verarbeitet, persistiert und in einem Web-Dashboard darstellt. Das Projekt wird von einem zweiköpfigen Entwicklerteam im Rahmen eines Studienprojekts umgesetzt. 

Zur Debatte standen:
- **Microservices-Architektur:** Verteilung der Fachdomänen (Ingestion, Zustandsbewertung, Notification, Dashboard-API) auf isolierte Deployable Services.
- **Modularer Monolith:** Zusammenhängende Anwendung mit strikter logischer Kapselung der Domänen in getrennte Module/Projekte innerhalb einer einzigen Solution.

### Entscheidung
Wir entscheiden uns für einen **Modularen Monolithen** auf Basis von **ASP.NET Core (.NET 8/9)**.

Die Software wird in klar abgegrenzte Module unterteilt (z. B. `TNWatch.TelemetryIngestion`, `TNWatch.CoreDomain`, `TNWatch.Alerting`, `TNWatch.WebUI`). Diese kommunizieren intern über definierte Schnittstellen und In-Memory-Events, werden jedoch als eine gemeinsame Deployment-Einheit ausgeliefert.

### Konsequenzen & Begründung

#### Vorteile
- **Reduzierte operative Komplexität:** Kein Overhead für Service-Discovery, Distributed Tracing, API-Gateways oder komplexe Netzwerkkonfigurationen.
- **Entwicklungsgeschwindigkeit & Debugging:** Schnelle lokale Entwicklung für das 2-Personen-Team; die gesamte Anwendung lässt sich in einer IDE mit einem Klick starten und durchgehend debuggen.
- **Transaktionssicherheit (ACID):** Konsistente Datenhaltung ohne aufwändige Sagas oder 2-Phase-Commits.
- **Code-Sharing:** Typisierte C#-Modelle (z. B. DTOs, Enums wie Gerätestatus) werden domänenübergreifend ohne Codeduplikation verwendet.

#### Nachteile & Risiken
- Komponenten können nicht vollkommen unabhängig voneinander horizontal skaliert werden.
- Ein fataler Fehler im Monolithen kann theoretisch den gesamten Prozess betreffen.

#### Mitigation
- Bei der angestrebten Flottengröße (50–1.000 Geräte mit 60-Sekunden-Intervall) liegt die Ingestion-Last bei unter 20 Requests/Sekunde. Ein moderner .NET Core Host verarbeitet problemlos Tausende Requests pro Sekunde auf einer Standard-CPU.
- Saubere Trennung der Schichten durch Dependency Injection verhindert ungewollte enge Kopplung.

---

## ADR-02: Client- & Schnittstellen-Kommunikation (REST & SignalR vs. reines Messaging)

### Status
**Akzeptiert**

### Kontext
Das Web-Dashboard benötigt Zugriff auf Stammdaten (Geräteliste, Konfigurationen, Historien), muss aber auch Live-Updates (Kartenpositionen, Spannungsabfälle, Alarmzustände) verzögerungsfrei an die Benutzeroberfläche weiterleiten. Zudem sollen zukünftig externe Clients auf das System zugreifen können.

### Entscheidung
Wir implementieren einen **hybriden Kommunikationsansatz**:
1. **REST (HTTP / JSON)** für Standard-Abfragen (CRUD-Operationen, Abruf historischer Telemetriekurven, Authentifizierung).
2. **SignalR (WebSockets)** für asynchrone Echtzeit-Pushes von Telemetriedaten und Statusänderungen an das Web-Frontend.

### Konsequenzen & Begründung

#### Vorteile
- **Standardisierung & Interoperabilität:** REST ist der universelle Industriestandard; Endpunkte sind einfach via Swagger/OpenAPI dokumentierbar und testbar.
- **Echtzeitfähigkeit ohne Polling:** SignalR pusht neu eintreffende Telemetriewerte unmittelbar auf die geöffnete Detail- oder Kartenansicht der Techniker. Dadurch entfallen ressourcenfressende Polling-Zyklen.
- **Native .NET-Integration:** SignalR ist nahtlos in ASP.NET Core und Blazor integriert.

#### Nachteile & Risiken
- WebSockets sind zustandsbehaftet (Stateful Connections), was bei Verbindungsabbrüchen im Mobilfunk (z. B. Techniker-Tablet) erneute Handshakes erfordert.

#### Mitigation
- Blazor Server bzw. die SignalR-Client-Bibliothek übernimmt automatische Reconnects bei Verbindungsverlust transparent.

---

## ADR-03: Persistenztechnologie (PostgreSQL vs. MongoDB)

### Status
**Akzeptiert**

### Kontext
Das System verwaltet zwei Hauptkategorien von Daten:
1. **Strukturierte relationale Daten:** Benutzer, Rollen, Gerätestammdaten, Konfigurationen und Schwellenwerte.
2. **Zeitreihendaten (Time-Series):** Kontinuierlich eintreffende Messpunkte (Spannung $U$, Strom $I$, GPS-Position, Timestamp).

Verglichen wurden das relationale Datenbanksystem **PostgreSQL** und die dokumentenbasierte NoSQL-Datenbank **MongoDB**.

### Entscheidung
Wir wählen **PostgreSQL** in Verbindung mit dem Object-Relational Mapper **Entity Framework Core (EF Core)** und dem `Npgsql`-Treiber.

### Konsequenzen & Begründung

#### Vorteile
- **Referenzielle Integrität & ACID:** Strenge Typisierung und Foreign-Key-Beziehungen verhindern verwaiste Datensätze (z. B. Messwerte ohne zugehörige TrafficNode).
- **Exzellente Zeitreihen- & Geodaten-Unterstützung:** PostgreSQL bietet native Geo-Funktionen (`PostGIS`) und kann bei Bedarf transparent um die Erweiterung `TimescaleDB` für hyper-effiziente Zeitreihenanalyse ergänzt werden.
- **Hybrid-Fähigkeit:** Sollten künftige Sensortypen flexible Zusatzparameter liefern, können diese im nativen `JSONB`-Datentyp von PostgreSQL performant indiziert werden.
- **First-Class EF Core Support:** Code-First-Migrationen, LINQ-Abfragen und automatisches Schema-Management beschleunigen die Entwicklung.

#### Nachteile & Risiken
- Schema-Änderungen erfordern Datenbankmigrationen (im Gegensatz zur schemalosen MongoDB).

#### Mitigation
- Einsatz von EF Core Migrations, die automatisiert beim Anwendungsstart im Container oder in der CI/CD-Pipeline ausgeführt werden.

---

## ADR-04: Telemetrie-Ingestion-Protokoll (MQTT vs. HTTP-POST)

### Status
**Akzeptiert**

### Kontext
Die im Feld befindlichen TrafficNodes sind batteriebetriebene Einheiten, die über Mobilfunk (LTE-M / NB-IoT / 2G/4G) Daten übertragen. Energieeffizienz und minimaler Datenverbrauch sind kritische Faktoren für die Batterielebensdauer der Sensoren.

### Entscheidung
Wir setzen für die Ingestion der Sensordaten auf **MQTT (Message Queuing Telemetry Transport)** mit **Eclipse Mosquitto** als Broker und der C#-Bibliothek **MQTTnet** im Backend.

Topic-Schema: `tn-watch/nodes/{nodeId}/telemetry` und `tn-watch/nodes/{nodeId}/status`

### Konsequenzen & Begründung

#### Vorteile
- **Geringer Netzwerk- und Energie-Overhead:** MQTT-Header sind winzig (ab 2 Byte) im Vergleich zu HTTP-Headern (mehrere hundert Byte). Dies schont Datenvolumen und den Akku der Feldgeräte.
- **Entkopplung durch Publish/Subscribe:** Feldgeräte senden ihre Daten an den Broker, ohne die IP oder Verfügbarkeit des Backends kennen zu müssen.
- **Pufferung & QoS:** Bei Backend-Neustarts puffert der Broker Nachrichten gemäß Quality of Service (QoS 1).
- **Industriestandard:** MQTT ist das führende Protokoll im IoT- und SCADA-Bereich.

#### Nachteile & Risiken
- Erfordert den Betrieb eines zusätzlichen Containers (Mosquitto Broker).

#### Mitigation
- Eclipse Mosquitto ist extrem leichtgewichtig (benötigt < 10 MB RAM) und lässt sich unkompliziert per Docker Compose orchestrieren.

---

## ADR-05: Frontend-Architektur (ASP.NET Core Blazor)

### Status
**Akzeptiert**

### Kontext
Für das Dashboard (Flottenübersicht, Detailkurven für Spannung/Strom, interaktive OpenStreetMap-/Google-Maps-Kartenansicht, Alarmüberwachung) wurde ursprünglich React erwogen. Für ein 2-Personen-Team mit C#-Fokus bietet sich jedoch eine Full-Stack-.NET-Lösung an.

### Entscheidung
Wir setzen auf **ASP.NET Core Blazor (Interactive Server / Razor Components)**.

### Konsequenzen & Begründung

#### Vorteile
- **Einheitlicher C#-Technologiestack:** Frontend und Backend werden in derselben Sprache (C#) entwickelt.
- **Gemeinsame Domänenmodelle:** DTOs, Validierungslogik (z. B. DataAnnotations / FluentValidation) und Enums werden direkt geteilt.
- **Integrierte Echtzeit-Synchronisation:** Durch die native SignalR-Verbindung von Blazor Server aktualisieren sich UI-Komponenten bei eintreffenden Telemetriedaten automatisch ohne manuellen REST-Fetch-Code.
- **Reiches Ökosystem:** Karten (Leaflet via JS-Interop oder BlazorLeaflet) und Diagramme (Chart.js / ApexCharts via Blazor-Wrapper) lassen sich nahtlos integrieren.

#### Nachteile & Risiken
- Bei reinem Blazor Server besteht eine ständige WebSocket-Verbindung zum Server; bei instabilen Netzen kann die UI kurz einfrieren.
- JavaScript-Bibliotheken (wie z. B. spezielle Leaflet-Plugins) erfordern JS-Interop.

#### Mitigation
- Moderne Blazor-Modelle erlauben hybride Interaktivität. Für Standardkomponenten wird serverseitiges Rendering verwendet, interaktive Diagramme nutzen schlankes JS-Interop.

---

## ADR-06: Authentifizierung & Autorisierung (OpenID Connect / OIDC)

### Status
**Akzeptiert**

### Kontext
Das System unterscheidet zwischen verschiedenen Benutzerrollen:
- **Flottenmanager / Betriebsleiter:** Vollzugriff, Konfiguration, Schwellenwert-Anpassung, Nutzerverwaltung.
- **Servicetechniker:** Lesender Zugriff, Routenplanung, Erfassung von Wartungs- und Batterietausch-Aktionen.

Verglichen wurden ein eigenes Token-System (Custom JWT) und standardisiertes **OpenID Connect (OIDC)**.

### Entscheidung
Wir implementieren die Authentifizierung über **OpenID Connect (OIDC)** auf Basis von OAuth 2.0 (unter Nutzung von z. B. Keycloak oder externen Identity Providern) mit Role-Based Access Control (RBAC).

### Konsequenzen & Begründung

#### Vorteile
- **Enterprise-Sicherheitsstandard:** Einhaltung moderner Sicherheitsstandards (RFC 6749 / RFC 7519); Passwörter und Tokens werden nicht unverschlüsselt oder in selbstgebauten Schemata verwaltet.
- **Zentrale Rollen- & Rechteverwaltung:** Rollen (`Flottenmanager`, `Techniker`) werden über Claims gesteuert und greifen nahtlos über `.NET`-Attribute (`[Authorize(Roles = "Manager")]`).
- **Erweiterbarkeit:** Ermöglicht zukünftig Single Sign-On (SSO) und Multi-Faktor-Authentifizierung (MFA) ohne Änderung des Anwendungscodes.

#### Nachteile & Risiken
- Höhere initiale Konfigurationskomplexität als eine simple lokale User-Tabelle.

#### Mitigation
- Nutzung standardisierter ASP.NET Core Middleware (`Microsoft.AspNetCore.Authentication.OpenIdConnect`). Für lokale Entwicklungstests und CI-Pipelines kann ein schlanker Mock/Test-IDP oder ein containerisiertes Keycloak-Profil bereitgestellt werden.

---

## ADR-07: Interne Datenverarbeitung (Event-Driven Architektur)

### Status
**Akzeptiert**

### Kontext
Das Eintreffen einer MQTT-Nachricht stößt mehrere voneinander unabhängige Folgeprozesse an:
1. Validierung der Sensordaten
2. Persistierung in der Datenbank
3. Regelbasierte Zustandsbewertung (Normal, Warnung, Kritisch, Timeout)
4. Eventuelles Auslösen von Alarmen (z. B. Push an UI, zukünftig E-Mail-Versand)
5. Live-Aktualisierung der Benutzeroberfläche

### Entscheidung
Wir wählen eine **In-Process Event-Driven Architektur** (unter Einsatz von `MediatR` bzw. `System.Threading.Channels`).

Typischer Event-Fluss:
$$\text{MQTT-Nachricht} \longrightarrow \text{IngestionService} \xrightarrow{\text{veröffentlicht}} \texttt{TelemetryReceivedEvent}$$
Mehrere Handler reagieren asynchron und unabhängig auf das Ereignis:
- `PersistenceHandler` $\rightarrow$ Schreibt Messpunkt in PostgreSQL.
- `StatusEvaluationHandler` $\rightarrow$ Prüft Schwellenwerte; publiziert bei Grenzwertüberschreitung `DeviceStateChangedEvent`.
- `SignalRNotificationHandler` $\rightarrow$ Pusht Update an alle aktiven Dashboard-Clients.

### Konsequenzen & Begründung

#### Vorteile
- **Lose Kopplung (Single Responsibility):** Das Ingestion-Modul muss nicht wissen, wer die Daten speichert oder wer Alarme versendet.
- **Hohe Erweiterbarkeit:** Neue Funktionen (z. B. Post-MVP E-Mail-Benachrichtigung oder ML-Batterieprognose) hängen sich einfach als neuer Event-Listener ein, ohne bestehenden Code zu verändern (Open-Closed-Prinzip).
- **Asynchrone Nicht-blockierende Verarbeitung:** Entlastet den Ingestion-Worker und vermeidet Flaschenhälse.

#### Nachteile & Risiken
- Fehlerbehandlung erfordert Sorgfalt (z. B. wenn die Persistierung fehlschlägt, der SignalR-Push aber bereits abgesetzt wurde).

#### Mitigation
- Transaktionsrelevante Schritte (Persistenz) werden priorisiert ausgeführt; strukturierte Logging-Pipelines (`Serilog`) erfassen Event-Verarbeitungsschritte lückenlos.

---

## ADR-08: Betriebs- & Deployment-Modell (Containerisiert On-Premises / Docker Compose)

### Status
**Akzeptiert**

### Kontext
Das Gesamtsystem muss im Rahmen der Hochschulabgabe durch den Dozenten und die Entwickler lokal und reproduzierbar gestartet werden können. Gleichzeitig soll für Präsentationszwecke ein Betrieb auf einer virtuellen Maschine (z. B. Linux VPS) möglich sein.

### Entscheidung
Wir setzen auf **vollständige Containerisierung mit Docker und Docker Compose**.

Das System wird als Multi-Container-Setup betrieben:
1. `tn-watch-app` (ASP.NET Core & Blazor Server Host)
2. `tn-watch-db` (PostgreSQL)
3. `tn-watch-broker` (Eclipse Mosquitto MQTT Broker)
4. `tn-watch-simulator` (Telemetrie-Simulator für Tests und Demos)

### Konsequenzen & Begründung

#### Vorteile
- **"Works on My Machine"-Garantie:** Das gesamte Setup startet mit einem einzigen Befehl (`docker compose up --build`) auf macOS, Linux und Windows.
- **Keine Cloud-Abhängigkeit & keine Kosten:** Keine laufenden Kosten für Cloud-Infrastruktur; DSGVO-konforme lokale Datenhaltung.
- **Portabilität:** Derselbe `docker-compose.yml`-Stack kann 1:1 auf einen günstigen Cloud-Server (z. B. Hetzner Cloud VM) geschoben werden, falls eine öffentliche Demo benötigt wird.

#### Nachteile & Risiken
- Keine automatischen Cloud-PaaS-Features (wie automatische Hochverfügbarkeit oder Managed Backups).

#### Mitigation
- Für die Anforderungen des Projekts (bis zu 1.000 Geräte) ist die Single-Node-Docker-Performance mehr als ausreichend. Backups werden über standardisierte Docker-Volume-Skripte realisiert.

---

## ADR-09: Datenbank-Architektur (Gemeinsame vs. Getrennte Datenbanken)

### Status
**Akzeptiert**

### Kontext
In verteilten Architekturen wird oft das Muster *Database-per-Service* diskutiert (z. B. separate Datenbanken für Benutzer, Telemetrie und Konfiguration). Es muss entschieden werden, ob das Datenmodell auf mehrere Datenbank-Instanzen aufgeteilt oder zentral betrieben wird.

### Entscheidung
Wir verwenden eine **gemeinsame PostgreSQL-Datenbank mit logischer Schematrennung** (Shared Database / Separate Schemas):
- Schema `core`: Gerätestammdaten, Benutzer, Rollen, Schwellenwert-Konfigurationen.
- Schema `telemetry`: Zeitreihen-Messdaten, GPS-Historie, historische Batteriewerte.
- Schema `alerting`: Alarmprotokolle, Statuswechsel-Historie, Quittierungen.

### Konsequenzen & Begründung

#### Vorteile
- **Ressourceneffizienz:** Nur eine PostgreSQL-Instanz im Docker-Setup (spart Arbeitsspeicher und CPU auf Entwicklungs- und Zielrechnern).
- **Integrierte Abfragen:** Historische Analysen (z. B. *"Welche Stationen vom Typ X hatten in den letzten 7 Tagen eine Unterspannung?"*) lassen sich performing und elegant per SQL-Join durchführen.
- **Saubere Kapselung:** Die logischen Schemas verhindern unkontrollierten Wildwuchs im Datenmodell und erzwingen saubere Modulgrenzen auf Datenbankebene.
- **Einfache Sicherung:** Konsistente Snapshots und Backups über die gesamte Domäne mit Standardwerkzeugen (`pg_dump`).

#### Nachteile & Risiken
- Sollte in Zukunft ein Schema extrem wachsen, teilen sich alle Schemas dieselben I/O-Ressourcen der Datenbank.

#### Mitigation
- PostgreSQL verarbeitet Tabellen mit Millionen Zeilen mühelos, insbesondere bei sauber gesetzten Indizes auf `(node_id, timestamp)`. Sollte die Telemetrie extrem anwachsen, kann das Schema `telemetry` ohne Architekturumbruch auf eine partitionierte Tabelle umgestellt werden.

---

## Zusammenfassung der Architekturentscheidungen

| ADR | Domäne | Gewählte Lösung | Kernbegründung |
| :--- | :--- | :--- | :--- |
| **ADR-01** | Architektur-Stil | **Modularer Monolith (.NET)** | Minimale Komplexität, optimale Team-Produktivität, perfekte Eignung für Mengengerüst |
| **ADR-02** | Schnittstellen | **REST & SignalR (Hybrid)** | Standard-REST für Stammdaten + SignalR für latenzfreie Telemetrie-Live-Pushes |
| **ADR-03** | Persistenz | **PostgreSQL (EF Core)** | Relationale Integrität, strukturierte Zeitreihen & Geodaten, ACID-Garantie |
| **ADR-04** | Telemetrie | **MQTT (Mosquitto)** | Extrem schlankes Binärprotokoll, akkuschonend für Feldgeräte, IoT-Standard |
| **ADR-05** | Frontend | **ASP.NET Core Blazor** | Einheitlicher C#-Stack, geteilte DTOs, native SignalR-Anbindung für Live-UI |
| **ADR-06** | Auth | **OpenID Connect (OIDC)** | Höchste Sicherheitsstandards, standardisierte RBAC-Rollenverwaltung |
| **ADR-07** | Verarbeitung | **Event-Driven (In-Memory)** | Entkopplung von Ingestion, Statusbewertung, Speicherung und UI-Push |
| **ADR-08** | Deployment | **Docker Compose (On-Prem)** | 100 % reproduzierbar für Abgabe, keine Cloud-Kosten, einfache Portabilität |
| **ADR-09** | DB-Strategie | **Gemeinsame DB (getrennte Schemas)** | Ressourcenarm, ACID-transaktionssicher, performante relationale Analysen |
