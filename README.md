# MQTTNotifier
Windows 10 background application that creates Toast Notifications from MQTT messages.

Supports two message formats:
- Raw: Message text appears as the content
- JSON (HomeAssistant format): `{"title": "Message title", "message": "Hello world!"}`

# Usage
The program folder can be placed anywhere (e.g. C:\Program Files\MQTTNotifier)

Add a shortcut to `MQTTNotifier.exe` in your Startup folder (optional)

# Configuration
All configuration is in `MQTTNotifier.exe.config` - which must be located beside the executable itself.

# Credits
Uses the M2MQTT library: https://www.nuget.org/packages/M2Mqtt/
