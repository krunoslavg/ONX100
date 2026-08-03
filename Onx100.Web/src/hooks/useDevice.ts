import { useCallback, useEffect, useState } from 'react';
import { deviceApi, DeviceApiError } from '../api/deviceApi';
import { createDeviceHub } from '../realtime/deviceHub';
import type { DeviceState } from '../types/device';

type DeviceAction = 'connect' | 'disconnect' | 'refresh' | 'power' | 'input' | 'volume' | 'mute' | null;

const initialState: DeviceState = {
    connectionState: 'Disconnected',
    powerState: 'Unknown',
    selectedInput: null,
    volume: null,
    isMuted: null,
    signalStates: { '1': 'Unknown', '2': 'Unknown', '3': 'Unknown', '4': 'Unknown' }
};

export function useDevice() {
    const [deviceState, setDeviceState] = useState<DeviceState>(initialState);
    const [busyAction, setBusyAction] = useState<DeviceAction>(null);
    const [error, setError] = useState<string | null>(null);
    const [realtimeConnected, setRealtimeConnected] = useState(false);

    useEffect(() => {
        let disposed = false;
        const connection = createDeviceHub(state => {
            if (!disposed) {
                setDeviceState(state);
            }
        }, connected => {
            if (!disposed) {
                setRealtimeConnected(connected);
            }
        });

        async function initialize(): Promise<void> {
            try {
                await connection.start();

                if (!disposed) {
                    setRealtimeConnected(true);
                }
            } catch {
                if (!disposed) {
                    setRealtimeConnected(false);
                }
            }

            try {
                const state = await deviceApi.getState();

                if (!disposed) {
                    setDeviceState(state);
                }
            } catch (requestError) {
                if (!disposed) {
                    setError(getErrorMessage(requestError));
                }
            }
        }

        void initialize();

        return () => {
            disposed = true;
            void connection.stop();
        };
    }, []);

    const runAction = useCallback(async (action: DeviceAction, operation: () => Promise<DeviceState>): Promise<void> => {
        setBusyAction(action);
        setError(null);

        try {
            const state = await operation();
            setDeviceState(state);
        } catch (requestError) {
            setError(getErrorMessage(requestError));
        } finally {
            setBusyAction(null);
        }
    }, []);

    const connect = useCallback(() => runAction('connect', deviceApi.connect), [runAction]);
    const disconnect = useCallback(() => runAction('disconnect', deviceApi.disconnect), [runAction]);
    const refresh = useCallback(() => runAction('refresh', deviceApi.refreshState), [runAction]);
    const powerOn = useCallback(() => runAction('power', deviceApi.powerOn), [runAction]);
    const powerOff = useCallback(() => runAction('power', deviceApi.powerOff), [runAction]);
    const selectInput = useCallback((input: number) => runAction('input', () => deviceApi.selectInput(input)), [runAction]);
    const setVolume = useCallback((volume: number) => runAction('volume', () => deviceApi.setVolume(volume)), [runAction]);
    const setMute = useCallback((enabled: boolean) => runAction('mute', () => deviceApi.setMute(enabled)), [runAction]);

    return { deviceState, busyAction, error, realtimeConnected, connect, disconnect, refresh, powerOn, powerOff, selectInput, setVolume, setMute, clearError: () => setError(null) };
}

function getErrorMessage(error: unknown): string {
    if (error instanceof DeviceApiError) {
        return error.message;
    }

    return 'The ONX-100 service is currently unavailable.';
}