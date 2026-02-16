import { useState, useRef, useEffect } from 'react';
import { useChatStore } from '../../store/chatStore';
import type { ChatMessageData } from '../../types/blueprint';

function ChatMessage({ message }: { message: ChatMessageData }) {
  const isUser = message.role === 'user';
  const isTool = message.role === 'tool';

  if (isTool) {
    return (
      <div className="chat-message chat-message-tool">
        <div className="chat-tool-indicator">
          <span className={`chat-tool-dot ${message.isStreaming ? 'pulsing' : 'complete'}`} />
          <span className="chat-tool-name">{message.toolName}</span>
        </div>
        <div className="chat-tool-result">{message.content}</div>
      </div>
    );
  }

  return (
    <div className={`chat-message ${isUser ? 'chat-message-user' : 'chat-message-assistant'}`}>
      <div className="chat-message-content">{message.content}</div>
    </div>
  );
}

interface ChatPanelProps {
  onSendMessage: (message: string) => void;
  onUndo: () => void;
  onRedo: () => void;
  isConnected: boolean;
}

export default function ChatPanel({ onSendMessage, onUndo, onRedo, isConnected }: ChatPanelProps) {
  const [input, setInput] = useState('');
  const messages = useChatStore((s) => s.messages);
  const isStreaming = useChatStore((s) => s.isStreaming);
  const currentStreamText = useChatStore((s) => s.currentStreamText);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, currentStreamText]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (input.trim() && !isStreaming) {
      onSendMessage(input.trim());
      setInput('');
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e);
    }
  };

  return (
    <div className="chat-panel">
      <div className="chat-header">
        <h2>Blueprint AI</h2>
        <div className="chat-header-actions">
          <button onClick={onUndo} className="chat-btn" title="Undo (Ctrl+Z)">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M3 7v6h6"/><path d="M21 17a9 9 0 0 0-9-9 9 9 0 0 0-6 2.3L3 13"/></svg>
          </button>
          <button onClick={onRedo} className="chat-btn" title="Redo (Ctrl+Y)">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 7v6h-6"/><path d="M3 17a9 9 0 0 1 9-9 9 9 0 0 1 6 2.3L21 13"/></svg>
          </button>
          <span className={`connection-dot ${isConnected ? 'connected' : 'disconnected'}`} />
        </div>
      </div>

      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-empty">
            <div className="chat-empty-icon">
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/>
              </svg>
            </div>
            <p className="chat-empty-title">Blueprint AI Agent</p>
            <p className="chat-empty-subtitle">Describe the Blueprint you want to create</p>
            <div className="chat-examples">
              <button onClick={() => onSendMessage("Create a blueprint that prints 'Hello World' when the game starts")} className="chat-example-btn">
                Print "Hello World" on BeginPlay
              </button>
              <button onClick={() => onSendMessage("Create a blueprint with a delay node that waits 3 seconds then prints a message")} className="chat-example-btn">
                Delay then Print
              </button>
              <button onClick={() => onSendMessage("Create a blueprint that checks a boolean variable and branches to print different messages")} className="chat-example-btn">
                Branch on Boolean
              </button>
            </div>
          </div>
        )}
        {messages.map((msg) => (
          <ChatMessage key={msg.id} message={msg} />
        ))}
        {isStreaming && currentStreamText && (
          <div className="chat-message chat-message-assistant">
            <div className="chat-message-content">
              {currentStreamText}
              <span className="typing-cursor" />
            </div>
          </div>
        )}
        {isStreaming && !currentStreamText && messages[messages.length - 1]?.role !== 'tool' && (
          <div className="chat-message chat-message-assistant">
            <div className="chat-message-content">
              <span className="typing-dots"><span /><span /><span /></span>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      <form onSubmit={handleSubmit} className="chat-input-form">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={isConnected ? "Describe your Blueprint..." : "Connecting..."}
          disabled={!isConnected || isStreaming}
          className="chat-input"
          rows={1}
        />
        <button
          type="submit"
          disabled={!isConnected || isStreaming || !input.trim()}
          className="chat-send-btn"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
          </svg>
        </button>
      </form>
    </div>
  );
}
