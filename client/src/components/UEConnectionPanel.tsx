import { useState } from 'react';
import { useUEConnectionStore } from '../store/ueConnectionStore';

interface UEConnectionPanelProps {
  onImportBlueprint: (name: string) => void;
}

export default function UEConnectionPanel({ onImportBlueprint }: UEConnectionPanelProps) {
  const [showDropdown, setShowDropdown] = useState(false);
  const {
    isConnected,
    baseUrl,
    engineVersion,
    error,
    availableBlueprints,
    isChecking,
    setBaseUrl,
    connect,
    disconnect,
    fetchBlueprints,
  } = useUEConnectionStore();

  const handleToggle = () => {
    if (isConnected) {
      setShowDropdown(!showDropdown);
      if (!showDropdown) fetchBlueprints();
    } else {
      setShowDropdown(!showDropdown);
    }
  };

  const handleConnect = async () => {
    await connect();
  };

  const handleDisconnect = async () => {
    await disconnect();
    setShowDropdown(false);
  };

  const handleImport = (name: string) => {
    onImportBlueprint(name);
    setShowDropdown(false);
  };

  return (
    <div className="ue-connection-panel">
      <button
        onClick={handleToggle}
        className={`ue-btn ${isConnected ? 'ue-btn-connected' : ''}`}
        title={isConnected ? `UE Connected (${engineVersion})` : 'Connect to Unreal Engine'}
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z"/>
          <path d="M8 12l3 3 5-5"/>
        </svg>
        <span className="ue-btn-label">UE</span>
        <span className={`ue-status-dot ${isConnected ? 'connected' : 'disconnected'}`} />
      </button>

      {showDropdown && (
        <div className="ue-dropdown">
          {!isConnected ? (
            <div className="ue-connect-form">
              <div className="ue-dropdown-header">Connect to Unreal Engine</div>
              <input
                type="text"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                placeholder="http://localhost:8089"
                className="ue-url-input"
              />
              {error && <div className="ue-error">{error}</div>}
              <button
                onClick={handleConnect}
                disabled={isChecking}
                className="ue-connect-btn"
              >
                {isChecking ? 'Connecting...' : 'Connect'}
              </button>
            </div>
          ) : (
            <div className="ue-connected-panel">
              <div className="ue-dropdown-header">
                Unreal Engine {engineVersion}
                <button onClick={handleDisconnect} className="ue-disconnect-btn">
                  Disconnect
                </button>
              </div>
              <div className="ue-blueprints-section">
                <div className="ue-blueprints-title">
                  Open Blueprints
                  <button onClick={fetchBlueprints} className="ue-refresh-btn" title="Refresh">
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <path d="M23 4v6h-6"/><path d="M1 20v-6h6"/>
                      <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15"/>
                    </svg>
                  </button>
                </div>
                {availableBlueprints.length === 0 ? (
                  <div className="ue-no-blueprints">No blueprints open in editor</div>
                ) : (
                  <div className="ue-blueprints-list">
                    {availableBlueprints.map((bp) => (
                      <button
                        key={bp.name}
                        onClick={() => handleImport(bp.name)}
                        className="ue-blueprint-item"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/>
                        </svg>
                        <span>{bp.name}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
