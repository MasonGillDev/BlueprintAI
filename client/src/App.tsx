import { useState, useEffect, useCallback } from 'react';
import BlueprintCanvas from './components/Graph/BlueprintCanvas';
import ChatPanel from './components/Chat/ChatPanel';
import SettingsModal from './components/SettingsModal';
import VariablesPanel from './components/VariablesPanel';
import UEConnectionPanel from './components/UEConnectionPanel';
import { useSignalR } from './hooks/useSignalR';
import './App.css';

function App() {
  const { isConnected, sendMessage, undo, redo, setProvider, importFromUE } = useSignalR();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [currentProvider, setCurrentProvider] = useState('anthropic');

  const handleProviderChange = useCallback(
    (providerId: string) => {
      setCurrentProvider(providerId);
      setProvider(providerId);
    },
    [setProvider]
  );

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'z' && !e.shiftKey) {
        e.preventDefault();
        undo();
      }
      if ((e.metaKey || e.ctrlKey) && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
        e.preventDefault();
        redo();
      }
      if ((e.metaKey || e.ctrlKey) && e.key === ',') {
        e.preventDefault();
        setSettingsOpen(true);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [undo, redo]);

  return (
    <div className="app-layout">
      <div className="canvas-area">
        <div className="canvas-toolbar">
          <span className="canvas-title">Blueprint Editor</span>
          <div className="canvas-toolbar-actions">
            <UEConnectionPanel onImportBlueprint={importFromUE} />
            <VariablesPanel />
            <button
              onClick={() => setSettingsOpen(true)}
              className="settings-btn"
              title="Settings (Ctrl+,)"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="3"/><path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"/>
              </svg>
            </button>
          </div>
        </div>
        <BlueprintCanvas />
      </div>
      <ChatPanel
        onSendMessage={sendMessage}
        onUndo={undo}
        onRedo={redo}
        isConnected={isConnected}
      />
      <SettingsModal
        isOpen={settingsOpen}
        onClose={() => setSettingsOpen(false)}
        onProviderChange={handleProviderChange}
        currentProvider={currentProvider}
      />
    </div>
  );
}

export default App;
