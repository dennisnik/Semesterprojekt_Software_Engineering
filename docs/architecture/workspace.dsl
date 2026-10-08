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
        keycloak = softwareSystem "Identity Provider (OIDC)" "Zentraler Authentifizierungs- und Autorisierungsdienst (z. B. Keycloak)." "ExternalSystem"
        googleMaps = softwareSystem "Google Maps API" "Externer Kartendienst für Routenplanung und Geocoding." "ExternalSystem"

        // ========================================================
        // C4 LEVEL 1 & 2: DAS TN-WATCH SYSTEM & CONTAINER
        // ========================================================
        tnWatch = softwareSystem "TN-Watch System" "Zentrales webbasiertes Überwachungs- und Koordinierungssystem für mobile Verkehrsleitsysteme." {
            
            // Container 1: MQTT-Broker
            mqttBroker = container "MQTT Broker" "Nimmt eingehende Telemetriedaten der Datenquelle per MQTT über gesicherte Topics entgegen." "Eclipse Mosquitto" "Broker"

            // Container 2: Datenbank
            database = container "PostgreSQL Datenbank" "Speichert Gerätestammdaten, Konfigurationen, Zeitreihen-Messdaten und Alarmhistorien (Schemata: core, telemetry, alerting)." "PostgreSQL 16" "Database"

            // Container 3: Web Application (Modularer Monolith)
            webApp = container "TN-Watch Webanwendung" "Modularer Monolith: Beinhaltet Web-Dashboard, REST-API, MQTT-Ingestion und regelbasierte Zustandsbewertung." "ASP.NET Core 8 / Blazor Server" "App" {
                
                // ========================================================
                // C4 LEVEL 3: KOMPONENTEN INNERHALB DER WEB APPLICATION
                // ========================================================
                ui = component "Blazor Dashboard & UI" "Razor Components: Flottenübersicht, Detailkurven, Leaflet-Kartenansicht und Benutzerverwaltung." "Blazor Server / Razor"
                signalrHub = component "SignalR Realtime Hub" "Pusht eintreffende Telemetriedaten und Statusänderungen in Echtzeit per WebSocket an UI-Clients." "ASP.NET Core SignalR"
                restApi = component "REST API Controller" "Stellt OpenAPI-Endpunkte für Stammdaten, historische Telemetrie und Exporte bereit." "ASP.NET Core Web API"
                mqttIngestion = component "MQTT Ingestion Worker" "Abonniert MQTT-Topics, parst JSON-Nutzlasten und validiert Sensordaten." "BackgroundService (MQTTnet)"
                eventBus = component "In-Memory Event Bus" "Entkoppelt Ingestion, Persistierung und Alerting über asynchrone Ereignisse." "MediatR / Channels"
                statusEngine = component "Status Evaluation Engine" "Bewertet Batteriespannung anhand von Schwellenwerten mit gleitendem 3-Werte-Mittelwert (Hysterese)." "C# Domain Service"
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
        webApp -> keycloak "Authentifiziert Benutzer via OIDC / OAuth 2.0 [HTTPS]"
        webApp -> googleMaps "Übergibt Koordinaten zur Routenplanung [HTTPS]"

        // WebApp <-> Broker & DB
        webApp -> mqttBroker "Abonniert 'tn-watch/nodes/+/telemetry' [MQTT / TCP]"
        webApp -> database "Liest und schreibt Stammdaten, Telemetrie und Alarme [TCP / SQL, Port 5432]"

        // ========================================================
        // BEZIEHUNGEN: C3 (KOMPONENTEN)
        // ========================================================
        flottenmanager -> ui "Interagiert mit [HTTPS]"
        techniker -> ui "Interagiert mit [HTTPS]"
        
        ui -> authMiddleware "Verifiziert Rechte via"
        authMiddleware -> keycloak "Validiert Claims / Tokens [HTTPS]"

        ui -> restApi "Fragt Daten ab via"
        ui -> signalrHub "Empfängt Live-Updates via [WebSockets]"

        mqttIngestion -> mqttBroker "Liest Telemetrienachrichten aus [MQTTnet]"
        mqttIngestion -> eventBus "Publiziert TelemetryReceivedEvent"

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
