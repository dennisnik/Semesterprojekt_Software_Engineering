# MQTT-Schnittstellenvertrag (Ingestion)

> Stand: 12.11.2026 · gilt für `TNWatch.TelemetryIngestion` · Konfiguration: Abschnitte `Mqtt` und `Ingestion` in `appsettings.json`

## 1. Broker

| Eigenschaft | Wert |
|---|---|
| Broker | `mqtt.ti-labs.ai:8883` (Kurs-Broker, extern betrieben, siehe ADR-04) |
| Transport | MQTT 3.1.1 über TLS ≥ 1.2, Zertifikats- und Hostname-Prüfung aktiv |
| Authentifizierung | Benutzername/Passwort der Gruppe, nur über Umgebungsvariablen `Mqtt__Username` / `Mqtt__Password` |
| QoS / Session | QoS 1, persistente Session (`CleanSession=false`) mit fester `Mqtt__ClientId` → der Broker puffert Nachrichten während eines Neustarts |
| Reconnect | Verbindungsprüfung alle 3 s, Verbindungs-Timeout 10 s → NFA03 (≤ 10 s nach Wiederverfügbarkeit) |

**Zertifikatsprüfung:** Das Let's-Encrypt-Zertifikat des Brokers ist gültig. Let's Encrypt betreibt seit 2025 jedoch keinen OCSP-Dienst mehr, deshalb meldet .NET auf manchen Plattformen `RevocationStatusUnknown`. Mit `Mqtt:TolerateUnknownRevocation=true` (Standard) wird **nur** dieser eine Status toleriert. Kette und Hostname werden weiterhin strikt geprüft (`MqttIngestionWorker.ValidateCertificate`).

## 2. Telemetrie

**Topic-Schema** (konfigurierbar über `Ingestion:TelemetryTopicPatterns`, konfigurierte Listen ersetzen die Standardwerte):

```
traffic-nodes/{nodeId}/measurements                    ← live am Kurs-Broker (seit 19.11.2026, z. B. TN0001)
traffic-device/{tenant}/trafficnode/{nodeId}/status    ← Format des älteren Exports data/traffic_devices_signals.csv
```

Die Platzhalter `{tenant}` und `{nodeId}` werden aus dem Topic gelesen. Die MQTT-Abos werden daraus abgeleitet (`traffic-nodes/+/measurements`, `traffic-device/+/trafficnode/+/status`). Zusätzlich wird `traffic-nodes/#` abonniert (`Mqtt:ExtraSubscriptions`), damit weitere Unter-Topics der Nodes, z. B. Batterie-Ereignisse, ankommen.

**Payload** (Format von `data/sample_msg.json`):

```json
{ "current_mA": 551.0, "voltage_mV": 11276.0, "lat": 48.201157, "lon": 8.649726,
  "altitude_m": 609.6, "satellites": 7, "time": "2026-11-12T08:59:30Z", "temperature_C": 14.5 }
```

| Feld | Einheit | Pflicht | Verarbeitung |
|---|---|---|---|
| `voltage_mV` (alternativ `voltage_V`, `voltage`) | mV (bzw. V) | ja | → Volt; plausibel nur `0 < U ≤ 20 V`, sonst Ablehnung |
| `current_mA` (alternativ `current_A`, `current`) | mA (bzw. A) | ja | → Ampere; plausibel nur `|I| ≤ 50 A`; negativ = Laden |
| `lat`/`latitude`, `lon`/`lng`/`longitude` | Grad | nein | `null`, außerhalb des Wertebereichs oder `0/0` → letzte bekannte Position bleibt erhalten |
| `time`/`timestamp`/`_time` | ISO-8601 oder Unix s/ms | nein | `null`/ungültig/> 5 min in der Zukunft → Empfangszeit |
| `altitude_m`, `satellites`, `temperature_C` | – | nein | werden gespeichert |
| `device_id`/`node_id`/`nodeId` | – | nur ohne `{nodeId}` im Topic | Fallback für die Node-ID |

Abgelehnte Nachrichten werden mit Begründung geloggt und gezählt (`/api/diagnostics`). Die literale Datei `data/sample_msg.json` (alle Werte `0`) wird **abgelehnt** (Spannung 0 V = Sensor nicht bereit).

## 3. Batterie-Ereignisse (F10)

Topic noch nicht festgelegt (`Ingestion:BatteryEventTopicPatterns`, Standard leer). Nachrichten mit Feld `eventId` werden unabhängig vom Topic als Batterie-Ereignis erkannt. Sendet die Node sie unterhalb von `traffic-nodes/…`, kommen sie über das Zusatz-Abo `traffic-nodes/#` an. Die Node-IDs passen zusammen: Das Beispiel-Ereignis nennt `TN0001`, und genau diese Node sendet live.

| Typ | Beispiel | Erkennung |
|---|---|---|
| Tausch | `data/exchange.example.json` | `newBat` vorhanden (zusätzlich `oldBat`, `trafficNode`, `position`) |
| Entsorgung | `data/discard.example.json` | nur `serial` |

`date` + `time` sind lokale Zeit ohne Offset und werden als `Europe/Berlin` interpretiert (`Ingestion:EventTimeZone`). `eventId` ist der Primärschlüssel, deshalb werden doppelt zugestellte Ereignisse (QoS 1) ignoriert. Ein Tausch setzt die aktuelle Batterie (`newBat`) der Node.

## 4. Beobachtungen am Kurs-Broker (Discovery, 12.11.2026)

Read-only-Abo `#`, 2 × 3 min + 30 min (Werkzeug: `tools/TNWatch.MqttProbe`):

- Das Abo wird gewährt, und Topics anderer Gruppen sind sichtbar. Eine ACL filtert also nicht.
- Auf `traffic-device/#` kam **keine** Nachricht an, d. h. im Zeitraum war keine TrafficNode aktiv.
- Eine andere Kursgruppe sendete Testnachrichten auf `<gruppe>/test` **exakt im Format von `sample_msg.json`** (mV/mA, `lat`/`lon`/`time` = `null`). Das bestätigt das obige Payload-Format inklusive der `null`-Fälle.
- Es wurden keine Batterie-Ereignisse beobachtet.
- **19.11.2026:** erneut 10 min auf `#`: **0 Nachrichten**, also weder TrafficNodes noch Testnachrichten anderer Gruppen. Echte Daten liegen weiterhin nicht vor.
- **19.11.2026, ab ca. 09:40:** Die erste echte Node sendet: `traffic-nodes/TN0001/measurements`, Payload exakt im Format von `sample_msg.json` (U ≈ 12,0 V, I ≈ 50 mA, `lat`/`lon`/`time` = `null`), etwa alle 1–2 Minuten, nicht retained. Ende-zu-Ende geprüft: Die App legt `TN0001` an und bewertet sie als *Warnung*. Ohne GPS erscheint die Node in der Tabelle, aber nicht auf der Karte.

**Offen:** genaues Topic der Batterie-Ereignisse und ob die Nodes künftig GPS senden. Weitere Topics lassen sich ohne Codeänderung per Konfiguration ergänzen, z. B. `Ingestion__TelemetryTopicPatterns__2=tinf25d-two-and-a-half-men/{nodeId}`.
