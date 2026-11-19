workspace "TN-Watch" "C4-Architekturmodell für das TN-Watch IoT-Flottenüberwachungssystem" {

    model {
        // ========================================================
        // C4 LEVEL 1: PERSONEN / BENUTZER
        // ========================================================
        flottenmanager = person "Betriebsleiter / Flottenmanager" "Verwaltet die Geräteflotte, analysiert Störungen und konfiguriert Schwellenwerte (Markus)." "User"
        techniker = person "Servicetechniker" "Wartet Batterien vor Ort, nutzt GPS-Standorte und Historien auf mobilen Endgeräten (Kevin / Anja)." "User"

        // ========================================================
        // EXTERNE SYSTEME & DATENQUELLE
        // ========================================================
        telemetrySource = softwareSystem "Telemetrie-Datenquelle" "Liefert Telemetriedaten (Spannung, Strom, GPS) per MQTT – transparent als reale Hardware-Box (TrafficNode) oder Software-Simulator." "ExternalSystem"
        googleMaps = softwareSystem "Google Maps" "Externer Kartendienst; Routenplanung per Maps-URL (ohne API-Key)." "ExternalSystem"
        mqttBroker = softwareSystem "MQTT-Broker (Kurs)" "Extern betriebener Broker mqtt.ti-labs.ai:8883 (TLS), an den die TrafficNodes publizieren." "ExternalSystem,Broker"
        osm = softwareSystem "OpenStreetMap Tiles" "Kartenkacheln für die Leaflet-Karte." "ExternalSystem"

        // ========================================================
        // C4 LEVEL 1 & 2: DAS TN-WATCH SYSTEM & CONTAINER
        // ========================================================
        tnWatch = softwareSystem "TN-Watch System" "Zentrales webbasiertes Überwachungs- und Koordinierungssystem für mobile Verkehrsleitsysteme." {
            
            // Container 1: Identity Provider (im Compose-Stack betrieben, ADR-06/08)
            identity = container "Keycloak" "Realm tnwatch: Login (OIDC), Rollen Betriebsleiter/Techniker, Benutzerverwaltung." "Keycloak 26" "IdP"

            // Container 2: Datenbank
            database = container "PostgreSQL Datenbank" "Speichert Gerätestammdaten, Batterie-Ereignisse, Zeitreihen-Messdaten und Statuswechsel (Schemata: core, telemetry, alerting) sowie die Keycloak-DB." "PostgreSQL 16" "Database"

            // Container 3: Web Application (Modularer Monolith)
            webApp = container "TN-Watch Webanwendung" "Modularer Monolith: Web-Dashboard, REST-API, MQTT-Ingestion, In-Process-Simulator und regelbasierte Zustandsbewertung." "ASP.NET Core 8 / Blazor Server" "App" {
                
                // ========================================================
                // C4 LEVEL 3: KOMPONENTEN INNERHALB DER WEB APPLICATION
                // ========================================================
                ui = component "Blazor Dashboard & UI" "Razor Components: Flottenübersicht, Detailkurven, Leaflet-Kartenansicht und Benutzerverwaltung." "Blazor Server / Razor"
                signalrHub = component "SignalR Realtime Hub" "Pusht eintreffende Telemetriedaten und Statusänderungen in Echtzeit per WebSocket an UI-Clients." "ASP.NET Core SignalR"
                restApi = component "REST API Controller" "Stellt OpenAPI-Endpunkte für Stammdaten, historische Telemetrie und Exporte bereit." "ASP.NET Core Web API"
                mqttIngestion = component "MQTT Ingestion Worker" "Abonniert MQTT-Topics (TLS, QoS 1, Auto-Reconnect) und reicht Rohnachrichten an die Pipeline weiter." "BackgroundService (MQTTnet)"
                simulator = component "Telemetry Simulator" "Erzeugt simulierte TrafficNodes im Format sample_msg.json (Dashboard-Schalter, nur Betriebsleiter)." "BackgroundService"
                eventBus = component "Ingestion Pipeline / Event Bus" "Begrenzter Channel + Konsument: parst, validiert und verteilt Telemetrie- und Batterie-Ereignisse." "System.Threading.Channels"
                statusEngine = component "Status Evaluation Engine" "Bewertet Batteriespannung anhand von Schwellenwerten mit gleitendem 3-Werte-Mittelwert (Hysterese); Offline-Watchdog." "C# Domain Service"
                persistenceService = component "Persistence & Data Access" "Verwaltet Lese- und Schreibzugriffe auf PostgreSQL via EF Core (DbContext)." "Entity Framework Core"
                authMiddleware = component "OIDC Auth Middleware" "Validiert Tokens und steuert rollenbasierten Zugriff (RBAC)." "ASP.NET Core Security"
            }
        }

        // ========================================================
        // BEZIEHUNGEN: C1 & C2
        // ========================================================
        // Datenquelle -> Broker (App ist die genaue Herkunft egal)
        telemetrySource -> mqttBroker "Publiziert Telemetriedaten (QoS 1) [MQTTS / JSON, Port 8883/1883]"

        // Benutzer -> WebApp
        flottenmanager -> webApp "Verwaltet Flotte, sichtet Alarme, konfiguriert System [HTTPS]"
        techniker -> webApp "Prüft Batteriestatus, sichtet Standorte & Historie mobil [HTTPS]"

        // WebApp <-> Externe Systeme
        webApp -> identity "Authentifiziert Benutzer via OIDC / OAuth 2.0 [HTTP(S)]"
        flottenmanager -> identity "Meldet sich an, verwaltet Benutzer [HTTPS]"
        techniker -> identity "Meldet sich an [HTTPS]"
        identity -> database "Speichert Realm und Benutzer [JDBC]"
        webApp -> googleMaps "Übergibt Koordinaten zur Routenplanung [HTTPS]"

        // WebApp <-> Broker & DB
        webApp -> mqttBroker "Abonniert 'traffic-device/+/trafficnode/+/status' [MQTTS, Port 8883]"
        webApp -> osm "Lädt Kartenkacheln (im Browser) [HTTPS]"
        webApp -> database "Liest und schreibt Stammdaten, Telemetrie und Alarme [TCP / SQL, Port 5432]"

        // ========================================================
        // BEZIEHUNGEN: C3 (KOMPONENTEN)
        // ========================================================
        flottenmanager -> ui "Interagiert mit [HTTPS]"
        techniker -> ui "Interagiert mit [HTTPS]"
        
        ui -> authMiddleware "Verifiziert Rechte via"
        authMiddleware -> identity "Validiert Claims / Tokens [HTTP(S)]"

        ui -> restApi "Fragt Daten ab via"
        ui -> signalrHub "Empfängt Live-Updates via [WebSockets]"

        mqttIngestion -> mqttBroker "Liest Telemetrienachrichten aus [MQTTnet]"
        mqttIngestion -> eventBus "Schreibt Rohnachrichten in den Channel"
        simulator -> eventBus "Schreibt simulierte Rohnachrichten in den Channel"

        eventBus -> statusEngine "Leitet Telemetrie weiter an"
        eventBus -> persistenceService "Triggert Persistierung via"
        
        statusEngine -> eventBus "Publiziert StateChangedEvent bei Grenzwertüberschreitung"
        eventBus -> signalrHub "Triggert Broadcast neuer Daten an verbundene Clients"

        restApi -> persistenceService "Liest Gerätedaten und Historien via"
        persistenceService -> database "Führt SQL-Queries und Migrations aus [EF Core / Npgsql]"
        ui -> googleMaps "Verlinkt/bettet Kartenansicht ein via [HTTPS]"
    }

    views {
        // Level 1: System Context Diagram
        systemContext tnWatch "SystemContext" "C4 Level 1: System-Kontextdiagramm für TN-Watch" {
            include *
            autoLayout lr
        }

        // Level 2: Container Diagram
        container tnWatch "Containers" "C4 Level 2: Container-Diagramm für TN-Watch" {
            include *
            autoLayout lr
        }

        // Level 3: Component Diagram
        component webApp "Components" "C4 Level 3: Komponenten-Diagramm der TN-Watch Webanwendung" {
            include *
            autoLayout tb
        }

        // Styling & Themes
        styles {
            element "Person" {
                shape Person
                background #08427b
                color #ffffff
            }
            element "Software System" {
                background #1168bd
                color #ffffff
            }
            element "ExternalSystem" {
                background #999999
                color #ffffff
            }
            element "Container" {
                background #438dd5
                color #ffffff
            }
            element "Database" {
                shape Cylinder
                background #225c99
                color #ffffff
            }
            element "IdP" {
                background #6c5ce7
                color #ffffff
            }
            element "Broker" {
                shape Pipe
                background #e67e22
                color #ffffff
            }
            element "Component" {
                background #85bbf0
                color #000000
            }
        }
    }
}
