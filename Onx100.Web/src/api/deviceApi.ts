import type { ApiError, DeviceState } from '../types/device';

export class DeviceApiError extends Error {
    public readonly code: string;
    public readonly status: number;

    constructor(code: string, status: number, message: string) {
        super(message);
        this.name = 'DeviceApiError';
        this.code = code;
        this.status = status;
    }
}

async function request(method: string, url: string): Promise<DeviceState> {
    const response = await fetch(url, { method, headers: { Accept: 'application/json' } });

    if (!response.ok) {
        let error: ApiError = { code: 'request_failed', message: `Request failed with status ${response.status}.` };

        try {
            error = await response.json() as ApiError;
        } catch {
            // API did not return a structured error response.
        }

        throw new DeviceApiError(error.code, response.status, error.message);
    }

    return await response.json() as DeviceState;
}

export const deviceApi = {
    getState: (): Promise<DeviceState> => request('GET', '/api/device/state'),
    refreshState: (): Promise<DeviceState> => request('POST', '/api/device/refresh'),
    connect: (): Promise<DeviceState> => request('POST', '/api/device/connect'),
    disconnect: (): Promise<DeviceState> => request('POST', '/api/device/disconnect'),
    powerOn: (): Promise<DeviceState> => request('POST', '/api/device/power/on'),
    powerOff: (): Promise<DeviceState> => request('POST', '/api/device/power/off'),
    selectInput: (input: number): Promise<DeviceState> => request('PUT', `/api/device/input/${input}`),
    setVolume: (volume: number): Promise<DeviceState> => request('PUT', `/api/device/volume/${volume}`),
    setMute: (enabled: boolean): Promise<DeviceState> => request('PUT', `/api/device/mute/${enabled}`)
};