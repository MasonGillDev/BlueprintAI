export type NodeStyle = 'Event' | 'Function' | 'Pure' | 'FlowControl' | 'Variable' | 'Macro' | 'Comment';

export type PinType =
  | 'Exec' | 'Bool' | 'Int' | 'Float' | 'String' | 'Vector'
  | 'Rotator' | 'Transform' | 'Object' | 'Class' | 'Byte'
  | 'Name' | 'Text' | 'Enum' | 'Struct' | 'Array' | 'Set'
  | 'Map' | 'Delegate' | 'Wildcard';

export type PinDirection = 'Input' | 'Output';

export type DeltaType =
  | 'NodeAdded' | 'NodeRemoved' | 'NodeUpdated'
  | 'ConnectionAdded' | 'ConnectionRemoved'
  | 'CommentAdded' | 'CommentRemoved'
  | 'VariableAdded' | 'VariableRemoved'
  | 'FullSync';

export interface Pin {
  id: string;
  name: string;
  type: PinType;
  direction: PinDirection;
  defaultValue?: string;
  subType?: string;
  isConnected: boolean;
}

export interface BlueprintNode {
  id: string;
  title: string;
  category: string;
  style: NodeStyle;
  inputPins: Pin[];
  outputPins: Pin[];
  positionX: number;
  positionY: number;
  isCompact: boolean;
}

export interface Connection {
  id: string;
  sourceNodeId: string;
  sourcePinId: string;
  targetNodeId: string;
  targetPinId: string;
  pinType: PinType;
}

export interface BlueprintComment {
  id: string;
  text: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  color: string;
}

export interface BlueprintVariable {
  id: string;
  name: string;
  type: PinType;
  defaultValue?: string;
  category: string;
  isEditable: boolean;
}

export interface Blueprint {
  id: string;
  name: string;
  nodes: BlueprintNode[];
  connections: Connection[];
  comments: BlueprintComment[];
  variables: BlueprintVariable[];
  version: number;
}

export interface BlueprintDelta {
  type: DeltaType;
  node?: BlueprintNode;
  connection?: Connection;
  comment?: BlueprintComment;
  variable?: BlueprintVariable;
  removedId?: string;
  fullState?: Blueprint;
  version: number;
}

export interface ChatMessageData {
  id: string;
  role: 'user' | 'assistant' | 'tool';
  content: string;
  toolName?: string;
  toolCallId?: string;
  isStreaming?: boolean;
}

export interface ProviderInfo {
  id: string;
  name: string;
  models: string[];
  hasApiKey: boolean;
}

export interface UEBlueprintInfo {
  name: string;
  path: string;
}

export interface UEConnectionStatus {
  isConnected: boolean;
  engineVersion?: string;
  error?: string;
}
