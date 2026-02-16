import { create } from 'zustand';
import type { ChatMessageData } from '../types/blueprint';

interface ChatState {
  messages: ChatMessageData[];
  isStreaming: boolean;
  currentStreamText: string;
  activeToolCalls: Map<string, string>;

  addMessage: (message: ChatMessageData) => void;
  appendToStream: (text: string) => void;
  finalizeStream: () => void;
  startStreaming: () => void;
  setToolCallStarted: (toolName: string, toolCallId: string) => void;
  setToolCallCompleted: (toolName: string, toolCallId: string, result: string) => void;
  clearMessages: () => void;
}

let messageIdCounter = 0;

export const useChatStore = create<ChatState>((set, get) => ({
  messages: [],
  isStreaming: false,
  currentStreamText: '',
  activeToolCalls: new Map(),

  addMessage: (message: ChatMessageData) =>
    set((state) => ({
      messages: [...state.messages, message],
    })),

  appendToStream: (text: string) =>
    set((state) => ({
      currentStreamText: state.currentStreamText + text,
    })),

  finalizeStream: () =>
    set((state) => {
      const messages = [...state.messages];
      if (state.currentStreamText) {
        messages.push({
          id: `msg-${++messageIdCounter}`,
          role: 'assistant',
          content: state.currentStreamText,
        });
      }
      return {
        messages,
        isStreaming: false,
        currentStreamText: '',
        activeToolCalls: new Map(),
      };
    }),

  startStreaming: () =>
    set({
      isStreaming: true,
      currentStreamText: '',
      activeToolCalls: new Map(),
    }),

  setToolCallStarted: (toolName: string, toolCallId: string) =>
    set((state) => {
      const newMap = new Map(state.activeToolCalls);
      newMap.set(toolCallId, toolName);
      const messages = [...state.messages];
      messages.push({
        id: `tool-${toolCallId}`,
        role: 'tool',
        content: `Calling ${toolName}...`,
        toolName,
        toolCallId,
        isStreaming: true,
      });
      return { activeToolCalls: newMap, messages };
    }),

  setToolCallCompleted: (toolName: string, toolCallId: string, result: string) =>
    set((state) => {
      const newMap = new Map(state.activeToolCalls);
      newMap.delete(toolCallId);
      const messages = state.messages.map((m) =>
        m.toolCallId === toolCallId
          ? { ...m, content: result, isStreaming: false }
          : m
      );
      return { activeToolCalls: newMap, messages };
    }),

  clearMessages: () =>
    set({
      messages: [],
      isStreaming: false,
      currentStreamText: '',
      activeToolCalls: new Map(),
    }),
}));
