import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { DeviceState } from '../types/device';

export function createDeviceHub(onStateChanged: (state: DeviceState) => void, onRealtimeConnectionChanged: (connected: boolean) => void): HubConnection {
    const connection = new HubConnectionBuilder().withUrl('/hubs/device').withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.Warning).build();

    connection.on('DeviceStateChanged', onStateChanged);
    connection.onreconnecting(() => onRealtimeConnectionChanged(false));
    connection.onreconnected(() => onRealtimeConnectionChanged(true));
    connection.onclose(() => onRealtimeConnectionChanged(false));

    return connection;
}