export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Disconnecting';
export type PowerState = 'Unknown' | 'Off' | 'Warming' | 'On' | 'Cooling';
export type SignalState = 'Unknown' | 'Ok' | 'Lost';

export interface DeviceState {
    connectionState: ConnectionState;
    powerState: PowerState;
    selectedInput: number | null;
    volume: number | null;
    isMuted: boolean | null;
    signalStates: Record<string, SignalState>;
}

export interface ApiError {
    code: string;
    message: string;
}