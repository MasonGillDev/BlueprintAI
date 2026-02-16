import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { useBlueprintStore } from '../store/blueprintStore';
import { useChatStore } from '../store/chatStore';
import type { BlueprintDelta } from '../types/blueprint';

const HUB_URL = 'http://localhost:5000/hub/blueprint';

export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const applyDelta = useBlueprintStore((s) => s.applyDelta);
  const appendToStream = useChatStore((s) => s.appendToStream);
  const finalizeStream = useChatStore((s) => s.finalizeStream);
  const startStreaming = useChatStore((s) => s.startStreaming);
  const setToolCallStarted = useChatStore((s) => s.setToolCallStarted);
  const setToolCallCompleted = useChatStore((s) => s.setToolCallCompleted);
  const addMessage = useChatStore((s) => s.addMessage);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveTextDelta', (text: string) => {
      appendToStream(text);
    });

    connection.on('ReceiveBlueprintDelta', (delta: BlueprintDelta) => {
      applyDelta(delta);
    });

    connection.on('ReceiveToolCallStarted', (toolName: string, toolCallId: string) => {
      setToolCallStarted(toolName, toolCallId);
    });

    connection.on('ReceiveToolCallCompleted', (toolName: string, toolCallId: string, result: string) => {
      setToolCallCompleted(toolName, toolCallId, result);
    });

    connection.on('ReceiveAskUser', (question: string) => {
      addMessage({
        id: `ask-${Date.now()}`,
        role: 'assistant',
        content: question,
      });
    });

    connection.on('ReceiveError', (error: string) => {
      addMessage({
        id: `err-${Date.now()}`,
        role: 'assistant',
        content: `Error: ${error}`,
      });
    });

    connection.on('ReceiveStreamComplete', () => {
      finalizeStream();
    });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    connection
      .start()
      .then(() => {
        setIsConnected(true);
        connectionRef.current = connection;
      })
      .catch((err) => console.error('SignalR connection error:', err));

    return () => {
      connection.stop();
    };
  }, []);

  const sendMessage = useCallback(
    async (message: string) => {
      if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
        startStreaming();
        addMessage({
          id: `user-${Date.now()}`,
          role: 'user',
          content: message,
        });
        await connectionRef.current.invoke('SendMessage', message);
      }
    },
    [startStreaming, addMessage]
  );

  const undo = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('Undo');
    }
  }, []);

  const redo = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('Redo');
    }
  }, []);

  const setProvider = useCallback(async (providerId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SetProvider', providerId);
    }
  }, []);

  const cancelRequest = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('CancelRequest');
    }
  }, []);

  const importFromUE = useCallback(async (blueprintName: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('ImportFromUE', blueprintName);
    }
  }, []);

  return { isConnected, sendMessage, undo, redo, setProvider, cancelRequest, importFromUE };
}
