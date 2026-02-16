import { create } from 'zustand';
import type { Blueprint, BlueprintDelta, BlueprintNode, Connection, BlueprintComment, BlueprintVariable } from '../types/blueprint';

interface BlueprintState {
  nodes: BlueprintNode[];
  connections: Connection[];
  comments: BlueprintComment[];
  variables: BlueprintVariable[];
  version: number;

  applyDelta: (delta: BlueprintDelta) => void;
  reset: () => void;
}

export const useBlueprintStore = create<BlueprintState>((set) => ({
  nodes: [],
  connections: [],
  comments: [],
  variables: [],
  version: 0,

  applyDelta: (delta: BlueprintDelta) => {
    set((state) => {
      switch (delta.type) {
        case 'FullSync':
          if (delta.fullState) {
            return {
              nodes: delta.fullState.nodes,
              connections: delta.fullState.connections,
              comments: delta.fullState.comments,
              variables: delta.fullState.variables,
              version: delta.version,
            };
          }
          return state;

        case 'NodeAdded':
          if (delta.node) {
            return {
              nodes: [...state.nodes, delta.node],
              version: delta.version,
            };
          }
          return state;

        case 'NodeRemoved':
          if (delta.removedId) {
            return {
              nodes: state.nodes.filter((n) => n.id !== delta.removedId),
              connections: state.connections.filter(
                (c) => c.sourceNodeId !== delta.removedId && c.targetNodeId !== delta.removedId
              ),
              version: delta.version,
            };
          }
          return state;

        case 'NodeUpdated':
          if (delta.node) {
            return {
              nodes: state.nodes.map((n) => (n.id === delta.node!.id ? delta.node! : n)),
              version: delta.version,
            };
          }
          return state;

        case 'ConnectionAdded':
          if (delta.connection) {
            return {
              connections: [...state.connections, delta.connection],
              version: delta.version,
            };
          }
          return state;

        case 'ConnectionRemoved':
          if (delta.removedId) {
            return {
              connections: state.connections.filter((c) => c.id !== delta.removedId),
              version: delta.version,
            };
          }
          return state;

        case 'CommentAdded':
          if (delta.comment) {
            return {
              comments: [...state.comments, delta.comment],
              version: delta.version,
            };
          }
          return state;

        case 'CommentRemoved':
          if (delta.removedId) {
            return {
              comments: state.comments.filter((c) => c.id !== delta.removedId),
              version: delta.version,
            };
          }
          return state;

        case 'VariableAdded':
          if (delta.variable) {
            return {
              variables: [...state.variables, delta.variable],
              version: delta.version,
            };
          }
          return state;

        case 'VariableRemoved':
          if (delta.removedId) {
            return {
              variables: state.variables.filter((v) => v.id !== delta.removedId),
              version: delta.version,
            };
          }
          return state;

        default:
          return state;
      }
    });
  },

  reset: () =>
    set({
      nodes: [],
      connections: [],
      comments: [],
      variables: [],
      version: 0,
    }),
}));
