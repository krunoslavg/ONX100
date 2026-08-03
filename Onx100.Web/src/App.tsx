import { useState } from 'react';
import './App.css';
import { useDevice } from './hooks/useDevice';
import type { SignalState } from './types/device';

function App() {
    const { deviceState, busyAction, error, realtimeConnected, connect, disconnect, refresh, powerOn, powerOff, selectInput, setVolume, setMute, clearError } = useDevice();
    const [volumeDraft, setVolumeDraft] = useState<number | null>(null);

    const isBusy = busyAction !== null;
    const isConnected = deviceState.connectionState === 'Connected';
    const isPowerOn = deviceState.powerState === 'On';
    const powerTransitioning = deviceState.powerState === 'Warming' || deviceState.powerState === 'Cooling';
    const displayedVolume = volumeDraft ?? deviceState.volume ?? 40;
    const audioStatus = deviceState.isMuted === null ? 'neutral' : deviceState.isMuted ? 'warning' : 'success';

    async function applyVolume(): Promise<void> {
        await setVolume(displayedVolume);
        setVolumeDraft(null);
    }

    return (
        <main className="app-shell">
            <section className="control-panel">
                <header className="app-header">
                    <div>
                        <p className="eyebrow">ONIO Pro AV</p>
                        <h1>ONX-100 Control Panel</h1>
                        <p className="subtitle">Browser control interface for the ONX-100 device driver</p>
                    </div>

                    <div className="status-group">
                        <StatusBadge label={deviceState.connectionState} status={isConnected ? 'success' : 'neutral'} />
                        <StatusBadge label={realtimeConnected ? 'Live updates' : 'Live updates offline'} status={realtimeConnected ? 'success' : 'warning'} />
                    </div>
                </header>

                {error && (
                    <div className="error-banner" role="alert">
                        <span>{error}</span>
                        <button type="button" onClick={clearError} aria-label="Dismiss error">×</button>
                    </div>
                )}

                <section className="toolbar card">
                    <div>
                        <h2>Connection</h2>
                        <p>{getConnectionDescription(deviceState.connectionState)}</p>
                    </div>

                    <div className="button-row">
                        <button type="button" className="button primary" disabled={isBusy || isConnected} onClick={() => void connect()}>
                            {busyAction === 'connect' ? 'Connecting…' : 'Connect'}
                        </button>

                        <button type="button" className="button secondary" disabled={isBusy || !isConnected} onClick={() => void disconnect()}>
                            {busyAction === 'disconnect' ? 'Disconnecting…' : 'Disconnect'}
                        </button>

                        <button type="button" className="button secondary" disabled={isBusy} onClick={() => void refresh()}>
                            {busyAction === 'refresh' ? 'Refreshing…' : 'Refresh state'}
                        </button>
                    </div>
                </section>

                <section className="dashboard-grid">
                    <section className="card">
                        <div className="section-heading">
                            <div>
                                <h2>Power</h2>
                                <p>Current state: <strong>{deviceState.powerState}</strong></p>
                            </div>

                            <div className={`power-indicator power-${deviceState.powerState.toLowerCase()}`} />
                        </div>

                        {powerTransitioning && (
                            <p className="progress-message">
                                {deviceState.powerState === 'Warming' ? 'Device is warming up…' : 'Device is cooling down…'}
                            </p>
                        )}

                        <div className="button-row">
                            <button type="button" className="button success" disabled={isBusy || isPowerOn || powerTransitioning} onClick={() => void powerOn()}>
                                {busyAction === 'power' && !isPowerOn ? 'Please wait…' : 'Power on'}
                            </button>

                            <button type="button" className="button danger" disabled={isBusy || deviceState.powerState === 'Off' || powerTransitioning} onClick={() => void powerOff()}>
                                {busyAction === 'power' && isPowerOn ? 'Please wait…' : 'Power off'}
                            </button>
                        </div>
                    </section>

                    <section className="card">
                        <div className="section-heading">
                            <div>
                                <h2>Input</h2>
                                <p>{isPowerOn ? `Selected input: ${deviceState.selectedInput ?? 'Unknown'}` : 'Available when the device is on'}</p>
                            </div>
                        </div>

                        <div className="input-grid">
                            {[1, 2, 3, 4].map(input => (
                                <button type="button" key={input} className={`input-button ${deviceState.selectedInput === input ? 'active' : ''}`} disabled={isBusy || !isPowerOn} onClick={() => void selectInput(input)}>
                                    <span>Input {input}</span>
                                    <SignalBadge state={deviceState.signalStates[String(input)] ?? 'Unknown'} />
                                </button>
                            ))}
                        </div>
                    </section>

                    <section className="card">
                        <div className="section-heading">
                            <div>
                                <h2>Audio</h2>
                                <p>Volume: <strong>{deviceState.volume ?? 'Unknown'}</strong></p>
                            </div>

                            <StatusBadge label={deviceState.isMuted === true ? 'Muted' : deviceState.isMuted === false ? 'Audio active' : 'Unknown'} status={audioStatus} />
                        </div>

                        <label className="volume-control">
                            <span>Volume level</span>

                            <div className="volume-row">
                                <input type="range" min="0" max="100" value={displayedVolume} disabled={isBusy} onChange={event => setVolumeDraft(Number(event.target.value))} />
                                <output>{displayedVolume}</output>
                            </div>
                        </label>

                        <div className="button-row">
                            <button type="button" className="button primary" disabled={isBusy || volumeDraft === null || displayedVolume === deviceState.volume} onClick={() => void applyVolume()}>
                                {busyAction === 'volume' ? 'Applying…' : 'Apply volume'}
                            </button>

                            <button type="button" className="button secondary" disabled={isBusy || deviceState.isMuted === null} onClick={() => void setMute(!deviceState.isMuted)}>
                                {busyAction === 'mute' ? 'Applying…' : deviceState.isMuted ? 'Unmute' : 'Mute'}
                            </button>
                        </div>
                    </section>

                    <section className="card state-card">
                        <h2>Device state</h2>

                        <dl>
                            <div><dt>Connection</dt><dd>{deviceState.connectionState}</dd></div>
                            <div><dt>Power</dt><dd>{deviceState.powerState}</dd></div>
                            <div><dt>Selected input</dt><dd>{deviceState.selectedInput ?? 'Unknown'}</dd></div>
                            <div><dt>Volume</dt><dd>{deviceState.volume ?? 'Unknown'}</dd></div>
                            <div><dt>Mute</dt><dd>{deviceState.isMuted === null ? 'Unknown' : deviceState.isMuted ? 'On' : 'Off'}</dd></div>
                        </dl>
                    </section>
                </section>

                <footer>ONX-100 ASP.NET Core API · React · SignalR</footer>
            </section>
        </main>
    );
}

function StatusBadge({ label, status }: { label: string; status: 'success' | 'warning' | 'neutral' }) {
    return <span className={`status-badge status-${status}`}>{label}</span>;
}

function SignalBadge({ state }: { state: SignalState }) {
    return <span className={`signal-badge signal-${state.toLowerCase()}`}>{state}</span>;
}

function getConnectionDescription(connectionState: string): string {
    if (connectionState === 'Connected') return 'The API is connected to the ONX-100 device.';
    if (connectionState === 'Connecting') return 'Opening the ONX-100 device session…';
    if (connectionState === 'Disconnecting') return 'Closing the ONX-100 device session…';
    return 'The API is not currently connected to the device.';
}

export default App;