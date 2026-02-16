import { useState, useEffect } from 'react';
import type { ProviderInfo } from '../types/blueprint';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onProviderChange: (providerId: string) => void;
  currentProvider: string;
}

const API_BASE = 'http://localhost:5000';

export default function SettingsModal({
  isOpen,
  onClose,
  onProviderChange,
  currentProvider,
}: SettingsModalProps) {
  const [providers, setProviders] = useState<ProviderInfo[]>([]);
  const [selectedProvider, setSelectedProvider] = useState(currentProvider);
  const [apiKey, setApiKey] = useState('');
  const [model, setModel] = useState('');
  const [baseUrl, setBaseUrl] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (isOpen) {
      fetch(`${API_BASE}/api/settings/providers`)
        .then((r) => r.json())
        .then((data) => setProviders(data))
        .catch(console.error);
    }
  }, [isOpen]);

  useEffect(() => {
    setSelectedProvider(currentProvider);
  }, [currentProvider]);

  useEffect(() => {
    const provider = providers.find((p) => p.id === selectedProvider);
    if (provider) {
      setModel(provider.models[0] || '');
      setBaseUrl(selectedProvider === 'ollama' ? 'http://localhost:11434' : '');
    }
  }, [selectedProvider, providers]);

  const handleSave = async () => {
    setSaving(true);
    try {
      const body: Record<string, string> = {};
      if (apiKey) body.apiKey = apiKey;
      if (model) body.model = model;
      if (baseUrl) body.baseUrl = baseUrl;

      await fetch(`${API_BASE}/api/settings/provider/${selectedProvider}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });

      onProviderChange(selectedProvider);
      onClose();
    } catch (err) {
      console.error('Failed to save settings:', err);
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  const currentProviderInfo = providers.find((p) => p.id === selectedProvider);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Settings</h3>
          <button onClick={onClose} className="modal-close">&times;</button>
        </div>
        <div className="modal-body">
          <div className="form-group">
            <label>LLM Provider</label>
            <select
              value={selectedProvider}
              onChange={(e) => setSelectedProvider(e.target.value)}
            >
              {providers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} {p.hasApiKey ? '' : '(no key set)'}
                </option>
              ))}
            </select>
          </div>

          {selectedProvider !== 'ollama' && (
            <div className="form-group">
              <label>API Key</label>
              <input
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder={
                  currentProviderInfo?.hasApiKey ? '(key already set)' : 'Enter API key...'
                }
              />
            </div>
          )}

          <div className="form-group">
            <label>Model</label>
            <select value={model} onChange={(e) => setModel(e.target.value)}>
              {currentProviderInfo?.models.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </div>

          {(selectedProvider === 'ollama' || selectedProvider === 'openai') && (
            <div className="form-group">
              <label>Base URL</label>
              <input
                type="text"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                placeholder="http://localhost:11434"
              />
            </div>
          )}
        </div>
        <div className="modal-footer">
          <button onClick={onClose} className="btn-secondary">
            Cancel
          </button>
          <button onClick={handleSave} disabled={saving} className="btn-primary">
            {saving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}
