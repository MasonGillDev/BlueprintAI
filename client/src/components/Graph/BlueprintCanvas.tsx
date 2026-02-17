import { useMemo, useCallback, useState, useEffect } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  BackgroundVariant,
  applyNodeChanges,
  type Node,
  type Edge,
  type NodeChange,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useBlueprintStore } from '../../store/blueprintStore';
import BlueprintNodeComponent from './BlueprintNode';
import BlueprintEdgeComponent from './BlueprintEdge';
import CommentNodeComponent from './CommentNode';
import type { BlueprintNode, Connection } from '../../types/blueprint';
import { NODE_HEADER_COLORS } from '../../constants/colors';

const nodeTypes = {
  blueprint: BlueprintNodeComponent,
  comment: CommentNodeComponent,
};

const edgeTypes = {
  blueprint: BlueprintEdgeComponent,
};

function convertToFlowNodes(
  bpNodes: BlueprintNode[],
  comments: { id: string; text: string; positionX: number; positionY: number; width: number; height: number; color: string }[]
): Node[] {
  const nodes: Node[] = bpNodes.map((n) => ({
    id: n.id,
    type: 'blueprint',
    position: { x: n.positionX, y: n.positionY },
    data: { ...n },
    draggable: true,
  }));

  comments.forEach((c) => {
    nodes.push({
      id: c.id,
      type: 'comment',
      position: { x: c.positionX, y: c.positionY },
      data: { text: c.text, width: c.width, height: c.height, color: c.color },
      draggable: true,
      style: { zIndex: -1 },
    });
  });

  return nodes;
}

function convertToFlowEdges(connections: Connection[]): Edge[] {
  return connections.map((c) => ({
    id: c.id,
    source: c.sourceNodeId,
    sourceHandle: c.sourcePinId,
    target: c.targetNodeId,
    targetHandle: c.targetPinId,
    type: 'blueprint',
    data: { pinType: c.pinType },
  }));
}

export default function BlueprintCanvas() {
  const bpNodes = useBlueprintStore((s) => s.nodes);
  const connections = useBlueprintStore((s) => s.connections);
  const comments = useBlueprintStore((s) => s.comments);
  const updateNodePosition = useBlueprintStore((s) => s.updateNodePosition);

  const [flowNodes, setFlowNodes] = useState<Node[]>([]);
  const flowEdges = useMemo(() => convertToFlowEdges(connections), [connections]);

  // Sync store → local flow nodes when store changes (new nodes, deltas, etc.)
  useEffect(() => {
    setFlowNodes(convertToFlowNodes(bpNodes, comments));
  }, [bpNodes, comments]);

  const onNodesChange = useCallback(
    (changes: NodeChange[]) => {
      setFlowNodes((nds) => applyNodeChanges(changes, nds));

      // Persist final positions back to store after drag ends
      for (const change of changes) {
        if (change.type === 'position' && change.dragging === false && change.position) {
          updateNodePosition(change.id, change.position.x, change.position.y);
        }
      }
    },
    [updateNodePosition]
  );

  return (
    <div className="bp-canvas">
      <ReactFlow
        nodes={flowNodes}
        edges={flowEdges}
        onNodesChange={onNodesChange}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        fitView
        minZoom={0.1}
        maxZoom={2}
        defaultEdgeOptions={{ type: 'blueprint' }}
        proOptions={{ hideAttribution: true }}
      >
        <Background
          variant={BackgroundVariant.Dots}
          gap={20}
          size={1}
          color="#333333"
        />
        <Controls />
        <MiniMap
          nodeColor={(node) => {
            if (node.type === 'comment') return '#3A3A3A';
            const style = (node.data as BlueprintNode)?.style;
            return style ? NODE_HEADER_COLORS[style] : '#4A4A4A';
          }}
          style={{ backgroundColor: '#1A1A1A' }}
        />
      </ReactFlow>
    </div>
  );
}
